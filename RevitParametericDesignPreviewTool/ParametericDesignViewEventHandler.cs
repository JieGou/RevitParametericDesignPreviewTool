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
using Autodesk.Revit.UI;

namespace RevitParametericDesignPreviewTool
{
    public class ParametericDesignViewEventHandler : IExternalEventHandler
    {
        private System.Windows.Forms.Integration.ElementHost rvtPreviewControlHost;
        private ElementId targetElementId;
        private string viewName = "Parameteric Design View";

        public bool DisposingView { get; set; }
        public ElementId ViewId { get; private set; }

        public ParametericDesignViewEventHandler(System.Windows.Forms.Integration.ElementHost rvtPreviewControlHost, ElementId targetElementId)
        {
            this.rvtPreviewControlHost = rvtPreviewControlHost;
            this.targetElementId = targetElementId;
        }

        private View3D CreateView3D(Document doc)
        {
            using (var collector = new FilteredElementCollector(doc))
            {
                collector.OfClass(typeof(ViewFamilyType));
                var viewFamilyType = collector.Cast<ViewFamilyType>().FirstOrDefault(viewType => viewType.ViewFamily == ViewFamily.ThreeDimensional);

                if (viewFamilyType == null) throw new InvalidDataException("Not ViewFamilyType for 3D found");

                var view = View3D.CreateIsometric(doc, viewFamilyType.Id);

                if (view == null) throw new InvalidOperationException("Failed to create 3D view for ParametericDesignControl");

                view.Name = this.viewName;
                var isolatingIds = new List<ElementId>();
                isolatingIds.Add(this.targetElementId);

                var targetElement = doc.GetElement(this.targetElementId);
                var dependentIds = targetElement.GetDependentElements(null);
                isolatingIds.AddRange(dependentIds);

                foreach (var dependentId in dependentIds)
                {
                    var rebarElem = doc.GetElement(dependentId) as Autodesk.Revit.DB.Structure.Rebar;
                    if (rebarElem?.Category.Id.IntegerValue == BuiltInCategory.OST_Rebar.GetHashCode())
                    {
                        rebarElem.SetSolidInView(view, true);
                    }
                }

                view.IsolateElementsTemporary(isolatingIds);
                view.ConvertTemporaryHideIsolateToPermanent();
                view.DisplayStyle = DisplayStyle.ShadingWithEdges;
                view.DetailLevel = ViewDetailLevel.Fine;

                using (var patternCollecotr = new FilteredElementCollector(doc))
                {
                    patternCollecotr.OfClass(typeof(FillPatternElement));
                    var solidFillPatternElem = patternCollecotr.FirstOrDefault(e => e.Name == "<Solid fill>");

                    var graphicOverride = new OverrideGraphicSettings();
                    graphicOverride.SetSurfaceForegroundPatternId(solidFillPatternElem.Id);
                    graphicOverride.SetSurfaceForegroundPatternColor(new Color(255, 250, 224));
                    graphicOverride.SetSurfaceTransparency(80);

                    var rebarGraphicOverride = new OverrideGraphicSettings();
                    rebarGraphicOverride.SetSurfaceForegroundPatternId(solidFillPatternElem.Id);
                    rebarGraphicOverride.SetSurfaceForegroundPatternColor(new Color(255, 0, 0));

                    view.SetElementOverrides(this.targetElementId, graphicOverride);

                    foreach (var dependentId in dependentIds)
                    {
                        var rebarElem = doc.GetElement(dependentId) as Autodesk.Revit.DB.Structure.Rebar;
                        if (rebarElem?.Category.Id.IntegerValue == BuiltInCategory.OST_Rebar.GetHashCode())
                        {
                            view.SetElementOverrides(rebarElem.Id, rebarGraphicOverride);
                        }
                    }
                }

                this.ViewId = view.Id;

                return view;
            }
        }

        private void DisposeOldView(Document doc)
        {
            using (var collector = new FilteredElementCollector(doc))
            {
                collector.OfClass(typeof(View3D));
                Element viewElem = collector.FirstOrDefault(v => v.Name == this.viewName);
                if (viewElem != null)
                    doc.Delete(viewElem.Id);
            }
        }

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;
            var message = this.DisposingView ? "Delete Parameteric Design View" : "Create Parameteric Design View";
            using (var trans = new Transaction(doc, message))
            {
                try
                {
                    trans.Start();

                    if (this.DisposingView && this.ViewId != null)
                    {
                        doc.Delete(this.ViewId);
                        this.ViewId = null;
                    }
                    else
                    {
                        this.DisposeOldView(doc);
                        this.CreateView3D(doc);
                    }

                    trans.Commit();
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Revit", "Failed to create or delete Parameteric Design View");
                    trans.RollBack();
                }
            }

            PreviewControl control = this.rvtPreviewControlHost.Child as PreviewControl;
            if (control != null)
                control.Dispose();

            if (!this.DisposingView)
            {
                var previewCtrl = new PreviewControl(doc, this.ViewId);
                this.rvtPreviewControlHost.Child = previewCtrl;
            }
        }

        public string GetName()
        {
            return "Parameteric Design View event hanlder";
        }
    }
}
