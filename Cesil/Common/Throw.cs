using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.Serialization;

namespace Cesil
{
    internal static class Throw
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T Exception<T>(string message)
        => throw new Exception(message);

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T InvalidOperationException<T>(string message)
        => throw new InvalidOperationException(message);

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T SerializationException<T>(string message)
        => throw new SerializationException(message);

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T ArgumentException<T>(string message, string name)
        => throw new ArgumentException(message, name);

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T ArgumentNullException<T>(string name)
        => throw new ArgumentNullException(name);

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T ObjectDisposedException<T>(string typeName)
        => throw new ObjectDisposedException($"Instance of {typeName}");

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T ArgumentOutOfRangeException<T>(string paramName, int val, int upperExclusive)
        => throw new ArgumentOutOfRangeException(paramName, $"Expected between 0 and {upperExclusive}, was {val}");


        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T ArgumentOutOfRangeException<T>(string paramName, Index ix, int effective, int upperExclusive)
        => throw new ArgumentOutOfRangeException(paramName, $"Expected Index between 0 and {upperExclusive}, was {effective} ({ix})");

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T ArgumentOutOfRangeException<T>(string paramName, Range r, int effectiveStart, int effectiveEnd, int upperExclusive)
        => throw new ArgumentOutOfRangeException(paramName, $"Expected range end points to be between 0 and {upperExclusive}, was {effectiveStart}..{effectiveEnd} ({r})");

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T KeyNotFoundException<T>(string key)
        => throw new KeyNotFoundException($"Key was {key}");

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T NotSupportedException<T>(string type, string method)
        => throw new NotSupportedException($"Method {method} on {type} is not supported");

        internal static T OperationCanceledException<T>()
        => throw new OperationCanceledException();

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T PoisonAndRethrow<T>(PoisonableBase toPoison, Exception e)
        {
            toPoison.SetPoison(e);

            // this preserves stack traces in a way `throw e` doesn't.
            //
            // in "normal" code we'd just re-throw with a naked `throw`, but
            //   we don't want any throws outside of this class for other reasons.
            //
            // automating poisoning is a nice bonus
            var wrapped = ExceptionDispatchInfo.Capture(e);
            wrapped.Throw();

            return default;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ParseFailed(Parser parser, in ReadContext ctx, ReadOnlySpan<char> data)
        {
            string msg;
            if (ctx.HasColumn)
            {
                msg = $"Failed to parse \"{new string(data)}\" for column index={ctx.Column} using {parser}";
            }
            else
            {
                msg = $"Failed to parse \"{new string(data)}\"using {parser}";
            }

            throw new SerializationException(msg);
        }
    }
}
