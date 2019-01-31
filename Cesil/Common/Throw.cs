using System;
using System.Runtime.Serialization;

namespace Cesil
{
    internal static class Throw
    {
        internal static void Exception(string message)
        => throw new Exception(message);

        internal static void InvalidOperation(string message)
        => throw new InvalidOperationException(message);

        internal static void SerializationException(string message)
        => throw new SerializationException(message);

        internal static void ArgumentException(string message, string name)
        => throw new ArgumentException(message, name);

        internal static void ArgumentNullException(string name)
        => throw new ArgumentNullException(name);

        internal static void ObjectDisposed(string typeName)
        => throw new ObjectDisposedException($"Instance of {typeName}");
    }
}
