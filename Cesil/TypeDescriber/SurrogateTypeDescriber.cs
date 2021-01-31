using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;

using static Cesil.BindingFlagsConstants;

namespace Cesil
{
    /// <summary>
    /// A ITypeDesciber that enumerates members on a surrogate type and maps them to another type.
    /// 
    /// Used when you don't control the type you need to (de)serialize - you markup the surrogate type
    ///   and then the uncontrolled type is (de)serialized as if it were the surrogate type.
    /// </summary>
    public sealed class SurrogateTypeDescriber : ITypeDescriber, IEquatable<SurrogateTypeDescriber>
    {
        internal readonly ITypeDescriber TypeDescriber;

        internal readonly ITypeDescriber FallbackDescriber;

        internal readonly bool ThrowOnNoRegisteredSurrogate;

        internal readonly ImmutableDictionary<TypeInfo, TypeInfo> SurrogateTypes;

        internal SurrogateTypeDescriber(ITypeDescriber typeDescriber, ITypeDescriber fallback, SurrogateTypeDescriberFallbackBehavior fallbackBehavior, ImmutableDictionary<TypeInfo, TypeInfo> surrogateTypes)
        {
            SurrogateTypes = surrogateTypes;
            TypeDescriber = typeDescriber;
            FallbackDescriber = fallback;

            switch (fallbackBehavior)
            {
                case SurrogateTypeDescriberFallbackBehavior.Throw:
                    ThrowOnNoRegisteredSurrogate = true;
                    break;
                case SurrogateTypeDescriberFallbackBehavior.UseFallback:
                    ThrowOnNoRegisteredSurrogate = false;
                    break;
                default:
                    Throw.ImpossibleException($"Unexpected {nameof(SurrogateTypeDescriberFallbackBehavior)}: {fallbackBehavior}");
                    break;
            }
        }

        // builders

        /// <summary>
        /// Creates a SurrogateTypeDescriberBuilder which using TypeDescriber.Default to describes surrogates,
        ///   and falls back to TypeDescriber.Default if no surrogate is registered.
        /// </summary>
        public static SurrogateTypeDescriberBuilder CreateBuilder()
        => SurrogateTypeDescriberBuilder.CreateBuilder();

        /// <summary>
        /// Creates a SurrogateTypeDescriberBuilder with the given fallback behavior.
        /// 
        /// Uses TypeDescriber.Default to describes surrogates,
        ///   and falls back to TypeDescriber.Default if no surrogate is registered and the provided SurrogateTypeDescriberFallbackBehavior
        ///   allows it.
        /// </summary>
        public static SurrogateTypeDescriberBuilder CreateBuilder(SurrogateTypeDescriberFallbackBehavior fallbackBehavior)
        => SurrogateTypeDescriberBuilder.CreateBuilder(fallbackBehavior);

        /// <summary>
        /// Creates a SurrogateTypeDescriberBuilder with the given fallback behavior and type describer.
        /// 
        /// Uses the given ITypeDescriber to describes surrogates,
        ///   and falls back to TypeDescriber.Default if no surrogate is registered if the provided SurrogateTypeDescriberFallbackBehavior
        ///   allows it.
        /// </summary>
        public static SurrogateTypeDescriberBuilder CreateBuilder(SurrogateTypeDescriberFallbackBehavior fallbackBehavior, ITypeDescriber typeDescriber)
        => SurrogateTypeDescriberBuilder.CreateBuilder(fallbackBehavior, typeDescriber);

        /// <summary>
        /// Creates a SurrogateTypeDescriberBuilder with the given fallback behavior, type describer, and fallback type describer.
        /// 
        /// Uses the given ITypeDescriber to describes surrogates,
        ///   and falls back to provided fallback if no surrogate is registered and the provided SurrogateTypeDescriberFallbackBehavior
        ///   allows it.
        /// </summary>
        public static SurrogateTypeDescriberBuilder CreateBuilder(SurrogateTypeDescriberFallbackBehavior fallbackBehavior, ITypeDescriber typeDescriber, ITypeDescriber fallbackTypeDescriber)
        => SurrogateTypeDescriberBuilder.CreateBuilder(fallbackBehavior, typeDescriber, fallbackTypeDescriber);

