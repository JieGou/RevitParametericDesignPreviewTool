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
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Structure;
using RView = Autodesk.Revit.DB.View;
using RApplication = Autodesk.Revit.ApplicationServices.Application;
using System.Runtime.InteropServices;

namespace RevitParametericDesignPreviewTool
{
    public partial class ParametericDesignControl : System.Windows.Forms.Form
    {
        private ElementId targetElementId = null;
        private Document rvtDoc = null;
        private RApplication rvtApp = null;
        private UIApplication rvtUIApp = null;
        private ParametericDesignViewEventHandler dbViewEventHandler = null;
        private ExternalEvent dbViewEvent = null;

        private ParametricDesignModifierEventHandler modifierEventHandler = null;
        private ExternalEvent modifierEvent = null;

        public ElementId TargetElementId
        {
            get { return this.targetElementId; }
        }

        // DLL imports from user32.dll to set focus to
        // Revit to force it to forward the external event
        // Raise to actually call the external event 
        // Execute.

        /// <summary>
        /// The GetForegroundWindow function returns a 
        /// handle to the foreground window.
        /// </summary>
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        /// <summary>
        /// Move the window associated with the passed 
        /// handle to the front.
        /// </summary>
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        public ElementId CurrentDBViewId
        {
            get { return this.dbViewEventHandler.ViewId; }
        }

        public ParametericDesignControl(RApplication application, ElementId targetElementId)
        {
            InitializeComponent();

            this.rvtApp = application;
            this.rvtUIApp = new UIApplication(application);
            this.rvtDoc = this.rvtUIApp.ActiveUIDocument.Document;
            this.targetElementId = targetElementId;

            this.dbViewEventHandler = new ParametericDesignViewEventHandler(this.rvtPreviewControlHost, targetElementId);
            this.dbViewEvent = ExternalEvent.Create(this.dbViewEventHandler);

            this.modifierEventHandler = new ParametricDesignModifierEventHandler(this);
            this.modifierEvent = ExternalEvent.Create(this.modifierEventHandler);
        }

        private void UpdateRebarTypeList()
        {
            using (var collector = new FilteredElementCollector(this.rvtDoc))
            {
                //collector.WhereElementIsNotElementType();
                collector.OfClass(typeof(RebarBarType));

                this.cmbRebarType.Items.Clear();

                foreach (var type in collector.Cast<RebarBarType>())
                {
                    this.cmbRebarType.Items.Add(new RebarTypeItem(type));
                }

                if (collector.Count() > 0)
                    this.cmbRebarType.SelectedIndex = this.cmbRebarType.Items.IndexOf(this.cmbRebarType.Items[0]);
            }
        }

        private void UpdateRebarShapeList()
        {
            using (var collector = new FilteredElementCollector(this.rvtDoc))
            {
                //collector.WhereElementIsNotElementType();
                collector.OfClass(typeof(RebarShape));

                this.cmbRebarShape.Items.Clear();

                foreach (var shape in collector.Cast<RebarShape>())
                {
                    //var imgSize = new Size(200, 200);
                    //shape.GetPreviewImage(imgSize);
                    this.cmbRebarShape.Items.Add(new RebarShapeItem(shape));
                }

                if (collector.Count() > 0)
                    this.cmbRebarShape.SelectedIndex = this.cmbRebarShape.Items.IndexOf(this.cmbRebarShape.Items[0]);
            }
        }

        private void ParametericDesignWindow_Shown(object sender, EventArgs e)
        {
            this.UpdateRebarShapeList();
            this.UpdateRebarTypeList();
            this.dbViewEventHandler.DisposingView = false;
            this.dbViewEvent.Raise();
        }

        private void ParametericDesignControl_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.dbViewEventHandler.DisposingView = true;
            this.dbViewEvent.Raise();
        }

        private void btnApplyChange_Click(object sender, EventArgs e)
        {
            this.ShowSpinner();
            this.modifierEventHandler.Options = new ParametricDesignModifierOptions()
            {
                Spacing = Decimal.ToDouble(this.numInputRebarSpacing.Value),
                CoverSpace = Decimal.ToDouble(this.numInputRebarCoverSpace.Value),
                RebarBarTypeId = ((RebarTypeItem)this.cmbRebarType.SelectedItem).Id,
                RebarShapeId = ((RebarShapeItem)this.cmbRebarShape.SelectedItem).Id,
                View3dId = this.CurrentDBViewId
            };


            this.modifierEvent.Raise();

            this.SendWindowToBack();
        }

        public void SendWindowToBack()
        {
            // Set focus to Revit for a moment.
            // Otherwise, it may take a while before 
            // Revit forwards the event Raise to the
            // event handler Execute method.
            SetForegroundWindow(this.rvtUIApp.MainWindowHandle);
        }

        public void BringWindowToFront()
        {
            //IntPtr hBefore = GetForegroundWindow();
            SetForegroundWindow(this.Handle);
        }

        public void ShowSpinner()
        {
            this.pbSpinner.Visible = true;
        }

        public void HideSpinner()
        {
            this.pbSpinner.Visible = false;
        }
    }

    public class RebarTypeItem
    {
        public String Name { get; private set; }
        public ElementId Id { get; private set; }
        public String UniqueId { get; private set; }

        public override String ToString()
        {
            return this.Name;
        }

        public RebarTypeItem(RebarBarType rebarType)
        {
            this.Name = rebarType.Name;
            this.Id = rebarType.Id;
            this.UniqueId = rebarType.UniqueId;
        }
    }

    public class RebarShapeItem
    {
        public String Name { get; private set; }
        public ElementId Id { get; private set; }
        public String UniqueId { get; private set; }

        public override String ToString()
        {
            return this.Name;
        }

        public RebarShapeItem(RebarShape rebarShape)
        {
            this.Name = rebarShape.Name;
            this.Id = rebarShape.Id;
            this.UniqueId = rebarShape.UniqueId;
        }
    }
}
