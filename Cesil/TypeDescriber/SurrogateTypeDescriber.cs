using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Cesil
{
    /// <summary>
    /// How to behave if a ManualTypeDescriber needs to
    ///   describe a type that isn't explicitly configured.
    /// </summary>
    public enum SurrogateTypeDescriberFallbackBehavior : byte
    {
        /// <summary>
        /// Throw if no type is configured.
        /// </summary>
        Throw = 1,
        /// <summary>
        /// Use DefaultTypeDescriber if no type is configured.
        /// </summary>
        UseDefault = 2,
        /// <summary>
        /// Use the ITypeDescriber provided for use on registered surrogate types.
        /// </summary>
        UseProvided = 3
    }

    /// <summary>
    /// A ITypeDesciber that enumerates members on a surrogate type and maps them to another type.
    /// 
    /// Used when you don't control the type you need to (de)serialize - you markup the surrogate type
    ///   and then the uncontrolled type is (de)serialized as if it were the surrogate type.
    /// </summary>
    [NotEquatable("Mutable")]
    public sealed class SurrogateTypeDescriber : ITypeDescriber
    {
        private readonly ITypeDescriber TypeDescriber;

        private readonly ITypeDescriber? FallbackDescriber;

        private readonly bool ThrowOnNoRegisteredSurrogate;

        private readonly Dictionary<TypeInfo, TypeInfo> SurrogateTypes;

        /// <summary>
        /// Create a new SurrogateTypeDescriber.
        /// </summary>
        public SurrogateTypeDescriber(ITypeDescriber surrogateTypeDescriber, SurrogateTypeDescriberFallbackBehavior fallbackBehavior)
        {
            SurrogateTypes = new Dictionary<TypeInfo, TypeInfo>();
            TypeDescriber = surrogateTypeDescriber;

            if (surrogateTypeDescriber == null)
            {
                Throw.ArgumentNullException<object>(nameof(surrogateTypeDescriber));
                return;
            }

            switch (fallbackBehavior)
            {
                case SurrogateTypeDescriberFallbackBehavior.Throw:
                    ThrowOnNoRegisteredSurrogate = true;
                    FallbackDescriber = null;
                    break;
                case SurrogateTypeDescriberFallbackBehavior.UseDefault:
                    ThrowOnNoRegisteredSurrogate = false;
                    FallbackDescriber = TypeDescribers.Default;
                    break;
                case SurrogateTypeDescriberFallbackBehavior.UseProvided:
                    ThrowOnNoRegisteredSurrogate = false;
                    FallbackDescriber = surrogateTypeDescriber;
                    break;
                default:
                    Throw.ArgumentException<object>($"Unexpected {nameof(SurrogateTypeDescriberFallbackBehavior)}: {fallbackBehavior}", nameof(fallbackBehavior));
                    return;
            }
        }

        /// <summary>
        /// Create a new SurrogateTypeDescriber, using the given ITypeDescriber.
        /// 
        /// Uses the given ITypeDescriber on types that are not explicitly registered.
        /// </summary>
        public SurrogateTypeDescriber(ITypeDescriber proxiedTypeDescriber) : this(proxiedTypeDescriber, SurrogateTypeDescriberFallbackBehavior.UseProvided) { }
        /// <summary>
        /// Create a new SurrogateTypeDescriber, using the given SurrogateTypeDescriberFallbackBehavior.
        /// 
        /// Uses TypeDescribers.Default as it's inner ITypeDescriber.
        /// </summary>
        public SurrogateTypeDescriber(SurrogateTypeDescriberFallbackBehavior fallbackBehavior) : this(TypeDescribers.Default, fallbackBehavior) { }

        /// <summary>
        /// Registered a surrogate type for forType.
        /// 
        /// Whenever forType is passed to one of the EnumerateXXX methods, surrogateType
        ///   will be used to discover members instead.  The discovered members will then
        ///   be mapped to forType, and returned.
        /// </summary>
        public void AddSurrogateType(TypeInfo forType, TypeInfo surrogateType)
        {
            if (forType == null)
            {
                Throw.ArgumentNullException<object>(nameof(forType));
                return;
            }

            if (surrogateType == null)
            {
                Throw.ArgumentNullException<object>(nameof(surrogateType));
                return;
            }

            if (forType == surrogateType)
            {
                Throw.InvalidOperationException<object>($"Type {forType} cannot be a surrogate for itself");
                return;
            }

            if (!SurrogateTypes.TryAdd(forType, surrogateType))
            {
                Throw.InvalidOperationException<object>($"Surrogate already registered for {forType}");
            }
        }

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
                    return Throw.InvalidOperationException<IEnumerable<DeserializableMember>>($"No surrogate registered for {forType}");
                }

                return TypeDescriber.EnumerateMembersToDeserialize(forType);
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
                    return Throw.InvalidOperationException<IEnumerable<SerializableMember>>($"No surrogate registered for {forType}");
                }

                return TypeDescriber.EnumerateMembersToSerialize(forType);

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
        public InstanceProvider? GetInstanceProvider(TypeInfo forType)
        {
            if (!SurrogateTypes.TryGetValue(forType, out var proxy))
            {
                if (ThrowOnNoRegisteredSurrogate)
                {
                    return Throw.InvalidOperationException<InstanceProvider>($"No surrogate registered for {forType}");
                }

                return TypeDescriber.GetInstanceProvider(forType);
            }

            var fromProxy = TypeDescriber.GetInstanceProvider(proxy);
            if(fromProxy == null)
            {
                return Throw.InvalidOperationException<InstanceProvider>($"No {nameof(InstanceProvider)} returned by {TypeDescriber} for {proxy}");
            }

            return Map(forType, fromProxy);
        }

        private static DeserializableMember Map(TypeInfo ontoType, DeserializableMember member)
        {
            MethodInfo? resetOnType = null;
            if (member.HasReset)
            {
                var surrogateResetWrapper = member.Reset;
                if (surrogateResetWrapper.Mode != BackingMode.Method)
                {
                    return Throw.InvalidOperationException<DeserializableMember>($"Cannot map reset {surrogateResetWrapper} onto {ontoType}, reset isn't backed by a method");
                }

                var surrogateReset = surrogateResetWrapper.Method;

                var surrogateResetBinding = GetEquivalentFlagsFor(surrogateReset.IsPublic, surrogateReset.IsStatic);

                // intentionally letting this be null
                resetOnType = ontoType.GetMethod(surrogateReset.Name, surrogateResetBinding);
                if (resetOnType == null)
                {
                    return Throw.InvalidOperationException<DeserializableMember>($"No equivalent to {resetOnType} found on {ontoType}");
                }
            }

            var surrogateSetterWrapper = member.Setter;
            switch (surrogateSetterWrapper.Mode)
            {
                case BackingMode.Field:
                    {
                        var surrogateField = surrogateSetterWrapper.Field;
                        var surrogateFieldBinding = GetEquivalentFlagsFor(surrogateField.IsPublic, surrogateField.IsStatic);

                        // intentionally allowing null here
                        var fieldOnType = ontoType.GetField(surrogateField.Name, surrogateFieldBinding);
                        if (fieldOnType == null)
                        {
                            return Throw.InvalidOperationException<DeserializableMember>($"No equivalent to {surrogateField} found on {ontoType}");
                        }

                        if (fieldOnType.FieldType != surrogateField.FieldType)
                        {
                            return Throw.InvalidOperationException<DeserializableMember>($"Field {fieldOnType} type ({fieldOnType.FieldType}) does not match surrogate field {surrogateField} type ({surrogateField.FieldType})");
                        }

                        var required = GetEquivalentRequiredFor(member.IsRequired);

                        return DeserializableMember.CreateInner(ontoType, member.Name, (Setter?)fieldOnType, member.Parser, required, (Reset?)resetOnType);
                    }
                case BackingMode.Method:
                    {
                        var surrogateSetter = surrogateSetterWrapper.Method;

                        var surrogateSetterBinding = GetEquivalentFlagsFor(surrogateSetter.IsPublic, surrogateSetter.IsStatic);

                        // intentionally letting this be null
                        var setterOnType = ontoType.GetMethod(surrogateSetter.Name, surrogateSetterBinding);
                        if (setterOnType == null)
                        {
                            return Throw.InvalidOperationException<DeserializableMember>($"No equivalent to {surrogateSetter} found on {ontoType}");
                        }

                        var paramsOnType = setterOnType.GetParameters();
                        var paramsOnSurrogate = surrogateSetter.GetParameters();

                        if (paramsOnType.Length != paramsOnSurrogate.Length)
                        {
                            return Throw.InvalidOperationException<DeserializableMember>($"Parameters for {setterOnType} do not match parameters for {surrogateSetter}");
                        }

                        for (var i = 0; i < paramsOnType.Length; i++)
                        {
                            var pOnType = paramsOnType[i];
                            var pOnSurrogate = paramsOnSurrogate[i];

                            if (pOnType.ParameterType != pOnSurrogate.ParameterType)
                            {
                                return Throw.InvalidOperationException<DeserializableMember>($"Parameter #{(i + 1)} on {setterOnType} does not match same parameter on {surrogateSetter}");
                            }
                        }

                        var required = GetEquivalentRequiredFor(member.IsRequired);

                        return DeserializableMember.CreateInner(ontoType, member.Name, (Setter?)setterOnType, member.Parser, required, (Reset?)resetOnType);
                    }
                case BackingMode.Delegate:
                    return Throw.InvalidOperationException<DeserializableMember>($"Cannot map setter {surrogateSetterWrapper} onto {ontoType}, setter is backed by a delegate");
                default:
                    return Throw.InvalidOperationException<DeserializableMember>($"Unexpected {nameof(BackingMode)}: {surrogateSetterWrapper.Mode}");
            }
        }

        private static SerializableMember Map(TypeInfo ontoType, SerializableMember member)
        {
            ShouldSerialize? shouldSerializeOnType;
            
            if (member.HasShouldSerialize)
            {
                var surrogateShouldSerializeWrapper = member.ShouldSerialize;
                if (surrogateShouldSerializeWrapper.Mode == BackingMode.Method)
                {
                    var surrogateShouldSerialize = surrogateShouldSerializeWrapper.Method;
                    var surrogateShouldSerializeBinding = GetEquivalentFlagsFor(surrogateShouldSerialize.IsPublic, surrogateShouldSerialize.IsStatic);

                    // intentionally letting this be null
                    var shouldSerializeOnTypeMtd = ontoType.GetMethod(surrogateShouldSerialize.Name, surrogateShouldSerializeBinding);
                    if (shouldSerializeOnTypeMtd == null)
                    {
                        return Throw.InvalidOperationException<SerializableMember>($"No equivalent to {surrogateShouldSerialize} found on {ontoType}");
                    }

                    shouldSerializeOnType = ShouldSerialize.ForMethod(shouldSerializeOnTypeMtd);
                }
                else
                {
                    return Throw.InvalidOperationException<SerializableMember>($"Cannot map 'should serialize' {surrogateShouldSerializeWrapper} onto {ontoType}, 'should serialize' isn't backed by a method");
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
                        var surrogateField = surrogateGetterWrapper.Field;
                        var surrogateFieldBinding = GetEquivalentFlagsFor(surrogateField.IsPublic, surrogateField.IsStatic);

                        var fieldOnType = ontoType.GetField(surrogateField.Name, surrogateFieldBinding);
                        if (fieldOnType == null)
                        {
                            return Throw.InvalidOperationException<SerializableMember>($"No equivalent to {surrogateField} found on {ontoType}");
                        }

                        if (fieldOnType.FieldType != surrogateField.FieldType)
                        {
                            return Throw.InvalidOperationException<SerializableMember>($"Field {fieldOnType} type ({fieldOnType.FieldType}) does not match surrogate field {surrogateField} type ({surrogateField.FieldType})");
                        }

                        var emitDefaultField = GetEquivalentEmitFor(member.EmitDefaultValue);
                        return SerializableMember.CreateInner(ontoType, member.Name, (Getter?)fieldOnType, member.Formatter, shouldSerializeOnType, emitDefaultField);
                    }
                case BackingMode.Delegate:
                    return Throw.InvalidOperationException<SerializableMember>($"Cannot map getter {surrogateGetterWrapper} onto {ontoType}, getter isn't backed by a method");
                case BackingMode.Method:
                    goto handleMethod;
                default:
                    return Throw.InvalidOperationException<SerializableMember>($"Unexpected {nameof(BackingMode)}: {surrogateGetterWrapper.Mode}");
            }

handleMethod:
            var surrogateGetter = surrogateGetterWrapper.Method;
            var surrogateGetterBinding = GetEquivalentFlagsFor(surrogateGetter.IsPublic, surrogateGetter.IsStatic);

            // intentionally letting this be null
            var getterOnType = ontoType.GetMethod(surrogateGetter.Name, surrogateGetterBinding);
            if (getterOnType == null)
            {
                return Throw.InvalidOperationException<SerializableMember>($"No equivalent to {surrogateGetter} found on {ontoType}");
            }

            var surrogateParams = surrogateGetter.GetParameters();
            var onTypeParams = getterOnType.GetParameters();

            if (surrogateParams.Length != onTypeParams.Length)
            {
                return Throw.InvalidOperationException<SerializableMember>($"Parameters for {getterOnType} do not match parameters for {surrogateGetter}");
            }

            for (var i = 0; i < surrogateParams.Length; i++)
            {
                var sP = surrogateParams[i].ParameterType.GetTypeInfo();
                var tP = onTypeParams[i].ParameterType.GetTypeInfo();

                if (sP != tP)
                {
                    return Throw.InvalidOperationException<SerializableMember>($"Parameter #{(i + 1)} on {getterOnType} does not match same parameter on {surrogateGetter}");
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
                    return Throw.InvalidOperationException<InstanceProvider>($"Cannot map a delegate {nameof(InstanceProvider)} between types");
                case BackingMode.Constructor:
                    {
                        var surrogateCons = builder.Constructor;
                        var surrogateConsBinding = GetEquivalentFlagsFor(surrogateCons.IsPublic, false);

                        var consOnType = ontoType.GetConstructor(surrogateConsBinding, null, Type.EmptyTypes, null);
                        if (consOnType == null)
                        {
                            return Throw.InvalidOperationException<InstanceProvider>($"No equivalent to {surrogateCons} found on {ontoType}");
                        }

                        return new InstanceProvider(consOnType);
                    }
                case BackingMode.Method:
                    return Throw.InvalidOperationException<InstanceProvider>($"Cannot map a method {nameof(InstanceProvider)} between types");
                default:
                    return Throw.InvalidOperationException<InstanceProvider>($"Unexpected {nameof(BackingMode)}: {builder.Mode}");
            }
        }

        // internal for testing purposes
        internal static BindingFlags GetEquivalentFlagsFor(bool isPublic, bool isStatic)
        {
            return
                (isPublic ? BindingFlags.Public : BindingFlags.NonPublic) |
                (isStatic ? BindingFlags.Static : BindingFlags.Instance);
        }

        // internal for testing purposes
        internal static WillEmitDefaultValue GetEquivalentEmitFor(bool b)
        => b ? WillEmitDefaultValue.Yes : WillEmitDefaultValue.No;

        // internal for testing purposes
        internal static IsMemberRequired GetEquivalentRequiredFor(bool b)
        => b ? IsMemberRequired.Yes : IsMemberRequired.No;

        /// <summary>
        /// Returns a representation of this SurrogateTypeDescriber object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        {
            var ret = new StringBuilder();

            ret.Append($"{nameof(SurrogateTypeDescriber)} using type describer {this.TypeDescriber}");
            if (ThrowOnNoRegisteredSurrogate)
            {
                ret.Append(" which throws when no surrogate registered");
            }
            else
            {
                ret.Append(" which delegates when no surrogate registered");
            }

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

        /// <summary>
        /// Delegates to TypeDescriber.
        /// </summary>
        public Parser? GetDynamicCellParserFor(in ReadContext ctx, TypeInfo targetType)
        => TypeDescriber.GetDynamicCellParserFor(in ctx, targetType);

        /// <summary>
        /// Delegates to TypeDescriber.
        /// </summary>
        public DynamicRowConverter? GetDynamicRowConverter(in ReadContext ctx, IEnumerable<ColumnIdentifier> columns, TypeInfo targetType)
        => TypeDescriber.GetDynamicRowConverter(in ctx, columns, targetType);

        /// <summary>
        /// Delegates to TypeDescriber.
        /// </summary>
        public IEnumerable<DynamicCellValue> GetCellsForDynamicRow(in WriteContext ctx, object row)
        => TypeDescriber.GetCellsForDynamicRow(in ctx, row);
    }
}
