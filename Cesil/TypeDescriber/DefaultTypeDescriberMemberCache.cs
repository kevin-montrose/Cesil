using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Cesil
{
    // todo: combine this and DefaultTypeDescriberDelegateCache
    internal sealed class DefaultTypeDescriberMemberCache
    {
        internal static readonly DefaultTypeDescriberMemberCache Instance = new DefaultTypeDescriberMemberCache();

        private readonly ConcurrentDictionary<TypeInfo, IEnumerable<DeserializableMember>> DeserializableMembers;
        private readonly ConcurrentDictionary<TypeInfo, IEnumerable<SerializableMember>> SerializableMembers;
        private readonly ConcurrentDictionary<TypeInfo, Formatter> Formatters;

        private DefaultTypeDescriberMemberCache()
        {
            DeserializableMembers = new ConcurrentDictionary<TypeInfo, IEnumerable<DeserializableMember>>();
            SerializableMembers = new ConcurrentDictionary<TypeInfo, IEnumerable<SerializableMember>>();
            Formatters = new ConcurrentDictionary<TypeInfo, Formatter>();
        }

        internal bool TryGetSerializableMembers(TypeInfo t, [MaybeNullWhen(returnValue: false)]out IEnumerable<SerializableMember> members)
        => SerializableMembers.TryGetValue(t, out members);

        internal bool TryGetDeserializableMembers(TypeInfo t, [MaybeNullWhen(returnValue: false)]out IEnumerable<DeserializableMember> members)
        => DeserializableMembers.TryGetValue(t, out members);

        internal bool TryGetFormatter(TypeInfo t, [MaybeNullWhen(returnValue: false)]out Formatter formatter)
        => Formatters.TryGetValue(t, out formatter);

        internal void Add(TypeInfo t, IEnumerable<SerializableMember> members)
        => SerializableMembers.TryAdd(t, members);

        internal void Add(TypeInfo t, IEnumerable<DeserializableMember> members)
        => DeserializableMembers.TryAdd(t, members);

        internal void Add(TypeInfo t, Formatter formatter)
        => Formatters.TryAdd(t, formatter);
    }
}
