using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
namespace DevDash.Controls
{
    /// <summary>
    /// Add this component in runtime or designtime and assign a datagridview to it to enable grouping on that grid.
    /// You can also add an <see cref="DataGridViewGrouperControl"/> which will create its own grouper.
    /// </summary>
    [DefaultEvent("DisplayGroup")]
    public partial class DataGridViewGrouper : Component
    {
        private DataGridView _grid;
        private bool _selectionSet;
        private Point _capturedCollapseBox = new Point(-1, -1);
        private readonly GroupingSource _source = new GroupingSource();
        private readonly List<int> _selectedGroups = new List<int>();

        public event EventHandler PropertiesChanged;

        public DataGridViewGrouper()
        {
            _source.DataSourceChanged += new EventHandler(Source_DataSourceChanged);
            _source.Grouper = this;
        }

        public DataGridViewGrouper(DataGridView grid) : this() => DataGridView = grid;

        [DefaultValue(null)]
        public DataGridView DataGridView
        {
            get => _grid;
            set
            {
                if (_grid == value) return;
                if (_grid != null)
                {
                    _grid.RowPrePaint -= new DataGridViewRowPrePaintEventHandler(Grid_RowPrePaint);
                    _grid.CellBeginEdit -= new DataGridViewCellCancelEventHandler(Grid_CellBeginEdit);
                    _grid.CellClick -= new DataGridViewCellEventHandler(Grid_CellClick);
                    _grid.MouseMove -= new MouseEventHandler(Grid_MouseMove);
                    _grid.SelectionChanged -= new EventHandler(Grid_SelectionChanged);
                    _grid.DataSourceChanged -= new EventHandler(Grid_DataSourceChanged);
                    _grid.AllowUserToAddRowsChanged -= new EventHandler(Grid_AllowUserToAddRowsChanged);
                }
                RemoveGrouping();
                _selectedGroups.Clear();
                _grid = value;
                if (_grid != null)
                {
                    _grid.RowPrePaint += new DataGridViewRowPrePaintEventHandler(Grid_RowPrePaint);
                    _grid.CellBeginEdit += new DataGridViewCellCancelEventHandler(Grid_CellBeginEdit);
                    _grid.CellClick += new DataGridViewCellEventHandler(Grid_CellClick);
                    _grid.MouseMove += new MouseEventHandler(Grid_MouseMove);
                    _grid.SelectionChanged += new EventHandler(Grid_SelectionChanged);
                    _grid.DataSourceChanged += new EventHandler(Grid_DataSourceChanged);
                    _grid.AllowUserToAddRowsChanged += new EventHandler(Grid_AllowUserToAddRowsChanged);
                }
            }
        }

        void Grid_AllowUserToAddRowsChanged(object sender, EventArgs e) => _source.CheckNewRow();

        #region Select/Collapse/Expand
        void Grid_MouseMove(object sender, MouseEventArgs e)
        {
            {
                DataGridView.HitTestInfo ht = _grid.HitTest(e.X, e.Y);
                if (IsGroupRow(ht.RowIndex))
                {
                    CheckCollapsedFocused(ht.ColumnIndex, ht.RowIndex);
                    return;
                }
            }
            CheckCollapsedFocused(-1, -1);
        }

        void InvalidateCapturedBox()
        {
            if (_capturedCollapseBox.Y == -1) return;
            try
            {
                _grid.InvalidateCell(_capturedCollapseBox.X, _capturedCollapseBox.Y);
            }
            catch
            {
                _capturedCollapseBox = new Point(-1, -1);
            }
        }

        void CheckCollapsedFocused(int col, int row)
        {
            if (_capturedCollapseBox.X != col || _capturedCollapseBox.Y != row)
            {
                InvalidateCapturedBox();
                _capturedCollapseBox = new Point(col, row);
                InvalidateCapturedBox();
            }
        }