        /// <summary>
        /// Creates a SurrogateTypeDescriberBuilder which copies it's fallback behavior, type describer, fallback type describer, and
        ///   surrogate types from the given SurrogateTypeDescriber.
        /// </summary>
        public static SurrogateTypeDescriberBuilder CreateBuilder(SurrogateTypeDescriber typeDescriber)
        => SurrogateTypeDescriberBuilder.CreateBuilder(typeDescriber);

        /// <summary>
        /// Enumerate all the members on forType to deserialize.
        /// 
        /// If a surrogate is registered, the surrogate will be used for discovery - the returned 
        ///   members will be mapped from the surrogate to forType.
        ///   
        /// If a surrogate is not registered, either an exception will be thrown or forType will
        ///   be passed to TypeDescriber.EnumerateMembersToDeserialize depending on the value of
        ///   ThrowOnNoRegisteredSurrogate.
        /// </summary>
        public IEnumerable<DeserializableMember> EnumerateMembersToDeserialize(TypeInfo forType)
        {
            if (!SurrogateTypes.TryGetValue(forType, out var proxy))
            {
                if (ThrowOnNoRegisteredSurrogate)
                {
                    Throw.InvalidOperationException($"No surrogate registered for {forType}");
                }

                return FallbackDescriber.EnumerateMembersToDeserialize(forType);
            }

            var ret = new List<DeserializableMember>();

            var fromProxy = TypeDescriber.EnumerateMembersToDeserialize(proxy);
            foreach (var member in fromProxy)
            {
                var mapped = Map(forType, member);
                ret.Add(mapped);
            }

            return ret;
        }

        /// <summary>
        /// Enumerate all the members on forType to serialize.
        /// 
        /// If a surrogate is registered, the surrogate will be used for discovery - the returned 
        ///   members will be mapped from the surrogate to forType.
        ///   
        /// If a surrogate is not registered, either an exception will be thrown or forType will
        ///   be passed to TypeDescriber.EnumerateMembersToSerialize depending on the value of
        ///   ThrowOnNoRegisteredSurrogate.
        /// </summary>
        public IEnumerable<SerializableMember> EnumerateMembersToSerialize(TypeInfo forType)
        {
            if (!SurrogateTypes.TryGetValue(forType, out var proxy))
            {
                if (ThrowOnNoRegisteredSurrogate)
                {
                    Throw.InvalidOperationException($"No surrogate registered for {forType}");
                }

                return FallbackDescriber.EnumerateMembersToSerialize(forType);
            }

            var ret = new List<SerializableMember>();

            var fromProxy = TypeDescriber.EnumerateMembersToSerialize(proxy);
            foreach (var member in fromProxy)
            {
                var mapped = Map(forType, member);
                ret.Add(mapped);
            }

            return ret;
        }

        /// <summary>
        /// Gets an instance builder usable to construct the given type.
        /// 
        /// If a surrogate is registered, the surrogate will be used for discovery - the returned 
        ///   constructor will be mapped from the surrogate to forType.
        ///   
        /// If a surrogate is not registered, either an exception will be thrown or forType will
        ///   be passed to TypeDescriber.GetInstanceProvider depending on the value of
        ///   ThrowOnNoRegisteredSurrogate.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        public InstanceProvider? GetInstanceProvider(TypeInfo forType)
        {
            if (!SurrogateTypes.TryGetValue(forType, out var proxy))
            {
                if (ThrowOnNoRegisteredSurrogate)
                {
                    Throw.InvalidOperationException($"No surrogate registered for {forType}");
                }

                return FallbackDescriber.GetInstanceProvider(forType);
            }

            var fromProxy = TypeDescriber.GetInstanceProvider(proxy);
            if (fromProxy == null)
            {
                Throw.InvalidOperationException($"No {nameof(InstanceProvider)} returned by {TypeDescriber} for {proxy}");
            }

            return Map(forType, fromProxy);
        }

