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

namespace RevitParametericDesignPreviewTool
{
    public partial class ParametericDesignControl : System.Windows.Forms.Form
    {
        private ElementId targetElementId = null;
        private Document rvtDoc = null;
        private RApplication rvtApp = null;
        private UIApplication rvtUIApp = null;
        private ParametericDesignDummyViewEventHandler dbViewEventHandler = null;
        private ExternalEvent dbViewEvent = null;

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

            this.dbViewEventHandler = new ParametericDesignDummyViewEventHandler(this.rvtPreviewControlHost, targetElementId);
            this.dbViewEvent = ExternalEvent.Create(this.dbViewEventHandler);
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
