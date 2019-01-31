using System.Collections.Generic;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// A ITypeDesciber that enumerates members on a surrogate type and maps them to another type.
    /// 
    /// Used when you don't control the type you need to (de)serialize - you markup the surrogate type
    ///   and then the uncontrolled type is (de)serialized as if it were the surrogate type.
    /// </summary>
    public sealed class SurrogateTypeDescriber : ITypeDescriber
    {
        /// <summary>
        /// The type describer to use when enumerating on a surrogate type.
        /// </summary>
        public ITypeDescriber TypeDescriber { get; }
        /// <summary>
        /// Whether to throw when no surrogate type is registered for a type
        ///   that is being described.
        ///   
        /// If false, types with no registered surrogate types are described
        ///   by TypeDescriber.
        /// </summary>
        public bool ThrowOnNoRegisteredSurrogate { get; }

        private readonly Dictionary<TypeInfo, TypeInfo> SurrogateTypes;

        /// <summary>
        /// Create a new SurrogateTypeDescriber.
        /// </summary>
        public SurrogateTypeDescriber(ITypeDescriber surrogateTypeDescriber, bool throwOnNoRegisteredSurrogate)
        {
            if(surrogateTypeDescriber == null)
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
            if(forType == null)
            {
                Throw.ArgumentNullException(nameof(forType));
            }

            if (surrogateType == null)
            {
                Throw.ArgumentNullException(nameof(surrogateType));
            }

            if(forType == surrogateType)
            {
                Throw.InvalidOperation($"Type {forType} cannot be a surrogate for itself");
            }

            if (!SurrogateTypes.TryAdd(forType, surrogateType))
            {
                Throw.InvalidOperation($"Surrogate already registered for {forType}");
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
            if(!SurrogateTypes.TryGetValue(forType, out var proxy))
            {
                if (ThrowOnNoRegisteredSurrogate)
                {
                    Throw.InvalidOperation($"No surrogate registered for {forType}");
                }

                foreach(var member in TypeDescriber.EnumerateMembersToDeserialize(forType))
                {
                    yield return member;
                }

                yield break;
            }

            var fromProxy = TypeDescriber.EnumerateMembersToDeserialize(proxy);
            foreach(var member in fromProxy)
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
                    Throw.InvalidOperation($"No surrogate registered for {forType}");
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

        private static DeserializableMember Map(TypeInfo ontoType, DeserializableMember member)
        {
            if(member.Field != null)
            {
                var surrogateField = member.Field;
                var surrogateFieldBinding =
                    // explicitly ignoring DeclaredOnly; shadowing is fine
                    (surrogateField.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic) |
                    (surrogateField.IsStatic ? BindingFlags.Static : BindingFlags.Instance);

                var fieldOnType = ontoType.GetField(surrogateField.Name, surrogateFieldBinding);
                if(fieldOnType == null)
                {
                    Throw.InvalidOperation($"No equivalent to {surrogateField} found on {ontoType}");
                }

                if(fieldOnType.FieldType != surrogateField.FieldType)
                {
                    Throw.InvalidOperation($"Field {fieldOnType} type ({fieldOnType.FieldType}) does not match surrogate field {surrogateField} type ({surrogateField.FieldType})");
                }

                return DeserializableMember.Create(member.Name, fieldOnType, member.Parser, member.IsRequired);
            }

            var surrogateSetter = member.Setter;
            var surrogateSetterBinding =
                    // explicitly ignoring DeclaredOnly; shadowing is fine
                    (surrogateSetter.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic) |
                    (surrogateSetter.IsStatic ? BindingFlags.Static : BindingFlags.Instance);

            var setterOnType = ontoType.GetMethod(surrogateSetter.Name, surrogateSetterBinding);
            if(setterOnType == null)
            {
                Throw.InvalidOperation($"No equivalent to {surrogateSetter} found on {ontoType}");
            }

            var paramsOnType = setterOnType.GetParameters();
            var paramsOnSurrogate = surrogateSetter.GetParameters();

            if(paramsOnType.Length != paramsOnSurrogate.Length)
            {
                Throw.InvalidOperation($"Parameters for {setterOnType} do not match parameters for {surrogateSetter}");
            }

            for(var i = 0; i < paramsOnType.Length; i++)
            {
                var pOnType = paramsOnType[i];
                var pOnSurrogate = paramsOnSurrogate[i];

                if(pOnType.ParameterType != pOnSurrogate.ParameterType)
                {
                    Throw.InvalidOperation($"Parameter #{(i + 1)} on {setterOnType} does not match same parameter on {surrogateSetter}");
                }
            }

            return DeserializableMember.Create(member.Name, setterOnType, member.Parser, member.IsRequired);
        }

        private static SerializableMember Map(TypeInfo ontoType, SerializableMember member)
        {
            MethodInfo shouldSerializeOnType;
            if (member.ShouldSerialize != null)
            {
                var surrogateShouldSerialize = member.ShouldSerialize;
                var surrogateShouldSerializeBinding =
                    // explicitly ignoring DeclaredOnly; shadowing is fine
                    (surrogateShouldSerialize.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic) |
                    (surrogateShouldSerialize.IsStatic ? BindingFlags.Static : BindingFlags.Instance);

                shouldSerializeOnType = ontoType.GetMethod(surrogateShouldSerialize.Name, surrogateShouldSerializeBinding);
                if (shouldSerializeOnType == null)
                {
                    Throw.InvalidOperation($"No equivalent to {surrogateShouldSerialize} found on {ontoType}");
                }
            }
            else
            {
                shouldSerializeOnType = null;
            }


            if (member.Field != null)
            {
                var surrogateField = member.Field;
                var surrogateFieldBinding =
                    // explicitly ignoring DeclaredOnly; shadowing is fine
                    (surrogateField.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic) |
                    (surrogateField.IsStatic ? BindingFlags.Static : BindingFlags.Instance);

                var fieldOnType = ontoType.GetField(surrogateField.Name, surrogateFieldBinding);
                if (fieldOnType == null)
                {
                    Throw.InvalidOperation($"No equivalent to {surrogateField} found on {ontoType}");
                }

                if (fieldOnType.FieldType != surrogateField.FieldType)
                {
                    Throw.InvalidOperation($"Field {fieldOnType} type ({fieldOnType.FieldType}) does not match surrogate field {surrogateField} type ({surrogateField.FieldType})");
                }

                return SerializableMember.Create(ontoType, member.Name, fieldOnType, member.Formatter, shouldSerializeOnType, member.EmitDefaultValue);
            }

            var surrogateGetter = member.Getter;
            var surrogateGetterBinding =
                    // explicitly ignoring DeclaredOnly; shadowing is fine
                    (surrogateGetter.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic) |
                    (surrogateGetter.IsStatic ? BindingFlags.Static : BindingFlags.Instance);

            var getterOnType = ontoType.GetMethod(surrogateGetter.Name, surrogateGetterBinding);
            if(getterOnType == null)
            {
                Throw.InvalidOperation($"No equivalent to {surrogateGetter} found on {ontoType}");
            }

            var surrogateParams = surrogateGetter.GetParameters();
            var onTypeParams = getterOnType.GetParameters();

            if(surrogateParams.Length != onTypeParams.Length)
            {
                Throw.InvalidOperation($"Parameters for {getterOnType} do not match parameters for {surrogateGetter}");
            }

            for(var i = 0; i < surrogateParams.Length; i++)
            {
                var sP = surrogateParams[i].ParameterType.GetTypeInfo();
                var tP = onTypeParams[i].ParameterType.GetTypeInfo();

                if(sP != tP)
                {
                    Throw.InvalidOperation($"Parameter #{(i + 1)} on {getterOnType} does not match same parameter on {surrogateGetter}");
                }
            }

            return SerializableMember.Create(ontoType, member.Name, getterOnType, member.Formatter, shouldSerializeOnType, member.EmitDefaultValue);
        }
    }
}
