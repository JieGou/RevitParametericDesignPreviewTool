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
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Structure;
using RView = Autodesk.Revit.DB.View;
using RApplication = Autodesk.Revit.ApplicationServices.Application;

namespace RevitParametericDesignPreviewTool
{
    public partial class ParametericDesignControl : Window
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

            this.Loaded += ParametericDesignWindow_Shown;
            this.Closing += ParametericDesignControl_FormClosing;
            this.btnApplyChange.Click += btnApplyChange_Click;
        }

        private void UpdateRebarTypeList()
        {
            using (var collector = new FilteredElementCollector(this.rvtDoc))
            {
                collector.OfClass(typeof(RebarBarType));

                this.cmbRebarType.Items.Clear();
                foreach (var type in collector.Cast<RebarBarType>())
                {
                    this.cmbRebarType.Items.Add(new RebarTypeItem(type));
                }

                // ���Ȱ���ǰ�����Ѵ��ڸֽ������Ԥѡ��������˵���һ��
                int index = -1;
                var preferredTypeId = this.GetPreferredRebarTypeId();
                if (preferredTypeId != null)
                {
                    index = this.cmbRebarType.Items
                        .Cast<RebarTypeItem>()
                        .ToList()
                        .FindIndex(i => i.Id.IntegerValue == preferredTypeId.IntegerValue);
                }

                if (index >= 0)
                    this.cmbRebarType.SelectedIndex = index;
                else if (collector.GetElementCount() > 0)
                    this.cmbRebarType.SelectedIndex = 0;
            }
        }

        private void UpdateRebarShapeList()
        {
            using (var collector = new FilteredElementCollector(this.rvtDoc))
            {
                collector.OfClass(typeof(RebarShape));

                this.cmbRebarShape.Items.Clear();

                foreach (var shape in collector.Cast<RebarShape>())
                {
                    this.cmbRebarShape.Items.Add(new RebarShapeItem(shape));
                }

                // ���Ȱ���ǰ�����Ѵ��ڸֽ����״Ԥѡ��������˵���һ��
                int index = -1;
                var preferredShapeId = this.GetPreferredRebarShapeId();
                if (preferredShapeId != null && preferredShapeId != ElementId.InvalidElementId)
                {
                    index = this.cmbRebarShape.Items
                        .Cast<RebarShapeItem>()
                        .ToList()
                        .FindIndex(i => i.Id.IntegerValue == preferredShapeId.IntegerValue);
                }

                if (index >= 0)
                    this.cmbRebarShape.SelectedIndex = index;
                else if (collector.GetElementCount() > 0)
                    this.cmbRebarShape.SelectedIndex = 0;
            }
        }

        private void ParametericDesignWindow_Shown(object sender, RoutedEventArgs e)
        {
            this.UpdateRebarShapeList();
            this.UpdateRebarTypeList();

            // ����������ͨ���Ѵ��ڸֽ�Ʊ����㣻ʧ������˵���������������
            this.TryInitCoverByReverseFromExistingRebar();

            this.dbViewEventHandler.DisposingView = false;
            this.dbViewEvent.Raise();
        }

        private void ParametericDesignControl_FormClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.dbViewEventHandler.DisposingView = true;
            this.dbViewEvent.Raise();
        }

        private void btnApplyChange_Click(object sender, RoutedEventArgs e)
        {
            this.ShowSpinner();

            // �� IntegerUpDown ��ȡֵ����ֵ��Ĭ��ֵ
            double spacing = (this.numInputRebarSpacing.Value.HasValue ? this.numInputRebarSpacing.Value.Value : 100);
            double cover = (this.numInputRebarCoverSpace.Value.HasValue ? this.numInputRebarCoverSpace.Value.Value : 40);

            this.modifierEventHandler.Options = new ParametricDesignModifierOptions()
            {
                Spacing = spacing,
                CoverSpace = cover,
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
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
                SetForegroundWindow(hwnd);
        }

        public void ShowSpinner()
        {
            this.pbSpinner.Visibility = System.Windows.Visibility.Visible;
        }

        public void HideSpinner()
        {
            this.pbSpinner.Visibility = System.Windows.Visibility.Collapsed;
        }

        // ========= ���������ռ��ֽ������ѡ��������״ =========

        private IEnumerable<Rebar> GetHostRebars()
        {
            if (this.rvtDoc == null || this.targetElementId == null) return Enumerable.Empty<Rebar>();

            var host = this.rvtDoc.GetElement(this.targetElementId);
            if (host == null) return Enumerable.Empty<Rebar>();

            var depIds = host.GetDependentElements(null);
            var rebars = depIds
                .Select(id => this.rvtDoc.GetElement(id) as Rebar)
                .Where(r => r != null && r.Category != null && r.Category.Id.IntegerValue == BuiltInCategory.OST_Rebar.GetHashCode());

            return rebars;
        }

        /// <summary>
        /// ��������ѡ��ĸֽ����ͣ��������ȣ�
        /// </summary>
        private ElementId GetPreferredRebarTypeId()
        {
            var rebars = this.GetHostRebars().ToList();
            if (rebars.Count == 0) return null;

            var typeId = rebars
                .Select(r => r.GetTypeId())
                .Where(id => id != null && id != ElementId.InvalidElementId)
                .GroupBy(id => id)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

            return typeId;
        }

        /// <summary>
        /// ��������ѡ��ĸֽ���״���������ȣ�����ȡ������״�������θֽ
        /// </summary>
        private ElementId GetPreferredRebarShapeId()
        {
            var ids = new List<ElementId>();
            foreach (var r in this.GetHostRebars())
            {
                try
                {
                    var sid = r.GetShapeId();
                    if (sid != null && sid != ElementId.InvalidElementId)
                        ids.Add(sid);
                }
                catch
                {
                    // �����θֽ�����ƥ��ʱ GetShapeId �������쳣������
                }
            }

            if (ids.Count == 0) return null;

            return ids
                .GroupBy(id => id)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();
        }

        // ========= �����㷴�����ʼ�� =========

        /// <summary>
        /// ͨ���Ѵ��ڸֽ�Ʊ����㣨mm�����㷨���� ScaleToBox�����γߴ�����ֽ�ֱ����
        /// Cover = (HostSize - (CenterlineSize + BarDiameter)) / 2
        /// ���ȡ X/Y ����Ľ�Сֵ��
        /// </summary>
        private double? ReverseComputeCoverFromExistingRebarMm()
        {
            if (this.rvtDoc == null || this.targetElementId == null) return null;

            var host = this.rvtDoc.GetElement(this.targetElementId) as FamilyInstance;
            if (host == null) return null;

            // ��������׼���봴��ʱһ�£�
            var geo = new GeometrySupport(host);
            var origin = geo.ProfilePoints[0];
            var xVec = geo.ProfilePoints[3] - origin;
            var yVec = geo.ProfilePoints[1] - origin;
            var xDir = xVec.Normalize();
            var yDir = yVec.Normalize();
            var hostX = xVec.GetLength();
            var hostY = yVec.GetLength();

            // ѡһ�������ֽ�
            var rebar = this.GetHostRebars().FirstOrDefault(r => r.IsRebarShapeDriven());
            if (rebar == null) return null;

            // �ֽ�ֱ��
            double barDia = 0.0;
            var barType = this.rvtDoc.GetElement(rebar.GetTypeId()) as RebarBarType;
            if (barType != null)
            {
                barDia = barType.BarModelDiameter;
                //ͨ����������ȡֱ�� 
                var pDia = barType.get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER).AsDouble();
            }

            var allCurves = rebar.GetTransformedCenterlineCurves(
                adjustForSelfIntersection: true,
                suppressHooks: true,
                suppressBendRadius: false,
                multiplanarOption: MultiplanarOption.IncludeOnlyPlanarCurves,
                barPositionIndex: 0);

            // ������������״����ֱ�ߺ�Բ��������ȡ���Ͷ�ģ�������ͬ���ܳ�������
            var majorityKind = allCurves
                .GroupBy(c => ClassifyCurveKind(c))
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Sum(c => c.Length))
                .Select(g => g.Key)
                .FirstOrDefault();

            var curves = allCurves.Where(c => ClassifyCurveKind(c) == majorityKind).ToList();
            if (curves.Count == 0) curves = allCurves.ToList(); // ����

            double minX = double.PositiveInfinity, maxX = double.NegativeInfinity;
            double minY = double.PositiveInfinity, maxY = double.NegativeInfinity;

            foreach (var c in curves)
            {
                // �������/�е�/�յ㣬���Ը���ֱ����Բ����ֵ
                var p0 = c.GetEndPoint(0);
                var p1 = c.Evaluate(0.5, true);
                var p2 = c.GetEndPoint(1);

                foreach (var p in new[] { p0, p1, p2 })
                {
                    var v = p - origin;
                    double sx = v.DotProduct(xDir);
                    double sy = v.DotProduct(yDir);
                    if (sx < minX) minX = sx;
                    if (sx > maxX) maxX = sx;
                    if (sy < minY) minY = sy;
                    if (sy > maxY) maxY = sy;
                }
            }

            if (double.IsInfinity(minX) || double.IsInfinity(maxX) || double.IsInfinity(minY) || double.IsInfinity(maxY))
                return null;

            var centerlineX = Math.Max(0.0, maxX - minX);
            var centerlineY = Math.Max(0.0, maxY - minY);

            // ����ߴ� = �����߳ߴ� + ֱ����������뾶��
            var outerX = centerlineX + barDia;
            var outerY = centerlineY + barDia;

            // ���� Cover��Ӣ�ߣ�
            var coverX = (hostX - outerX) / 2.0;
            var coverY = (hostY - outerY) / 2.0;

            // ȡ�ϴ�ֵ�������� 0 ��
            var coverFt = Math.Max(0.0, Math.Max(coverX, coverY));

            // ת�� mm
            return coverFt * 304.8;
        }

        // �������͹��ֱࣺ�ߡ�Բ��������
        private CurveKind ClassifyCurveKind(Curve c)
        {
            if (c is Line) return CurveKind.Line;
            if (c is Arc) return CurveKind.Arc;
            return CurveKind.Other;
        }

        private enum CurveKind
        {
            Line,
            Arc,
            Other
        }

        /// <summary>
        /// ��ȡ�����ı����㣨mm�������� Other ��Ϊ���˲���
        /// </summary>
        private double? GetPreferredRebarCoverMm()
        {
            if (this.rvtDoc == null || this.targetElementId == null) return null;

            var host = this.rvtDoc.GetElement(this.targetElementId);
            if (host == null) return null;

            var prefs = new[]
            {
                BuiltInParameter.CLEAR_COVER_OTHER,
                BuiltInParameter.CLEAR_COVER_EXTERIOR,
                BuiltInParameter.CLEAR_COVER_INTERIOR,
                BuiltInParameter.CLEAR_COVER_TOP,
                BuiltInParameter.CLEAR_COVER_BOTTOM
            };

            foreach (var bip in prefs)
            {
                var p = host.get_Parameter(bip);
                if (p == null || p.StorageType != StorageType.ElementId) continue;

                var coverTypeId = p.AsElementId();
                if (coverTypeId == null || coverTypeId == ElementId.InvalidElementId) continue;

                var coverType = this.rvtDoc.GetElement(coverTypeId) as RebarCoverType;
                if (coverType == null) continue;

                var mm = coverType.CoverDistance * 304.8;
                if (mm > 0) return mm;
            }

            return null;
        }

        /// <summary>
        /// �÷��ƽ����ʼ�������㣻ʧ��ʱ���˵�����������
        /// </summary>
        private void TryInitCoverByReverseFromExistingRebar()
        {
            var mm = this.ReverseComputeCoverFromExistingRebarMm() ?? this.GetPreferredRebarCoverMm();
            if (!mm.HasValue) return;

            // IntegerUpDown��������������ʾ�������� 0~150
            int v = (int)Math.Round(mm.Value, MidpointRounding.AwayFromZero);
            if (v < 0) v = 0;
            if (v > 150) v = 150;
            this.numInputRebarCoverSpace.Value = v;
        }
    }

    public class RebarTypeItem
    {
        public string Name { get; private set; }
        public ElementId Id { get; private set; }
        public string UniqueId { get; private set; }
        public override string ToString() => this.Name;

        public RebarTypeItem(RebarBarType rebarType)
        {
            this.Name = rebarType.Name;
            this.Id = rebarType.Id;
            this.UniqueId = rebarType.UniqueId;
        }
    }

    /// <summary>
    /// �ֽ���״
    /// </summary>
    public class RebarShapeItem
    {
        public string Name { get; private set; }
        public ElementId Id { get; private set; }
        public string UniqueId { get; private set; }
        public override string ToString() => this.Name;

        public RebarShapeItem(RebarShape rebarShape)
        {
            this.Name = rebarShape.Name;
            this.Id = rebarShape.Id;
            this.UniqueId = rebarShape.UniqueId;
        }
    }
}