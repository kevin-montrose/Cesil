using System;
using System.Reflection;

namespace Cesil
{
    // todo: builder implies the wrong thing... creator maybe?

    /// <summary>
    /// Delegate used to create InstanceBuilders.
    /// </summary>
    public delegate bool InstanceBuilderDelegate<T>(out T instance);

    /// <summary>
    /// Represents a way to create new instances of a type.
    /// 
    /// This can be backed by a zero-parameter constructor, a static 
    ///   method, or a delegate.
    /// </summary>
    public sealed class InstanceBuilder : IEquatable<InstanceBuilder>
    {
        internal BackingMode Mode
        {
            get
            {
                if (Constructor != null) return BackingMode.Constructor;
                if (Delegate != null) return BackingMode.Delegate;
                if (Method != null) return BackingMode.Method;

                return BackingMode.None;
            }
        }

        internal TypeInfo ConstructsType { get; }
        internal ConstructorInfo Constructor { get; }
        internal Delegate Delegate { get; }
        internal MethodInfo Method { get; }

        internal InstanceBuilder(ConstructorInfo cons)
        {
            Constructor = cons;
            ConstructsType = cons.DeclaringType.GetTypeInfo();
        }

        internal InstanceBuilder(Delegate del, TypeInfo forType)
        {
            Delegate = del;
            ConstructsType = forType;
        }

        internal InstanceBuilder(MethodInfo mtd, TypeInfo forType)
        {
            Method = mtd;
            ConstructsType = forType;
        }

        /// <summary>
        /// Creates a new InstanceBuilder from a method.
        /// 
        /// The method must:
        ///   - be static
        ///   - return a bool
        ///   - have a single out parameter of the constructed type
        /// </summary>
        public static InstanceBuilder ForMethod(MethodInfo mtd)
        {
            if (mtd == null)
            {
                return Throw.ArgumentNullException<InstanceBuilder>(nameof(mtd));
            }

            if (!mtd.IsStatic)
            {
                return Throw.ArgumentException<InstanceBuilder>("Method must be static", nameof(mtd));
            }

            if (mtd.ReturnType.GetTypeInfo() != Types.BoolType)
            {
                return Throw.ArgumentException<InstanceBuilder>("Method must return a boolean", nameof(mtd));
            }

            var ps = mtd.GetParameters();
            if (ps.Length != 1)
            {
                return Throw.ArgumentException<InstanceBuilder>("Method must have a single out parameter", nameof(mtd));
            }

            var outP = ps[0].ParameterType.GetTypeInfo();
            if (!outP.IsByRef)
            {
                return Throw.ArgumentException<InstanceBuilder>("Method must have a single out parameter, parameter was not by ref", nameof(mtd));
            }

            var constructs = outP.GetElementType().GetTypeInfo();

            return new InstanceBuilder(mtd, constructs);
        }

        /// <summary>
        /// Create a new InstanceBuilder from a parameterless constructor.
        /// 
        /// The constructed type must be concrete, that is:
        ///   - not an interface
        ///   - not an abstract class
        ///   - not a generic parameter
        ///   - not an unbound generic type (ie. a generic type definition)
        /// </summary>
        public static InstanceBuilder ForParameterlessConstructor(ConstructorInfo cons)
        {
            if (cons == null)
            {
                return Throw.ArgumentNullException<InstanceBuilder>(nameof(cons));
            }

            var ps = cons.GetParameters();
            if (ps.Length != 0)
            {
                return Throw.ArgumentException<InstanceBuilder>("Constructor must take 0 parameters", nameof(cons));
            }

            var t = cons.DeclaringType.GetTypeInfo();
            if (t.IsInterface)
            {
                return Throw.ArgumentException<InstanceBuilder>("Constructed type must be concrete, found an interface", nameof(cons));
            }

            if (t.IsAbstract)
            {
                return Throw.ArgumentException<InstanceBuilder>("Constructed type must be concrete, found an abstract class", nameof(cons));
            }

            if (t.IsGenericTypeParameter)
            {
                return Throw.ArgumentException<InstanceBuilder>("Constructed type must be concrete, found a generic parameter", nameof(cons));
            }

            if (t.IsGenericTypeDefinition)
            {
                return Throw.ArgumentException<InstanceBuilder>("Constructed type must be concrete, found a generic type definition", nameof(cons));
            }

            return new InstanceBuilder(cons);
        }

