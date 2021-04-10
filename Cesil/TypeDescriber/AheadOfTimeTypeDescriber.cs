using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

using static Cesil.BindingFlagsConstants;

namespace Cesil
{
    /// <summary>
    /// An ITypeDescriber that use (de)serializer that was generated ahead of time
    ///   by using Cesil.SourceGenerator.
    /// </summary>
    public sealed class AheadOfTimeTypeDescriber : ITypeDescriber, IEquatable<AheadOfTimeTypeDescriber>
    {
        internal const string CURRENT_CESIL_VERSION = "0.9.0";
#pragma warning disable CS0618 // Obsolete to prevent clients from using them directly, but fine for us
        private const GeneratedSourceVersionAttribute.GeneratedTypeKind SERIALIZER_KIND = GeneratedSourceVersionAttribute.GeneratedTypeKind.Serializer;
        private const GeneratedSourceVersionAttribute.GeneratedTypeKind DESERIALIZER_KIND = GeneratedSourceVersionAttribute.GeneratedTypeKind.Deserializer;
#pragma warning restore CS0618

        /// <summary>
        /// Create a new AheadOfTimeTypeDescriber.
        /// 
        /// Note that a pre-allocated instance is also available on TypeDescribers.
        /// </summary>
        public AheadOfTimeTypeDescriber() { }

        /// <summary>
        /// Returns an InstanceProvider that can be used to create new instance of the given type
        ///   while deserializing.
        ///   
        /// Note that this InstanceProvider is generated ahead of time with a source gneerator, 
        ///   and cannot be changed at runtime.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        public InstanceProvider? GetInstanceProvider(TypeInfo forType)
        {
            var paired = GetPairedType(forType, DESERIALIZER_KIND);
            if (paired == null)
            {
                return null;
            }

            var instanceMtd = paired.GetMethodNonNull("__InstanceProvider", PublicStatic);

#pragma warning disable CS0618 // This obsolete to prevent clients from using them, but they are fine for us.
            var forConstructorAttrs = instanceMtd.GetCustomAttributes<ConstructorInstanceProviderAttribute>();
#pragma warning restore CS0618

            if (forConstructorAttrs.Any())
            {
                var consOnTypes = forConstructorAttrs.Select(x => x.ForType).Distinct().ToImmutableArray();
                if (consOnTypes.Length > 1)
                {
                    Throw.ImpossibleException($"Generated type {paired} (for {forType}) claims multiple constructors for an InstanceProvider.");
                }

                var consOnType = consOnTypes.Single();
                var consParams = forConstructorAttrs.OrderBy(x => x.ParameterIndex).Select(i => i.ParameterType).ToArray();

                var cons = consOnType.GetConstructor(AllInstance, null, consParams, null);
                if (cons == null)
                {
                    Throw.ImpossibleException($"Generated type {paired} (for {forType}) claims a constructor for an InstanceProvider that could not be found.");
                }

                return InstanceProvider.ForConstructorWithParametersInner(cons, paired);
            }

            return InstanceProvider.ForMethodInner(instanceMtd, paired);
        }

