using System;
using System.Buffers;
using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class Writer<T> : SyncWriterBase<T>
    {
        internal Writer(ConcreteBoundConfiguration<T> config, IWriterAdapter inner, object? context) : base(config, inner, context) { }

        internal override void WriteInner(T row)
        {
            try
            {
                WriteHeadersAndEndRowIfNeeded();

                var columnsValue = Columns;

                for (var i = 0; i < columnsValue.Length; i++)
                {
                    var needsSeparator = i != 0;

                    if (needsSeparator)
                    {
                        PlaceCharInStaging(Configuration.Options.ValueSeparator);
                    }

                    var col = columnsValue[i];

                    var ctx = WriteContexts[i].SetRowNumberForWriteColumn(RowNumber);

                    if (!col.Write(row, ctx, Buffer))
                    {
                        Throw.SerializationException<object>($"Could not write column {col.Name}, formatter returned false");
                    }

                    ReadOnlySequence<char> res = default;
                    if (!Buffer.MakeSequence(ref res))
                    {
                        // nothing was written, so just move on
                        continue;
                    }

                    WriteValue(res);
                    Buffer.Reset();
                }

                RowNumber++;
            }
            catch (Exception e)
            {
                Throw.PoisonAndRethrow<object>(this, e);
            }
        }

        public override void WriteComment(string comment)
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            Utils.CheckArgumentNull(comment, nameof(comment));

            try
            {

                WriteHeadersAndEndRowIfNeeded();

                var (commentChar, segments) = SplitCommentIntoLines(comment);

                if (segments.IsSingleSegment)
                {
                    PlaceCharInStaging(commentChar);
                    var segSpan = segments.First.Span;
                    if (segSpan.Length > 0)
                    {
                        PlaceAllInStaging(segSpan);
                    }
                }
                else
                {
                    // we know we can write directly now
                    var isFirstRow = true;
                    foreach (var seg in segments)
                    {
                        if (!isFirstRow)
                        {
                            EndRecord();
                        }

                        PlaceCharInStaging(commentChar);
                        var segSpan = seg.Span;
                        if (segSpan.Length > 0)
                        {
                            PlaceAllInStaging(segSpan);
                        }
                        isFirstRow = false;
                    }
                }
            }
            catch (Exception e)
            {
                Throw.PoisonAndRethrow<object>(this, e);
            }
        }

        private void WriteHeadersAndEndRowIfNeeded()
        {
            var shouldEndRecord = true;

            if (IsFirstRow)
            {
                if (!CheckHeaders())
                {
                    shouldEndRecord = false;
                }
            }

            if (shouldEndRecord)
            {
                EndRecord();
            }
        }

        // returns true if it did write out headers,
        //   so we need to end a record before
        //   writing the next one
        private bool CheckHeaders()
        {
            // make a note of what the columns to write actually are
            Columns = Configuration.SerializeColumns;
            CreateWriteContexts();

            IsFirstRow = false;

            if (Configuration.Options.WriteHeader == WriteHeader.Never)
            {
                // nothing to write, so bail
                return false;
            }

            WriteHeaders();

            return true;
        }

        private void WriteHeaders()
        {
            var needsEscape = Configuration.SerializeColumnsNeedEscape;

            var columnsValue = Columns;

            var options = Configuration.Options;

            for (var i = 0; i < columnsValue.Length; i++)
            {
                // for the separator
                if (i != 0)
                {
                    PlaceCharInStaging(options.ValueSeparator);
                }

                var colName = columnsValue[i].Name;
                var escape = needsEscape[i];

                if (!escape)
                {
                    PlaceAllInStaging(colName.AsSpan());
                }
                else
                {
                    var escapedValueStartAndStop = Utils.NonNullValue(options.EscapedValueStartAndEnd);
                    var escapeValueEscapeChar = Utils.NonNullValue(options.EscapedValueEscapeCharacter);

                    // start with the escape char
                    PlaceCharInStaging(escapedValueStartAndStop);

                    // try and blit everything in relatively few calls

                    var colSpan = colName.AsSpan();

                    var start = 0;
                    var end = Utils.FindChar(colSpan, start, escapedValueStartAndStop);
                    while (end != -1)
                    {
                        var len = end - start;
                        var toWrite = colSpan.Slice(start, len);

                        PlaceAllInStaging(toWrite);

                        // place the escape char
                        PlaceCharInStaging(escapeValueEscapeChar);

                        start = end;
                        end = Utils.FindChar(colSpan, start + 1, escapedValueStartAndStop);
                    }

                    // copy the last bit
                    if (start != colSpan.Length)
                    {
                        var toWrite = colSpan.Slice(start);

                        PlaceAllInStaging(toWrite);
                    }

                    // end with the escape char
                    PlaceCharInStaging(escapedValueStartAndStop);
                }
            }
        }

        public override void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;

                try
                {

                    if (IsFirstRow)
                    {
                        CheckHeaders();
                    }

                    if (Configuration.Options.WriteTrailingRowEnding == WriteTrailingRowEnding.Always)
                    {
                        EndRecord();
                    }

                    if (HasStaging)
                    {
                        if (InStaging > 0)
                        {
                            FlushStaging();
                        }

                        Staging.Dispose();
                        Staging = EmptyMemoryOwner.Singleton;
                        StagingMemory = Memory<char>.Empty;
                    }

                    Inner.Dispose();
                    Buffer.Dispose();
                }
                catch (Exception e)
                {
                    if (HasStaging)
                    {
                        Staging.Dispose();
                        Staging = EmptyMemoryOwner.Singleton;
                        StagingMemory = Memory<char>.Empty;
                    }

                    Buffer.Dispose();

                    Throw.PoisonAndRethrow<object>(this, e);
                }
            }
        }

        public override string ToString()
        {
            return $"{nameof(Writer<T>)} with {Configuration}";
        }
    }
}