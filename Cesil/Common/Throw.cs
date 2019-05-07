using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Cesil
{
    internal static class Throw
    {
        internal static void Exception(string message)
        => throw new Exception(message);

        internal static void InvalidOperationException(string message)
        => throw new InvalidOperationException(message);

        internal static void SerializationException(string message)
        => throw new SerializationException(message);

        internal static void ArgumentException(string message, string name)
        => throw new ArgumentException(message, name);

        internal static void ArgumentNullException(string name)
        => throw new ArgumentNullException(name);

        internal static void ObjectDisposedException(string typeName)
        => throw new ObjectDisposedException($"Instance of {typeName}");

        internal static void ArgumentOutOfRangeException(string paramName, int val, int upperExclusive)
        {
            string msg;
            if(upperExclusive > 0)
            {
                msg = $"Expected between 0 and {upperExclusive}, was {val}";
            }
            else
            {
                msg = $"Was empty, index was {val}";
            }

            throw new ArgumentOutOfRangeException(paramName, msg);
        }

        internal static void KeyNotFoundException(string key)
        => throw new KeyNotFoundException($"Key was {key}");
    }
}