        /// <summary>
        /// Enumerate members which will be deserialized for the given type.
        /// 
        /// Note that these members are generated ahead of time with a source gneerator, 
        ///   and cannot be changed at runtime.
        /// </summary>
        public IEnumerable<DeserializableMember> EnumerateMembersToDeserialize(TypeInfo forType)
        {
            var paired = GetPairedType(forType, DESERIALIZER_KIND);
            if (paired == null)
            {
                return Enumerable.Empty<DeserializableMember>();
            }

            var colNamesProp = paired.GetPropertyNonNull("__ColumnNames", PublicStatic);
            var colNames = (ImmutableArray<string>)colNamesProp.GetValueNonNull(null);

            var ret = ImmutableArray.CreateBuilder<DeserializableMember>(colNames.Length);

            ParameterInfo[]? consPs = null;

            for (var i = 0; i < colNames.Length; i++)
            {
                var name = colNames[i];

                var colReaderName = $"__Column_{i}";
                var colReaderMtd = paired.GetMethodNonNull(colReaderName, PublicInstance);

#pragma warning disable CS0618 // These are obsolete to prevent clients from using them, but they are fine for us.
                var isRequired = colReaderMtd.GetCustomAttribute<IsRequiredAttribute>() != null;
                var setterBackedByParameter = colReaderMtd.GetCustomAttribute<SetterBackedByConstructorParameterAttribute>();
                var setterIsInitOnly = colReaderMtd.GetCustomAttribute<SetterBackedByInitOnlyPropertyAttribute>();
#pragma warning restore CS0618

                Setter setter;
                if (setterBackedByParameter == null && setterIsInitOnly == null)
                {
                    // directly a method
                    var setterName = $"__Column_{i}_Setter";
                    var setterMtd = paired.GetMethodNonNull(setterName, PublicStatic);
                    setter = Setter.ForMethod(setterMtd);
                }
                else if (setterBackedByParameter != null)
                {
                    // parameter to constructor
                    if (consPs == null)
                    {
                        var ip = GetInstanceProvider(forType);
                        ip = Utils.NonNull(ip);

                        var cons = ip.Constructor.Value;
                        consPs = cons.GetParameters();
                    }

                    var consParameterIndex = setterBackedByParameter.Index;
                    if (consParameterIndex < 0 || consParameterIndex >= consPs.Length)
                    {
                        Throw.ImpossibleException($"Setter for column {i} claims to be backed by constructor parameter, but its position is out of bounds (index={consParameterIndex})");
                    }

                    var p = consPs[setterBackedByParameter.Index];
                    setter = Setter.ForConstructorParameter(p);
                }
                else
                {
                    // init only property
                    var initOnly = Utils.NonNull(setterIsInitOnly);

                    var prop = forType.GetProperty(initOnly.PropertyName, initOnly.BindingFlags);
                    if (prop == null)
                    {
                        Throw.ImpossibleException($"Setter for column {i} claims to be backed by init-only property {initOnly.PropertyName} with bindings ({initOnly.BindingFlags}), but it could not be found");
                    }

                    setter = Setter.ForProperty(prop);
                }

                var resetMethodName = $"__Column_{i}_Reset";
                var resetMtd = paired.GetMethod(resetMethodName, PublicStatic);
                var reset = (Reset?)resetMtd;

                var parserMethodName = $"__Column_{i}_Parser";
                var parserMtd = paired.GetMethod(parserMethodName, PublicStatic);
                Parser? parser;
                if (parserMtd != null)
                {
                    parser = Parser.ForMethod(parserMtd);
                }
                else
                {
                    parser = Utils.NonNull(Parser.GetDefault(setter.Takes));
                }

                ret.Add(DeserializableMember.CreateInner(forType, name, setter, parser, isRequired ? MemberRequired.Yes : MemberRequired.No, reset, paired));
            }

            return ret.ToImmutable();
        }

        /// <summary>
        /// Enumerate members which will be serialized for the given type.
        /// 
        /// Note that these members are generated ahead of time with a source gneerator, 
        ///   and cannot be changed at runtime.
        /// </summary>
        public IEnumerable<SerializableMember> EnumerateMembersToSerialize(TypeInfo forType)
        {
            var paired = GetPairedType(forType, SERIALIZER_KIND);
            if (paired == null)
            {
                return Enumerable.Empty<SerializableMember>();
            }

            var colNamesField = paired.GetFieldNonNull("ColumnNames", PublicStatic);
            var colNames = (ImmutableArray<string>)colNamesField.GetValueNonNull(null);

            var ret = ImmutableArray.CreateBuilder<SerializableMember>(colNames.Length);

            for (var i = 0; i < colNames.Length; i++)
            {
                var name = colNames[i];

                var colWriterName = $"__Column_{i}";
                var colWriterMtd = paired.GetMethodNonNull(colWriterName, PublicStatic);

#pragma warning disable CS0618 // This is obsolete to prevent clients from using them, but they are fine for us.
                var emitsDefaultValue = colWriterMtd.GetCustomAttribute<DoesNotEmitDefaultValueAttribute>() == null;
#pragma warning restore CS0618

                var shouldSerializeName = $"__Column_{i}_ShouldSerialize";
                var shouldSerializeMtd = paired.GetMethod(shouldSerializeName, PublicStatic);
                var shouldSerialize = (ShouldSerialize?)shouldSerializeMtd;

                var getterName = $"__Column_{i}_Getter";
                var getterMtd = paired.GetMethodNonNull(getterName, PublicStatic);
                var getter = Getter.ForMethod(getterMtd);

                var formatterName = $"__Column_{i}_Formatter";
                var formatterMtd = paired.GetMethod(formatterName, PublicStatic);
                Formatter formatter;
                if (formatterMtd == null)
                {
                    // if a method isn't provided, it must be using the default
                    formatter = Utils.NonNull(Formatter.GetDefault(getter.Returns));
                }
                else
                {
                    formatter = Formatter.ForMethod(formatterMtd);
                }


                ret.Add(SerializableMember.ForGeneratedMethod(name, colWriterMtd, getter, formatter, shouldSerialize, emitsDefaultValue));
            }

            return ret.ToImmutable();
        }

