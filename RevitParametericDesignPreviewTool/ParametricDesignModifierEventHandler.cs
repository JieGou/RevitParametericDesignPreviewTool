// (C) Copyright 2022 by Autodesk, Inc. 
//
// Permission to use, copy, modify, and distribute this software
// in object code form for any purpose and without fee is hereby
// granted, provided that the above copyright notice appears in
// all copies and that both that copyright notice and the limited
// warranty and restricted rights notice below appear in all
// supporting documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS. 
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK,
// INC. DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL
// BE UNINTERRUPTED OR ERROR FREE.
//
// Use, duplication, or disclosure by the U.S. Government is
// subject to restrictions set forth in FAR 52.227-19 (Commercial
// Computer Software - Restricted Rights) and DFAR 252.227-7013(c)
// (1)(ii)(Rights in Technical Data and Computer Software), as
// applicable.
//

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace RevitParametericDesignPreviewTool
{
    public class ParametricDesignModifierEventHandler : IExternalEventHandler
    {
        private ParametericDesignControl parent;
        public ParametricDesignModifierOptions Options { get; set; }

        public ParametricDesignModifierEventHandler(ParametericDesignControl parent)
        {
            this.parent = parent;
        }

        private Rebar CreateRebar(Document doc, Element targetElement, RebarBarType rebarType, RebarShape rebarShape)
        {
            //var location = targetElement.Location as LocationPoint;

            var column = targetElement as FamilyInstance;
            var geometryData = new GeometrySupport(column);

            List<XYZ> profilePoints = geometryData.ProfilePoints;
            XYZ origin = profilePoints[0];
            XYZ yVec = profilePoints[1] - origin;
            XYZ xVec = profilePoints[3] - origin;

            var stirrup = Rebar.CreateFromRebarShape(doc, rebarShape, rebarType, targetElement, origin, xVec, yVec) ?? throw new InvalidOperationException("Failed to create stirrup");
            var spacingFromMm2Ft = this.Options.Spacing / 304.8;
            var length = targetElement.get_Parameter(BuiltInParameter.INSTANCE_LENGTH_PARAM).AsDouble();

            double offestFromMm2Ft = this.Options.CoverSpace / 304.8;
            List<XYZ> profilePointsOffset = geometryData.OffsetPoints(offestFromMm2Ft);
            XYZ originOffset = profilePointsOffset[0];
            XYZ yVecOffset = profilePointsOffset[1] - originOffset;
            XYZ xVecOffset = profilePointsOffset[3] - originOffset;
            stirrup.GetShapeDrivenAccessor().ScaleToBox(originOffset, xVecOffset, yVecOffset);
            stirrup.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(spacingFromMm2Ft, length, true, true, true);

            return stirrup;
        }

        private void RemoveOldRebars(Document doc, Element targetElement)
        {
            var dependentIds = targetElement.GetDependentElements(null);

            foreach (var dependentId in dependentIds)
            {
                var rebarElem = doc.GetElement(dependentId) as Autodesk.Revit.DB.Structure.Rebar;
                if (rebarElem?.Category.Id.IntegerValue == BuiltInCategory.OST_Rebar.GetHashCode())
                {
                    doc.Delete(rebarElem.Id);
                }
            }
        }

        public void Execute(UIApplication app)
        {
            if (this.Options == null) throw new InvalidDataException("Parameter Design Options are not set.");

            Rebar stirrup = null;
            Document doc = app.ActiveUIDocument.Document;
            using (var trans = new Transaction(doc, "Apply Parameter Design Options"))
            {
                try
                {
                    trans.Start();

                    var targetElement = doc.GetElement(this.parent.TargetElementId);
                    this.RemoveOldRebars(doc, targetElement);

                    var rebarType = doc.GetElement(this.Options.RebarBarTypeId) as RebarBarType;
                    var rebarShape = doc.GetElement(this.Options.RebarShapeId) as RebarShape;
                    stirrup = this.CreateRebar(doc, targetElement, rebarType, rebarShape);

                    trans.Commit();
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Revit", $"Failed to create new stirrups or delete old ones for the column ({this.parent.TargetElementId.IntegerValue})");
                    trans.RollBack();
                }
            }

            if (stirrup != null)
            {
                using (var trans = new Transaction(doc, "Apply Graphic Overrides to Rebar"))
                {
                    try
                    {
                        trans.Start();
                        var view3d = doc.GetElement(this.Options.View3dId) as View3D;
                        stirrup.SetSolidInView(view3d, true);

                        using (var patternCollecotr = new FilteredElementCollector(doc))
                        {
                            patternCollecotr.OfClass(typeof(FillPatternElement));
                            var solidFillPatternElem = patternCollecotr.FirstOrDefault(e => e.Name == GlobalConst.SolidFillPatternName);
                            if (solidFillPatternElem != null)
                            {
                                var graphicOverride = new OverrideGraphicSettings();
                                graphicOverride.SetSurfaceForegroundPatternId(solidFillPatternElem.Id);
                                graphicOverride.SetSurfaceForegroundPatternColor(new Color(255, 0, 0));

                                view3d.SetElementOverrides(stirrup.Id, graphicOverride);
                            }
                        }

                        trans.Commit();
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Revit", $"Failed to change graphic overrides for the new created stirrups ({stirrup.Id.IntegerValue})");
                        trans.RollBack();
                    }
                }
            }

            this.parent.HideSpinner();
            this.parent.BringWindowToFront();
        }

        public string GetName()
        {
            return "Parameteric Design Modifier event hanlder";
        }
    }

    public class ParametricDesignModifierOptions
    {
        /// <summary>
        /// 间距，单位毫米
        /// </summary>
        public double Spacing { get; set; }
        /// <summary>
        /// 保护层厚度，单位毫米
        /// </summary>
        public double CoverSpace { get; set; }
        public ElementId RebarBarTypeId { get; set; }
        public ElementId RebarShapeId { get; set; }
        public ElementId View3dId { get; set; }
    }
}
