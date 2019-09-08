using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace Cesil
{
    // todo: add a test that all Throw methods are NoInlining

    internal static class Throw
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Exception(string message)
        => throw new Exception(message);

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void InvalidOperationException(string message)
        => throw new InvalidOperationException(message);

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void SerializationException(string message)
        => throw new SerializationException(message);

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ArgumentException(string message, string name)
        => throw new ArgumentException(message, name);

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ArgumentNullException(string name)
        => throw new ArgumentNullException(name);

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ObjectDisposedException(string typeName)
        => throw new ObjectDisposedException($"Instance of {typeName}");

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ArgumentOutOfRangeException(string paramName, int val, int upperExclusive)
        => throw new ArgumentOutOfRangeException(paramName, $"Expected between 0 and {upperExclusive}, was {val}");


        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ArgumentOutOfRangeException(string paramName, Index ix, int effective, int upperExclusive)
        => throw new ArgumentOutOfRangeException(paramName, $"Expected Index between 0 and {upperExclusive}, was {effective} ({ix})");

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ArgumentOutOfRangeException(string paramName, Range r, int effectiveStart, int effectiveEnd, int upperExclusive)
        => throw new ArgumentOutOfRangeException(paramName, $"Expected range end points to be between 0 and {upperExclusive}, was {effectiveStart}..{effectiveEnd} ({r})");

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void KeyNotFoundException(string key)
        => throw new KeyNotFoundException($"Key was {key}");

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void NotSupportedException(string type, string method)
        => throw new NotSupportedException($"Method {method} on {type} is not supported");
    }
}
