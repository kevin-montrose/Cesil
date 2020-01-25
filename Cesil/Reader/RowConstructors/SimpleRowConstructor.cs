using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Cesil
{
    internal sealed class SimpleRowConstructor<TRow> : IRowConstructor<TRow>
    {
        public IEnumerable<string> Columns
        {
            get
            {
                foreach (var item in Setters)
                {
                    yield return item.Name ?? "--UNKNOWN--";
                }
            }
        }

        private bool _RowStarted;
        public bool RowStarted => _RowStarted;

        private readonly InstanceProviderDelegate<TRow> InstanceProvider;
        private ImmutableArray<(string? Name, MemberRequired Required, ParseAndSetOnDelegate<TRow>? Setter)> Setters;

        public bool IsDisposed => RequiredTracker.IsDisposed;

        private readonly RequiredSet RequiredTracker;

        private bool CurrentPopulated;
        private TRow Current;

        internal SimpleRowConstructor(
            MemoryPool<char> pool,
            InstanceProviderDelegate<TRow> instanceProvider,
            ImmutableArray<(string Name, MemberRequired Required, ParseAndSetOnDelegate<TRow> Setter)> setters
        )
        {
            InstanceProvider = instanceProvider;
            Setters = setters!;

            _RowStarted = false;
            CurrentPopulated = false;
            Current = default!;

            var hasRequired = false;
            foreach (var s in setters)
            {
                if (s.Required == MemberRequired.Yes)
                {
                    hasRequired = true;
                    break;
                }
            }

            if (hasRequired)
            {
                RequiredTracker = new RequiredSet(pool, setters.Length);

                for (var i = 0; i < setters.Length; i++)
                {
                    if (setters[i].Required == MemberRequired.Yes)
                    {
                        RequiredTracker.SetIsRequired(i);
                    }
                }

            }
            else
            {
                RequiredTracker = default;
            }
        }

        public void SetColumnOrder(HeadersReader<TRow>.HeaderEnumerator columns)
        {
            // took ownership, have to dispose
            using (columns)
            {
                RequiredTracker.ClearRequired();

                var inOrder = ImmutableArray.CreateBuilder<(string? Name, MemberRequired Required, ParseAndSetOnDelegate<TRow>? Setter)>();
                var ix = 0;

                while (columns.MoveNext())
                {
                    var ci = columns.Current;

                    var found = false;

                    foreach (var s in Setters)
                    {
                        if (Utils.AreEqual(s.Name.AsMemory(), ci))
                        {
                            found = true;

                            if (s.Required == MemberRequired.Yes)
                            {
                                RequiredTracker.SetIsRequired(ix);
                            }

                            inOrder.Add(s);
                            break;
                        }
                    }

                    if (!found)
                    {
                        inOrder.Add((null, MemberRequired.No, null));
                    }

                    ix++;
                }

                Setters = inOrder.ToImmutable();
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

        public void ColumnAvailable(Options options, int rowNumber, int columnNumber, object? context, in ReadOnlySpan<char> data)
        {
            if (!CurrentPopulated || !RowStarted)
            {
                Throw.Exception<object>($"No current row available, shouldn't be trying to read a column");
                return;
            }

            if (columnNumber >= Setters.Length)
            {
                Throw.SerializationException<object>($"Unexpected column (Index={columnNumber})");
                return;
            }

            RequiredTracker.MarkSet(columnNumber);

            var config = Setters[columnNumber];

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

        public TRow FinishRow()
        {
            if (!CurrentPopulated)
            {
                return Throw.Exception<TRow>($"No current row available, shouldn't be trying to finish a row");
            }

            if (!RequiredTracker.CheckRequiredAndClear(out var missingIx))
            {
                if (missingIx < Setters.Length)
                {
                    var details = Setters[missingIx];

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
                RequiredTracker.Dispose();
            }
        }
    }
}