        void Grid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex != -1 && e.RowIndex == _capturedCollapseBox.Y)
            {
                var groupRow = GetGroupRow(e.RowIndex);
                groupRow.Collapsed = !groupRow.Collapsed;
            }
        }

        void Grid_SelectionChanged(object sender, EventArgs e)
        {
            if (_selectionSet)
            {
                _selectionSet = false;
                InvalidateSelected();
            }
        }

        void SetSelection()
        {
            _selectionSet = true;
            _selectedGroups.Clear();
            foreach (DataGridViewCell c in _grid.SelectedCells)
                if (IsGroupRow(c.RowIndex))
                    if (!_selectedGroups.Contains(c.RowIndex))
                        _selectedGroups.Add(c.RowIndex);
            InvalidateSelected();
        }

        void InvalidateSelected()
        {
            if (_selectedGroups.Count == 0 || _grid.SelectionMode == DataGridViewSelectionMode.FullRowSelect) return;
            int count = _grid.Rows.Count;
            foreach (int i in _selectedGroups)
                if (i < count)
                    _grid.InvalidateRow(i);
        }

        public void ExpandAll() => _source.CollapseExpandAll(false);

        public void CollapseAll() => _source.CollapseExpandAll(true);

        GroupRow GetGroupRow(int RowIndex) => (GroupRow)_source.Groups.Rows[RowIndex];

        IEnumerable<DataGridViewRow> GetRows(int index)
        {
            var gr = GetGroupRow(index);
            for (int i = 0; i < gr.Count; i++)
            {
                yield return _grid.Rows[++index];
            }
        }

        private void SelectGroup(int offset)
        {
            foreach (DataGridViewRow row in GetRows(offset))
                row.Selected = true;
        }

        #endregion

        public GroupList Groups
        {
            get
            {
                return _source.Groups;
            }
        }

        public bool IsGroupRow(int RowIndex)
        {
            return _source.IsGroupRow(RowIndex);
        }

        void Source_DataSourceChanged(object sender, EventArgs e) => OnPropertiesChanged();

        void OnPropertiesChanged() => PropertiesChanged?.Invoke(this, EventArgs.Empty);

        public IEnumerable<PropertyDescriptor> GetProperties()
        {
            foreach (PropertyDescriptor pd in _source.GetItemProperties(null))
                yield return pd;
        }

        void Grid_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (IsGroupRow(e.RowIndex))
                e.Cancel = true;
        }

        protected override void Dispose(bool disposing)
        {
            DataGridView = null;
            _source.Dispose();
            base.Dispose(disposing);
        }

        public GroupingSource GroupingSource => _source;

        void Grid_DataSourceChanged(object sender, EventArgs e)
        {
            if (!GridUsesGroupSource)
            {
                try
                {
                    _source.DataSource = _grid.DataSource;
                }
                catch
                {
                    _source.RemoveGrouping();
                }
            }
        }

        public bool RemoveGrouping()
        {
            if (GridUsesGroupSource)
                try
                {
                    _grid.DataSource = _source.DataSource;
                    _grid.DataMember = _source.DataMember;
                    _source.RemoveGrouping();
                    return true;
                }
                catch { }
            return false;
        }

        public event EventHandler GroupingChanged
        {
            add { _source.GroupingChanged += value; }
            remove { _source.GroupingChanged -= value; }
        }

        bool GridUsesGroupSource
        {
            get
            {
                return _grid != null && _grid.DataSource == _source;
            }
        }

        public void ResetGrouping()
        {
            if (!GridUsesGroupSource) return;
            _capturedCollapseBox = new Point(-1, -1);
            _source.ResetGroups();
        }

        [DefaultValue(null)]
        public GroupingInfo GroupOn
        {
            get
            {
                return _source.GroupOn;
            }
            set
            {
                if (GroupOn == value) return;
                if (value == null)
                    RemoveGrouping();
                else
                    CheckSource().GroupOn = value;
            }
        }

        public bool SetGroupOn(string Name)
        {
            if (string.IsNullOrEmpty(Name))
                return RemoveGrouping();
            return CheckSource().SetGroupOn(Name);
        }

        /// <summary>
        /// Ensures the datagridview uses the groupingsource as its datasource
        /// </summary>
        /// <returns></returns>
        GroupingSource CheckSource()
        {
            if (_grid == null)
                throw new Exception("No target datagridview set");
            if (!GridUsesGroupSource)
            {
                _source.DataSource = _grid.DataSource;
                _source.DataMember = _grid.DataMember;
                _grid.DataSource = _source;
            }
            return _source;
        }

        #region Painting

        private const int CollapseBoxWidth = 10;
        private const int LineOffset = CollapseBoxWidth / 2;
        private const int CollapseBox_Y_Offset = 5;
        private readonly Pen _linePen = Pens.SteelBlue;

        int HeaderOffset => _grid.RowHeadersVisible ? _grid.RowHeadersWidth / 2 + CollapseBoxWidth / 2 : LineOffset * 4;

        int DirectionOffsset => _grid.RightToLeft == RightToLeft.Yes ? _grid.Width - _grid.RowHeadersWidth : 0;

        int TitleOffset => _grid.RightToLeft == RightToLeft.Yes ? _grid.Width - HeaderOffset * 4 : HeaderOffset * 2;

        bool DrawExpandCollapseLines => _grid.RowHeadersVisible;

        void Grid_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (IsGroupRow(e.RowIndex))
            {
                e.Handled = true;
                PaintGroupRow(e);
            }
        }

        /// <summary>
        /// This event is fired when the group row has to be painted and the display values are requested
        /// </summary>
        public event EventHandler<GroupDisplayEventArgs> DisplayGroup
        {
            add { _source.DisplayGroup += value; }
            remove { _source.DisplayGroup -= value; }
        }

        void PaintGroupRow(DataGridViewRowPrePaintEventArgs e)
        {
            GroupRow groupRow = (GroupRow)_source[e.RowIndex];
            if (!_selectionSet)
            {
                SetSelection();
            }
            var info = groupRow.GetDisplayInfo(_selectedGroups.Contains(e.RowIndex));
            if (info == null || info.Cancel) return; //cancelled
            if (info.Font == null)
                info.Font = e.InheritedRowStyle.Font;
            var rowBounds = e.RowBounds;
            rowBounds.Height--;
            using (var bgb = new SolidBrush(info.BackColor))
            {
                //line under the group row
                e.Graphics.DrawLine(Pens.SteelBlue, rowBounds.Left, rowBounds.Bottom, rowBounds.Right, rowBounds.Bottom);
                //group value
                {
                    rowBounds.X = TitleOffset - _grid.HorizontalScrollingOffset;
                    //clear background
                    e.Graphics.FillRectangle(bgb, rowBounds);
                    using (var brush = new SolidBrush(info.ForeColor))
                    {
                        var format = new StringFormat
                        {
                            LineAlignment = StringAlignment.Center,
                            // FormatFlags = StringFormatFlags.DisplayFormatControl
                        };
                        if (info.Header != null)
                        {
                            var size = e.Graphics.MeasureString(info.Header, info.Font);
                            e.Graphics.DrawString(info.Header, info.Font, brush, rowBounds, format);
                            rowBounds.X -= 25;// (int)size.Width - 5;
                        }
                    }
                    e.Graphics.FillRectangle(bgb, 0, rowBounds.Top, TitleOffset, rowBounds.Height);
                }
            }

            //collapse/expand symbol               
            {
                var circle = GetCollapseBoxBounds(DirectionOffsset, e.RowBounds.Y);
                e.Graphics.DrawEllipse(_linePen, circle);
                bool collapsed = groupRow.Collapsed;
                int cx;
                if (DrawExpandCollapseLines && !collapsed)
                {
                    cx = HeaderOffset - LineOffset;
                    e.Graphics.DrawLine(_linePen, cx, circle.Bottom, cx, circle.Bottom);
                }
                circle.Inflate(-2, -2);
                var cy = circle.Y + circle.Height / 2;
                e.Graphics.DrawLine(_linePen, circle.X, cy, circle.Right, cy);
                if (collapsed)
                {
                    cx = circle.X + circle.Width / 2;
                    e.Graphics.DrawLine(_linePen, cx, circle.Top, cx, circle.Bottom);
                }
            }
        }

        private Rectangle GetCollapseBoxBounds(int dirOffsset, int Y_Offset)
        {
            return new Rectangle(
                dirOffsset + HeaderOffset - CollapseBoxWidth,
                Y_Offset + CollapseBox_Y_Offset,
                CollapseBoxWidth,
                CollapseBoxWidth);
        }
        #endregion
    }

    public class GroupDisplayEventArgs : CancelEventArgs
    {
        /// <summary>
        /// Contains the details of the grouping information being displayed 
        /// </summary>
        public readonly GroupRow Group;

        public readonly GroupingInfo GroupingInfo;

        public GroupDisplayEventArgs(GroupRow Row, GroupingInfo Info)
        {
            Group = Row;
            GroupingInfo = Info;
        }

        /// <summary>
        /// Returns the grouping value for the row being drawn
        /// </summary>
        public object Value { get { return Group.Value; } }

        /// <summary>
        /// The header normally contains the property/grouper name, it can be changed here
        /// </summary>
        public string Header { get; set; }

        public Color BackColor { get; set; }

        public Color ForeColor { get; set; }

        public Font Font { get; set; }

        /// <summary>
        /// Indicates if the row begin displayed is currently selected
        /// </summary>
        public bool Selected { get; internal set; }

        /// <summary>
        /// Same as <see cref="Group"/>. Added for backward compatibility
        /// </summary>
        public GroupRow Row { get { return Group; } }
    }

    public interface IDataGridViewGrouperOwner
    {
        DataGridViewGrouper Grouper { get; }
    }
}
