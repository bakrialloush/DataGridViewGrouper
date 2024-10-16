using System;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;

namespace DevDash.Controls
{
    [DefaultEvent("GroupingChanged")]
    public partial class GroupingSource : BindingSource, ICancelAddNew
    {
        private CurrencyManager _currencyManager;
        private GroupingInfo _groupOn;
        private GroupInfo _info;
        private PropertyDescriptorCollection _props;
        private bool _resetting;
        private readonly int _suspendListChange = 0;
        private bool _suspendSync;
        internal DataGridViewGrouper Grouper;


        [DefaultValue(null)]
        public GroupingInfo GroupOn
        {
            get
            {
                return _groupOn;
            }
            set
            {
                if (_groupOn == value) return;

                if (value == null)
                    RemoveGrouping();
                else
                {
                    if (value.Equals(_groupOn)) return;
                    SetGroupOn(value);
                }
            }
        }

        void SetGroupOn(GroupingInfo value)
        {
            _info = null;
            if (value.GroupValueType != typeof(string))
            {
                value = new StringGroupWrapper(value);
            }
            _groupOn = value;
            OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
            OnGroupingChanged();
        }

        public void RemoveGrouping()
        {
            if (_groupOn == null) return;
            _groupOn = null;
            ResetGroups();
            OnGroupingChanged();
        }

        public bool SetGroupOn(string Property)
        {
            return SetGroupOn(GetProperty(Property));
        }

        public PropertyDescriptor GetProperty(string Name)
        {
            PropertyDescriptor descriptor = GetItemProperties(null)[Name];
            return descriptor ?? throw new Exception(Name + " is not a valid property");
        }

        public bool SetGroupOn(PropertyDescriptor p)
        {
            if (p == null) throw new ArgumentNullException();
            if (_groupOn == null || !_groupOn.IsProperty(p))
            {
                GroupOn = new PropertyGrouper(p);
                return true;
            }
            return false;
        }

        public bool IsGroupRow(int Index)
        {
            if (_info == null) return false;
            if (Index < 0 || Index >= Count) return false;
            return _info.Rows[Index] is GroupRow;
        }

        public void CollapseExpandAll(bool collapse)
        {
            if (Groups == null) return;
            var cur = CurrentGroup;
            Groups.CollapseExpandAll(collapse);
            if (cur != null)
                try
                {
                    CurrentGroup = cur;
                }
                catch { }
        }

        [DefaultValue(null)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public GroupRow CurrentGroup
        {
            get
            {
                return GetGroup(Position);
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException();
                Position = value.Index;
                if (!value.Collapsed)
                {
                    Position++;
                }
            }
        }

        public GroupRow GetGroup(int RowIndex)
        {
            if (RowIndex == -1 || Groups == null) return null;
            return Groups.GetByRow(RowIndex);
        }

        public GroupList Groups
        {
            get
            {
                return Info.Groups;
            }
        }

        internal void CheckNewRow()
        {
            if (ShouldReset)
                _info.Groups.CheckNewRow(true);
        }

        class NullValue
        {
            public override string ToString()
            {
                return "<Null>";
            }

            public static readonly NullValue Instance = new NullValue();
        }

        class GroupInfo
        {
            public readonly GroupingSource Owner;

            public GroupInfo(GroupingSource Owner)
            {
                this.Owner = Owner;

                Set();
            }

            public IList Rows;
            //public List<GroupRow> Groups = new List<GroupRow>();

            public GroupList Groups;

            private void Set()
            {
                Groups = null;

                if (Owner._groupOn == null)
                {
                    Rows = Owner.List;
                    return;
                }

                Groups = new GroupList(Owner);

                Rows = Groups.Fill();
            }

            public void ReBuild()
            {
                if (Groups == null)
                    Set();
                else
                    Groups.Fill();
            }

            public void Sort()
            {
                Groups.Sort(SortOrder.Ascending);
            }
        }

        GroupInfo Info
        {
            get
            {
                if (_info == null)
                {
                    _info = new GroupInfo(this);
                    if (NeedSync)
                        SyncCurrencyManagers();
                }
                return _info;
            }
        }

        void OnGroupingChanged() => GroupingChanged?.Invoke(this, EventArgs.Empty);

        public event EventHandler GroupingChanged;

        internal DataGridView Grid
        {
            get
            {
                if (Grouper == null) return null;
                return Grouper.DataGridView;
            }
        }

        public void ResetGroups()
        {
            Reset(false);
        }

        void Reset(bool fromlistchange)
        {
            if (_info == null || _resetting) return;
            int pos = Position;
            var cur = Current;
            var grid = Grid;
            int? firstrow = grid == null ? (int?)null : grid.FirstDisplayedScrollingRowIndex;
            _resetting = true;
            try
            {
                if (fromlistchange)
                    _info.ReBuild();
                else
                    _info = null;
                base.OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));

                if (pos != -1)
                {
                    int i = cur is GroupRow ? pos : IndexOf(cur);
                    if (i == -1) i = pos;

                    if (Position == i)
                        OnPositionChanged(EventArgs.Empty);
                    else
                        this.Position = i;

                    if (firstrow.HasValue)
                    {
                        try
                        {
                            if (grid.Rows.Count > firstrow.Value && firstrow.Value > -1)
                                grid.FirstDisplayedScrollingRowIndex = firstrow.Value;
                            //OnPositionChanged(EventArgs.Empty);
                        }
                        catch
                        {
                        }
                    }
                }
            }
            finally
            {
                _resetting = false;

                if (NeedSync)
                    SyncCurrencyManagers();
            }
        }

