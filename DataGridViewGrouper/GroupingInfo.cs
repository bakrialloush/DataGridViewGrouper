using System;
using System.ComponentModel;

namespace DevDash.Controls
{
    /// <summary>
    /// Information on how a <see cref="GroupingSource"/> should group its information
    /// </summary>
    public abstract class GroupingInfo
    {
        public abstract object GetGroupValue(object Row);

        public virtual bool IsProperty(PropertyDescriptor p)
        {
            return p != null && IsProperty(p.Name);
        }
        public virtual bool IsProperty(string Name)
        {
            return Name == ToString();
        }

        public static implicit operator GroupingInfo(PropertyDescriptor p)
        {
            return new PropertyGrouper(p);
        }

        public virtual Type GroupValueType
        {
            get { return typeof(object); }
        }
    }

    /// <summary>
    /// Groups on the value of a property
    /// </summary>
    public class PropertyGrouper : GroupingInfo
    {
        public readonly PropertyDescriptor Property;
        public PropertyGrouper(PropertyDescriptor Property)
        {
            if (Property == null) throw new ArgumentNullException();
            this.Property = Property;
        }

        public override bool IsProperty(PropertyDescriptor p)
        {
            return p == Property || (p != null && p.Name == Property.Name);
        }

        public override object GetGroupValue(object Row)
        {
            return Property.GetValue(Row);
        }

        public override string ToString()
        {
            return Property.Name;
        }

        public override Type GroupValueType
        {
            get { return Property.PropertyType; }
        }
    }

    public class DelegateGrouper<T> : GroupingInfo
    {
        public readonly string Name;
        public readonly Func<T, object> GroupProvider;

        public DelegateGrouper(Func<T, object> GroupProvider, string Name)
        {
            if (GroupProvider == null) throw new ArgumentNullException();
            this.Name = Name;
            if (Name == null) this.Name = GroupProvider.ToString();
            this.GroupProvider = GroupProvider;
        }

        public override object GetGroupValue(object Row)
        {
            return GroupProvider((T)Row);
        }
    }


    public abstract class GroupWrapper : GroupingInfo
    {
        public readonly GroupingInfo Grouper;

        public GroupWrapper(GroupingInfo Grouper) : this(Grouper, true) { }

        public GroupWrapper(GroupingInfo Grouper, bool RemovePreviousWrappers)
        {
            if (Grouper == null) throw new ArgumentNullException();
            if (RemovePreviousWrappers)
                while (Grouper is GroupWrapper)
                    Grouper = ((GroupWrapper)Grouper).Grouper;
            this.Grouper = Grouper;
        }

        public override bool IsProperty(PropertyDescriptor p)
        {
            return Grouper.IsProperty(p);
        }

        public override object GetGroupValue(object Row)
        {
            return GetValue(Grouper.GetGroupValue(Row));
        }

        public override Type GroupValueType
        {
            get
            {
                return Grouper.GroupValueType;
            }
        }

        protected abstract object GetValue(object GroupValue);
    }

    /// <summary>
    /// Forces grouping whichever value is grouped on its string value
    /// </summary>
    public class StringGroupWrapper : GroupWrapper
    {
        public StringGroupWrapper(GroupingInfo Grouper) : base(Grouper) { }

        protected override object GetValue(object GroupValue)
        {
            if (GroupValue == null) return (string)null;
            return GetValue(GroupValue.ToString());
        }

        public override Type GroupValueType
        {
            get
            {
                return typeof(string);
            }
        }

        protected virtual string GetValue(string s)
        {
            return s;
        }
    }

    public class StartLetterGrouper : StringGroupWrapper
    {
        public readonly int Letters;

        public StartLetterGrouper(GroupingInfo Grouper) : this(Grouper, 1) { }

        public StartLetterGrouper(GroupingInfo Grouper, int Letters) : base(Grouper)
        {
            this.Letters = Letters;
        }

        protected override string GetValue(string s)
        {
            if (s.Length < Letters) return s;
            return s.Substring(0, Letters);
        }
    }


}
