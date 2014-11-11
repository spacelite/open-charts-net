﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OpenCharts
{
    public partial class cChart : UserControl
    {
        public cChart()
        {
            InitializeComponent();

            SetStyle(ControlStyles.DoubleBuffer, true);
            SetStyle(ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.UserPaint, true);

            _pointTooltip.OwnerDraw = true;
            _pointTooltip.Draw += _pointTooltip_Draw;
            _pointTooltip.Popup += _pointTooltip_Popup;

            _serCatOffset = new float[xAxis.Length];
            _serScale = new float[xAxis.Length];
            for (int i = 0; i < xAxis.Length; i++)
                _serScale[i] = 1;
        }

        #region Properties

        private int _borderWidth = 0;
        [Category("OpenCharts")]
        public int BorderWidth
        {
            get { return _borderWidth; }
            set { _borderWidth = value; }
        }

        private Color _borderColor = Color.FromArgb(0x45, 0x72, 0xA7);
        [Category("OpenCharts")]
        public Color BorderColor
        {
            get { return _borderColor; }
            set { _borderColor = value; }
        }

        private int _plotBorderWidth = 0;
        [Category("OpenCharts")]
        public int PlotBorderWidth
        {
            get { return _plotBorderWidth; }
            set { _plotBorderWidth = value; }
        }

        private Color _plotBorderColor = Color.FromArgb(0xC0, 0xC0, 0xC0);
        [Category("OpenCharts")]
        public Color PlotBorderColor
        {
            get { return _plotBorderColor; }
            set { _plotBorderColor = value; }
        }

        private cxAxis[] _xAxis = new cxAxis[] {
            new cxAxis()
        };
        [Category("OpenCharts"), TypeConverter(typeof(cxAxis_Converter))]
        public cxAxis[] xAxis
        {
            get { return _xAxis; }
            set
            {
                _xAxis = value;
                if (_xAxis == null)
                    _xAxis = new cxAxis[] { new cxAxis() };
                _serCatOffset = new float[_xAxis.Length];
                _serScale = new float[xAxis.Length];
                for (int i = 0; i < xAxis.Length; i++)
                    _serScale[i] = 1;
            }
        }

        private cyAxis _yAxis = new cyAxis();
        [Category("OpenCharts"), TypeConverter(typeof(cyAxis_Converter))]
        public cyAxis yAxis
        {
            get { return _yAxis; }
            set { _yAxis = value; }
        }

        private cSeries[] _Series = null;
        [Category("OpenCharts"), TypeConverter(typeof(cSeries_Converter))]
        public cSeries[] Series
        {
            get { return _Series; }
            set { _Series = value; }
        }

        private Color[] _seriesColorCollection = new Color[]
        {
            Color.FromArgb(0x7C, 0xB5, 0xEC),
            Color.FromArgb(0x43, 0x43, 0x48),
            Color.FromArgb(0x90, 0xED, 0x7D),
            Color.FromArgb(0xF7, 0xA3, 0x5C),
            Color.FromArgb(0x80, 0x85, 0xE9),
            Color.FromArgb(0xF1, 0x5C, 0x80),
            Color.FromArgb(0xE4, 0xD3, 0x54),
            Color.FromArgb(0x80, 0x85, 0xE8),
            Color.FromArgb(0x8D, 0x46, 0x53),
            Color.FromArgb(0x91, 0xE8, 0xE1)
        };
        [Category("OpenCharts")]
        public Color[] SeriesColorCollection
        {
            get { return _seriesColorCollection; }
            set { _seriesColorCollection = value; }
        }

        private Font _seriesFont = new Font("Segoe UI", 10);
        [Category("OpenCharts")]
        public Font SeriesFont
        {
            get { return _seriesFont; }
            set { _seriesFont = value; }
        }

        #endregion

        // Paint tools.
        private float[] _serScale;
        private float[] _serCatOffset;
        private eScrolling _serScrolling = eScrolling.None;
        private float _serScrollingPrevMouseX = 0;
        private int _serScrollingAxis = 0;

        private string _serFormat = "0";
        private PointF _plotUpperPoint;
        private PointF _plotLowerPoint;
        private Point _serSelectedPoint = new Point(-1, -1);

        private Point _pointBottomLeft;
        private int _pointBottomLeft_LeftOffset = 64;
        private int _pointBottomLeft_BottomOffset = 0;
        private Point _pointBottomRight;
        private int _pointBottomRight_RightOffset = 16;
        private int _pointBottomRight_BottomOffset = 0;

        private int[] _catsVisible;
        private float[] _catSkipFactor;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (!yAxis.GridValuesShow)
                _pointBottomLeft_LeftOffset = 32;

            _RecalculateDraw();

            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int _catsWidth = _pointBottomRight.X - _pointBottomLeft.X; // Yep, cats.
            if (_catsWidth < 0) _catsWidth = 0;
            float[] _widthPerCat = new float[xAxis.Length];
            _catsVisible = new int[xAxis.Length];
            _catSkipFactor = new float[xAxis.Length];
            int[] _catsToCount = new int[xAxis.Length];

            for (int i = 0; i < xAxis.Length; i++)
            {
                if (xAxis[i].Categories == null)
                    continue;
                _widthPerCat[i] = (float)_catsWidth / (float)xAxis[i].Categories.Length;
                _catsVisible[i] = xAxis[i].Categories.Length - (int)_serCatOffset[i];
                _catSkipFactor[i] = 1;
                if (_widthPerCat[i] < xAxis[i].CategoriesMinWidth)
                {
                    _catsVisible[i] = _catsWidth / xAxis[i].CategoriesMinWidth;
                    if (_catsVisible[i] == 0) continue;
                    _widthPerCat[i] = _catsWidth / _catsVisible[i];
                    _catSkipFactor[i] = ((float)xAxis[i].Categories.Length) / (float)_catsVisible[i];
                    _catSkipFactor[i] /= _serScale[i];
                    if (_catsVisible[i] >= (xAxis[i].Categories.Length / (_serScale[i])))
                        _catSkipFactor[i] = 1; // todo: not actually fix. 
                    if (_catSkipFactor[i] == 0)
                    {
                        _catSkipFactor[i] = 1;
                        //_serScale = _serScale > 1.5f ? _serScale - 1 : 1;
                    }
                }
                _catsToCount[i] = xAxis[i].Categories.Length / (int)_serScale[i];
            }

            // yAxis. Title.
            if (!String.IsNullOrEmpty(yAxis.Title.Text))
            {
                g.RotateTransform(270);
                StringFormat _titleFormat = new StringFormat();
                _titleFormat.Alignment = (StringAlignment)(int)yAxis.Title.Aligment;
                g.DrawString(yAxis.Title.Text, yAxis.Title.Font, new SolidBrush(yAxis.Title.Color), new RectangleF(-(_plotLowerPoint.Y), 0, _plotLowerPoint.Y - _plotUpperPoint.Y, 20), _titleFormat);
                g.RotateTransform(90);
            }

            string _tooltipValue = String.Empty;
            Color _tooltipColor = Color.White;

            // Series.
            if (_Series != null && _Series.Length > 0)
            {
                // Set upper && lower limits.
                float _upperLimit = 0;
                float _lowerLimit = 0;
                foreach (cSeries _ser in Series)
                {

                    if (_ser.Data != null)
                        foreach (string _data in _ser.Data)
                        {
                            double _ddata = 0;
                            if (double.TryParse(_data, out _ddata))
                            {
                                if (_ddata > _upperLimit)
                                    _upperLimit = (float)_ddata;
                                if (_ddata < _lowerLimit)
                                    _lowerLimit = (float)_ddata;
                            }
                        }
                }
                _upperLimit += _upperLimit * .1f;
                if (_lowerLimit < 0)
                    _lowerLimit += _lowerLimit * .1f;
                else
                    _lowerLimit += _lowerLimit / .1f;

                float _pixelsPerPoint = (_plotLowerPoint.Y - _plotUpperPoint.Y) / (_upperLimit - _lowerLimit);

                // Draw grid.
                if (yAxis.GridLineCount > 0)
                {
                    float _gridLinesHeight = (_plotLowerPoint.Y - _plotUpperPoint.Y) / yAxis.GridLineCount;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
                    for (int i = 0; i < yAxis.GridLineCount; i++)
                    {
                        PointF _gridPoint = new PointF(_pointBottomLeft_LeftOffset, _plotLowerPoint.Y - _gridLinesHeight * (i));
                        if (i != 0) g.DrawLine(Pens.LightGray, _gridPoint.X, _gridPoint.Y, this.Width - _pointBottomRight_RightOffset, _gridPoint.Y);
                        if (yAxis.GridValuesShow)
                        {
                            float _gridPointValue = (((i) * _gridLinesHeight) / _pixelsPerPoint) + _lowerLimit;
                            string gridPointValueString = yAxis.GridValuesToShort ? _TransformNumberToShort(_gridPointValue) : _gridPointValue.ToString(_serFormat);
                            g.DrawString(gridPointValueString, yAxis.GridValuesFont, new SolidBrush(yAxis.GridValuesColor), new PointF(_gridPoint.X - 36, _gridPoint.Y - 10));
                        }
                    }
                }

                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                int _serPointColor_current = 0;
                Color _serPointColor = _seriesColorCollection[_serPointColor_current];

                for (int xa = 0; xa < xAxis.Length; xa++)
                {
                    if (xAxis[xa].Categories != null)
                    {
                        int columnscount = Series.Count(x => x.Type == cSeries.eType.Column);
                        float columnwidth = ((_widthPerCat[xa] - 8) / columnscount) / _catSkipFactor[xa];
                        int columnindex = 0;

                        for (int k = 0; k < Series.Length; k++)
                        {
                            if (Series[k].Data != null && xa == Series[k].xAxis)
                            {
                                List<cSeries.Point> _serPoints = new List<cSeries.Point>();
                                for (int i = (int)_serCatOffset[xa]; i < Series[k].Data.Length && i < _catsToCount[xa] + _serCatOffset[xa]; i++)
                                {
                                    double _datavalue = 0;
                                    if (double.TryParse(Series[k].Data[i], out _datavalue))
                                        _serPoints.Add(new cSeries.Point(
                                            _pointBottomLeft.X + ((float)_catsWidth / (float)_catsToCount[xa]) / 2 + ((float)_catsWidth / (float)_catsToCount[xa]) * (i - (int)_serCatOffset[xa]),
                                            _plotLowerPoint.Y - ((float)_datavalue - _lowerLimit) * _pixelsPerPoint));
                                    else
                                        _serPoints.Add(null);
                                }

                                SolidBrush _serEllipseBrush = new SolidBrush(_serPointColor);
                                int ar = _serPointColor.R; ar = ar < 200 ? ar + 50 : 250;
                                int ag = _serPointColor.G; ag = ag < 200 ? ag + 50 : 250;
                                int ab = _serPointColor.B; ab = ab < 200 ? ab + 50 : 250;
                                int aa = 100;
                                SolidBrush _areaBrush = new SolidBrush(Color.FromArgb(aa, ar, ag, ab));

                                Pen _serLinePen = new Pen(_serPointColor);
                                _serLinePen.Width = 2 / _catSkipFactor[xa];
                                if (_serLinePen.Width == 0)
                                    _serLinePen.Width = 1;

                                for (int i = 0; i < _serPoints.Count; i++)
                                {
                                    if (_serPoints[i] != null)
                                    {
                                        if (_serPoints[i].X > _pointBottomRight.X)
                                            continue; // dunno.
                                        switch (Series[k].Type)
                                        {
                                            case cSeries.eType.Point:
                                                g.FillEllipse(_serEllipseBrush, _serPoints[i].X - 4 / _catSkipFactor[xa], _serPoints[i].Y - 4 / _catSkipFactor[xa], 8 / _catSkipFactor[xa], 8 / _catSkipFactor[xa]);
                                                break;
                                            case cSeries.eType.Line:
                                                g.FillEllipse(_serEllipseBrush, _serPoints[i].X - 4 / _catSkipFactor[xa], _serPoints[i].Y - 4 / _catSkipFactor[xa], 8 / _catSkipFactor[xa], 8 / _catSkipFactor[xa]);
                                                if (i + 1 < _serPoints.Count && _serPoints[i + 1] != null)
                                                {
                                                    if (_serPoints[i + 1].X > _pointBottomRight.X)
                                                        continue; // dunno.
                                                    g.DrawLine(_serLinePen, _serPoints[i].X, _serPoints[i].Y, _serPoints[i + 1].X, _serPoints[i + 1].Y);
                                                }
                                                break;
                                            case cSeries.eType.Column:
                                                g.FillRectangle(_serEllipseBrush, _serPoints[i].X + (columnscount > 1 ? -columnwidth * columnscount / 2 + columnwidth * columnindex : 0), _serPoints[i].Y, columnwidth, _plotLowerPoint.Y - _serPoints[i].Y);
                                                break;
                                            case cSeries.eType.Area:
                                                if (i + 1 < _serPoints.Count && _serPoints[i + 1] != null && i < _catsToCount[xa])
                                                {
                                                    if (_serPoints[i + 1].X > _pointBottomRight.X)
                                                        continue; // dunno.
                                                    List<PointF> _areaPoints = new List<PointF>();
                                                    _areaPoints.Add(new PointF(_serPoints[i].X, _serPoints[i].Y));
                                                    _areaPoints.Add(new PointF(_serPoints[i + 1].X, _serPoints[i + 1].Y));
                                                    _areaPoints.Add(new PointF(_serPoints[i + 1].X, _plotLowerPoint.Y));
                                                    _areaPoints.Add(new PointF(_serPoints[i].X, _plotLowerPoint.Y));
                                                    g.FillPolygon(_areaBrush, _areaPoints.ToArray());
                                                    g.DrawLine(_serLinePen, _serPoints[i].X, _serPoints[i].Y, _serPoints[i + 1].X, _serPoints[i + 1].Y);
                                                }
                                                else
                                                    g.FillEllipse(_serEllipseBrush, _serPoints[i].X - 4 / _catSkipFactor[xa], _serPoints[i].Y - 4 / _catSkipFactor[xa], 8 / _catSkipFactor[xa], 8 / _catSkipFactor[xa]);
                                                break;
                                            case cSeries.eType.AreaSolid:
                                                if (i + 1 < _serPoints.Count && _serPoints[i + 1] != null && i < _catsToCount[xa])
                                                {
                                                    if (_serPoints[i + 1].X > _pointBottomRight.X)
                                                        continue; // dunno.
                                                    float half_dis = (_serPoints[i + 1].X - _serPoints[i].X) / 2;
                                                    List<PointF> _areaPoints = new List<PointF>();
                                                    _areaPoints.Add(new PointF(_serPoints[i].X - half_dis, _serPoints[i].Y));
                                                    _areaPoints.Add(new PointF(_serPoints[i].X + half_dis, _serPoints[i].Y));
                                                    _areaPoints.Add(new PointF(_serPoints[i].X + half_dis, _serPoints[i + 1].Y));
                                                    _areaPoints.Add(new PointF(_serPoints[i + 1].X + half_dis, _serPoints[i + 1].Y));
                                                    _areaPoints.Add(new PointF(_serPoints[i + 1].X + half_dis, _plotLowerPoint.Y));
                                                    _areaPoints.Add(new PointF(_serPoints[i].X - half_dis, _plotLowerPoint.Y));
                                                    g.FillPolygon(_areaBrush, _areaPoints.ToArray());
                                                    g.DrawLine(_serLinePen, _serPoints[i].X - half_dis, _serPoints[i].Y, _serPoints[i].X + half_dis, _serPoints[i].Y);
                                                    g.DrawLine(_serLinePen, _serPoints[i].X + half_dis, _serPoints[i].Y, _serPoints[i].X + half_dis, _serPoints[i + 1].Y);
                                                    g.DrawLine(_serLinePen, _serPoints[i].X + half_dis, _serPoints[i + 1].Y, _serPoints[i + 1].X + half_dis, _serPoints[i + 1].Y);
                                                }
                                                else
                                                    g.FillEllipse(_serEllipseBrush, _serPoints[i].X - 4 / _catSkipFactor[xa], _serPoints[i].Y - 4 / _catSkipFactor[xa], 8 / _catSkipFactor[xa], 8 / _catSkipFactor[xa]);
                                                break;
                                        }
                                    }
                                }
                                if (Series[k].Type == cSeries.eType.Column)
                                    columnindex++;

                                // Next color.
                                _serPointColor_current++;
                                if (_seriesColorCollection.Length <= _serPointColor_current)
                                    _serPointColor_current = 0;
                                _serPointColor = _seriesColorCollection[_serPointColor_current];
                            }
                        }
                    }
                }
            }


            // Categories.
            for (int xa = 0; xa < xAxis.Length; xa++)
            {
                // Bottom line.
                int _bottomLineHeight = 4;
                Pen _bottomLinePen = new Pen(xAxis[xa].BottomLineColor);
                g.DrawLine(_bottomLinePen, _pointBottomLeft.X, _pointBottomLeft.Y + 24 * xa, _pointBottomRight.X, _pointBottomRight.Y + 24 * xa);
                g.DrawLine(_bottomLinePen, _pointBottomLeft.X, _pointBottomLeft.Y + 24 * xa, _pointBottomLeft.X, _pointBottomLeft.Y + 24 * xa + _bottomLineHeight);
                g.DrawLine(_bottomLinePen, _pointBottomRight.X, _pointBottomLeft.Y + 24 * xa, _pointBottomRight.X, _pointBottomRight.Y + 24 * xa + _bottomLineHeight);

                if (xAxis[xa].Categories != null)
                {
                    SolidBrush _catBrush = new SolidBrush(xAxis[xa].CategoriesColor);
                    StringFormat _catFormat = new StringFormat();
                    _catFormat.Alignment = StringAlignment.Center;

                    int _catIndex = 0;
                    for (float i = 0; _catIndex < _catsVisible[xa] && i + _serCatOffset[xa] < xAxis[xa].Categories.Length; i += _catSkipFactor[xa], _catIndex++)
                    {
                        Point _linePoint = new Point(_pointBottomLeft.X + (_catIndex + 1) * (int)_widthPerCat[xa], _pointBottomLeft.Y);
                        if (_catIndex + 1 != _catsVisible[xa])
                            g.DrawLine(_bottomLinePen, _linePoint.X, _linePoint.Y + 24 * xa, _linePoint.X, _linePoint.Y + _bottomLineHeight + 24 * xa);
                        if (i + _serCatOffset[xa] < xAxis[xa].Categories.Length)
                        {
                            float temp = 0;
                            try
                            {
                                string catvalue = xAxis[xa].Categories[(int)_serCatOffset[xa] + (int)Math.Ceiling(i)];
                                if (float.TryParse(catvalue, out temp))
                                    catvalue = _TransformNumberToShort(temp);
                                g.DrawString(catvalue, xAxis[xa].CategoriesFont, _catBrush, new RectangleF(_linePoint.X - _widthPerCat[xa], _linePoint.Y + 2 + 24 * xa, _widthPerCat[xa], 14), _catFormat);
                            }
                            catch (IndexOutOfRangeException)
                            {
                                // Dunno why.
                                _serCatOffset[xa] -= 2;
                                i -= _catSkipFactor[xa];
                                _catIndex--;
                            }
                        }
                    }
                }

                // Scrollbar.
                if (_serScale[xa] > 1)
                {
                    int maxoffset = xAxis[xa].Categories.Length - _catsVisible[xa];
                    if (maxoffset > 0)
                    {
                        float scrollbarwidth = _pointBottomRight.X - _pointBottomLeft.X;
                        float scrollwidth = ((float)_pointBottomRight.X - (float)_pointBottomLeft.X - 4) / _serScale[xa];
                        float tsw = scrollbarwidth - scrollwidth - 4;

                        g.DrawRectangle(Pens.LightGray, _pointBottomLeft.X, _pointBottomLeft.Y - 8 + 24 * xa, scrollbarwidth, 8);
                        g.FillRectangle(new SolidBrush(Color.Gray), _pointBottomLeft.X + 2 + tsw * ((float)_serCatOffset[xa] / ((float)xAxis[xa].Categories.Length - _catsVisible[xa] * _catSkipFactor[xa])), _pointBottomLeft.Y - 6 + 24 * xa, scrollwidth, 4);
                    }
                }
                if (xAxis.Length > 1)
                {
                    if (_serScale[xa] > 1)
                        g.DrawRectangle(Pens.LightGray, _pointBottomLeft.X - 12, _pointBottomLeft.Y - 8 + 24 * xa, 8, 8);
                    if (_serScrollingAxis == xa)
                    {
                        g.DrawRectangle(Pens.LightGray, _pointBottomLeft.X - 12, _pointBottomLeft.Y - 8 + 24 * xa, 8, 8);
                        g.FillRectangle(Brushes.LightGray, _pointBottomLeft.X - 10, _pointBottomLeft.Y - 6 + 24 * xa, 4, 4);
                    }
                }
            }

            // Zoom value.
            if (_serScale[_serScrollingAxis] > 1)
                g.DrawString((_serScale[_serScrollingAxis] * 100).ToString() + "%", new Font("Segoe UI", 10), new SolidBrush(Color.Gray), this.Width - 48, 8);

            // Borders (chart + plot).
            if (BorderWidth > 0)
            {
                Pen _borderPen = new Pen(BorderColor);
                _borderPen.Width = BorderWidth;
                g.DrawRectangle(_borderPen, 0, 0, this.Width - 1, this.Height - 1);
            }
            if (PlotBorderWidth > 0)
            {
                Pen _plotBorderPen = new Pen(PlotBorderColor);
                _plotBorderPen.Width = PlotBorderWidth;
                g.DrawRectangle(_plotBorderPen, _plotUpperPoint.X, _plotUpperPoint.Y, _pointBottomRight.X - _plotUpperPoint.X, _plotLowerPoint.Y - _plotUpperPoint.Y);
            }
        }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            // TODO: rework here.
            return;

            /*int _catsWidth = _pointBottomRight.X - _pointBottomLeft.X; // Yep, cats.
            float _widthPerCat = (float)_catsWidth / (float)xAxis.Categories.Length;
            _catsVisible = xAxis.Categories.Length - (int)_serCatOffset;
            _catSkipFactor = 1;
            if (_widthPerCat < xAxis.CategoriesMinWidth)
            {
                _catsVisible = _catsWidth / xAxis.CategoriesMinWidth;
                _widthPerCat = _catsWidth / _catsVisible;
                _catSkipFactor = ((float)xAxis.Categories.Length) / (float)_catsVisible;
                _catSkipFactor /= _serScale;
                if (_catsVisible >= (xAxis.Categories.Length / (_serScale)))
                    _catSkipFactor = 1; // todo: not actually fix. 
                if (_catSkipFactor == 0)
                {
                    _catSkipFactor = 1;
                    //_serScale = _serScale > 1.5f ? _serScale - 1 : 1;
                }
            }

            int _catsToCount = xAxis.Categories.Length / (int)_serScale;

            // Series.
            if (_Series != null && _Series.Length > 0)
            {
                // Set upper && lowe limits.
                float _upperLimit = 0;
                float _lowerLimit = 0;
                foreach (cSeries _ser in Series)
                {

                    if (_ser.Data != null)
                        foreach (string _data in _ser.Data)
                        {
                            double _ddata = 0;
                            if (double.TryParse(_data, out _ddata))
                            {
                                if (_ddata > _upperLimit)
                                    _upperLimit = (float)_ddata;
                                if (_ddata < _lowerLimit)
                                    _lowerLimit = (float)_ddata;
                            }
                        }
                }
                _upperLimit += _upperLimit * .1f;
                if (_lowerLimit < 0)
                    _lowerLimit += _lowerLimit * .1f;
                else
                    _lowerLimit += _lowerLimit / .1f;

                float _pixelsPerPoint = (_plotLowerPoint.Y - _plotUpperPoint.Y) / (_upperLimit - _lowerLimit);

                int _serPointColor_current = 0;
                Color _serPointColor = _seriesColorCollection[_serPointColor_current];

                bool _clearSelected = false;
                int _sern = 0;
                if (xAxis.Categories != null)
                    for (int k = 0; k < Series.Length; k++)
                    {
                        if (Series[k].Data != null)
                        {
                            List<cSeries.Point> _serPoints = new List<cSeries.Point>();
                            for (int i = (int)_serCatOffset; i < Series[k].Data.Length && i < _catsToCount + _serCatOffset; i++)
                            {
                                double _datavalue = 0;
                                if (double.TryParse(Series[k].Data[i], out _datavalue))
                                    _serPoints.Add(new cSeries.Point(
                                        _pointBottomLeft.X + ((float)_catsWidth / (float)_catsToCount) / 2 + ((float)_catsWidth / (float)_catsToCount) * (i - (int)_serCatOffset),
                                        _plotLowerPoint.Y - ((float)_datavalue - _lowerLimit) * _pixelsPerPoint));
                                else
                                    _serPoints.Add(null);
                            }


                            for (int i = 0; i < _serPoints.Count; i++)
                            {
                                if (_serPoints[i] != null)
                                {

                                    if (e.X > _serPoints[i].X - 8 && e.X < _serPoints[i].X + 16 &&
                                        e.Y > _serPoints[i].Y - 8 && e.Y < _serPoints[i].Y + 16)
                                    {
                                        if (_serSelectedPoint.X == _sern && _serSelectedPoint.Y == i)
                                            _clearSelected = true;
                                        else
                                        {
                                            _serSelectedPoint = new Point(_sern, i + (int)_serCatOffset);
                                            _pointTooltipInfo.Point = new PointF(_serPoints[i].X, _serPoints[i].Y);
                                            _pointTooltipInfo.CatIndex = i + (int)_serCatOffset;
                                            _pointTooltipInfo.Value = Series[k].Name + ": " + Series[k].Data[i + (int)_serCatOffset];
                                            _pointTooltipInfo.Color = _serPointColor;
                                            _pointTooltipInfo.Point = new PointF(_pointTooltipInfo.Point.X - _pointTooltipInfo.Width / 2, _pointTooltipInfo.Point.Y - _pointTooltipInfo.Height - 8);
                                            if (_pointTooltipInfo.Point.Y < 0)
                                                _pointTooltipInfo.Point = new PointF(_pointTooltipInfo.Point.X, _pointTooltipInfo.Point.Y + _pointTooltipInfo.Height + 12);
                                            // Shadow.
                                            _pointTooltip.Show("some", this, (int)_pointTooltipInfo.Point.X, (int)_pointTooltipInfo.Point.Y);
                                            //this.Refresh();
                                            return;
                                        }
                                    }
                                }
                            }
                        }
                        _sern++;
                        // Next color.
                        _serPointColor_current++;
                        if (_seriesColorCollection.Length <= _serPointColor_current)
                            _serPointColor_current = 0;
                        _serPointColor = _seriesColorCollection[_serPointColor_current];
                    }
                //if (_clearSelected)
                {
                    _serSelectedPoint = new Point(-1, -1);
                    _pointTooltip.Hide(this);
                }

            }*/
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (e.Delta > 0)
            {
                if (_catsVisible[_serScrollingAxis] < (xAxis[_serScrollingAxis].Categories.Length / (_serScale[_serScrollingAxis])))
                    if (_serScale[_serScrollingAxis] < xAxis[_serScrollingAxis].MaxZoom || xAxis[_serScrollingAxis].MaxZoom < 0)
                        _serScale[_serScrollingAxis]++;
            }
            else
            {
                _serScale[_serScrollingAxis]--;
            }
            if (_serScale[_serScrollingAxis] <= 1)
            {
                _serScale[_serScrollingAxis] = 1;
                //for (int i = 0; i < xAxis.Length; i++)
                _serCatOffset[_serScrollingAxis] = 0;
                this.Refresh();
            }
            else
                this.Refresh();
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            // Check scrollbar.
            if (_serScale[_serScrollingAxis] != -1)
                for (int i = 0; i < xAxis.Length; i++)
                    if (e.Location.Y > _pointBottomLeft.Y - 8 + i * 24 && e.Location.Y < _pointBottomLeft.Y + 8 + i * 24)
                    {
                        _serScrollingPrevMouseX = e.Location.X;
                        _serScrolling = eScrolling.Horizontal;
                        _serScrollingAxis = i;
                        break;
                    }
                    else
                    {
                        _serScrollingPrevMouseX = e.Location.X;
                        _serScrolling = eScrolling.HorizonatlReversed;
                        //_serScrollingAxis = 0;
                    }
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_serScrolling != eScrolling.None)
            {
                this.Refresh();
                _serScrolling = eScrolling.None;
                //_serScrollingAxis = -1;
            }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_serScrolling != eScrolling.None && _serScale[_serScrollingAxis] > 1)
            {
                float scrollbarwidth = _pointBottomRight.X - _pointBottomLeft.X;
                float scrollwidth = ((float)_pointBottomRight.X - (float)_pointBottomLeft.X - 4) / _serScale[_serScrollingAxis];
                float tsw = scrollbarwidth - scrollwidth - 4;
                float pixelsPerCat = tsw * ((float)1 / ((float)xAxis[_serScrollingAxis].Categories.Length - _catsVisible[_serScrollingAxis] * _catSkipFactor[_serScrollingAxis]));
                float mcats = Math.Abs(e.Location.X - _serScrollingPrevMouseX);
                mcats /= pixelsPerCat;

                if (e.Location.X > _serScrollingPrevMouseX)
                {
                    _serCatOffset[_serScrollingAxis] += _serScrolling == eScrolling.Horizontal ? mcats : -mcats;
                }
                else
                    _serCatOffset[_serScrollingAxis] -= _serScrolling == eScrolling.Horizontal ? mcats : -mcats; ;
                if (_serCatOffset[_serScrollingAxis] < 0)
                    _serCatOffset[_serScrollingAxis] = 0;
                else
                {

                    if (_serCatOffset[_serScrollingAxis] > xAxis[_serScrollingAxis].Categories.Length - _catSkipFactor[_serScrollingAxis] * _catsVisible[_serScrollingAxis])
                        _serCatOffset[_serScrollingAxis] = Convert.ToInt32(xAxis[_serScrollingAxis].Categories.Length - _catSkipFactor[_serScrollingAxis] * _catsVisible[_serScrollingAxis]);
                    else
                        this.Refresh();
                }
                _serScrollingPrevMouseX = e.Location.X;
            }
        }

        private void _pointTooltip_Popup(object sender, PopupEventArgs e)
        {
            e.ToolTipSize = new Size((int)_pointTooltipInfo.Width, (int)_pointTooltipInfo.Height);
        }
        private void _pointTooltip_Draw(object sender, DrawToolTipEventArgs e)
        {
            Graphics gr = e.Graphics;
            gr.Clear(Color.White);

            float width = _pointTooltipInfo.Width;
            float height = _pointTooltipInfo.Height;

            // Shadow.
            // g.FillRectangle(new SolidBrush(Color.FromArgb(50, 0, 0, 0)), 2, height, width - 2, 2);
            //g.FillRectangle(new SolidBrush(Color.FromArgb(50, 0, 0, 0)), width, 2, 2, height);
            gr.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
            //g.FillRectangle(new SolidBrush(Color.FromArgb(150, 255, 255, 255)), 0, 0, width - 1, height - 1);

            int r = _pointTooltipInfo.Color.R - 50; r = r < 0 ? 0 : r;
            int g = _pointTooltipInfo.Color.G - 50; g = g < 0 ? 0 : g;
            int b = _pointTooltipInfo.Color.B - 50; b = b < 0 ? 0 : b;

            _pointTooltipInfo.Color = Color.FromArgb(r, g, b);
            gr.DrawRectangle(new Pen(_pointTooltipInfo.Color), 0, 0, width - 1, height - 1);
            gr.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            gr.FillEllipse(new SolidBrush(_pointTooltipInfo.Color), 4, 19, 8, 8);
            //gr.DrawString(xAxis.Categories[_pointTooltipInfo.CatIndex], xAxis.CategoriesFont, new SolidBrush(xAxis.CategoriesColor), new PointF(6, 0));
            //gr.DrawString(_pointTooltipInfo.Value, xAxis.CategoriesFont, new SolidBrush(xAxis.CategoriesColor), new PointF(16, 16));
        }

        private void _RecalculateDraw()
        {
            _pointBottomLeft = new Point(_pointBottomLeft_LeftOffset, this.Height - _pointBottomLeft_BottomOffset - 24 * xAxis.Length);
            _pointBottomRight = new Point(this.Width - _pointBottomRight_RightOffset, this.Height - _pointBottomRight_BottomOffset - 24 * xAxis.Length);

            _plotUpperPoint = new PointF(_pointBottomLeft_LeftOffset, 32);
            _plotLowerPoint = new PointF(_pointBottomLeft_LeftOffset, _pointBottomLeft.Y - _seriesFont.Height);
        }
        private string _TransformNumberToShort(float number)
        {
            string val = number.ToString(_serFormat);
            if (Math.Abs(number) > 1000000000)
                val = val.Remove(val.Length - 9, 9) + "B";
            else
                if (Math.Abs(number) > 1000000)
                    val = val.Remove(val.Length - 6, 6) + "M";
                else
                    if (Math.Abs(number) > 1000)
                        val = val.Remove(val.Length - 3, 3) + "K";

            return val;
        }

        private ToolTip _pointTooltip = new ToolTip();
        private TooltipInfo _pointTooltipInfo = new TooltipInfo();
    }
    public enum eScrolling
    {
        None,
        Horizontal,
        HorizonatlReversed
    }

    public class cxAxis
    {
        private string[] _Categories;
        public string[] Categories
        {
            get { return _Categories; }
            set { _Categories = value; }
        }
        private Font _CategoriesFont = new Font("Segoe UI", 8, FontStyle.Bold);
        public Font CategoriesFont
        {
            get { return _CategoriesFont; }
            set { _CategoriesFont = value; }
        }
        private Color _CategoriesColor = Color.FromArgb(70, 70, 70);
        public Color CategoriesColor
        {
            get { return _CategoriesColor; }
            set { _CategoriesColor = value; }
        }
        private int _CategoriesMinWidth = 32;
        public int CategoriesMinWidth
        {
            get { return _CategoriesMinWidth; }
            set { _CategoriesMinWidth = value; }
        }

        private int _maxZoom = -1;
        public int MaxZoom
        {
            get { return _maxZoom; }
            set { _maxZoom = value; }
        }
        // Bottom line.
        private Color _BottomLineColor = Color.FromArgb(0xC0, 0xD0, 0xE0);
        public Color BottomLineColor
        {
            get { return _BottomLineColor; }
            set { _BottomLineColor = value; }
        }
    }
    public class cxAxis_Converter : ExpandableObjectConverter
    {
        public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
        {
            cxAxis axis = (cxAxis)value;
            string cats = String.Empty;
            foreach (string cat in axis.Categories)
                cats += cat + "; ";
            return cats;
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    public class cyAxis
    {
        private cTitle _Title = new cTitle();
        [TypeConverter(typeof(cTitle_Converter))]
        public cTitle Title
        {
            get { return _Title; }
            set { _Title = value; }
        }

        // Grid.
        private Color _GridLineColor = Color.FromArgb(0xC0, 0xC0, 0xC0);
        public Color GridLineColor
        {
            get { return _GridLineColor; }
            set { _GridLineColor = value; }
        }
        private int _GridLineCount = 8;
        public int GridLineCount
        {
            get { return _GridLineCount; }
            set { _GridLineCount = value; }
        }
        private bool _GridValuesShow = true;
        public bool GridValuesShow
        {
            get { return _GridValuesShow; }
            set { _GridValuesShow = value; }
        }
        private bool _GridValuesToShort = true;
        public bool GridValuesToShort
        {
            get { return _GridValuesToShort; }
            set { _GridValuesToShort = value; }
        }
        private Font _GridValuesFont = new Font("Segoe UI", 8);
        public Font GridValuesFont
        {
            get { return _GridValuesFont; }
            set { _GridValuesFont = value; }
        }
        private Color _GridValuesColor = Color.FromArgb(70, 70, 70);
        public Color GridValuesColor
        {
            get { return _GridValuesColor; }
            set { _GridValuesColor = value; }
        }

        public class cTitle
        {
            private string _Text = String.Empty;
            public string Text
            {
                get { return _Text; }
                set { _Text = value; }
            }
            private Font _Font = new Font("Consolas", 12, FontStyle.Bold);
            public Font Font
            {
                get { return _Font; }
                set { _Font = value; }
            }
            private Color _Color = Color.FromArgb(50, 50, 50);
            public Color Color
            {
                get { return _Color; }
                set { _Color = value; }
            }
            private AligmentVertical _Aligment = AligmentVertical.Middle;
            public AligmentVertical Aligment
            {
                get { return _Aligment; }
                set { _Aligment = value; }
            }
        }
        public class cTitle_Converter : ExpandableObjectConverter
        {
            public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
            {
                return ((cTitle)value).Text;
            }
        }
    }
    public class cyAxis_Converter : ExpandableObjectConverter
    {
        public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
        {
            return String.Empty;
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    public class cSeries
    {
        private string _Name = String.Empty;
        public string Name
        {
            get { return _Name; }
            set { _Name = value; }
        }
        private string[] _Data;
        public string[] Data
        {
            get { return _Data; }
            set { _Data = value; }
        }
        private eType _Type = eType.Line;
        public eType Type
        {
            get { return _Type; }
            set { _Type = value; }
        }
        private int _xAxis = 0;
        public int xAxis
        {
            get { return _xAxis; }
            set { _xAxis = value; }
        }

        public class Point
        {
            public float X;
            public float Y;
            public Point(float x, float y)
            {
                X = x;
                Y = y;
            }
            public bool Selected;
        }
        public enum eType
        {
            Point,
            Line,
            Column,
            Area,
            AreaSolid // dunno.
        }
    }
    public class cSeries_Converter : TypeConverter
    {
        public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
        {
            cSeries[] val = (cSeries[])value;
            string sval = String.Empty;
            foreach (cSeries s in val)
                sval += s.Name + "; ";
            return sval;
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    public enum AligmentHorizontal
    {
        Left,
        Center,
        Right
    }
    public enum AligmentVertical
    {
        Low,
        Middle,
        High
    }

    public class TooltipInfo
    {
        public PointF Point = new PointF();
        public int CatIndex = -1;
        public string Value = String.Empty;
        public Color Color = Color.White;
        public float Width = 104;
        public float Height = 32;
    }
}
