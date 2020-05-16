using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Cesil
{
    internal sealed class DefaultTypeDescriberCache : IDefaultTypeDescriberCache
    {
        // todo: move this to actually be in the DefaultTypeDescriber
        //       so consumers can avoid bloating or using it by changing
        //       the ITypeDescriber they use
        internal static readonly IDefaultTypeDescriberCache Instance = new DefaultTypeDescriberCache();

        private readonly ConcurrentDictionary<TypeInfo, IEnumerable<DeserializableMember>> DeserializableMembers;
        private readonly ConcurrentDictionary<TypeInfo, IEnumerable<SerializableMember>> SerializableMembers;
        private readonly ConcurrentDictionary<TypeInfo, Formatter> Formatters;

        private readonly ConcurrentDictionary<object, Delegate> DelegateCache;

        private DefaultTypeDescriberCache()
        {
            DelegateCache = new ConcurrentDictionary<object, Delegate>();
            DeserializableMembers = new ConcurrentDictionary<TypeInfo, IEnumerable<DeserializableMember>>();
            SerializableMembers = new ConcurrentDictionary<TypeInfo, IEnumerable<SerializableMember>>();
            Formatters = new ConcurrentDictionary<TypeInfo, Formatter>();
        }

        void IDelegateCache.AddDelegate<T, V>(T key, V cached)
        => DelegateCache.TryAdd(key, cached);

        bool IDelegateCache.TryGetDelegate<T, V>(T key, [MaybeNullWhen(returnValue: false)]out V del)
        {
            if (!DelegateCache.TryGetValue(key, out var cached))
            {
                del = default;
                return false;
            }

            del = (V)cached;
            return true;
        }

        bool IDefaultTypeDescriberCache.TryGetSerializableMembers(TypeInfo t, [MaybeNullWhen(returnValue: false)]out IEnumerable<SerializableMember> members)
        => SerializableMembers.TryGetValue(t, out members);

        bool IDefaultTypeDescriberCache.TryGetDeserializableMembers(TypeInfo t, [MaybeNullWhen(returnValue: false)]out IEnumerable<DeserializableMember> members)
        => DeserializableMembers.TryGetValue(t, out members);

        bool IDefaultTypeDescriberCache.TryGetFormatter(TypeInfo t, [MaybeNullWhen(returnValue: false)]out Formatter formatter)
        => Formatters.TryGetValue(t, out formatter);

        void IDefaultTypeDescriberCache.AddSerializableMembers(TypeInfo t, IEnumerable<SerializableMember> members)
        => SerializableMembers.TryAdd(t, members);

        void IDefaultTypeDescriberCache.AddDeserializableMembers(TypeInfo t, IEnumerable<DeserializableMember> members)
        => DeserializableMembers.TryAdd(t, members);

        void IDefaultTypeDescriberCache.AddFormatter(TypeInfo t, Formatter formatter)
        => Formatters.TryAdd(t, formatter);
    }
}
