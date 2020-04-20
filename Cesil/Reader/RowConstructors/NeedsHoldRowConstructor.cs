using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Cesil
{
    internal sealed class NeedsHoldRowConstructor<TRow, THold> :
        IRowConstructor<TRow>
        where THold : class, new()
    {
        public IEnumerable<string> Columns
        {
            get
            {
                for (var i = 0; i < MemberLookup.Length; i++)
                {
                    var t = LookupColumn(i);

                    if (t.SimpleMember != null)
                    {
                        yield return SimpleMembers[t.SimpleMember.Value].Name;
                        continue;
                    }

                    if (t.HeldMember != null)
                    {
                        yield return HeldMembers[t.HeldMember.Value].Name;
                        continue;
                    }

                    yield return "--UNKNOWN--";
                }
            }
        }

        private THold Hold;
        private readonly ClearHoldDelegate<THold> ClearHold;

        private readonly GetInstanceGivenHoldDelegate<TRow, THold> GetInstance;

        // use the single allocation MemberLookup if we don't re-order columns,
        //   or the MemberLookupOverride which is backed by the MemoryPool if we do
        private readonly ImmutableArray<(int? SimpleMember, int? HeldMember)> MemberLookup;
        private UnmanagedLookupArray<(int? SimpleMember, int? HeldMember)>? MemberLookupOverride;

        private readonly ImmutableArray<(string Name, MemberRequired Required, ParseAndSetOnDelegate<THold> ParseAndHold, MoveFromHoldToRowDelegate<TRow, THold> MoveToRow, ParseAndSetOnDelegate<TRow> SetOnRow)> SimpleMembers;
        private readonly ImmutableArray<(string Name, MemberRequired Required, ParseAndSetOnDelegate<THold> ParseAndHold)> HeldMembers;

        public bool IsDisposed => RequiredTracker.IsDisposed;

        private readonly MemoryPool<char> MemoryPool;

        private readonly bool HasRequired;
        private readonly RequiredSet RequiredTracker;

        private bool _RowStarted;
        public bool RowStarted => _RowStarted;

        private bool HasSimple;
        private UnmanagedLookupArray<ShallowReadContext> SimpleSet;

        private bool CurrentPopulated;
        private TRow Current;
        private int HeldCount;

        private NeedsHoldRowConstructor(
            MemoryPool<char> pool,
            ClearHoldDelegate<THold> clear,
            GetInstanceGivenHoldDelegate<TRow, THold> getInstance,
            ImmutableDictionary<int, (string Name, MemberRequired Required, ParseAndSetOnDelegate<THold> ParseAndHold, MoveFromHoldToRowDelegate<TRow, THold> MoveToRow, ParseAndSetOnDelegate<TRow> SetOnRow)> simple,
            ImmutableDictionary<int, (string Name, MemberRequired Required, ParseAndSetOnDelegate<THold> ParseAndHold)> held
        )
        {
            MemoryPool = pool;
            ClearHold = clear;

            GetInstance = getInstance;

            Hold = new THold();
            ClearHold = clear;

            CurrentPopulated = false;
            _RowStarted = false;
            Current = default!;

            var lookupArr = ImmutableArray.CreateBuilder<(int? SimpleMember, int? HeldMember)>();
            var simpleArr = ImmutableArray.CreateBuilder<(string Name, MemberRequired Required, ParseAndSetOnDelegate<THold> ParseAndHold, MoveFromHoldToRowDelegate<TRow, THold> MoveToRow, ParseAndSetOnDelegate<TRow> SetOnRow)>();
            var heldArr = ImmutableArray.CreateBuilder<(string Name, MemberRequired Required, ParseAndSetOnDelegate<THold> ParseAndHold)>();

            var max = simple.Keys.Concat(held.Keys).Max();

            // we won't actually use this (we'll always clone it first) so
            //    just keep track of whether we'll _need_ one
            RequiredTracker = default;

            for (var i = 0; i <= max; i++)
            {
                if (simple.TryGetValue(i, out var simpleVal))
                {
                    simpleArr.Add(simpleVal);
                    lookupArr.Add((simpleArr.Count - 1, null));

                    if (simpleVal.Required == MemberRequired.Yes)
                    {
                        HasRequired = true;
                    }

                    continue;
                }

                if (held.TryGetValue(i, out var heldVal))
                {
                    heldArr.Add(heldVal);
                    lookupArr.Add((null, heldArr.Count - 1));

                    if (heldVal.Required == MemberRequired.Yes)
                    {
                        HasRequired = true;
                    }

                    continue;
                }

                lookupArr.Add((null, null));
            }

            MemberLookup = lookupArr.ToImmutable();
            SimpleMembers = simpleArr.ToImmutable();
            HeldMembers = heldArr.ToImmutable();

            HeldCount = 0;

            MemberLookupOverride = null;

            HasSimple = SimpleMembers.Any();
        }

        private NeedsHoldRowConstructor(
            MemoryPool<char> pool,
            ClearHoldDelegate<THold> clear,
            GetInstanceGivenHoldDelegate<TRow, THold> getInstance,
            ImmutableArray<(int? SimpleMember, int? HeldMember)> lookup,
            ImmutableArray<(string Name, MemberRequired Required, ParseAndSetOnDelegate<THold> ParseAndHold, MoveFromHoldToRowDelegate<TRow, THold> MoveToRow, ParseAndSetOnDelegate<TRow> SetOnRow)> simple,
            ImmutableArray<(string Name, MemberRequired Required, ParseAndSetOnDelegate<THold> ParseAndHold)> held,
            bool hasRequired,
            RequiredSet requiredTracker,
            bool hasSimple,
            UnmanagedLookupArray<ShallowReadContext> simpleSetTracker
        )
        {
            MemoryPool = pool;
            ClearHold = clear;

            GetInstance = getInstance;


            Hold = new THold();

            CurrentPopulated = false;
            _RowStarted = false;
            Current = default!;

            HasRequired = hasRequired;
            RequiredTracker = requiredTracker;

            SimpleSet = simpleSetTracker;

            MemberLookup = lookup;
            SimpleMembers = simple;
            HeldMembers = held;

            HeldCount = 0;

            MemberLookupOverride = null;

            HasSimple = hasSimple;
            SimpleSet = simpleSetTracker;
        }

        public IRowConstructor<TRow> Clone()
        {
            RequiredSet tracker = default;
            if (HasRequired)
            {
                tracker = new RequiredSet(MemoryPool, MemberLookup.Length);

                for (var i = 0; i < MemberLookup.Length; i++)
                {
                    var l = LookupColumn(i);

                    if (l.SimpleMember != null)
                    {
                        var config = SimpleMembers[l.SimpleMember.Value];
                        if (config.Required == MemberRequired.Yes)
                        {
                            tracker.SetIsRequired(i);
                        }

                        continue;
                    }

                    if (l.HeldMember != null)
                    {
                        var config = HeldMembers[l.HeldMember.Value];
                        if (config.Required == MemberRequired.Yes)
                        {
                            tracker.SetIsRequired(i);
                        }

                        continue;
                    }
                }
            }

            UnmanagedLookupArray<ShallowReadContext> simpleTracker = default;
            if (HasSimple)
            {
                simpleTracker = new UnmanagedLookupArray<ShallowReadContext>(MemoryPool, SimpleMembers.Length);
            }

            return new NeedsHoldRowConstructor<TRow, THold>(MemoryPool, ClearHold, GetInstance, MemberLookup, SimpleMembers, HeldMembers, HasRequired, tracker, HasSimple, simpleTracker);
        }

        internal static NeedsHoldRowConstructor<TRow, THold> Create(
            MemoryPool<char> pool,
            Delegate clearHold,
            Delegate constructFromHold,
            ImmutableDictionary<int, (string Name, MemberRequired Required, Delegate ParseAndHold, Delegate MoveToRow, Delegate SetOnRow)> simple,
            ImmutableDictionary<int, (string Name, MemberRequired Required, Delegate ParseAndHold)> holdType
        )
        {
            var clear = (ClearHoldDelegate<THold>)clearHold;
            var getInstance = (GetInstanceGivenHoldDelegate<TRow, THold>)constructFromHold;

            var simpleMembersBuilder = ImmutableDictionary.CreateBuilder<int, (string Name, MemberRequired Required, ParseAndSetOnDelegate<THold> ParseAndHold, MoveFromHoldToRowDelegate<TRow, THold> MoveToRow, ParseAndSetOnDelegate<TRow> SetOnRow)>();
            foreach (var kv in simple)
            {
                simpleMembersBuilder.Add(
                    kv.Key,
                    (
                        kv.Value.Name,
                        kv.Value.Required,
                        (ParseAndSetOnDelegate<THold>)kv.Value.ParseAndHold,
                        (MoveFromHoldToRowDelegate<TRow, THold>)kv.Value.MoveToRow,
                        (ParseAndSetOnDelegate<TRow>)kv.Value.SetOnRow
                    )
                );
            }
            var simpleMembers = simpleMembersBuilder.ToImmutable();

            var holdMembersBuilder = ImmutableDictionary.CreateBuilder<int, (string Name, MemberRequired Required, ParseAndSetOnDelegate<THold> ParseAndHold)>();
            foreach (var kv in holdType)
            {
                holdMembersBuilder.Add(
                    kv.Key,
                    (
                        kv.Value.Name,
                        kv.Value.Required,
                        (ParseAndSetOnDelegate<THold>)kv.Value.ParseAndHold
                    )
                );
            }
            var holdMembers = holdMembersBuilder.ToImmutable();

            return new NeedsHoldRowConstructor<TRow, THold>(pool, clear, getInstance, simpleMembers, holdMembers);
        }

        public void SetColumnOrder(HeadersReader<TRow>.HeaderEnumerator columns)
        {
            // took ownership, have to dispose
            using (columns)
            {
                var lookupOverride = new UnmanagedLookupArray<(int? SimpleMember, int? HeldMember)>(MemoryPool, columns.Count);

                RequiredTracker.ClearRequired();

                var ix = 0;
                while (columns.MoveNext())
                {
                    var ci = columns.Current;

                    var found = false;

                    for (var simpleIx = 0; simpleIx < SimpleMembers.Length; simpleIx++)
                    {
                        var simple = SimpleMembers[simpleIx];
                        if (Utils.AreEqual(ci, simple.Name.AsMemory()))
                        {
                            lookupOverride.Set(ix, (simpleIx, null));

                            if (simple.Required == MemberRequired.Yes)
                            {
                                RequiredTracker.SetIsRequired(ix);
                            }

                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        for (var heldIx = 0; heldIx < HeldMembers.Length; heldIx++)
                        {
                            var held = HeldMembers[heldIx];
                            if (Utils.AreEqual(ci, held.Name.AsMemory()))
                            {
                                lookupOverride.Set(ix, (null, heldIx));

                                if (held.Required == MemberRequired.Yes)
                                {
                                    RequiredTracker.SetIsRequired(ix);
                                }

                                found = true;
                                break;
                            }
                        }
                    }

                    if (!found)
                    {
                        lookupOverride.Set(ix, (null, null));
                    }

                    ix++;
                }

                MemberLookupOverride = lookupOverride;
            }
        }

        public bool TryPreAllocate(in ReadContext ctx, ref TRow prealloced)
        {
            prealloced = default!;
            return false;
        }

        public void StartRow(in ReadContext ctx)
        {
            if (RowStarted)
            {
                Throw.ImpossibleException<object>("Row already started");
            }

            _RowStarted = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private (int? SimpleMember, int? HeldMember) LookupColumn(int colNumber)
        {
            if (MemberLookupOverride != null)
            {
                MemberLookupOverride.Value.Get(colNumber, default, out var m);
                return m;
            }

            return MemberLookup[colNumber];
        }

        public void ColumnAvailable(Options options, int rowNumber, int columnNumber, object? context, in ReadOnlySpan<char> data)
        {
            if (!_RowStarted)
            {
                Throw.ImpossibleException<object>("Row hasn't been started, column is unexpected");
                return;
            }

            if (columnNumber >= MemberLookup.Length)
            {
                Throw.InvalidOperationException<object>($"Unexpected column (Index={columnNumber})");
                return;
            }

            var res = LookupColumn(columnNumber);
            if (res.SimpleMember != null)
            {
                var simpleCol = SimpleMembers[res.SimpleMember.Value];

                if (simpleCol.Required == MemberRequired.Yes)
                {
                    RequiredTracker.MarkSet(columnNumber);
                }

                var ci = ColumnIdentifier.CreateInner(columnNumber, simpleCol.Name);
                var ctx = ReadContext.ReadingColumn(options, rowNumber, ci, context);

                if (simpleCol.Required == MemberRequired.Yes && data.Length == 0)
                {
                    Throw.SerializationException<object>($"Column [{ctx.Column}] is required, but was not found in row");
                    return;
                }

                if (CurrentPopulated)
                {
                    simpleCol.SetOnRow(ref Current, in ctx, data);
                }
                else
                {
                    simpleCol.ParseAndHold(ref Hold, in ctx, data);
                    SimpleSet.Add(new ShallowReadContext(in ctx));
                }

                return;
            }
            else if (res.HeldMember != null)
            {
                var holdConfig = HeldMembers[res.HeldMember.Value];

                if (holdConfig.Required == MemberRequired.Yes)
                {
                    RequiredTracker.MarkSet(columnNumber);
                }

                var ci = ColumnIdentifier.CreateInner(columnNumber, holdConfig.Name);
                var ctx = ReadContext.ReadingColumn(options, rowNumber, ci, context);

                if (holdConfig.Required == MemberRequired.Yes && data.Length == 0)
                {
                    Throw.SerializationException<object>($"Column [{ctx.Column}] is required, but was not found in row");
                    return;
                }

                holdConfig.ParseAndHold(ref Hold, in ctx, data);

                HeldCount++;

                if (HeldCount == HeldMembers.Length)
                {
                    CurrentPopulated = true;
                    Current = GetInstance(Hold);

                    if (HasSimple)
                    {
                        for (var i = 0; i < SimpleSet.Count; i++)
                        {
                            SimpleSet.Get(i, default, out var shallowCtx);

                            if (shallowCtx.Mode != ReadContextMode.ReadingColumn)
                            {
                                Throw.ImpossibleException<object>($"{nameof(ShallowReadContext)} wasn't for {ReadContextMode.ReadingColumn}, which was not expected");
                                return;
                            }

                            var lookup = LookupColumn(shallowCtx.ColumnIndex);
                            if (lookup.SimpleMember == null)
                            {
                                Throw.ImpossibleException<object>($"Column [{shallowCtx.ColumnIndex}] recorded as a previously set simple column, but could not be found when creating row");
                                return;
                            }

                            var simple = SimpleMembers[lookup.SimpleMember.Value];

                            var realCi = ColumnIdentifier.CreateInner(shallowCtx.ColumnIndex, simple.Name);

                            var realCtx = ReadContext.ReadingColumn(options, shallowCtx.RowNumber, realCi, context);

                            var moveDelegate = simple.MoveToRow;
                            moveDelegate(ref Current, Hold, in realCtx);
                        }
                    }
                }

                return;
            }
        }

        public TRow FinishRow()
        {
            if (!CurrentPopulated)
            {
                if (RowStarted)
                {
                    return Throw.SerializationException<TRow>($"Current row has started, but insufficient columns have been parsed to create the row");
                }

                return Throw.ImpossibleException<TRow>($"No current row available, shouldn't be trying to finish a row");
            }

            if (!RequiredTracker.CheckRequiredAndClear(out var missingIx))
            {
                var lookup = LookupColumn(missingIx);
                if (lookup.SimpleMember != null)
                {
                    var simpleDetails = SimpleMembers[lookup.SimpleMember.Value];
                    return Throw.SerializationException<TRow>($"Column [{simpleDetails.Name}] is required, but was not found in row");
                }
                else
                {
                    // held isn't actually possible, because of the RowStarted check above
                    return Throw.ImpossibleException<TRow>($"Column in position {missingIx} was required and missing, but couldn't find a member to match it to.  This shouldn't happen.");
                }
            }

            var ret = Current;
            Current = default!;
            CurrentPopulated = false;
            _RowStarted = false;
            HeldCount = 0;

            ClearHold(Hold);

            if (HasSimple)
            {
                SimpleSet.Clear();
            }

            return ret;
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                RequiredTracker.Dispose();
                SimpleSet.Dispose();

                MemberLookupOverride?.Dispose();
                MemberLookupOverride = null;
            }
        }
    }
}
