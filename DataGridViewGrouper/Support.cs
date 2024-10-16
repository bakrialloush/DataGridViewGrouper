using System.Collections;


namespace DevDash
{
    public class GenericComparer : IGenericComparer
    {
        IComparer comp;
        IEqualityComparer eq;
        Type type;
        Type targettype;
        int factor = 1;

        public GenericComparer(Type Type)
        {
            this.Type = Type;
        }

        public Type Type
        {
            get => type;
            set
            {
                if (type == value) return;
                type = value ?? throw new ArgumentNullException();
                Reset();
            }
        }

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
                Reset();
            }
        }

        void Reset()
        {
            comp = null;
            eq = null;
        }

        public bool Descending
        {
            get => factor < 0;
            set => factor = value ? -1 : 1;
        }

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

        public IGenericComparer ThenBy(GenericComparer cmp) => new GenericComparers { cmp };
    }

    public interface IGenericComparer : IComparer, IEqualityComparer
    {
        IGenericComparer ThenBy(GenericComparer cmp);
    }

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

    internal static partial class CompareFunctions
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

        static bool HasBase(Type type)
        {
            return type.BaseType != null && type.BaseType != typeof(object);
        }

        static object GetGeneric(Type From, Type To, params Type[] GenericBaseTypes)
        {
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

                if (HasBase(From))
                    From = From.BaseType;
                else
                    return null;
            }
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
}