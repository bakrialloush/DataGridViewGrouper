using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;

//this file contains several support functions that are part of the library the grouper is kept in

namespace DevDash
{
    /// <summary>
    /// Comparer that tries to find the 'strongest' comparer for a type. 
    /// if the type implements a generic IComparable, that is used.
    /// otherwise if it implements a normal IComparable, that is used.
    /// If neither are implemented, the ToString versions are compared. 
    /// INullable structures are also supported.
    /// This way, the DefaultComparer can compare any object types and can be used for sorting any source.
    /// </summary>
    /// <example>Array.Sort(YourArray,new GenericComparer());</example>
    public class GenericComparer : IGenericComparer
    {
        public GenericComparer()
        {

        }
        public GenericComparer(Type Type)
        {
            this.Type = Type;
        }

        Type type;
        public Type Type
        {
            get
            {
                return type;
            }
            set
            {
                if (type == value) return;
                if (value == null) throw new ArgumentNullException();
                type = value;
                reset();
            }
        }

        Type targettype;
        /// <summary>
        /// normally the same as the type, but can be set to a different type
        /// </summary>
        public Type TargetType
        {
            get
            {
                if (targettype == null) return type;
                return targettype;
            }
            set
            {
                if (TargetType == value) return;
                targettype = value;
                reset();
            }
        }

        void reset()
        {
            comp = null;
            eq = null;
        }

        IComparer comp;
        IEqualityComparer eq;

        public bool Descending
        {
            get
            {
                return factor < 0;
            }
            set
            {
                factor = value ? -1 : 1;
            }
        }

        int factor = 1;

        public int Compare(object x, object y)
        {
            if (x == y) return 0;
            if (x == null) return -factor;
            if (y == null) return factor;
            if (type == null)
                Type = x.GetType();
            if (comp == null)
                comp = CompareFunctions.GetComparer(type, TargetType);
            return factor * comp.Compare(x, y);
        }

        #region IEqualityComparer Members

        public new bool Equals(object x, object y)
        {
            if (x == y) return true;
            if (x == null || y == null) return false;
            if (type == null)
                Type = x.GetType();
            if (eq == null)
                eq = CompareFunctions.GetEqualityComparer(type, TargetType);
            return eq.Equals(x, y);
        }

        public int GetHashCode(object obj)
        {
            if (obj == null) return 0; return obj.GetHashCode();
        }

        #endregion

        public IGenericComparer ThenBy(GenericComparer cmp)
        {
            var list = new GenericComparers();
            list.Add(cmp);
            return list;
        }
    }

    public interface IGenericComparer : IComparer, IEqualityComparer
    {
        IGenericComparer ThenBy(GenericComparer cmp);
    }

    /// <summary>
    /// A list of <see cref="GenericComparer"/> to compare multiple GenericComparers after one and other
    /// </summary>
    public class GenericComparers : List<GenericComparer>, IGenericComparer
    {
        public int Compare(object x, object y)
        {
            return ObjectExtensions.Compare(this, x, y);
        }

        public new bool Equals(object x, object y)
        {
            return this.All(c => c.Equals(x, y));
        }

        public int GetHashCode(object obj)
        {
            if (obj == null) return 0; return obj.GetHashCode();
        }

        public IGenericComparer ThenBy(GenericComparer cmp)
        {
            Add(cmp);
            return this;
        }
    }

    public static partial class ObjectExtensions
    {
        public static int Compare(this IEnumerable<IComparer> cmp, object x, object y)
        {
            foreach (var c in cmp)
            {
                int i = c.Compare(x, y);
                if (i != 0) return i;
            }
            return 0;
        }
    }

    static partial class CompareFunctions
    {
        static IComparer GetGenericComparer(Type From, Type To)
        {
            return (IComparer)GetGeneric(From, To, typeof(IComparable<>));
        }

        static IEqualityComparer GetGenericEqualityComparer(Type From, Type To)
        {
            return (IEqualityComparer)GetGeneric(From, To, typeof(IEquatable<>), typeof(IComparable<>));
        }

        static Type GetInnerType(Type type)
        {
            if (type.IsGenericType && typeof(Nullable<>) == type.GetGenericTypeDefinition())
                return type.GetGenericArguments()[0];
            return type;
        }

        static bool hasbase(Type type)
        {
            return type.BaseType != null && type.BaseType != typeof(object);
        }

        static object GetGeneric(Type From, Type To, params Type[] GenericBaseTypes)
        {
            //From = GetBaseType(From);
            while (true)
            {
                foreach (var g in GenericBaseTypes)
                {
                    var type = To;
                    while (type != null)
                    {

                        if (g.MakeGenericType(type).IsAssignableFrom(From))
                        {
                            if (g == typeof(IEquatable<>))
                                return Activator.CreateInstance(typeof(StrongEquatable<,>).MakeGenericType(From, type));
                            return Activator.CreateInstance(typeof(StrongCompare<,>).MakeGenericType(From, type));
                        }
                        var inner = GetInnerType(type);
                        if (inner == type)
                            type = type.BaseType;
                        else
                            type = inner;
                    }
                }

                if (hasbase(From))
                    From = From.BaseType;
                else
                    return null;
            }
            //return null;
        }

