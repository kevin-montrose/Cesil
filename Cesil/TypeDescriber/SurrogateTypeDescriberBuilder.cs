using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;

namespace Cesil
{
    /// <summary>
    /// Builder for creating a SurrogateTypeDescriber.
    /// 
    /// Creates ITypeDescribers that inspects one type to determine who to
    ///   (de)serialize another type.
    ///   
    /// Used when you don't control the type you need to (de)serialize - you markup the surrogate type
    ///   and then the uncontrolled type is (de)serialized as if it were the surrogate type.
    /// </summary>
    [NotEquatable("Mutable")]
    public sealed class SurrogateTypeDescriberBuilder
    {
        /// <summary>
        /// ITypeDescriber to use to discover providers and enumerate members on surrogate types.
        /// </summary>
        public ITypeDescriber TypeDescriber { get; private set; }

        /// <summary>
        /// ITypeDescriber that is used to discover providers or enumerate members if no registration exists
        /// and FallbackBehavior allows falling back.
        /// </summary>
        public ITypeDescriber FallbackTypeDescriber { get; private set; }

        /// <summary>
        /// The configured behavior to use when a type has no registered surrogate.
        /// </summary>
        public SurrogateTypeDescriberFallbackBehavior FallbackBehavior { get; private set; }

        private readonly ImmutableDictionary<TypeInfo, TypeInfo>.Builder SurrogateTypes;

        private SurrogateTypeDescriberBuilder(ITypeDescriber typeDescribe, ITypeDescriber fallback, SurrogateTypeDescriberFallbackBehavior behavior, ImmutableDictionary<TypeInfo, TypeInfo>.Builder types)
        {
            TypeDescriber = typeDescribe;
            FallbackTypeDescriber = fallback;
            FallbackBehavior = behavior;

            SurrogateTypes = types;
        }

        // make builders

        /// <summary>
        /// Creates a SurrogateTypeDescriberBuilder which using TypeDescriber.Default to describes surrogates,
        ///   and falls back to TypeDescriber.Default if no surrogate is registered.
        /// </summary>
        public static SurrogateTypeDescriberBuilder CreateBuilder()
        => CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback);

        /// <summary>
        /// Creates a SurrogateTypeDescriberBuilder with the given fallback behavior.
        /// 
        /// Uses TypeDescriber.Default to describes surrogates,
        ///   and falls back to TypeDescriber.Default if no surrogate is registered and the provided SurrogateTypeDescriberFallbackBehavior
        ///   allows it.
        /// </summary>
        public static SurrogateTypeDescriberBuilder CreateBuilder(SurrogateTypeDescriberFallbackBehavior fallbackBehavior)
        => CreateBuilder(fallbackBehavior, TypeDescribers.Default, TypeDescribers.Default);

        /// <summary>
        /// Creates a SurrogateTypeDescriberBuilder with the given fallback behavior and type describer.
        /// 
        /// Uses the given ITypeDescriber to describes surrogates,
        ///   and falls back to TypeDescriber.Default if no surrogate is registered if the provided SurrogateTypeDescriberFallbackBehavior
        ///   allows it.
        /// </summary>
        public static SurrogateTypeDescriberBuilder CreateBuilder(SurrogateTypeDescriberFallbackBehavior fallbackBehavior, ITypeDescriber typeDescriber)
        => CreateBuilder(fallbackBehavior, typeDescriber, TypeDescribers.Default);

        /// <summary>
        /// Creates a SurrogateTypeDescriberBuilder with the given fallback behavior, type describer, and fallback type describer.
        /// 
        /// Uses the given ITypeDescriber to describes surrogates,
        ///   and falls back to provided fallback if no surrogate is registered and the provided SurrogateTypeDescriberFallbackBehavior
        ///   allows it.
        /// </summary>
        public static SurrogateTypeDescriberBuilder CreateBuilder(SurrogateTypeDescriberFallbackBehavior fallbackBehavior, ITypeDescriber typeDescriber, ITypeDescriber fallbackTypeDescriber)
        {
            if (!Enum.IsDefined(typeof(SurrogateTypeDescriberFallbackBehavior), fallbackBehavior))
            {
                return Throw.ArgumentException<SurrogateTypeDescriberBuilder>($"Unexpected {nameof(SurrogateTypeDescriberFallbackBehavior)}: {fallbackBehavior}", nameof(fallbackBehavior));
            }

            Utils.CheckArgumentNull(typeDescriber, nameof(typeDescriber));
            Utils.CheckArgumentNull(fallbackTypeDescriber, nameof(fallbackTypeDescriber));

            var inner = ImmutableDictionary.CreateBuilder<TypeInfo, TypeInfo>();

            return new SurrogateTypeDescriberBuilder(typeDescriber, fallbackTypeDescriber, fallbackBehavior, inner);
        }