        internal void FireBaseReset(bool PreserveScrollPosition)
        {
            FireBaseChanged(new ListChangedEventArgs(ListChangedType.Reset, -1), PreserveScrollPosition);
        }

        internal void FireBaseChanged(ListChangedType type, int index, bool PreserveScrollPosition)
        {
            FireBaseChanged(new ListChangedEventArgs(type, index), PreserveScrollPosition);
        }
        internal void FireBaseChanged(ListChangedEventArgs e, bool PreserveScrollPosition)
        {
            int soffset = -1;
            PreserveScrollPosition &= Grid != null;
            if (PreserveScrollPosition)
                soffset = Grid.FirstDisplayedScrollingRowIndex;
            base.OnListChanged(e);
            if (soffset > 0)
                try
                {
                    Grid.FirstDisplayedScrollingRowIndex = soffset;
                }
                catch { }

        }

        /// <summary>
        /// This event is fired when the group row has to be painted and the display values are requested
        /// </summary>
        public event EventHandler<GroupDisplayEventArgs> DisplayGroup;

        internal void FireDisplayGroup(GroupDisplayEventArgs e) => DisplayGroup?.Invoke(this, e);

        void UnwireCurMan()
        {
            if (_currencyManager == null) return;
            _currencyManager.CurrentChanged -= new EventHandler(BSource_CurrentChanged);
        }

        protected override void Dispose(bool disposing)
        {
            UnwireCurMan();
            _groupOn = null;
            base.Dispose(disposing);
        }

        protected override void OnDataSourceChanged(EventArgs e)
        {
            UnwireCurMan();
            ResetGroups();

            object dataSource = DataSource;
            if (dataSource is ICurrencyManagerProvider provider)
            {
                _currencyManager = provider.CurrencyManager;
            }

            if (_currencyManager != null)
            {
                _currencyManager.CurrentChanged += new EventHandler(BSource_CurrentChanged);
                if (NeedSync) SyncCurrencyManagers();
            }
            base.OnDataSourceChanged(e);
        }

        protected override void OnListChanged(ListChangedEventArgs e)
        {
            if (_suspendListChange > 0 || _resetting) return;

            if (ShouldReset)
            {
                switch (e.ListChangedType)
                {
                    case ListChangedType.ItemChanged:
                        if (_groupOn.IsProperty(e.PropertyDescriptor) && !_info.Groups.IsNewRow(e.NewIndex))
                            Reset(true);
                        else
                            FireBaseChanged(new ListChangedEventArgs(ListChangedType.ItemChanged,
                                IndexOf(List[e.NewIndex]),
                                e.PropertyDescriptor),
                                false);
                        return;
                    case ListChangedType.ItemAdded:
                        if (_info.Groups.HasNewRow)
                            _info.Groups.AddNew(List[e.NewIndex]);
                        else
                            Reset(true);
                        return;
                    case ListChangedType.ItemDeleted:
                        Reset(true);
                        return;
                    case ListChangedType.Reset:
                        Reset(true);
                        return;
                    case ListChangedType.ItemMoved:
                        Reset(true); //check sorting??
                        return;
                }
            }

            switch (e.ListChangedType)
            {
                case ListChangedType.PropertyDescriptorAdded:
                case ListChangedType.PropertyDescriptorChanged:
                case ListChangedType.PropertyDescriptorDeleted:
                    _props = null;
                    break;
            }

            base.OnListChanged(e);
        }

