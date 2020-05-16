using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Cesil
{
    internal interface IDefaultTypeDescriberCache: IDelegateCache
    {
        bool TryGetSerializableMembers(TypeInfo t, [MaybeNullWhen(returnValue: false)]out IEnumerable<SerializableMember> members);

        bool TryGetDeserializableMembers(TypeInfo t, [MaybeNullWhen(returnValue: false)]out IEnumerable<DeserializableMember> members);

        bool TryGetFormatter(TypeInfo t, [MaybeNullWhen(returnValue: false)]out Formatter formatter);

        void AddSerializableMembers(TypeInfo t, IEnumerable<SerializableMember> members);

        void AddDeserializableMembers(TypeInfo t, IEnumerable<DeserializableMember> members);

        void AddFormatter(TypeInfo t, Formatter formatter);
    }
}
