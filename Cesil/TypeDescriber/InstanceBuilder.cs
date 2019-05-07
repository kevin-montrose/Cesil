using System;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Delegate used to create InstanceBuilders.
    /// </summary>
    public delegate bool InstanceBuilderDelegate<T>(out T instance);

    /// <summary>
    /// Represents a way to create new instances of a type.
    /// </summary>
    public sealed class InstanceBuilder
    {
        internal TypeInfo ConstructsType { get; }
        internal ConstructorInfo Constructor { get; }
        internal Delegate Delegate { get; }

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
                Throw.ArgumentNullException(nameof(cons));
            }

            var ps = cons.GetParameters();
            if (ps.Length != 0)
            {
                Throw.ArgumentException("Constructor must take 0 parameters", nameof(cons));
            }

            var t = cons.DeclaringType.GetTypeInfo();
            if (t.IsInterface)
            {
                Throw.ArgumentException("Constructed type must be concrete, found an interface", nameof(cons));
            }

            if (t.IsAbstract)
            {
                Throw.ArgumentException("Constructed type must be concrete, found an abstract class", nameof(cons));
            }

            if (t.IsGenericTypeParameter)
            {
                Throw.ArgumentException("Constructed type must be concrete, found a generic parameter", nameof(cons));
            }

            if (t.IsGenericTypeDefinition)
            {
                Throw.ArgumentException("Constructed type must be concrete, found a generic type definition", nameof(cons));
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
            if(del == null)
            {
                Throw.ArgumentNullException(nameof(del));
            }

            return new InstanceBuilder(del, typeof(T).GetTypeInfo());
        }
    }
}
