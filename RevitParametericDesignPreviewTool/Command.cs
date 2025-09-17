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
using Autodesk.Revit.UI.Selection;

namespace RevitParametericDesignPreviewTool
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiDocument = commandData.Application.ActiveUIDocument;
            var document = uiDocument.Document;

            Reference selRef = null;

            try
            {
                selRef = uiDocument.Selection.PickObject(ObjectType.Element, new StructuralColumnSelectionFilter());

                if (selRef == null)
                    return Result.Cancelled;
            }
            catch (Exception ex)
            {
                return Result.Cancelled;
            }

            try
            {
                var element = document.GetElement(selRef);
                if (element == null)
                {
                    TaskDialog.Show("Revit", "Invalid selected elelemt");
                    return Result.Cancelled;
                }

                var form = new ParametericDesignControl(commandData.Application.Application, element.Id);
                form.Show(new RevitWindowHndle(commandData.Application.MainWindowHandle));
                //form.Show();
            }
            catch (Exception e)
            {
                throw e;
            }

            return Result.Succeeded;
        }

        public class RevitWindowHndle : System.Windows.Forms.IWin32Window
        {
            public IntPtr Handle { get; private set; }

            public RevitWindowHndle(IntPtr handle)
            {
                this.Handle = handle;
            }
        }

        private class StructuralColumnSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                if (elem.Category != null && elem.Category.Id.IntegerValue == BuiltInCategory.OST_StructuralColumns.GetHashCode())
                    return true;

                return false;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}
