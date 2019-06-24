using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Cesil
{
    /// <summary>
    /// A ITypeDesciber that enumerates members on a surrogate type and maps them to another type.
    /// 
    /// Used when you don't control the type you need to (de)serialize - you markup the surrogate type
    ///   and then the uncontrolled type is (de)serialized as if it were the surrogate type.
    /// </summary>
    [NotEquatable("Mutable")]
    public sealed class SurrogateTypeDescriber : ITypeDescriber
    {
        internal ITypeDescriber TypeDescriber { get; }

        internal bool ThrowOnNoRegisteredSurrogate { get; }

        private readonly Dictionary<TypeInfo, TypeInfo> SurrogateTypes;

        /// <summary>
        /// Create a new SurrogateTypeDescriber.
        /// </summary>
        public SurrogateTypeDescriber(ITypeDescriber surrogateTypeDescriber, bool throwOnNoRegisteredSurrogate)
        {
            if (surrogateTypeDescriber == null)
            {
                Throw.ArgumentNullException(nameof(surrogateTypeDescriber));
            }

            SurrogateTypes = new Dictionary<TypeInfo, TypeInfo>();
            TypeDescriber = surrogateTypeDescriber;
            ThrowOnNoRegisteredSurrogate = throwOnNoRegisteredSurrogate;
        }

        /// <summary>
        /// Create a new SurrogateTypeDescriber, using the given ITypeDescriber.
        /// 
        /// Does not throw if no surrogate is registered for an enumerated type.
        /// </summary>
        public SurrogateTypeDescriber(ITypeDescriber proxiedTypeDescriber) : this(proxiedTypeDescriber, false) { }
        /// <summary>
        /// Create a new SurrogateTypeDescriber, using the given ITypeDescriber.
        /// 
        /// Uses TypeDescribers.Default as it's inner ITypeDescriber.
        /// </summary>
        public SurrogateTypeDescriber(bool throwOnNoConfiguredProxy) : this(TypeDescribers.Default, throwOnNoConfiguredProxy) { }

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
                Throw.ArgumentNullException(nameof(forType));
            }

            if (surrogateType == null)
            {
                Throw.ArgumentNullException(nameof(surrogateType));
            }

            if (forType == surrogateType)
            {
                Throw.InvalidOperationException($"Type {forType} cannot be a surrogate for itself");
            }

            if (!SurrogateTypes.TryAdd(forType, surrogateType))
            {
                Throw.InvalidOperationException($"Surrogate already registered for {forType}");
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
                    Throw.InvalidOperationException($"No surrogate registered for {forType}");
                }

                foreach (var member in TypeDescriber.EnumerateMembersToDeserialize(forType))
                {
                    yield return member;
                }

                yield break;
            }

            var fromProxy = TypeDescriber.EnumerateMembersToDeserialize(proxy);
            foreach (var member in fromProxy)
            {
                yield return Map(forType, member);
            }
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

                foreach (var member in TypeDescriber.EnumerateMembersToSerialize(forType))
                {
                    yield return member;
                }

                yield break;
            }

            var fromProxy = TypeDescriber.EnumerateMembersToSerialize(proxy);
            foreach (var member in fromProxy)
            {
                yield return Map(forType, member);
            }
        }

        /// <summary>
        /// Gets an instance builder usable to construct the given type.
        /// 
        /// If a surrogate is registered, the surrogate will be used for discovery - the returned 
        ///   constructor will be mapped from the surrogate to forType.
        ///   
        /// If a surrogate is not registered, either an exception will be thrown or forType will
        ///   be passed to TypeDescriber.GetInstanceBuilder depending on the value of
        ///   ThrowOnNoRegisteredSurrogate.
        /// </summary>
        public InstanceBuilder GetInstanceBuilder(TypeInfo forType)
        {
            if (!SurrogateTypes.TryGetValue(forType, out var proxy))
            {
                if (ThrowOnNoRegisteredSurrogate)
                {
                    Throw.InvalidOperationException($"No surrogate registered for {forType}");
                }

                return TypeDescriber.GetInstanceBuilder(forType);
            }

            var fromProxy = TypeDescriber.GetInstanceBuilder(forType);
            return Map(forType, fromProxy);
        }

        private static DeserializableMember Map(TypeInfo ontoType, DeserializableMember member)
        {
            MethodInfo resetOnType = null;
            if (member.Reset != null)
            {
                var surrogateResetWrapper = member.Reset;
                if (surrogateResetWrapper.Mode != BackingMode.Method)
                {
                    Throw.InvalidOperationException($"Cannot map reset {surrogateResetWrapper} onto {ontoType}, reset isn't backed by a method");
                }

                var surrogateReset = surrogateResetWrapper.Method;

                var surrogateResetBinding =
                    // explicitly ignoring DeclaredOnly; shadowing is fine
                    (surrogateReset.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic) |
                    (surrogateReset.IsStatic ? BindingFlags.Static : BindingFlags.Instance);

                resetOnType = ontoType.GetMethod(surrogateReset.Name, surrogateResetBinding);
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
                        var surrogateField = surrogateSetterWrapper.Field;
                        var surrogateFieldBinding =
                            // explicitly ignoring DeclaredOnly; shadowing is fine
                            (surrogateField.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic) |
                            (surrogateField.IsStatic ? BindingFlags.Static : BindingFlags.Instance);

                        var fieldOnType = ontoType.GetField(surrogateField.Name, surrogateFieldBinding);
                        if (fieldOnType == null)
                        {
                            Throw.InvalidOperationException($"No equivalent to {surrogateField} found on {ontoType}");
                        }

                        if (fieldOnType.FieldType != surrogateField.FieldType)
                        {
                            Throw.InvalidOperationException($"Field {fieldOnType} type ({fieldOnType.FieldType}) does not match surrogate field {surrogateField} type ({surrogateField.FieldType})");
                        }

                        var required = member.IsRequired ? IsMemberRequired.Yes : IsMemberRequired.No;

                        return DeserializableMember.Create(ontoType, member.Name, (Setter)fieldOnType, member.Parser, required, Reset.ForMethod(resetOnType));
                    }
                case BackingMode.Method:
                    {
                        var surrogateSetter = surrogateSetterWrapper.Method;

                        var surrogateSetterBinding =
                                // explicitly ignoring DeclaredOnly; shadowing is fine
                                (surrogateSetter.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic) |
                                (surrogateSetter.IsStatic ? BindingFlags.Static : BindingFlags.Instance);

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

                        var required = member.IsRequired ? IsMemberRequired.Yes : IsMemberRequired.No;

                        return DeserializableMember.Create(ontoType, member.Name, (Setter)setterOnType, member.Parser, required, (Reset)resetOnType);
                    }
                case BackingMode.Delegate:
                    Throw.InvalidOperationException($"Cannot map setter {surrogateSetterWrapper} onto {ontoType}, setter is backed by a delegate");
                    // just for control flow
                    return default;
                default:
                    Throw.InvalidOperationException($"Unexpected {nameof(BackingMode)}: {surrogateSetterWrapper.Mode}");
                    // just for control flow
                    return default;
            }
        }

        private static SerializableMember Map(TypeInfo ontoType, SerializableMember member)
        {
            ShouldSerialize shouldSerializeOnType;
            var surrogateShouldSerializeWrapper = member.ShouldSerialize;
            if (surrogateShouldSerializeWrapper != null)
            {
                if (surrogateShouldSerializeWrapper.Mode == BackingMode.Method)
                {
                    var surrogateShouldSerialize = surrogateShouldSerializeWrapper.Method;
                    var surrogateShouldSerializeBinding =
                        // explicitly ignoring DeclaredOnly; shadowing is fine
                        (surrogateShouldSerialize.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic) |
                        (surrogateShouldSerialize.IsStatic ? BindingFlags.Static : BindingFlags.Instance);

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
                    // just for flow control
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
                        var surrogateField = surrogateGetterWrapper.Field;
                        var surrogateFieldBinding =
                            // explicitly ignoring DeclaredOnly; shadowing is fine
                            (surrogateField.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic) |
                            (surrogateField.IsStatic ? BindingFlags.Static : BindingFlags.Instance);

                        var fieldOnType = ontoType.GetField(surrogateField.Name, surrogateFieldBinding);
                        if (fieldOnType == null)
                        {
                            Throw.InvalidOperationException($"No equivalent to {surrogateField} found on {ontoType}");
                        }

                        if (fieldOnType.FieldType != surrogateField.FieldType)
                        {
                            Throw.InvalidOperationException($"Field {fieldOnType} type ({fieldOnType.FieldType}) does not match surrogate field {surrogateField} type ({surrogateField.FieldType})");
                        }

                        var emitDefaultField = member.EmitDefaultValue ? WillEmitDefaultValue.Yes : WillEmitDefaultValue.No;
                        return SerializableMember.Create(ontoType, member.Name, (Getter)fieldOnType, member.Formatter, shouldSerializeOnType, emitDefaultField);
                    }
                case BackingMode.Delegate:
                    Throw.InvalidOperationException($"Cannot map getter {surrogateGetterWrapper} onto {ontoType}, getter isn't backed by a method");
                    // just for control flow
                    return default;
                case BackingMode.Method:
                    goto handleMethod;
                default:
                    Throw.InvalidOperationException($"Unexpected {nameof(BackingMode)}: {surrogateGetterWrapper.Mode}");
                    // just for control flow
                    return default;
            }

handleMethod:
            var surrogateGetter = surrogateGetterWrapper.Method;
            var surrogateGetterBinding =
                    // explicitly ignoring DeclaredOnly; shadowing is fine
                    (surrogateGetter.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic) |
                    (surrogateGetter.IsStatic ? BindingFlags.Static : BindingFlags.Instance);

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

            var emitDefault = member.EmitDefaultValue ? WillEmitDefaultValue.Yes : WillEmitDefaultValue.No;
            return SerializableMember.Create(ontoType, member.Name, (Getter)getterOnType, member.Formatter, shouldSerializeOnType, emitDefault);
        }

        private static InstanceBuilder Map(TypeInfo ontoType, InstanceBuilder builder)
        {
            switch (builder.Mode)
            {
                case BackingMode.Delegate:
                    Throw.InvalidOperationException($"Cannot map a delegate InstanceBuilder between types");
                    // just for control flow
                    return default;
                case BackingMode.Constructor:
                    {
                        var surrogateCons = builder.Constructor;
                        var surrogateConsBinding =
                                // explicitly ignoring DeclaredOnly; shadowing is fine
                                (surrogateCons.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic) |
                                BindingFlags.Instance;

                        var consOnType = ontoType.GetConstructor(surrogateConsBinding, null, Type.EmptyTypes, null);
                        if (consOnType == null)
                        {
                            Throw.InvalidOperationException($"No equivalent to {surrogateCons} found on {ontoType}");
                        }

                        return new InstanceBuilder(consOnType);
                    }
                case BackingMode.Method:
                    Throw.InvalidOperationException($"Cannot map a method InstanceBuilder between types");
                    // just for control flow
                    return default;
                default:
                    Throw.InvalidOperationException($"Unexpected {nameof(BackingMode)}: {builder.Mode}");
                    // just for control flow
                    return default;
            }
        }

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
        /// Delegates to DefaultTypeDescriber.
        /// </summary>
        public Parser GetDynamicCellParserFor(in ReadContext ctx, TypeInfo targetType)
        => TypeDescribers.Default.GetDynamicCellParserFor(in ctx, targetType);

        /// <summary>
        /// Delegates to DefaultTypeDescriber.
        /// </summary>
        public DynamicRowConverter GetDynamicRowConverter(in ReadContext ctx, IEnumerable<ColumnIdentifier> columns, TypeInfo targetType)
        => TypeDescribers.Default.GetDynamicRowConverter(in ctx, columns, targetType);

        /// <summary>
        /// Delegates to DefaultTypeDescriber.
        /// </summary>
        public IEnumerable<DynamicCellValue> GetCellsForDynamicRow(in WriteContext ctx, object row)
        => TypeDescribers.Default.GetCellsForDynamicRow(in ctx, row);
    }
}