        internal static IComparer GetComparer(Type From, Type To)
        {
            if (From == To && From == typeof(string)) return new StringComparer();
            From = GetInnerType(From);

            var gen = GetGenericComparer(From, To);
            if (gen != null)
                return gen;
            else if (typeof(IComparable).IsAssignableFrom(From))
            {
                return (IComparer)Activator.CreateInstance(typeof(NonGenericCompare<>).MakeGenericType(From));
            }
            return new StringComparer();
        }

        internal static IEqualityComparer GetEqualityComparer(Type From, Type To)
        {
            if (From == To && From == typeof(string)) return new StringComparer();
            From = GetInnerType(From);

            var eq = GetGenericEqualityComparer(From, To);
            if (eq != null)
                return eq;

            return new DefaultEquals();
        }

        class DefaultEquals : IEqualityComparer
        {
            public new bool Equals(object x, object y)
            {
                return x.Equals(y);
            }

            public int GetHashCode(object o)
            {
                return o.GetHashCode();
            }
        }
        /*
        class NullableComparer<T> : IComparer
            where T : struct
        {

            public readonly IComparer BaseComparer;
            public NullableComparer(IComparer BaseComparer)
            {
                this.BaseComparer = BaseComparer;

            }

            object getval(object o)
            {
                return ((Nullable<T>)o).Value;
            }

            public int Compare(object x, object y)
            {
                return BaseComparer.Compare(getval(x), getval(y));
            }
        }*/

        class StrongEquatable<F, T> : IEqualityComparer
          where F : IEquatable<T>
        {
            public new bool Equals(object x, object y)
            {
                return ((F)x).Equals((T)y);
            }

            public int GetHashCode(object o)
            {
                return o.GetHashCode();
            }
        }

        class StrongCompare<F, T> : IComparer, IEqualityComparer
            where F : IComparable<T>
        {
            public int Compare(object x, object y)
            {
                return ((F)x).CompareTo((T)y);
            }

            public new bool Equals(object x, object y)
            {
                return Compare(x, y) == 0;
            }

            public int GetHashCode(object o)
            {
                return o.GetHashCode();
            }
        }

        class NonGenericCompare<T> : IComparer
            where T : IComparable
        {
            public int Compare(object x, object y)
            {
                return ((T)x).CompareTo(y);
            }
        }

        class StringComparer : IComparer, IEqualityComparer
        {
            public int Compare(object x, object y)
            {
                return string.Compare(x.ToString(), y.ToString());
            }

            public new bool Equals(object x, object y)
            {
                return string.Equals(x.ToString(), y.ToString());
            }

            public int GetHashCode(object o)
            {
                return o.GetHashCode();
            }
        }
    }

    public class GenericComparer<T> : GenericComparer, IComparer<T>
    {

        public GenericComparer()
            : base(typeof(T))
        { }

        public int Compare(T a, T b)
        {
            return base.Compare(a, b);
        }
    }

    public class GenericComparer<T1, T2> : GenericComparer
    {
        public GenericComparer()
            : base(typeof(T1))
        {
            TargetType = typeof(T2);
        }
        public int Compare(T1 a, T2 b)
        {
            return base.Compare(a, b);
        }

        public bool Equals(T1 a, T2 b)
        {
            return base.Equals(a, b);
        }
    }

    public class PropertyDescriptorComparer : GenericComparer
    {
        public readonly PropertyDescriptor Prop;

        public PropertyDescriptorComparer(PropertyDescriptor Prop)
            : this(Prop, true)
        {
        }
        public PropertyDescriptorComparer(PropertyDescriptor Prop, bool Descending)
            : base(Prop.PropertyType)
        {
            this.Prop = Prop;
            this.Descending = Descending;
        }
    }

    static class Parser
    {

        public static string GetFieldName(Expression Field)
        {
            var arr = GetMembers(Field).ToArray();
            if (arr.Length == 0) throw new Exception("Could not resolve FieldName of " + Field);
            if (arr.Length == 1) return arr[0].Member.Name;
            throw new Exception("Multipe field names found for " + Field);
        }

        public static string GetFieldName<RecordType, T>(Expression<Func<RecordType, T>> Field)
        {
            return GetFieldName((LambdaExpression)Field);

        }

        public static IEnumerable<string> GetFieldNames<RecordType, T>(params Expression<Func<RecordType, T>>[] Fields)
        {
            return GetMembers(Fields).Select(f => f.Member.Name);
        }

        static IEnumerable<MemberExpression> GetMembers(params Expression[] expr)
        {
            foreach (var e in expr)
            {
                var exp = e;
                if (exp is LambdaExpression)
                    exp = (exp as LambdaExpression).Body;
                if (exp.NodeType == ExpressionType.Convert)
                    exp = (exp as UnaryExpression).Operand;
                if (exp is MemberExpression)
                    yield return (MemberExpression)exp;
                else if (exp is NewExpression)
                {
                    foreach (var me in
                        from ne in ((NewExpression)exp).Arguments
                        from m in GetMembers(ne)
                        select m)
                        yield return me;
                }

            }
        }
    }

}