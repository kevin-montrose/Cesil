using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.Serialization;

namespace Cesil
{
    internal static class Throw
    {
        private const string UNKNOWN_FILE = "<unknown file>";
        private const string UNKNOWN_MEMBER = "<unknown member>";

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ImpossibleException<V>(
            string message,
            IBoundConfiguration<V> config,
            [CallerFilePath]
            string? file = null,
            [CallerMemberName]
            string? member = null,
            [CallerLineNumber]
            int line = -1)
        => throw Cesil.ImpossibleException.Create(message, file ?? UNKNOWN_FILE, member ?? UNKNOWN_MEMBER, line, config);

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T ImpossibleException_Returns<T, V>(
            string message,
            IBoundConfiguration<V> config,
            [CallerFilePath]
            string? file = null,
            [CallerMemberName]
            string? member = null,
            [CallerLineNumber]
            int line = -1)
        => throw Cesil.ImpossibleException.Create(message, file ?? UNKNOWN_FILE, member ?? UNKNOWN_MEMBER, line, config);

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ImpossibleException(
            string message,
            Options options,
            [CallerFilePath]
            string? file = null,
            [CallerMemberName]
            string? member = null,
            [CallerLineNumber]
            int line = -1)
        => throw Cesil.ImpossibleException.Create(message, file ?? UNKNOWN_FILE, member ?? UNKNOWN_MEMBER, line, options);

        // prefer the other impossible throwers to this one
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ImpossibleException(
            string message,
            [CallerFilePath]
            string? file = null,
            [CallerMemberName]
            string? member = null,
            [CallerLineNumber]
            int line = -1)
        => throw Cesil.ImpossibleException.Create(message, file ?? UNKNOWN_FILE, member ?? UNKNOWN_MEMBER, line);

        // prefer the other impossible throwers to this one
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T ImpossibleException_Returns<T>(
            string message,
            [CallerFilePath]
            string? file = null,
            [CallerMemberName]
            string? member = null,
            [CallerLineNumber]
            int line = -1)
        => throw Cesil.ImpossibleException.Create(message, file ?? UNKNOWN_FILE, member ?? UNKNOWN_MEMBER, line);

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void InvalidOperationException(string message)
        => throw new InvalidOperationException(message);

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T InvalidOperationException_Returns<T>(string message)
        => throw new InvalidOperationException(message);

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void SerializationException(string message)
        => throw new SerializationException(message);

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ArgumentException(string message, string name)
        => throw new ArgumentException(message, name);

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ArgumentNullException(string name)
        => throw new ArgumentNullException(name);

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ObjectDisposedException(string typeName)
        => throw new ObjectDisposedException($"Instance of {typeName}");

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ArgumentOutOfRangeException(string paramName, int val, int upperExclusive)
        => throw new ArgumentOutOfRangeException(paramName, $"Expected between 0 and {upperExclusive}, was {val}");

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ArgumentOutOfRangeException(string paramName, Index ix, int effective, int upperExclusive)
        => throw new ArgumentOutOfRangeException(paramName, $"Expected Index between 0 and {upperExclusive}, was {effective} ({ix})");

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ArgumentOutOfRangeException(string paramName, Range r, int effectiveStart, int effectiveEnd, int upperExclusive)
        => throw new ArgumentOutOfRangeException(paramName, $"Expected range end points to be between 0 and {upperExclusive}, was {effectiveStart}..{effectiveEnd} ({r})");

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void KeyNotFoundException(string key)
        => throw new KeyNotFoundException($"Key was {key}");

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void NotSupportedException(string type, string method)
        => throw new NotSupportedException($"Method {method} on {type} is not supported");

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void OperationCanceledException()
        => throw new OperationCanceledException();

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void PoisonAndRethrow(PoisonableBase toPoison, Exception e)
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
        }

        [DoesNotReturn]
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
                msg = $"Failed to parse \"{new string(data)}\" using {parser}";
            }

            throw new SerializationException(msg);
        }
    }
}