        private static DeserializableMember Map(TypeInfo ontoType, DeserializableMember member)
        {
            MethodInfo? resetOnType = null;
            if (member.Reset.HasValue)
            {
                var surrogateResetWrapper = member.Reset.Value;
                if (surrogateResetWrapper.Mode != BackingMode.Method)
                {
                    Throw.InvalidOperationException($"Cannot map reset {surrogateResetWrapper} onto {ontoType}, reset isn't backed by a method");
                }

                var surrogateReset = surrogateResetWrapper.Method.Value;

                var surrogateResetBinding = GetEquivalentFlagsFor(surrogateReset.IsPublic, surrogateReset.IsStatic);

                Type[] resetTakesTypes;
                if (surrogateResetWrapper.TakesContext)
                {
                    resetTakesTypes = new[] { Types.ReadContext.MakeByRefType() };
                }
                else
                {
                    resetTakesTypes = Type.EmptyTypes;
                }

                // intentionally letting this be null
                resetOnType = ontoType.GetMethod(surrogateReset.Name, surrogateResetBinding, null, resetTakesTypes, null);
                if (resetOnType == null)
                {
                    Throw.InvalidOperationException($"No equivalent to {resetOnType} found on {ontoType}");
                }
            }

            var surrogateSetterWrapper = member.Setter;
            switch (surrogateSetterWrapper.Mode)
            {
                case BackingMode.Field:
                    {
                        var surrogateField = surrogateSetterWrapper.Field.Value;
                        var surrogateFieldBinding = GetEquivalentFlagsFor(surrogateField.IsPublic, surrogateField.IsStatic);

                        // intentionally allowing null here
                        var fieldOnType = ontoType.GetField(surrogateField.Name, surrogateFieldBinding);
                        if (fieldOnType == null)
                        {
                            Throw.InvalidOperationException($"No equivalent to {surrogateField} found on {ontoType}");
                        }

                        if (fieldOnType.FieldType != surrogateField.FieldType)
                        {
                            Throw.InvalidOperationException($"Field {fieldOnType} type ({fieldOnType.FieldType}) does not match surrogate field {surrogateField} type ({surrogateField.FieldType})");
                        }

                        var required = GetEquivalentRequiredFor(member.IsRequired);

                        return DeserializableMember.CreateInner(ontoType, member.Name, (Setter?)fieldOnType, member.Parser, required, (Reset?)resetOnType, null);
                    }
                case BackingMode.Method:
                    {
                        var surrogateSetter = surrogateSetterWrapper.Method.Value;

                        var surrogateSetterBinding = GetEquivalentFlagsFor(surrogateSetter.IsPublic, surrogateSetter.IsStatic);

                        // intentionally letting this be null
                        var setterOnType = ontoType.GetMethod(surrogateSetter.Name, surrogateSetterBinding);
                        if (setterOnType == null)
                        {
                            Throw.InvalidOperationException($"No equivalent to {surrogateSetter} found on {ontoType}");
                        }

                        var paramsOnType = setterOnType.GetParameters();
                        var paramsOnSurrogate = surrogateSetter.GetParameters();

                        if (paramsOnType.Length != paramsOnSurrogate.Length)
                        {
                            Throw.InvalidOperationException($"Parameters for {setterOnType} do not match parameters for {surrogateSetter}");
                        }

                        for (var i = 0; i < paramsOnType.Length; i++)
                        {
                            var pOnType = paramsOnType[i];
                            var pOnSurrogate = paramsOnSurrogate[i];

                            if (pOnType.ParameterType != pOnSurrogate.ParameterType)
                            {
                                Throw.InvalidOperationException($"Parameter #{(i + 1)} on {setterOnType} does not match same parameter on {surrogateSetter}");
                            }
                        }

                        var required = GetEquivalentRequiredFor(member.IsRequired);

                        return DeserializableMember.CreateInner(ontoType, member.Name, (Setter?)setterOnType, member.Parser, required, (Reset?)resetOnType, null);
                    }
                case BackingMode.Delegate:
                    Throw.InvalidOperationException($"Cannot map setter {surrogateSetterWrapper} onto {ontoType}, setter is backed by a delegate");
                    return default;
                default:
                    Throw.InvalidOperationException($"Unexpected {nameof(BackingMode)}: {surrogateSetterWrapper.Mode}");
                    return default;
            }
        }