        private static TypeInfo? GetPairedType(
            Type forType,
#pragma warning disable CS0618 // This is obsolete to prevent clients from using them, but they are fine for us.
            GeneratedSourceVersionAttribute.GeneratedTypeKind forMode
#pragma warning restore CS0618
        )
        {
            var inAssembly = forType.Assembly;
            var candidateTypes = inAssembly.GetTypes();

            TypeInfo? ret = null;

            foreach (var tRaw in candidateTypes)
            {
                var t = tRaw.GetTypeInfo();

#pragma warning disable CS0618 // This is obsolete to prevent clients from using them, but they are fine for us.
                var attr = t.GetCustomAttribute<GeneratedSourceVersionAttribute>();
#pragma warning restore CS0618

                if (attr is not object)
                {
                    continue;
                }

                var meantForType = attr.ForType.GetTypeInfo();
                if (meantForType != forType)
                {
                    continue;
                }

                var candidateMode = attr.Kind;
                if (candidateMode != forMode)
                {
                    continue;
                }

                var version = attr.Version;
                if (version != CURRENT_CESIL_VERSION)
                {
                    Throw.ImpossibleException($"Found a generated type ({t}) with an unexpected version ({version}), suggesting the generated source does not match the version ({CURRENT_CESIL_VERSION}) of Cesil in use.");
                }

                if (ret != null)
                {
                    Throw.ImpossibleException($"Found multiple generated types for {forType}");
                }

                ret = t;
            }

            return ret;
        }

        /// <summary>
        /// This operation is not supported with AheadOfTimeTypeDescriber
        /// </summary>
        [return: IntentionallyExposedPrimitive("Count, int is the best option")]
        public int GetCellsForDynamicRow(in WriteContext context, object row, Span<DynamicCellValue> cells)
        {
            Throw.NotSupportedException(nameof(AheadOfTimeTypeDescriber), nameof(GetCellsForDynamicRow));
            return default;
        }

        /// <summary>
        /// This operation is not supported with AheadOfTimeTypeDescriber
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        public Parser? GetDynamicCellParserFor(in ReadContext context, TypeInfo targetType)
        {
            Throw.NotSupportedException(nameof(AheadOfTimeTypeDescriber), nameof(GetDynamicCellParserFor));
            return default;
        }

        /// <summary>
        /// This operation is not supported with AheadOfTimeTypeDescriber
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        public DynamicRowConverter? GetDynamicRowConverter(in ReadContext context, IEnumerable<ColumnIdentifier> columns, TypeInfo targetType)
        {
            Throw.NotSupportedException(nameof(AheadOfTimeTypeDescriber), nameof(GetDynamicRowConverter));
            return default;
        }

        /// <summary>
        /// Returns true if this and the given AheadOfTimeTypeDescribers are equal
        /// </summary>
        public bool Equals(AheadOfTimeTypeDescriber? typeDescriber)
        => !ReferenceEquals(typeDescriber, null);

        /// <summary>
        /// Returns true if this AheadOfTimeTypeDescriber equals the given object
        /// </summary>
        public override bool Equals(object? obj)
        => Equals(obj as AheadOfTimeTypeDescriber);

        /// <summary>
        /// Returns a hash code for this AheadOfTimeTypeDescriber
        /// </summary>
        public override int GetHashCode()
        => 0;

        /// <summary>
        /// Returns a representation of this AheadOfTimeTypeDescriber object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        => $"{nameof(AheadOfTimeTypeDescriber)} instance";

        /// <summary>
        /// Compare two AheadOfTimeTypeDescribers for equality
        /// </summary>
        public static bool operator ==(AheadOfTimeTypeDescriber? a, AheadOfTimeTypeDescriber? b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two AheadOfTimeTypeDescribers for inequality
        /// </summary>
        public static bool operator !=(AheadOfTimeTypeDescriber? a, AheadOfTimeTypeDescriber? b)
        => !(a == b);
    }
}
