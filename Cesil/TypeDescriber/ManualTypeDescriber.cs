using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Cesil
{
    /// <summary>
    /// An ITypeDescriber that takes lets you register explicit members to return
    ///   when one of the EnumerateXXX() methods are called.
    /// </summary>
    public sealed class ManualTypeDescriber : ITypeDescriber, IEquatable<ManualTypeDescriber>
    {
        internal readonly bool ThrowsOnNoConfiguredType;

        internal readonly ITypeDescriber Fallback;

        internal readonly ImmutableDictionary<TypeInfo, InstanceProvider> Builders;

        internal readonly ImmutableDictionary<TypeInfo, ImmutableArray<SerializableMember>> Serializers;
        internal readonly ImmutableDictionary<TypeInfo, ImmutableArray<DeserializableMember>> Deserializers;

        internal ManualTypeDescriber(
            bool throwsOnNonConfiguredType,
            ITypeDescriber fallback,
            ImmutableDictionary<TypeInfo, InstanceProvider> builders,
            ImmutableDictionary<TypeInfo, ImmutableArray<SerializableMember>> serializers,
            ImmutableDictionary<TypeInfo, ImmutableArray<DeserializableMember>> deserializers
        )
        {
            ThrowsOnNoConfiguredType = throwsOnNonConfiguredType;
            Fallback = fallback;
            Builders = builders;
            Serializers = serializers;
            Deserializers = deserializers;
        }

        // create builders

        /// <summary>
        /// Create a new ManualTypeDescriberBuilder which fallbacks to TypeDescribers.Default when a type with
        ///    no registered members is requested.
        /// </summary>
        public static ManualTypeDescriberBuilder CreateBuilder()
        => ManualTypeDescriberBuilder.CreateBuilder();

        /// <summary>
        /// Create a new empty ManualTypeDescriberBuilder with the given fallback behavior, and a fallback ITypeDescriber of TypeDescribers.Default.
        /// </summary>
        public static ManualTypeDescriberBuilder CreateBuilder(ManualTypeDescriberFallbackBehavior fallbackBehavior)
        => ManualTypeDescriberBuilder.CreateBuilder(fallbackBehavior);

        /// <summary>
        /// Create a new empty ManualTypeDescriberBuilder with the given fallback behavior.
        /// </summary>
        public static ManualTypeDescriberBuilder CreateBuilder(ManualTypeDescriberFallbackBehavior fallbackBehavior, ITypeDescriber fallbackTypeDescriber)
        => ManualTypeDescriberBuilder.CreateBuilder(fallbackBehavior, fallbackTypeDescriber);

        /// <summary>
        /// Create a new ManualTypeDescriberBuilder that copies it's
        ///   initial values from the given ManualTypeDescriber.
        /// </summary>
        public static ManualTypeDescriberBuilder CreateBuilder(ManualTypeDescriber typeDescriber)
        => ManualTypeDescriberBuilder.CreateBuilder(typeDescriber);

        // ITypeDescriber methods

        /// <summary>
        /// Returns the registered InstanceProvider for the given TypeInfo.
        /// 
        /// If no provider has been registered, will either delegate to a fallback
        ///    ITypeProvider or throw an exception depending on configuration.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        public InstanceProvider? GetInstanceProvider(TypeInfo forType)
        {
            if (!Builders.TryGetValue(forType, out var ret))
            {
                if (ThrowsOnNoConfiguredType)
                {
                    return Throw.InvalidOperationException<InstanceProvider?>($"No configured instance provider for {forType}");
                }

                return Fallback.GetInstanceProvider(forType);
            }

            return ret;
        }

        /// <summary>
        /// Enumerate all the members on forType to deserialize.
        /// 
        /// If no members has been registered, will either delegate to a fallback
        ///    ITypeProvider or throw an exception depending on configuration.
        /// </summary>
        public IEnumerable<DeserializableMember> EnumerateMembersToDeserialize(TypeInfo forType)
        {
            if (!Deserializers.TryGetValue(forType, out var ret))
            {
                if (ThrowsOnNoConfiguredType)
                {
                    return Throw.InvalidOperationException<IEnumerable<DeserializableMember>>($"No configured members to deserialize for {forType}");
                }

                return Fallback.EnumerateMembersToDeserialize(forType);
            }

            return ret;
        }

        /// <summary>
        /// Enumerate all the members on forType to serialize.
        /// 
        /// If no members has been registered, will either delegate to a fallback
        ///    ITypeProvider or throw an exception depending on configuration.
        /// </summary>
        public IEnumerable<SerializableMember> EnumerateMembersToSerialize(TypeInfo forType)
        {
            if (!Serializers.TryGetValue(forType, out var ret))
            {
                if (ThrowsOnNoConfiguredType)
                {
                    return Throw.InvalidOperationException<IEnumerable<SerializableMember>>($"No configured members to serialize for {forType}");
                }

                return Fallback.EnumerateMembersToSerialize(forType);
            }

            return ret;
        }

        /// <summary>
        /// Returns a representation of this ManualTypeDescriber object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        {
            var ret = new StringBuilder();

            ret.Append($"{nameof(ManualTypeDescriber)}");

            if (ThrowsOnNoConfiguredType)
            {
                ret.Append(" which throws on unconfigured types");
            }
            else
            {
                ret.Append($" which delegates to ({Fallback}) on unconfigured types");
            }

            if (Builders.Any())
            {
                ret.Append(" and builds (");

                var isFirst = true;
                foreach (var build in Builders)
                {
                    if (!isFirst)
                    {
                        ret.Append(", ");
                    }

                    isFirst = false;
                    ret.Append(build);
                }
                ret.Append(")");
            }

            if (Deserializers.Any())
            {
                var firstType = true;
                ret.Append(" and reads (");
                foreach (var kv in Deserializers)
                {
                    if (!firstType)
                    {
                        ret.Append(", ");
                    }

                    firstType = false;
                    ret.Append($"for type {kv.Key} (");

                    var firstMember = true;
                    foreach (var mem in kv.Value)
                    {
                        if (!firstMember)
                        {
                            ret.Append(", ");
                        }

                        firstMember = false;
                        ret.Append(mem);
                    }
                    ret.Append(")");
                }

                ret.Append(")");
            }

            if (Serializers.Any())
            {
                var firstType = true;
                ret.Append(" and writes (");
                foreach (var kv in Serializers)
                {
                    if (!firstType)
                    {
                        ret.Append(", ");
                    }

                    firstType = false;
                    ret.Append($"for type {kv.Key} (");

                    var firstMember = true;
                    foreach (var mem in kv.Value)
                    {
                        if (!firstMember)
                        {
                            ret.Append(", ");
                        }

                        firstMember = false;
                        ret.Append(mem);
                    }
                    ret.Append(")");
                }

                ret.Append(")");
            }

            return ret.ToString();
        }

        /// <summary>
        /// Operation not supported, raises exception if used.
        /// </summary>
        public Parser GetDynamicCellParserFor(in ReadContext context, TypeInfo targetType)
        => Throw.NotSupportedException<Parser>(nameof(ManualTypeDescriber), nameof(GetDynamicCellParserFor));

        /// <summary>
        /// Operation not supported, raises exception if used.
        /// </summary>
        public DynamicRowConverter GetDynamicRowConverter(in ReadContext context, IEnumerable<ColumnIdentifier> columns, TypeInfo targetType)
        => Throw.NotSupportedException<DynamicRowConverter>(nameof(ManualTypeDescriber), nameof(GetDynamicRowConverter));

        /// <summary>
        /// Operation not supported, raises exception if used.
        /// </summary>
        [return: IntentionallyExposedPrimitive("Count, int is the best option")]
        public int GetCellsForDynamicRow(in WriteContext context, object row, Span<DynamicCellValue> cells)
        => Throw.NotSupportedException<int>(nameof(ManualTypeDescriber), nameof(GetCellsForDynamicRow));

        // equatable

        /// <summary>
        /// Returns true if this ManualTypeDescriber equals the given object
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is ManualTypeDescriber m)
            {
                return Equals(m);
            }

            return false;
        }

        /// <summary>
        /// Returns a stable hash for this ManualTypeDescriber.
        /// </summary>
        public override int GetHashCode()
        // can't include builders/members directly, because order isn't stable between logically equivalent describers
        => HashCode.Combine(Fallback, ThrowsOnNoConfiguredType, Builders.Count, Deserializers.Count, Serializers.Count);

        /// <summary>
        /// Returns true if this ManualTypeDescriber equals the given ManualTypeDescriber.
        /// </summary>
        public bool Equals(ManualTypeDescriber? typeDescriber)
        {
            if (ReferenceEquals(typeDescriber, null)) return false;

            if (!ReferenceEquals(typeDescriber.Fallback, Fallback)) return false;

            if (typeDescriber.ThrowsOnNoConfiguredType != ThrowsOnNoConfiguredType) return false;

            if (typeDescriber.Builders.Count != Builders.Count) return false;
            if (typeDescriber.Deserializers.Count != Deserializers.Count) return false;
            if (typeDescriber.Serializers.Count != Serializers.Count) return false;

            foreach (var b in Builders)
            {
                if (!typeDescriber.Builders.TryGetValue(b.Key, out var other)) return false;

                if (!b.Value.Equals(other)) return false;
            }

            foreach (var d in Deserializers)
            {
                if (!typeDescriber.Deserializers.TryGetValue(d.Key, out var other)) return false;

                if (d.Value.Length != other.Length) return false;

                foreach (var v in d.Value)
                {
                    if (!other.Contains(v)) return false;
                }
            }

            foreach (var s in Serializers)
            {
                if (!typeDescriber.Serializers.TryGetValue(s.Key, out var other)) return false;

                if (s.Value.Length != other.Length) return false;

                foreach (var v in s.Value)
                {
                    if (!other.Contains(v)) return false;
                }
            }

            return true;
        }

        // operators

        /// <summary>
        /// Compare two ManualTypeDescriber for equality
        /// </summary>
        public static bool operator ==(ManualTypeDescriber? a, ManualTypeDescriber? b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two ManualTypeDescriber for inequality
        /// </summary>
        public static bool operator !=(ManualTypeDescriber? a, ManualTypeDescriber? b)
        => !(a == b);
    }
}