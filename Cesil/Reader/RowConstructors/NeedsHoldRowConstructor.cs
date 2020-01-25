using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

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
                foreach (var t in MemberLookup)
                {
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

        private readonly THold Hold;
        private readonly ClearHoldDelegate<THold> ClearHold;

        private readonly GetInstanceGivenHoldDelegate<TRow, THold> GetInstance;

        private ImmutableArray<(int? SimpleMember, int? HeldMember)> MemberLookup;
        private ImmutableArray<(string Name, MemberRequired Required, ParseAndSetOnDelegate<THold> ParseAndHold, MoveFromHoldToRowDelegate<TRow, THold> MoveToRow, ParseAndSetOnDelegate<TRow> SetOnRow)> SimpleMembers;
        private ImmutableArray<(string Name, MemberRequired Required, ParseAndSetOnDelegate<THold> ParseAndHold)> HeldMembers;

        public bool IsDisposed => RequiredTracker.IsDisposed;

        private RequiredSet RequiredTracker;

        private bool _RowStarted;
        public bool RowStarted => _RowStarted;

        private NonNull<ImmutableArray<ReadContext>.Builder> SimpleSet;
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
            ClearHold = clear;

            GetInstance = getInstance;

            if (simple.Any())
            {
                SimpleSet.Value = ImmutableArray.CreateBuilder<ReadContext>();
            }
            else
            {
                SimpleSet.Clear();
            }

            Hold = new THold();
            ClearHold = clear;

            CurrentPopulated = false;
            _RowStarted = false;
            Current = default!;

            var lookupArr = ImmutableArray.CreateBuilder<(int? SimpleMember, int? HeldMember)>();
            var simpleArr = ImmutableArray.CreateBuilder<(string Name, MemberRequired Required, ParseAndSetOnDelegate<THold> ParseAndHold, MoveFromHoldToRowDelegate<TRow, THold> MoveToRow, ParseAndSetOnDelegate<TRow> SetOnRow)>();
            var heldArr = ImmutableArray.CreateBuilder<(string Name, MemberRequired Required, ParseAndSetOnDelegate<THold> ParseAndHold)>();

            var max = simple.Keys.Concat(held.Keys).Max();

            RequiredTracker = new RequiredSet(pool, max + 1);

            for (var i = 0; i <= max; i++)
            {
                if (simple.TryGetValue(i, out var simpleVal))
                {
                    simpleArr.Add(simpleVal);
                    lookupArr.Add((simpleArr.Count - 1, null));

                    if (simpleVal.Required == MemberRequired.Yes)
                    {
                        RequiredTracker.SetIsRequired(i);
                    }

                    continue;
                }

                if (held.TryGetValue(i, out var heldVal))
                {
                    heldArr.Add(heldVal);
                    lookupArr.Add((null, heldArr.Count - 1));

                    if (heldVal.Required == MemberRequired.Yes)
                    {
                        RequiredTracker.SetIsRequired(i);
                    }

                    continue;
                }

                lookupArr.Add((null, null));
            }

            MemberLookup = lookupArr.ToImmutable();
            SimpleMembers = simpleArr.ToImmutable();
            HeldMembers = heldArr.ToImmutable();

            HeldCount = 0;
        }

        public static NeedsHoldRowConstructor<TRow, THold> Create(
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
                var lookupArr = ImmutableArray.CreateBuilder<(int? SimpleMember, int? HeldMember)>();
                var simpleArr = ImmutableArray.CreateBuilder<(string Name, MemberRequired Required, ParseAndSetOnDelegate<THold> ParseAndHold, MoveFromHoldToRowDelegate<TRow, THold> MoveToRow, ParseAndSetOnDelegate<TRow> SetOnRow)>();
                var heldArr = ImmutableArray.CreateBuilder<(string Name, MemberRequired Required, ParseAndSetOnDelegate<THold> ParseAndHold)>();

                RequiredTracker.ClearRequired();

                var ix = 0;
                while (columns.MoveNext())
                {
                    var ci = columns.Current;

                    var found = false;

                    foreach (var simple in SimpleMembers)
                    {
                        if (Utils.AreEqual(ci, simple.Name.AsMemory()))
                        {
                            simpleArr.Add(simple);
                            lookupArr.Add((simpleArr.Count - 1, null));

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
                        foreach (var held in HeldMembers)
                        {
                            if (Utils.AreEqual(ci, held.Name.AsMemory()))
                            {
                                heldArr.Add(held);
                                lookupArr.Add((null, heldArr.Count - 1));

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
                        lookupArr.Add((null, null));
                    }

                    ix++;
                }

                MemberLookup = lookupArr.ToImmutable();
                SimpleMembers = simpleArr.ToImmutable();
                HeldMembers = heldArr.ToImmutable();
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
                Throw.Exception<object>("Row already started");
            }

            _RowStarted = true;
        }

        public void ColumnAvailable(Options options, int rowNumber, int columnNumber, object? context, in ReadOnlySpan<char> data)
        {
            if (!_RowStarted)
            {
                Throw.Exception<object>("Row hasn't been started, column is unexpected");
                return;
            }

            if (columnNumber >= MemberLookup.Length)
            {
                Throw.InvalidOperationException<object>($"Unexpected column (Index={columnNumber})");
                return;
            }

            var res = MemberLookup[columnNumber];
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
                    simpleCol.SetOnRow(Current, in ctx, data);
                }
                else
                {
                    simpleCol.ParseAndHold(Hold, in ctx, data);
                    SimpleSet.Value.Add(ctx);
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

                holdConfig.ParseAndHold(Hold, in ctx, data);

                HeldCount++;

                if (HeldCount == HeldMembers.Length)
                {
                    CurrentPopulated = true;
                    Current = GetInstance(Hold);

                    if (SimpleSet.HasValue)
                    {
                        var toMove = SimpleSet.Value.ToImmutable();
                        foreach (var oldCtx in toMove)
                        {
                            var lookup = MemberLookup[oldCtx.Column.Index];
                            if (lookup.SimpleMember == null)
                            {
                                Throw.Exception<object>($"Column [{oldCtx.Column}] recorded as a previously set simple column, but could not be found when creating row");
                                return;
                            }

                            var moveDelegate = SimpleMembers[lookup.SimpleMember.Value].MoveToRow;
                            moveDelegate(Current, Hold, in oldCtx);
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

                return Throw.Exception<TRow>($"No current row available, shouldn't be trying to finish a row");
            }

            if (!RequiredTracker.CheckRequiredAndClear(out var missingIx))
            {
                var lookup = MemberLookup[missingIx];
                if (lookup.SimpleMember != null)
                {
                    var simpleDetails = SimpleMembers[lookup.SimpleMember.Value];
                    return Throw.SerializationException<TRow>($"Column [{simpleDetails.Name}] is required, but was not found in row");
                }
                else if (lookup.HeldMember != null)
                {
                    var heldDetails = HeldMembers[lookup.HeldMember.Value];
                    return Throw.SerializationException<TRow>($"Column [{heldDetails.Name}] is required, but was not found in row");
                }
                else
                {
                    return Throw.Exception<TRow>($"Column in position {missingIx} was required and missing, but couldn't find a member to match it to.  This shouldn't happen.");
                }
            }

            var ret = Current;
            Current = default!;
            CurrentPopulated = false;
            _RowStarted = false;
            HeldCount = 0;

            ClearHold(Hold);

            if (SimpleSet.HasValue)
            {
                SimpleSet.Value.Clear();
            }

            return ret;
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                RequiredTracker.Dispose();
            }
        }
    }
}
