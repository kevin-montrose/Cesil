using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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
        private const string CURRENT_CESIL_VERSION = "0.7.0";
        private const byte SERIALIZER_KIND = 1;

        /// <summary>
        /// Create a new AheadOfTimeTypeDescriber.
        /// 
        /// Note that a pre-allocated instance is also available on TypeDescribers.
        /// </summary>
        public AheadOfTimeTypeDescriber() { }

        /// <summary>
        /// Enumerate members which will be deserialized for the given type.
        /// 
        /// Note that these members are generated ahead of time with a source gneerator, 
        ///   and cannot be changed at runtime.
        /// </summary>
        public IEnumerable<DeserializableMember> EnumerateMembersToDeserialize(TypeInfo forType)
        // todo: implement!
        => Throw.NotImplementedException<IEnumerable<DeserializableMember>>("Not implemented yet");

        /// <summary>
        /// Returns an InstanceProvider that can be used to create new instance of the given type
        ///   while deserializing.
        ///   
        /// Note that this InstanceProvider is generated ahead of time with a source gneerator, 
        ///   and cannot be changed at runtime.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        public InstanceProvider? GetInstanceProvider(TypeInfo forType)
        // todo: implement!
        => Throw.NotImplementedException<InstanceProvider>("Not implemented yet");

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
            var colNames = (string[])colNamesField.GetValueNonNull(null);

            var ret = ImmutableArray.CreateBuilder<SerializableMember>(colNames.Length);

            for (var i = 0; i < colNames.Length; i++)
            {
                var name = colNames[i];

                var colWriterName = $"__Column_{i}";
                var colWriterMtd = paired.GetMethodNonNull(colWriterName, PublicStatic);

                var emitsDefaultValue = colWriterMtd.GetCustomAttribute<DoesNotEmitDefaultValueAttribute>() == null;

                var shouldSerializeName = $"__Column_{i}_ShouldSerialize";
                var shouldSerializeMtd = paired.GetMethod(shouldSerializeName, PublicStatic);
                var shouldSerialize = (ShouldSerialize?)shouldSerializeMtd;

                var getterName = $"__Column_{i}_Getter";
                var getterMtd = paired.GetMethodNonNull(getterName, PublicStatic);
                var getter = Getter.ForMethod(getterMtd);

                var formatterName = $"__Column_{i}_Formatter";
                var formatterMtd = paired.GetMethodNonNull(formatterName, PublicStatic);
                var formatter = Formatter.ForMethod(formatterMtd);

                ret.Add(SerializableMember.ForGeneratedMethod(name, colWriterMtd, getter, formatter, shouldSerialize, emitsDefaultValue));
            }

            return ret.ToImmutable();
        }

        private static TypeInfo? GetPairedType(Type forType, byte forMode)
        {
            var inAssembly = forType.Assembly;
            var candidateTypes = inAssembly.GetTypes();

            TypeInfo? pairedType = null;

            foreach (var tRaw in candidateTypes)
            {
                var t = tRaw.GetTypeInfo();

                var attrs = t.CustomAttributes;
                if (!attrs.Any())
                {
                    continue;
                }

                IList<CustomAttributeTypedArgument>? config = null;

                foreach (var attr in attrs)
                {
                    if (attr.AttributeType == Types.GeneratedSourceVersionAttribute)
                    {
                        config = attr.ConstructorArguments;
                        break;
                    }
                }

                if (config == null)
                {
                    continue;
                }

                if (config.Count != 3)
                {
                    return Throw.InvalidOperationException<TypeInfo>($"Found a generated type ({t}) with the incorrect number of attribute arguments, suggesting the generated source does not match the version of Cesil in use.");
                }

                var version = config[0];
                if (version.ArgumentType != Types.String || (version.Value as string) != CURRENT_CESIL_VERSION)
                {
                    return Throw.InvalidOperationException<TypeInfo>($"Found a generated type ({t}) with an unexpected version ({version.Value}), suggesting the generated source does not match the version of Cesil in use.");
                }

                var meantForType = config[1];
                if (meantForType.ArgumentType != Types.Type)
                {
                    return Throw.InvalidOperationException<TypeInfo>($"Found a generated type ({t}) with an unexpected second attribute parameter type ({meantForType}, expected System.Type), suggesting the generated source does not match the version of Cesil in use.");
                }

                var mode = config[2];
                if (mode.ArgumentType != Types.Byte)
                {
                    return Throw.InvalidOperationException<TypeInfo>($"Found a generated type ({t}) with an unexpected third attribute parameter type ({meantForType}, expected byte), suggesting the generated source does not match the version of Cesil in use.");
                }

                var candidateType = (meantForType.Value as Type)?.GetTypeInfo();
                if (candidateType != forType)
                {
                    continue;
                }

                var candidateMode = mode.Value as byte?;
                if (candidateMode != forMode)
                {
                    continue;
                }

                pairedType = t;
                break;
            }

            return pairedType;
        }

        /// <summary>
        /// This operation is not supported with AheadOfTimeTypeDescriber
        /// </summary>
        [return: IntentionallyExposedPrimitive("Count, int is the best option")]
        public int GetCellsForDynamicRow(in WriteContext context, object row, Span<DynamicCellValue> cells)
        => Throw.NotImplementedException<int>($"{nameof(GetCellsForDynamicRow)} is not supported when using {nameof(AheadOfTimeTypeDescriber)}");

        /// <summary>
        /// This operation is not supported with AheadOfTimeTypeDescriber
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        public Parser? GetDynamicCellParserFor(in ReadContext context, TypeInfo targetType)
        => Throw.NotImplementedException<Parser>($"{nameof(GetDynamicCellParserFor)} is not supported when using {nameof(AheadOfTimeTypeDescriber)}");

        /// <summary>
        /// This operation is not supported with AheadOfTimeTypeDescriber
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        public DynamicRowConverter? GetDynamicRowConverter(in ReadContext context, IEnumerable<ColumnIdentifier> columns, TypeInfo targetType)
        => Throw.NotImplementedException<DynamicRowConverter>($"{nameof(GetDynamicRowConverter)} is not supported when using {nameof(AheadOfTimeTypeDescriber)}");

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
