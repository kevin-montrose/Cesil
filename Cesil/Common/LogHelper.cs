using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Cesil
{
    // todo: test that all non-private members of LogHelper are conditional?

    // use this pattern so debug statements can be disabled based on inner consts
    [ExcludeFromCoverage("Conditionally enabled logging, no need for code coverage")]
    internal static class LogHelper
    {
        private const string DEBUG_SYMBOL = "DEBUG";
        private const string NEVER_SYMBOL = "NEVER_INCLUDED_COMPILATION_SYMBOL_NO_REALLY_DO_NOT_DEFINE_THIS_YOU_WILL_REGRET_IT_INSERT_SIM_CITY_PNG";

        // consts
        private const bool STATE_TRANSITION = false;
        private const bool TRACKED_MEMORY_OWNER = false;
        private const bool NAME_LOOKUP = false;

        // NameLookup: log events related

        [Conditional(NAME_LOOKUP ? DEBUG_SYMBOL : NEVER_SYMBOL)]
        internal static void NameLookup_OrderedNames(NameLookup.OrdererNames sortedNames, [CallerMemberName] string? caller = null)
        {
            caller = SetCaller(caller);

            var vals = new List<string>();
            for (var i = 0; i < sortedNames.Count; i++)
            {
                var name = new string(sortedNames[i].Name.Span);
                vals.Add(name);
            }

            Debug.WriteLine(
                $"{caller}: ordered names ({string.Join(", ", vals.Select(x => '"' + x + '"'))})"
            );
        }

        [Conditional(NAME_LOOKUP ? DEBUG_SYMBOL : NEVER_SYMBOL)]
        internal static void NameLookup_StorePrefixGroups(int depth, ushort startOfPrefixGroup, ReadOnlySpan<char> name, int prefixLen, [CallerMemberName] string? caller = null)
        {
            caller = SetCaller(caller);

            Debug.WriteLine(
                $"{caller}: depth={depth}, start={startOfPrefixGroup}, prefix=\"{new string(name.Slice(0, prefixLen))}\""
            );
        }

        [Conditional(NAME_LOOKUP ? DEBUG_SYMBOL : NEVER_SYMBOL)]
        internal static void NameLookup_Indexes(int depth, ushort newFirstNamesIx, ushort newLastNamesIx, [CallerMemberName] string? caller = null)
        {
            Debug.WriteLine($"{caller}: depth={depth}, start:{newFirstNamesIx}, last:{newLastNamesIx}");
        }

        // State transition log events

        [Conditional(STATE_TRANSITION ? DEBUG_SYMBOL : NEVER_SYMBOL)]
        internal static void StateTransition_NewHeadersReader([CallerMemberName] string? caller = null)
        {
            caller = SetCaller(caller);

            Debug.WriteLine($"{caller}: New {nameof(HeadersReader<object>)}");
        }

        [Conditional(STATE_TRANSITION ? DEBUG_SYMBOL : NEVER_SYMBOL)]
        internal static void StateTransition_ChangeState(ReaderStateMachine.State from, char c, ReaderStateMachine.State to, ReaderStateMachine.AdvanceResult res, [CallerMemberName] string? caller = null)
        {
            caller = SetCaller(caller);

            Debug.WriteLine($"{caller}: {from} + {c} => {to} & {res}");
        }

        // TrackedMemoryPool (defined in Cesil.Tests) log events

        [Conditional(TRACKED_MEMORY_OWNER ? DEBUG_SYMBOL : NEVER_SYMBOL)]
        internal static void TrackedMemoryOwner_New(int poolId, [CallerMemberName] string? caller = null)
        {
            caller = SetCaller(caller);
            Debug.WriteLine($"{caller}: Initializing PoolId={poolId}");
        }

        [Conditional(TRACKED_MEMORY_OWNER ? DEBUG_SYMBOL : NEVER_SYMBOL)]
        internal static void TrackedMemoryOwner_Rent(int id, [CallerMemberName] string? caller = null)
        {
            caller = SetCaller(caller);
            Debug.WriteLine($"\t{caller}: Rented {id}");
        }

        [Conditional(TRACKED_MEMORY_OWNER ? DEBUG_SYMBOL : NEVER_SYMBOL)]
        internal static void TrackedMemoryOwner_Freed(int id, [CallerMemberName] string? caller = null)
        {
            caller = SetCaller(caller);
            Debug.WriteLine($"\t{caller}: Freed {id}");
        }

        // helpers

        private static string SetCaller(string? caller)
        => caller ?? "<UNKNOWN>";
    }
}
