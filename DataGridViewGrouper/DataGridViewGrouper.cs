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
        private DataGridView grid;
        private bool selectionset;
        private readonly GroupingSource source = new GroupingSource();
        private Point capturedCollapseBox = new Point(-1, -1);

        public event EventHandler PropertiesChanged;

        public DataGridViewGrouper()
        {
            source.DataSourceChanged += new EventHandler(source_DataSourceChanged);
            source.Grouper = this;
        }

        public DataGridViewGrouper(DataGridView Grid) : this()
        {
            DataGridView = Grid;
        }

        public DataGridViewGrouper(IContainer Container) : this()
        {
            Container.Add(this);
        }

        [DefaultValue(null)]
        public DataGridView DataGridView
        {
            get { return grid; }
            set
            {
                if (grid == value) return;
                if (grid != null)
                {
                    grid.RowPrePaint -= new DataGridViewRowPrePaintEventHandler(grid_RowPrePaint);
                    grid.CellBeginEdit -= new DataGridViewCellCancelEventHandler(grid_CellBeginEdit);
                    grid.CellClick -= new DataGridViewCellEventHandler(grid_CellClick);
                    grid.MouseMove -= new MouseEventHandler(grid_MouseMove);
                    grid.SelectionChanged -= new EventHandler(grid_SelectionChanged);
                    grid.DataSourceChanged -= new EventHandler(grid_DataSourceChanged);
                    grid.AllowUserToAddRowsChanged -= new EventHandler(grid_AllowUserToAddRowsChanged);
                }
                RemoveGrouping();
                selectedGroups.Clear();
                grid = value;
                if (grid != null)
                {
                    grid.RowPrePaint += new DataGridViewRowPrePaintEventHandler(grid_RowPrePaint);
                    grid.CellBeginEdit += new DataGridViewCellCancelEventHandler(grid_CellBeginEdit);
                    grid.CellClick += new DataGridViewCellEventHandler(grid_CellClick);
                    grid.MouseMove += new MouseEventHandler(grid_MouseMove);
                    grid.SelectionChanged += new EventHandler(grid_SelectionChanged);
                    grid.DataSourceChanged += new EventHandler(grid_DataSourceChanged);
                    grid.AllowUserToAddRowsChanged += new EventHandler(grid_AllowUserToAddRowsChanged);
                }
            }
        }

        void grid_AllowUserToAddRowsChanged(object sender, EventArgs e)
        {
            source.CheckNewRow();
        }

        #region Select/Collapse/Expand
        void grid_MouseMove(object sender, MouseEventArgs e)
        {
            //if (e.X < HeaderOffset && e.X >= HeaderOffset - CollapseBoxWidth)
            {
                DataGridView.HitTestInfo ht = grid.HitTest(e.X, e.Y);
                if (IsGroupRow(ht.RowIndex))
                {
                    var y = e.Y - ht.RowY;
                    // if (y >= CollapseBox_Y_Offset && y <= CollapseBox_Y_Offset + CollapseBoxWidth)
                    {
                        CheckCollapsedFocused(ht.ColumnIndex, ht.RowIndex);
                        return;
                    }
                }
            }
            CheckCollapsedFocused(-1, -1);
        }

        void InvalidateCapturedBox()
        {
            if (capturedCollapseBox.Y == -1) return;
            try
            {
                grid.InvalidateCell(capturedCollapseBox.X, capturedCollapseBox.Y);
            }
            catch
            {
                capturedCollapseBox = new Point(-1, -1);
            }
        }

        void CheckCollapsedFocused(int col, int row)
        {
            if (capturedCollapseBox.X != col || capturedCollapseBox.Y != row)
            {
                InvalidateCapturedBox();
                capturedCollapseBox = new Point(col, row);
                InvalidateCapturedBox();
            }
        }

        void grid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == -1) return;
            if (e.RowIndex == capturedCollapseBox.Y)
            {
                var groupRow = GetGroupRow(e.RowIndex);
                groupRow.Collapsed = !groupRow.Collapsed;
            }
        }

        /// <summary>
        /// selected group rows are kept seperately in order to invalidate the entire row
        /// and not just one cell when the selection is changed
        /// </summary>
        List<int> selectedGroups = new List<int>();
        void grid_SelectionChanged(object sender, EventArgs e)
        {
            if (selectionset)
            {
                selectionset = false;
                InvalidateSelected();
            }
        }

        void SetSelection()
        {
            // InvalidateSelected();
            selectionset = true;
            selectedGroups.Clear();
            foreach (DataGridViewCell c in grid.SelectedCells)
                if (IsGroupRow(c.RowIndex))
                    if (!selectedGroups.Contains(c.RowIndex))
                        selectedGroups.Add(c.RowIndex);
            InvalidateSelected();
        }

        void InvalidateSelected()
        {
            if (selectedGroups.Count == 0 || grid.SelectionMode == DataGridViewSelectionMode.FullRowSelect) return;
            int count = grid.Rows.Count;
            foreach (int i in selectedGroups)
                if (i < count)
                    grid.InvalidateRow(i);
        }

        public void ExpandAll()
        {
            source.CollapseExpandAll(false);
        }

        public void CollapseAll()
        {
            source.CollapseExpandAll(true);
        }

        GroupRow GetGroupRow(int RowIndex)
        {
            return (GroupRow)source.Groups.Rows[RowIndex];
        }

        IEnumerable<DataGridViewRow> GetRows(int index)
        {
            var gr = GetGroupRow(index);
            for (int i = 0; i < gr.Count; i++)
            {
                yield return grid.Rows[++index];
            }
        }

        void SelectGroup(int offset)
        {
            foreach (DataGridViewRow row in GetRows(offset))
                row.Selected = true;
        }

        #endregion

        public GroupList Groups
        {
            get
            {
                return source.Groups;
            }
        }

        public bool IsGroupRow(int RowIndex)
        {
            return source.IsGroupRow(RowIndex);
        }
        void source_DataSourceChanged(object sender, EventArgs e)
        {
            OnPropertiesChanged();
        }

        void OnPropertiesChanged()
        {
            if (PropertiesChanged != null)
                PropertiesChanged(this, EventArgs.Empty);
        }

        public IEnumerable<PropertyDescriptor> GetProperties()
        {
            foreach (PropertyDescriptor pd in source.GetItemProperties(null))
                yield return pd;
        }

        void grid_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (IsGroupRow(e.RowIndex))
                e.Cancel = true;
        }

        protected override void Dispose(bool disposing)
        {
            DataGridView = null;
            source.Dispose();
            base.Dispose(disposing);
        }

        /*
        void grid_Sorted(object sender, EventArgs e)
        {
            ResetGrouping();
        }*/

        public GroupingSource GroupingSource
        {
            get
            {
                return source;
            }
        }

        void grid_DataSourceChanged(object sender, EventArgs e)
        {
            if (!GridUsesGroupSource)
            {
                try
                {
                    source.DataSource = grid.DataSource;
                }
                catch
                {
                    source.RemoveGrouping();
                }
            }
        }

        public bool RemoveGrouping()
        {
            if (GridUsesGroupSource)
                try
                {
                    grid.DataSource = source.DataSource;
                    grid.DataMember = source.DataMember;
                    source.RemoveGrouping();
                    return true;
                }
                catch { }
            return false;
        }

        public event EventHandler GroupingChanged
        {
            add { source.GroupingChanged += value; }
            remove { source.GroupingChanged -= value; }
        }

        bool GridUsesGroupSource
        {
            get
            {
                return grid != null && grid.DataSource == source;
            }
        }

        public void ResetGrouping()
        {
            if (!GridUsesGroupSource) return;
            capturedCollapseBox = new Point(-1, -1);
            source.ResetGroups();
        }

        [DefaultValue(null)]
        public GroupingInfo GroupOn
        {
            get
            {
                return source.GroupOn;
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

        public bool IsGrouped
        {
            get
            {
                return source.IsGrouped;
            }
        }

        [DefaultValue(SortOrder.Ascending)]
        public SortOrder GroupSortOrder
        {
            get
            {
                return source.GroupSortOrder;
            }
            set
            {
                source.GroupSortOrder = value;
            }
        }

        [DefaultValue(null)]
        public GroupingOptions Options
        {
            get { return source.Options; }
            set { source.Options = value; }
        }

        public bool SetGroupOn(DataGridViewColumn col)
        {
            return SetGroupOn(col == null ? null : col.DataPropertyName);
        }

        public bool SetGroupOn(PropertyDescriptor Property)
        {
            return CheckSource().SetGroupOn(Property);
        }

        public void SetCustomGroup<T>(Func<T, object> GroupValueProvider, string Description = null)
        {
            CheckSource().SetCustomGroup(GroupValueProvider, Description);
        }

        public void SetGroupOnStartLetters(GroupingInfo g, int Letters)
        {
            CheckSource().SetGroupOnStartLetters(g, Letters);
        }

        public void SetGroupOnStartLetters(string Property, int Letters)
        {
            CheckSource().SetGroupOnStartLetters(Property, Letters);
        }

        public bool SetGroupOn(string Name)
        {
            if (string.IsNullOrEmpty(Name))
                return RemoveGrouping();
            return CheckSource().SetGroupOn(Name);
        }

        //added after linq was added to the framework to facilitate setting properties
        public bool SetGroupOn<T>(System.Linq.Expressions.Expression<Func<T, object>> Property)
        {
            if (Property == null)
                return RemoveGrouping();
            return CheckSource().SetGroupOn(Parser.GetFieldName(Property));
        }

        public PropertyDescriptor GetProperty(string Name)
        {
            return CheckSource().GetProperty(Name);
        }

        /// <summary>
        /// Ensures the datagridview uses the groupingsource as its datasource
        /// </summary>
        /// <returns></returns>
        GroupingSource CheckSource()
        {
            if (grid == null)
                throw new Exception("No target datagridview set");
            if (!GridUsesGroupSource)
            {
                source.DataSource = grid.DataSource;
                source.DataMember = grid.DataMember;
                grid.DataSource = source;
            }
            return source;
        }

        #region Painting

        private const int CollapseBoxWidth = 10;
        private const int LineOffset = CollapseBoxWidth / 2;
        private const int CollapseBox_Y_Offset = 5;
        private readonly Pen LinePen = Pens.SteelBlue;

        int HeaderOffset => grid.RowHeadersVisible ? grid.RowHeadersWidth / 2 + CollapseBoxWidth / 2 : LineOffset * 4;

        int DirectionOffsset => grid.RightToLeft == RightToLeft.Yes ? grid.Width - grid.RowHeadersWidth : 0;

        int TitleOffset => grid.RightToLeft == RightToLeft.Yes ? grid.Width - HeaderOffset * 4 : HeaderOffset * 2;

        bool DrawExpandCollapseLines => grid.RowHeadersVisible;

        void grid_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
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
            add { source.DisplayGroup += value; }
            remove { source.DisplayGroup -= value; }
        }

        public DataGridViewGrouper this[int GroupIndex]
        {
            get
            {
                return (DataGridViewGrouper)source[GroupIndex];
            }
        }

        void PaintGroupRow(DataGridViewRowPrePaintEventArgs e)
        {
            GroupRow groupRow = (GroupRow)source[e.RowIndex];
            if (!selectionset)
            {
                SetSelection();
            }
            var info = groupRow.GetDisplayInfo(selectedGroups.Contains(e.RowIndex));
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
                    rowBounds.X = TitleOffset - grid.HorizontalScrollingOffset;
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
                //if (capturedCollapseBox.Y == e.RowIndex)
                //    e.Graphics.FillEllipse(Brushes.Yellow, circle);
                e.Graphics.DrawEllipse(LinePen, circle);
                bool collapsed = groupRow.Collapsed;
                int cx;
                if (DrawExpandCollapseLines && !collapsed)
                {
                    cx = HeaderOffset - LineOffset;
                    e.Graphics.DrawLine(LinePen, cx, circle.Bottom, cx, circle.Bottom);
                }
                circle.Inflate(-2, -2);
                var cy = circle.Y + circle.Height / 2;
                e.Graphics.DrawLine(LinePen, circle.X, cy, circle.Right, cy);
                if (collapsed)
                {
                    cx = circle.X + circle.Width / 2;
                    e.Graphics.DrawLine(LinePen, cx, circle.Top, cx, circle.Bottom);
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

        public bool CurrentRowIsGroupRow
        {
            get
            {
                if (grid == null) return false;
                return IsGroupRow(grid.CurrentCellAddress.Y);
            }
        }

        [DefaultValue(null)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public GroupRow CurrentGroup
        {
            get
            {
                return source.CurrentGroup;
            }
            set
            {
                source.CurrentGroup = value;
            }
        }
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