        private static SerializableMember Map(TypeInfo ontoType, SerializableMember member)
        {
            ShouldSerialize? shouldSerializeOnType;

            if (member.ShouldSerialize.HasValue)
            {
                var surrogateShouldSerializeWrapper = member.ShouldSerialize.Value;
                if (surrogateShouldSerializeWrapper.Mode == BackingMode.Method)
                {
                    if (surrogateShouldSerializeWrapper.Takes.HasValue && surrogateShouldSerializeWrapper.IsStatic)
                    {
                        Throw.InvalidOperationException($"Cannot map 'should serialize' {surrogateShouldSerializeWrapper} onto {ontoType}, it takes a parameter");
                    }

                    var surrogateShouldSerialize = surrogateShouldSerializeWrapper.Method.Value;
                    var surrogateShouldSerializeBinding = GetEquivalentFlagsFor(surrogateShouldSerialize.IsPublic, surrogateShouldSerialize.IsStatic);

                    // intentionally letting this be null
                    var shouldSerializeOnTypeMtd = ontoType.GetMethod(surrogateShouldSerialize.Name, surrogateShouldSerializeBinding);
                    if (shouldSerializeOnTypeMtd == null)
                    {
                        Throw.InvalidOperationException($"No equivalent to {surrogateShouldSerialize} found on {ontoType}");
                    }

                    shouldSerializeOnType = ShouldSerialize.ForMethod(shouldSerializeOnTypeMtd);
                }
                else
                {
                    Throw.InvalidOperationException($"Cannot map 'should serialize' {surrogateShouldSerializeWrapper} onto {ontoType}, 'should serialize' isn't backed by a method");
                    return default;
                }
            }
            else
            {
                shouldSerializeOnType = null;
            }

            var surrogateGetterWrapper = member.Getter;
            switch (surrogateGetterWrapper.Mode)
            {
                case BackingMode.Field:
                    {
                        var surrogateField = surrogateGetterWrapper.Field.Value;
                        var surrogateFieldBinding = GetEquivalentFlagsFor(surrogateField.IsPublic, surrogateField.IsStatic);

                        var fieldOnType = ontoType.GetField(surrogateField.Name, surrogateFieldBinding);
                        if (fieldOnType == null)
                        {
                            Throw.InvalidOperationException($"No equivalent to {surrogateField} found on {ontoType}");
                        }

                        if (fieldOnType.FieldType != surrogateField.FieldType)
                        {
                            Throw.InvalidOperationException($"Field {fieldOnType} type ({fieldOnType.FieldType}) does not match surrogate field {surrogateField} type ({surrogateField.FieldType})");
                        }

                        var emitDefaultField = GetEquivalentEmitFor(member.EmitDefaultValue);
                        return SerializableMember.CreateInner(ontoType, member.Name, (Getter?)fieldOnType, member.Formatter, shouldSerializeOnType, emitDefaultField);
                    }
                case BackingMode.Delegate:
                    Throw.InvalidOperationException($"Cannot map getter {surrogateGetterWrapper} onto {ontoType}, getter isn't backed by a method");
                    return default;
                case BackingMode.Method:
                    goto handleMethod;
                default:
                    Throw.InvalidOperationException($"Unexpected {nameof(BackingMode)}: {surrogateGetterWrapper.Mode}");
                    return default;
            }

handleMethod:
            var surrogateGetter = surrogateGetterWrapper.Method.Value;
            var surrogateGetterBinding = GetEquivalentFlagsFor(surrogateGetter.IsPublic, surrogateGetter.IsStatic);

            // intentionally letting this be null
            var getterOnType = ontoType.GetMethod(surrogateGetter.Name, surrogateGetterBinding);
            if (getterOnType == null)
            {
                Throw.InvalidOperationException($"No equivalent to {surrogateGetter} found on {ontoType}");
            }

            var surrogateParams = surrogateGetter.GetParameters();
            var onTypeParams = getterOnType.GetParameters();

            if (surrogateParams.Length != onTypeParams.Length)
            {
                Throw.InvalidOperationException($"Parameters for {getterOnType} do not match parameters for {surrogateGetter}");
            }

            for (var i = 0; i < surrogateParams.Length; i++)
            {
                var sP = surrogateParams[i].ParameterType.GetTypeInfo();
                var tP = onTypeParams[i].ParameterType.GetTypeInfo();

                if (sP != tP)
                {
                    Throw.InvalidOperationException($"Parameter #{(i + 1)} on {getterOnType} does not match same parameter on {surrogateGetter}");
                }
            }

            var emitDefault = GetEquivalentEmitFor(member.EmitDefaultValue);
            return SerializableMember.CreateInner(ontoType, member.Name, (Getter?)getterOnType, member.Formatter, shouldSerializeOnType, emitDefault);
        }