        /// <summary>
        /// Create a new InstanceBuilder from delegate.
        /// 
        /// There are no restrictions on what the give delegate may do,
        ///   but be aware that it may be called from many different contexts.
        /// </summary>
        public static InstanceBuilder ForDelegate<T>(InstanceBuilderDelegate<T> del)
        {
            if (del == null)
            {
                return Throw.ArgumentNullException<InstanceBuilder>(nameof(del));
            }

            return new InstanceBuilder(del, typeof(T).GetTypeInfo());
        }

        /// <summary>
        /// Returns true if this object equals the given InstanceBuilder.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is InstanceBuilder i)
            {
                return Equals(i);
            }

            return false;
        }

        /// <summary>
        /// Returns true if this object equals the given InstanceBuilder.
        /// </summary>
        public bool Equals(InstanceBuilder i)
        {
            if (ReferenceEquals(i, null)) return false;

            return
                i.Constructor == Constructor &&
                i.ConstructsType == ConstructsType &&
                i.Delegate == Delegate &&
                i.Method == Method &&
                i.Mode == Mode;
        }

        /// <summary>
        /// Returns a stable hash for this InstanceBuilder.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(nameof(InstanceBuilder), Constructor, ConstructsType, Delegate, Method, Mode);

        /// <summary>
        /// Returns a representation of this InstanceBuilder object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        {
            switch (Mode)
            {
                case BackingMode.Constructor:
                    return $"{nameof(InstanceBuilder)} using parameterless constructor {Constructor} to create {ConstructsType}";
                case BackingMode.Delegate:
                    return $"{nameof(InstanceBuilder)} using delegate {Delegate} to create {ConstructsType}";
                case BackingMode.Method:
                    return $"{nameof(InstanceBuilder)} using method {Method} to create {ConstructsType}";
                default:
                    return Throw.InvalidOperationException<string>($"Unexpected {nameof(BackingMode)}: {Mode}");
            }
        }

        /// <summary>
        /// Convenience operator, equivalent to calling InstanceBuilder.ForMethod if non-null.
        /// 
        /// Returns null if mtd is null.
        /// </summary>
        public static explicit operator InstanceBuilder(MethodInfo mtd)
        => mtd == null ? null : ForMethod(mtd);

        /// <summary>
        /// Convenience operator, equivalent to calling InstanceBuilder.ForParameterlessConstructor if non-null.
        /// 
        /// Returns null if field is null.
        /// </summary>
        public static explicit operator InstanceBuilder(ConstructorInfo cons)
        => cons == null ? null : ForParameterlessConstructor(cons);

        /// <summary>
        /// Convenience operator, equivalent to calling InstanceBuilder.ForDelegate if non-null.
        /// 
        /// Returns null if del is null.
        /// </summary>
        public static explicit operator InstanceBuilder(Delegate del)
        {
            if (del == null) return null;

            var delType = del.GetType().GetTypeInfo();
            if (delType.IsGenericType)
            {
                var delGenType = delType.GetGenericTypeDefinition().GetTypeInfo();
                if (delGenType == Types.InstanceBuilderDelegateType)
                {
                    var genArgs = delType.GetGenericArguments();
                    var makes = genArgs[0].GetTypeInfo();

                    return new InstanceBuilder(del, makes);
                }
            }

            var mtd = del.Method;
            var ret = mtd.ReturnType.GetTypeInfo();
            if (ret != Types.BoolType)
            {
                return Throw.InvalidOperationException<InstanceBuilder>($"Delegate must return boolean, found {ret}");
            }

            var ps = mtd.GetParameters();
            if (ps.Length != 1)
            {
                return Throw.InvalidOperationException<InstanceBuilder>($"Delegate must have a single out parameter");
            }

            var outP = ps[0].ParameterType.GetTypeInfo();
            if (!outP.IsByRef)
            {
                return Throw.InvalidOperationException<InstanceBuilder>("Delegate must have a single out parameter, parameter was not by ref");
            }

            var constructs = outP.GetElementType().GetTypeInfo();

            var instanceBuilderDel = Types.InstanceBuilderDelegateType.MakeGenericType(constructs);
            var invoke = del.GetType().GetMethod("Invoke");

            var reboundDel = Delegate.CreateDelegate(instanceBuilderDel, del, invoke);

            return new InstanceBuilder(reboundDel, constructs);
        }

        /// <summary>
        /// Compare two InstanceBuilders for equality
        /// </summary>
        public static bool operator ==(InstanceBuilder a, InstanceBuilder b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two InstanceBuilders for inequality
        /// </summary>
        public static bool operator !=(InstanceBuilder a, InstanceBuilder b)
        => !(a == b);
    }
}