        /// <summary>
        /// Creates a SurrogateTypeDescriberBuilder which copies it's fallback behavior, type describer, fallback type describer, and
        ///   surrogate types from the given SurrogateTypeDescriber.
        /// </summary>
        public static SurrogateTypeDescriberBuilder CreateBuilder(SurrogateTypeDescriber typeDescriber)
        {
            Utils.CheckArgumentNull(typeDescriber, nameof(typeDescriber));

            var inner = ImmutableDictionary.CreateBuilder<TypeInfo, TypeInfo>();
            foreach (var kv in typeDescriber.SurrogateTypes)
            {
                inner[kv.Key] = kv.Value;
            }

            var behavior = typeDescriber.ThrowOnNoRegisteredSurrogate ? SurrogateTypeDescriberFallbackBehavior.Throw : SurrogateTypeDescriberFallbackBehavior.UseFallback;

            return new SurrogateTypeDescriberBuilder(typeDescriber.TypeDescriber, typeDescriber.FallbackDescriber, behavior, inner);
        }

        // build

        /// <summary>
        /// Create a SurrogateTypeDescriber with the options configured with this builder.
        /// </summary>
        public SurrogateTypeDescriber ToSurrogateTypeDescriber()
        {
            var inner = SurrogateTypes.ToImmutable();

            return new SurrogateTypeDescriber(TypeDescriber, FallbackTypeDescriber, FallbackBehavior, inner);
        }

        // modify

        /// <summary>
        /// Sets the behavior to fallback to when no surrogate type has been registered.
        /// </summary>
        public SurrogateTypeDescriberBuilder SetFallbackBehavior(SurrogateTypeDescriberFallbackBehavior fallbackBehavior)
        {
            if (!Enum.IsDefined(typeof(SurrogateTypeDescriberFallbackBehavior), fallbackBehavior))
            {
                return Throw.ArgumentException<SurrogateTypeDescriberBuilder>($"Unexpected {nameof(SurrogateTypeDescriberFallbackBehavior)}: {fallbackBehavior}", nameof(fallbackBehavior));
            }

            FallbackBehavior = fallbackBehavior;

            return this;
        }

        /// <summary>
        /// Sets the ITypeDescriber to use to discover providers and members on a surrogate type.
        /// </summary>
        public SurrogateTypeDescriberBuilder SetTypeDescriber(ITypeDescriber typeDescriber)
        {
            Utils.CheckArgumentNull(typeDescriber, nameof(typeDescriber));

            TypeDescriber = typeDescriber;

            return this;
        }

        /// <summary>
        /// Sets the ITypeDescriber to use when no surrogate type has been registered, provided FallbackBehavior allows it.
        /// </summary>
        public SurrogateTypeDescriberBuilder SetFallbackTypeDescriber(ITypeDescriber fallbackTypeDescriber)
        {
            Utils.CheckArgumentNull(fallbackTypeDescriber, nameof(fallbackTypeDescriber));

            FallbackTypeDescriber = fallbackTypeDescriber;

            return this;
        }

        /// <summary>
        /// Registered a surrogate type for forType.
        /// 
        /// Whenever forType is passed to one of the EnumerateXXX methods, surrogateType
        ///   will be used to discover members instead.  The discovered members will then
        ///   be mapped to forType, and returned.
        /// </summary>
        public SurrogateTypeDescriberBuilder AddSurrogateType(TypeInfo forType, TypeInfo surrogateType)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));
            Utils.CheckArgumentNull(surrogateType, nameof(surrogateType));

            if (forType == surrogateType)
            {
                return Throw.InvalidOperationException<SurrogateTypeDescriberBuilder>($"Type {forType} cannot be a surrogate for itself");
            }

            if (SurrogateTypes.ContainsKey(forType))
            {
                return Throw.InvalidOperationException<SurrogateTypeDescriberBuilder>($"Surrogate already registered for {forType}");
            }

            SurrogateTypes[forType] = surrogateType;

            return this;
        }

        // overrides

        /// <summary>
        /// Returns a representation of this SurrogateTypeDescriberBuilder object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        {
            var ret = new StringBuilder();

            ret.Append($"{nameof(SurrogateTypeDescriberBuilder)} using type describer {TypeDescriber}");
            ret.Append($" and fallback behavior {FallbackBehavior}");
            ret.Append($" with fallback type describer {FallbackTypeDescriber}");
            
            if (SurrogateTypes.Count > 0)
            {
                ret.Append(" and uses ");
                var isFirst = true;
                foreach (var kv in SurrogateTypes)
                {
                    if (!isFirst)
                    {
                        ret.Append(", ");
                    }

                    isFirst = false;
                    ret.Append($"{kv.Value} for {kv.Key}");
                }
            }

            return ret.ToString();
        }
    }
}