        private static InstanceProvider Map(TypeInfo ontoType, InstanceProvider builder)
        {
            switch (builder.Mode)
            {
                case BackingMode.Delegate:
                    Throw.InvalidOperationException($"Cannot map a delegate {nameof(InstanceProvider)} between types");
                    return default;
                case BackingMode.Constructor:
                    {
                        var surrogateCons = builder.Constructor.Value;
                        var surrogateConsBinding = GetEquivalentFlagsFor(surrogateCons.IsPublic, false);

                        var consPs = surrogateCons.GetParameters();
                        var consTypes = new Type[consPs.Length];
                        for (var i = 0; i < consPs.Length; i++)
                        {
                            consTypes[i] = consPs[i].ParameterType;
                        }

                        var consOnType = ontoType.GetConstructor(surrogateConsBinding, null, consTypes, null);
                        if (consOnType == null)
                        {
                            Throw.InvalidOperationException($"No equivalent to {surrogateCons} found on {ontoType}");
                        }

                        var fallbacks = ImmutableArray<InstanceProvider>.Empty;

                        if (builder.HasFallbacks)
                        {
                            var arrBuilder = ImmutableArray.CreateBuilder<InstanceProvider>();
                            var elseProv = (IElseSupporting<InstanceProvider>)builder;
                            foreach (var fallback in elseProv.Fallbacks)
                            {
                                var mapped = Map(ontoType, fallback);
                                arrBuilder.Add(mapped);
                            }

                            fallbacks = arrBuilder.ToImmutable();
                        }

                        return new InstanceProvider(consOnType, fallbacks, builder.ConstructsNullability, null);
                    }
                case BackingMode.Method:
                    Throw.InvalidOperationException($"Cannot map a method {nameof(InstanceProvider)} between types");
                    return default;
                default:
                    Throw.InvalidOperationException($"Unexpected {nameof(BackingMode)}: {builder.Mode}");
                    return default;
            }
        }

        // internal for testing purposes
        internal static BindingFlags GetEquivalentFlagsFor(bool isPublic, bool isStatic)
        {
            if (isPublic)
            {
                if (isStatic) return PublicStatic;

                return PublicInstance;
            }
            else
            {
                if (isStatic) return InternalStatic;

                return InternalInstance;
            }
        }

