using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Cesil
{
    // holds all the "cache-y" bits of the DefaultTypeDescriber
    //
    // these are actually members (instead of a separate instance)
    //   so that consumers can control the cache by chosing an
    //   instance of the DefaultTypeDescriber to use.
    public partial class DefaultTypeDescriber : IDelegateCache
    {
        private readonly bool CanCache;

        private readonly ConcurrentDictionary<TypeInfo, IEnumerable<DeserializableMember>> DeserializableMembers;
        private readonly ConcurrentDictionary<TypeInfo, IEnumerable<SerializableMember>> SerializableMembers;
        private readonly ConcurrentDictionary<TypeInfo, Formatter> Formatters;

        private readonly ConcurrentDictionary<object, Delegate> DelegateCache;

        void IDelegateCache.AddDelegate<T, V>(T key, V cached)
        => DelegateCache.TryAdd(key, cached);

        bool IDelegateCache.TryGetDelegate<T, V>(T key, [MaybeNullWhen(returnValue: false)] out V del)
        {
            if (!DelegateCache.TryGetValue(key, out var cached))
            {
                del = default;
                return false;
            }

            del = (V)cached;
            return true;
        }

        private bool TryGetSerializableMembers(TypeInfo t, [MaybeNullWhen(returnValue: false)] out IEnumerable<SerializableMember> members)
        => SerializableMembers.TryGetValue(t, out members);

        private bool TryGetDeserializableMembers(TypeInfo t, [MaybeNullWhen(returnValue: false)] out IEnumerable<DeserializableMember> members)
        => DeserializableMembers.TryGetValue(t, out members);

        private bool TryGetFormatter(TypeInfo t, [MaybeNullWhen(returnValue: false)] out Formatter formatter)
        => Formatters.TryGetValue(t, out formatter);

        private void AddSerializableMembers(TypeInfo t, IEnumerable<SerializableMember> members)
        => SerializableMembers.TryAdd(t, members);

        private void AddDeserializableMembers(TypeInfo t, IEnumerable<DeserializableMember> members)
        => DeserializableMembers.TryAdd(t, members);

        private void AddFormatter(TypeInfo t, Formatter formatter)
        => Formatters.TryAdd(t, formatter);

        /// <summary>
        /// Clears any internal caches this instance has created.
        /// 
        /// Caches may be used to accelerate member lookup or dynamic
        ///   operations.
        ///   
        /// Clearing caches is not necessary for correct functioning, 
        ///   but may be useful to manage memory use.
        /// </summary>
        public void ClearCache()
        {
            DeserializableMembers.Clear();
            SerializableMembers.Clear();
            Formatters.Clear();
            DelegateCache.Clear();
        }
    }
}