        bool ShouldReset => _info != null && _info.Groups != null;

        private void BSource_CurrentChanged(object sender, EventArgs e)
        {
            if (NeedSync)
                SyncCurrencyManagers();
        }

        bool NeedSync
        {
            get
            {
                if (_currencyManager == null || _suspendListChange > 0 || _suspendSync || _currencyManager.Count == 0) return false;
                var p1 = Position;
                if (p1 == _currencyManager.Position)
                {
                    if (p1 == -1) return false;
                    return Current != _currencyManager.Current;
                }
                return true;
            }
        }

        private void SyncCurrencyManagers()
        {
            _suspendSync = true;
            try
            {
                if (_currencyManager.Count > 0)
                    Position = IndexOf(_currencyManager.Current);
            }
            finally { _suspendSync = false; }

        }

        public override int IndexOf(object value)
        {
            return Info.Rows.IndexOf(value);
        }

        public partial class PropertyWrapper : PropertyDescriptor
        {

            public readonly PropertyDescriptor Property;
            public readonly GroupingSource Owner;
            public PropertyWrapper(PropertyDescriptor Property, GroupingSource Owner)
                : base(Property)
            {
                this.Property = Property;
                this.Owner = Owner;
            }
            public override bool CanResetValue(object component)
            {
                return Property.CanResetValue(component);
            }

            public override Type ComponentType
            {
                get { return Property.ComponentType; }
            }

            public override object GetValue(object component)
            {
                if (component is GroupRow)
                {
                    if (Owner._groupOn.IsProperty(Property))
                        return (component as GroupRow).Value;
                    return null;
                }
                return Property.GetValue(component);
            }

            public override bool IsReadOnly
            {
                get { return Property.IsReadOnly; }
            }

            public override Type PropertyType
            {
                get { return Property.PropertyType; }
            }

            public override string Category
            {
                get
                {
                    return Property.Category;
                }
            }

            public override string Description
            {
                get
                {
                    return Property.Description;
                }
            }

            public override string DisplayName
            {
                get
                {
                    return Property.DisplayName;
                }
            }

            public override bool DesignTimeOnly
            {
                get
                {
                    return Property.DesignTimeOnly;
                }
            }

            public override AttributeCollection Attributes
            {
                get
                {
                    return Property.Attributes;
                }
            }

            public override string Name
            {
                get
                {
                    return Property.Name;
                }
            }

            public override void ResetValue(object component)
            {
                Property.ResetValue(component);
            }

            public override void SetValue(object component, object value)
            {
                Property.SetValue(component, value);
            }

            public override bool ShouldSerializeValue(object component)
            {
                return Property.ShouldSerializeValue(component);
            }
        }

        public override PropertyDescriptorCollection GetItemProperties(PropertyDescriptor[] listAccessors)
        {
            if (listAccessors == null)
            {
                if (_props == null)
                {
                    /*
                    props = new PropertyDescriptorCollection(
                        base.GetItemProperties(null).Cast<PropertyDescriptor>()
                        .Select(pd => new PropertyWrapper(pd, this)).ToArray());*/
                    _props = base.GetItemProperties(null);
                    if (_props == null) return null;
                    PropertyDescriptor[] arr = new PropertyDescriptor[_props.Count];

                    for (int i = 0; i < _props.Count; i++)
                    {
                        arr[i] = new PropertyWrapper(_props[i], this);
                    }
                    _props = new PropertyDescriptorCollection(arr);
                }
                return _props;
            }
            return base.GetItemProperties(listAccessors);
        }

        /// <summary>
        /// The count of the underlying source (without the grouprows)
        /// </summary>
        public int BaseCount
        {
            get
            {
                return List.Count;
            }
        }

        public object GetBaseRow(int Index)
        {
            return List[Index];
        }

        /// <summary>
        /// The total count: number of records plus number of grouprows
        /// </summary>
        public override int Count
        {
            get
            {
                return Info.Rows.Count;
            }
        }

        public override object this[int index]
        {
            get
            {
                return Info.Rows[index];
            }
            set
            {
                Info.Rows[index] = value;
            }
        }
    }

}