        // internal for testing purposes
        internal static EmitDefaultValue GetEquivalentEmitFor(bool b)
        => b ? EmitDefaultValue.Yes : EmitDefaultValue.No;

        // internal for testing purposes
        internal static MemberRequired GetEquivalentRequiredFor(bool b)
        => b ? MemberRequired.Yes : MemberRequired.No;

        /// <summary>
        /// Returns a stable hash for this SurrogateTypeDescriber.
        /// </summary>
        public override int GetHashCode()
        // including count, not the actual types, because traversal order isn't stable
        => HashCode.Combine(TypeDescriber, FallbackDescriber, ThrowOnNoRegisteredSurrogate, SurrogateTypes.Count);


        /// <summary>
        /// Returns true if this SurrogateTypeDescriber equals the given object
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is SurrogateTypeDescriber other)
            {
                return Equals(other);
            }

            return false;
        }

        /// <summary>
        /// Returns true if this SurrogateTypeDescriber equals the given SurrogateTypeDescriber.
        /// </summary>
        public bool Equals(SurrogateTypeDescriber? typeDescriber)
        {
            if (ReferenceEquals(typeDescriber, null)) return false;

            if (typeDescriber.ThrowOnNoRegisteredSurrogate != ThrowOnNoRegisteredSurrogate) return false;
            if (typeDescriber.SurrogateTypes.Count != SurrogateTypes.Count) return false;

            if (!typeDescriber.TypeDescriber.Equals(TypeDescriber)) return false;
            if (!typeDescriber.FallbackDescriber.Equals(FallbackDescriber)) return false;

            foreach (var kv in typeDescriber.SurrogateTypes)
            {
                if (!SurrogateTypes.TryGetValue(kv.Key, out var val)) return false;

                if (kv.Value != val) return false;
            }

            return true;
        }

        /// <summary>
        /// Returns a representation of this SurrogateTypeDescriber object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        {
            var ret = new StringBuilder();

            ret.Append($"{nameof(SurrogateTypeDescriber)} using type describer {TypeDescriber}");
            if (ThrowOnNoRegisteredSurrogate)
            {
                ret.Append(" which throws when no surrogate registered");
            }
            else
            {
                ret.Append(" which delegates when no surrogate registered");
            }

            ret.Append($" and falls back to {FallbackDescriber} if no surrogate is registered");

            if (SurrogateTypes.Any())
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

        // dynamic bits

        /// <summary>
        /// Delegates to TypeDescriber.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        public Parser? GetDynamicCellParserFor(in ReadContext context, TypeInfo targetType)
        => TypeDescriber.GetDynamicCellParserFor(in context, targetType);

        /// <summary>
        /// Delegates to TypeDescriber.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        public DynamicRowConverter? GetDynamicRowConverter(in ReadContext context, IEnumerable<ColumnIdentifier> columns, TypeInfo targetType)
        => TypeDescriber.GetDynamicRowConverter(in context, columns, targetType);

        /// <summary>
        /// Delegates to TypeDescriber.
        /// </summary>
        [return: IntentionallyExposedPrimitive("Count, int is the best option")]
        public int GetCellsForDynamicRow(in WriteContext context, object row, Span<DynamicCellValue> cells)
        => TypeDescriber.GetCellsForDynamicRow(in context, row, cells);

        // operators

        /// <summary>
        /// Compare two SurrogateTypeDescribers for equality
        /// </summary>
        public static bool operator ==(SurrogateTypeDescriber? a, SurrogateTypeDescriber? b)
        => Utils.NullReferenceEquality(a, b);


        /// <summary>
        /// Compare two SurrogateTypeDescribers for inequality
        /// </summary>
        public static bool operator !=(SurrogateTypeDescriber? a, SurrogateTypeDescriber? b)
        => !(a == b);
    }
}
