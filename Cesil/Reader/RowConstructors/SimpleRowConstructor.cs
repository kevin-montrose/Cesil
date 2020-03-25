using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Cesil
{
    internal sealed class SimpleRowConstructor<TRow> : IRowConstructor<TRow>
    {
        public IEnumerable<string> Columns
        {
            get
            {
                for (var i = 0; i < Setters.Length; i++)
                {
                    LookupColumn(i, out var setterIx);
                    if (setterIx != null)
                    {
                        var item = Setters[setterIx.Value];
                        yield return item.Name ?? "--UNKNONW--";
                    }
                    else
                    {
                        yield return "--UNKNOWN--";
                    }
                }
            }
        }

        private readonly MemoryPool<char> MemoryPool;

        private bool _RowStarted;
        public bool RowStarted => _RowStarted;

        private readonly InstanceProviderDelegate<TRow> InstanceProvider;
        private readonly ImmutableArray<(string? Name, MemberRequired Required, ParseAndSetOnDelegate<TRow>? Setter)> Setters;
        private UnmanagedLookupArray<int>? SettersLookupOverride;

        private bool _IsDisposed;
        public bool IsDisposed => _IsDisposed;

        private readonly bool HasRequired;
        private readonly RequiredSet RequiredTracker;

        private bool CurrentPopulated;
        private TRow Current;

        internal SimpleRowConstructor(
            MemoryPool<char> pool,
            InstanceProviderDelegate<TRow> instanceProvider,
            ImmutableArray<(string Name, MemberRequired Required, ParseAndSetOnDelegate<TRow> Setter)> setters
        )
        {
            MemoryPool = pool;

            InstanceProvider = instanceProvider;
            Setters = setters!;

            // we'll never _use_ this (we'll clone all real instances)
            //   so don't actually allocate but do make a note if we'll
            //   need to really create a tracker later
            RequiredTracker = default;
            foreach (var setter in Setters)
            {
                if (setter.Required == MemberRequired.Yes)
                {
                    HasRequired = true;
                    break;
                }
            }

            _RowStarted = false;
            CurrentPopulated = false;
            Current = default!;

            _IsDisposed = false;
        }

        private SimpleRowConstructor(
            MemoryPool<char> pool,
            InstanceProviderDelegate<TRow> instanceProvider,
            ImmutableArray<(string? Name, MemberRequired Required, ParseAndSetOnDelegate<TRow>? Setter)> setters,
            bool hasRequired,
            RequiredSet tracker
        )
        {
            MemoryPool = pool;

            InstanceProvider = instanceProvider;
            Setters = setters;

            _RowStarted = false;
            CurrentPopulated = false;
            Current = default!;

            HasRequired = hasRequired;
            RequiredTracker = tracker;

            _IsDisposed = false;
        }

        public IRowConstructor<TRow> Clone()
        {
            RequiredSet tracker;

            if (HasRequired)
            {
                tracker = new RequiredSet(MemoryPool, Setters.Length);

                for (var i = 0; i < Setters.Length; i++)
                {
                    if (Setters[i].Required == MemberRequired.Yes)
                    {
                        RequiredTracker.SetIsRequired(i);
                    }
                }
            }
            else
            {
                tracker = default;
            }

            return new SimpleRowConstructor<TRow>(MemoryPool, InstanceProvider, Setters, HasRequired, tracker);
        }

        public void SetColumnOrder(HeadersReader<TRow>.HeaderEnumerator columns)
        {
            var totalRequired = 0;
            foreach (var col in Setters)
            {
                if (col.Required == MemberRequired.Yes)
                {
                    totalRequired++;
                }
            }

            // took ownership, have to dispose
            using (columns)
            {
                RequiredTracker.ClearRequired();

                var foundRequired = 0;

                var overrideLookup = new UnmanagedLookupArray<int>(MemoryPool, columns.Count);
                var ix = 0;

                while (columns.MoveNext())
                {
                    var ci = columns.Current;

                    var found = false;

                    for (var setterIx = 0; setterIx < Setters.Length; setterIx++)
                    {
                        var s = Setters[setterIx];
                        if (Utils.AreEqual(s.Name.AsMemory(), ci))
                        {
                            found = true;

                            if (s.Required == MemberRequired.Yes)
                            {
                                RequiredTracker.SetIsRequired(setterIx);
                                foundRequired++;
                            }

                            overrideLookup.Set(ix, setterIx);
                            break;
                        }
                    }

                    if (!found)
                    {
                        overrideLookup.Set(ix, -1);
                    }

                    ix++;
                }

                SettersLookupOverride = overrideLookup;

                // this is an error case, so we can be slow here
                if (foundRequired != totalRequired)
                {
                    for (var setterIx = 0; setterIx < Setters.Length; setterIx++)
                    {
                        var setter = Setters[setterIx];
                        if (setter.Required != MemberRequired.Yes) continue;

                        var found = false;

                        // one place where this is useful!
                        columns.Reset();
                        while (columns.MoveNext())
                        {
                            var ci = columns.Current;

                            if (Utils.AreEqual(setter.Name.AsMemory(), ci))
                            {
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                        {
                            Throw.SerializationException<object>($"Required column [{setter.Name}] was not found in header row");
                            return;
                        }
                    }
                }
            }
        }

        public bool TryPreAllocate(in ReadContext ctx, ref TRow prealloced)
        {
            if (prealloced != null)
            {
                Current = prealloced;
                CurrentPopulated = true;
                return true;
            }
            else if (!InstanceProvider(in ctx, out prealloced))
            {
                return Throw.InvalidOperationException<bool>($"Failed to obtain instance of {typeof(TRow).Name}");
            }

            Current = prealloced;
            CurrentPopulated = true;
            return true;
        }

        public void StartRow(in ReadContext ctx)
        {
            if (!CurrentPopulated)
            {
                Throw.Exception<object>("Row should already be pre-allocated");
            }

            _RowStarted = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LookupColumn(int columnNumber, out int? setterIx)
        {
            if (SettersLookupOverride != null)
            {
                SettersLookupOverride.Value.Get(columnNumber, -1, out var rawValue);

                setterIx = rawValue == -1 ? (int?)null : rawValue;
                return;
            }

            // don't blindly trust the value either
            if (columnNumber >= Setters.Length)
            {
                setterIx = null;
                return;
            }

            setterIx = columnNumber;
        }

        public void ColumnAvailable(Options options, int rowNumber, int columnNumber, object? context, in ReadOnlySpan<char> data)
        {
            if (!CurrentPopulated || !RowStarted)
            {
                Throw.Exception<object>($"No current row available, shouldn't be trying to read a column");
                return;
            }

            LookupColumn(columnNumber, out var setterNumber);
            if (setterNumber != null)
            {
                RequiredTracker.MarkSet(setterNumber.Value);

                var config = Setters[setterNumber.Value];

                var ctx = ReadContext.ReadingColumn(options, rowNumber, ColumnIdentifier.CreateInner(columnNumber, config.Name), context);

                if (config.Required == MemberRequired.Yes && data.Length == 0)
                {
                    Throw.SerializationException<object>($"Column [{ctx.Column}] is required, but was not found in row");
                    return;
                }

                var setter = config.Setter;

                // ignore it
                if (setter == null) return;

                setter(Current, in ctx, data);
            }
        }

        public TRow FinishRow()
        {
            if (!CurrentPopulated)
            {
                return Throw.Exception<TRow>($"No current row available, shouldn't be trying to finish a row");
            }

            if (!RequiredTracker.CheckRequiredAndClear(out var missingIx))
            {
                LookupColumn(missingIx, out var setterIx);
                if (setterIx.HasValue)
                {
                    var details = Setters[setterIx.Value];

                    return Throw.SerializationException<TRow>($"Column [{details.Name}] is required, but was not found in row");
                }

                return Throw.Exception<TRow>($"Column in position {missingIx} was required and missing, but couldn't find a member to match it to.  This shouldn't happen.");
            }

            var ret = Current;
            Current = default!;
            CurrentPopulated = false;
            _RowStarted = false;

            return ret;
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                _IsDisposed = true;

                RequiredTracker.Dispose();
                SettersLookupOverride?.Dispose();
                SettersLookupOverride = null;
            }
        }
    }
}
