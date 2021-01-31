using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using static Cesil.AwaitHelper;
using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class AsyncWriter<T> :
        AsyncWriterBase<T>
    {
        internal AsyncWriter(ConcreteBoundConfiguration<T> config, IAsyncWriterAdapter inner, object? context) : base(config, inner, context) { }

        public override ValueTask WriteAsync(T row, CancellationToken cancellationToken = default)
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            try
            {

                var checkHeadersAndEndRowTask = WriteHeadersAndEndRowIfNeededAsync(cancellationToken);
                if (!checkHeadersAndEndRowTask.IsCompletedSuccessfully(this))
                {
                    return WriteAsync_ContinueAfterHeadersAndEndRecordAsync(this, checkHeadersAndEndRowTask, row, cancellationToken);
                }

                var columnsValue = Columns;
                for (var i = 0; i < columnsValue.Length; i++)
                {
                    var needsSeparator = i != 0;
                    var col = columnsValue[i];

                    var writeColumnTask = WriteColumnAsync(row, i, col, needsSeparator, cancellationToken);
                    if (!writeColumnTask.IsCompletedSuccessfully(this))
                    {
                        return WriteAsync_ContinueAfterWriteColumnAsync(this, writeColumnTask, row, i, cancellationToken);
                    }
                }

                RowNumber++;

                return default;
            }
            catch (Exception e)
            {
                Throw.PoisonAndRethrow(this, e);

                return default;
            }

            // wait for the record to end, then continue async
            static async ValueTask WriteAsync_ContinueAfterHeadersAndEndRecordAsync(AsyncWriter<T> self, ValueTask waitFor, T row, CancellationToken cancellationToken)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                    var selfColumnsValue = self.Columns;
                    for (var i = 0; i < selfColumnsValue.Length; i++)
                    {
                        var needsSeparator = i != 0;
                        var col = selfColumnsValue[i];

                        var writeTask = self.WriteColumnAsync(row, i, col, needsSeparator, cancellationToken);
                        await ConfigureCancellableAwait(self, writeTask, cancellationToken);
                    }

                    self.RowNumber++;
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow(self, e);
                }
            }

            // wait for the column to be written, then continue with the loop
            static async ValueTask WriteAsync_ContinueAfterWriteColumnAsync(AsyncWriter<T> self, ValueTask waitFor, T row, int i, CancellationToken cancellationToken)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                    // the implicit increment at the end of the loop
                    i++;

                    var selfColumnsValue = self.Columns;
                    for (; i < selfColumnsValue.Length; i++)
                    {
                        const bool needsSeparator = true;                  // by definition, this isn't the first loop
                        var col = selfColumnsValue[i];

                        var writeTask = self.WriteColumnAsync(row, i, col, needsSeparator, cancellationToken);
                        await ConfigureCancellableAwait(self, writeTask, cancellationToken);
                    }

                    self.RowNumber++;
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow(self, e);
                }
            }
        }

        public override ValueTask WriteCommentAsync(ReadOnlyMemory<char> comment, CancellationToken cancellationToken = default)
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            try
            {
                var writeHeadersTask = WriteHeadersAndEndRowIfNeededAsync(cancellationToken);
                if (!writeHeadersTask.IsCompletedSuccessfully(this))
                {
                    return WriteCommentAsync_ContinueAfterWriteHeadersAndEndRowIfNeededAsync(this, writeHeadersTask, comment, cancellationToken);
                }

                var options = Configuration.Options;
                var commentCharNullable = options.CommentCharacter;

                if (commentCharNullable == null)
                {
                    Throw.InvalidOperationException($"No {nameof(Options.CommentCharacter)} configured, cannot write a comment line");
                }

                var commentChar = commentCharNullable.Value;
                var rowEndingMem = Configuration.RowEndingMemory;

                var splitIx = Utils.FindNextIx(0, comment, rowEndingMem);
                if (splitIx == -1)
                {
                    var writeTask = PlaceCharAndSegmentInStagingAsync(commentChar, comment, cancellationToken);

                    // doesn't matter if it completes, client has to await before the next call anyway
                    return writeTask;
                }
                else
                {
                    // multi segment
                    var prevIx = 0;

                    var isFirstRow = true;
                    while (splitIx != -1)
                    {
                        if (!isFirstRow)
                        {
                            var endRecordTask = EndRecordAsync(cancellationToken);
                            if (!endRecordTask.IsCompletedSuccessfully(this))
                            {
                                return WriteCommentAsync_ContinueAfterMultiSegmentEndRecordAsync(this, endRecordTask, commentChar, comment, prevIx, splitIx, rowEndingMem, cancellationToken);
                            }
                        }

                        var segSpan = comment[prevIx..splitIx];
                        var placeTask = PlaceCharAndSegmentInStagingAsync(commentChar, segSpan, cancellationToken);
                        if (!placeTask.IsCompletedSuccessfully(this))
                        {
                            return WriteCommentAsync_ContinueAfterMultiSegmentPlaceCharAndSegmentInStagingAsync(this, placeTask, commentChar, comment, splitIx, rowEndingMem, cancellationToken);
                        }

                        prevIx = splitIx + rowEndingMem.Length;
                        splitIx = Utils.FindNextIx(prevIx, comment, rowEndingMem);

                        isFirstRow = false;
                    }

                    if (prevIx != comment.Length)
                    {
                        if (!isFirstRow)
                        {
                            var endRecordTask = EndRecordAsync(cancellationToken);
                            if (!endRecordTask.IsCompletedSuccessfully(this))
                            {
                                return WriteCommentAsync_ContinueAfterMultiSegmentFinalCommentEndRecordAsync(this, endRecordTask, commentChar, comment, prevIx, cancellationToken);
                            }
                        }

                        var segSpan = comment[prevIx..];
                        var placeTask = PlaceCharAndSegmentInStagingAsync(commentChar, segSpan, cancellationToken);

                        // no need to wait, client will await before making another call
                        return placeTask;
                    }
                }
            }
            catch (Exception e)
            {
                Throw.PoisonAndRethrow(this, e);
            }

            return default;

            // wait for ending a row to finish, in the multi segment case, for the last comment, then continue
            static async ValueTask WriteCommentAsync_ContinueAfterMultiSegmentFinalCommentEndRecordAsync(AsyncWriter<T> self, ValueTask waitFor, char commentChar, ReadOnlyMemory<char> comment, int prevIx, CancellationToken cancellationToken)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                    var segSpan = comment[prevIx..];
                    var placeTask = self.PlaceCharAndSegmentInStagingAsync(commentChar, segSpan, cancellationToken);
                    await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow(self, e);
                }
            }

            static async ValueTask WriteCommentAsync_ContinueAfterMultiSegmentPlaceCharAndSegmentInStagingAsync(AsyncWriter<T> self, ValueTask waitFor, char commentChar, ReadOnlyMemory<char> comment, int splitIx, ReadOnlyMemory<char> rowEndingMem, CancellationToken cancellationToken)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                    int prevIx;
                    // finish loop
                    {
                        prevIx = splitIx + rowEndingMem.Length;
                        splitIx = Utils.FindNextIx(prevIx, comment, rowEndingMem);
                    }

                    while (splitIx != -1)
                    {
                        // not first row by definition
                        var endRecordTask = self.EndRecordAsync(cancellationToken);
                        await ConfigureCancellableAwait(self, endRecordTask, cancellationToken);

                        var segSpan = comment[prevIx..splitIx];
                        var placeTask = self.PlaceCharAndSegmentInStagingAsync(commentChar, segSpan, cancellationToken);
                        await ConfigureCancellableAwait(self, placeTask, cancellationToken);

                        prevIx = splitIx + rowEndingMem.Length;
                        splitIx = Utils.FindNextIx(prevIx, comment, rowEndingMem);
                    }

                    if (prevIx != comment.Length)
                    {
                        // not first row by definition
                        var endRecordTask = self.EndRecordAsync(cancellationToken);
                        await ConfigureCancellableAwait(self, endRecordTask, cancellationToken);

                        var segSpan = comment[prevIx..];
                        var placeTask = self.PlaceCharAndSegmentInStagingAsync(commentChar, segSpan, cancellationToken);
                        await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                    }
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow(self, e);
                }

            }

            // wait for ending the record in the multi segment case, then continue
            static async ValueTask WriteCommentAsync_ContinueAfterMultiSegmentEndRecordAsync(AsyncWriter<T> self, ValueTask waitFor, char commentChar, ReadOnlyMemory<char> comment, int prevIx, int splitIx, ReadOnlyMemory<char> rowEndingMem, CancellationToken cancellationToken)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                    // finish the loop
                    {
                        var segSpan = comment[prevIx..splitIx];
                        var placeTask = self.PlaceCharAndSegmentInStagingAsync(commentChar, segSpan, cancellationToken);
                        await ConfigureCancellableAwait(self, placeTask, cancellationToken);

                        prevIx = splitIx + rowEndingMem.Length;
                        splitIx = Utils.FindNextIx(prevIx, comment, rowEndingMem);
                    }

                    // back into the loop
                    while (splitIx != -1)
                    {
                        // never the first row, so always end
                        var endRecordTask = self.EndRecordAsync(cancellationToken);
                        await ConfigureCancellableAwait(self, endRecordTask, cancellationToken);

                        var segSpan = comment[prevIx..splitIx];
                        var placeTask = self.PlaceCharAndSegmentInStagingAsync(commentChar, segSpan, cancellationToken);
                        await ConfigureCancellableAwait(self, placeTask, cancellationToken);

                        prevIx = splitIx + rowEndingMem.Length;
                        splitIx = Utils.FindNextIx(prevIx, comment, rowEndingMem);
                    }

                    if (prevIx != comment.Length)
                    {
                        // never the first row, so always end
                        var endRecordTask = self.EndRecordAsync(cancellationToken);
                        await ConfigureCancellableAwait(self, endRecordTask, cancellationToken);

                        var segSpan = comment[prevIx..];
                        var placeTask = self.PlaceCharAndSegmentInStagingAsync(commentChar, segSpan, cancellationToken);
                        await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                    }
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow(self, e);
                }
            }

            // wait for writing headers (and maybe ending the row) to finish, then continue
            static async ValueTask WriteCommentAsync_ContinueAfterWriteHeadersAndEndRowIfNeededAsync(AsyncWriter<T> self, ValueTask waitFor, ReadOnlyMemory<char> comment, CancellationToken cancellationToken)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                    var options = self.Configuration.Options;
                    var commentCharNullable = options.CommentCharacter;

                    if (commentCharNullable == null)
                    {
                        Throw.InvalidOperationException($"No {nameof(Options.CommentCharacter)} configured, cannot write a comment line");
                    }

                    var commentChar = commentCharNullable.Value;
                    var rowEndingMem = self.Configuration.RowEndingMemory;

                    var splitIx = Utils.FindNextIx(0, comment, rowEndingMem);
                    if (splitIx == -1)
                    {
                        // single segment
                        var placeTask = self.PlaceCharAndSegmentInStagingAsync(commentChar, comment, cancellationToken);
                        await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                    }
                    else
                    {
                        // multi segment
                        var prevIx = 0;

                        var isFirstRow = true;
                        while (splitIx != -1)
                        {
                            if (!isFirstRow)
                            {
                                var endTask = self.EndRecordAsync(cancellationToken);
                                await ConfigureCancellableAwait(self, endTask, cancellationToken);
                            }

                            var segSpan = comment[prevIx..splitIx];
                            var placeTask = self.PlaceCharAndSegmentInStagingAsync(commentChar, segSpan, cancellationToken);
                            await ConfigureCancellableAwait(self, placeTask, cancellationToken);

                            prevIx = splitIx + rowEndingMem.Length;
                            splitIx = Utils.FindNextIx(prevIx, comment, rowEndingMem);

                            isFirstRow = false;
                        }

                        if (prevIx != comment.Length)
                        {
                            if (!isFirstRow)
                            {
                                var endTask = self.EndRecordAsync(cancellationToken);
                                await ConfigureCancellableAwait(self, endTask, cancellationToken);
                            }

                            var segSpan = comment[prevIx..];
                            var placeTask = self.PlaceCharAndSegmentInStagingAsync(commentChar, segSpan, cancellationToken);
                            await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                        }
                    }
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow(self, e);
                }
            }
        }

        private ValueTask WriteHeadersAndEndRowIfNeededAsync(CancellationToken cancellationToken)
        {
            var shouldEndRecord = true;
            if (IsFirstRow)
            {
                var headersTask = CheckHeadersAsync(cancellationToken);
                if (!headersTask.IsCompletedSuccessfully(this))
                {
                    return WriteHeadersAndEndRecordIfNeededAsync_ContinueAfterHeadersAsync(this, headersTask, cancellationToken);
                }

                if (!headersTask.Result)
                {
                    shouldEndRecord = false;
                }
            }

            if (shouldEndRecord)
            {
                var endRecordTask = EndRecordAsync(cancellationToken);
                return endRecordTask;
            }

            return default;

            static async ValueTask WriteHeadersAndEndRecordIfNeededAsync_ContinueAfterHeadersAsync(AsyncWriter<T> self, ValueTask<bool> waitFor, CancellationToken cancellationToken)
            {
                var shouldEndRecord = true;

                var res = await ConfigureCancellableAwait(self, waitFor, cancellationToken);


                if (!res)
                {
                    shouldEndRecord = false;
                }

                if (shouldEndRecord)
                {
                    var endTask = self.EndRecordAsync(cancellationToken);
                    await ConfigureCancellableAwait(self, endTask, cancellationToken);
                }
            }
        }

        private ValueTask WriteColumnAsync(T row, int colIx, Column col, bool needsSeparator, CancellationToken cancellationToken)
        {
            if (needsSeparator)
            {
                var sepTask = PlaceAllInStagingAsync(Configuration.ValueSeparatorMemory, cancellationToken);
                if (!sepTask.IsCompletedSuccessfully(this))
                {
                    return WriteColumnAsync_ContinueAfterSeparatorAsync(this, sepTask, row, colIx, col, cancellationToken);
                }
            }

            var ctx = WriteContexts[colIx].SetRowNumberForWriteColumn(RowNumber);
            if (!col.Write(row, ctx, Buffer))
            {
                Throw.SerializationException($"Could not write column {col.Name}, formatter returned false");
            }

            ReadOnlySequence<char> res = default;
            if (!Buffer.MakeSequence(ref res))
            {
                // nothing was written, so just move on
                return default;
            }

            var writeTask = WriteValueAsync(res, cancellationToken);
            if (!writeTask.IsCompletedSuccessfully(this))
            {
                return WriteColumnAsync_ContinueAfterWriteAsync(this, writeTask, cancellationToken);
            }

            Buffer.Reset();

            return default;

            // wait for the separator to be written, then continue async
            static async ValueTask WriteColumnAsync_ContinueAfterSeparatorAsync(AsyncWriter<T> self, ValueTask waitFor, T row, int colIx, Column col, CancellationToken cancellationToken)
            {
                await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                var ctx = self.WriteContexts[colIx].SetRowNumberForWriteColumn(self.RowNumber);

                if (!col.Write(row, ctx, self.Buffer))
                {
                    Throw.SerializationException($"Could not write column {col.Name}, formatter returned false");
                }

                ReadOnlySequence<char> res = default;
                if (!self.Buffer.MakeSequence(ref res))
                {
                    // nothing was written, so just move on
                    return;
                }

                var writeTask = self.WriteValueAsync(res, cancellationToken);
                await ConfigureCancellableAwait(self, writeTask, cancellationToken);

                self.Buffer.Reset();
            }

            // wait for the write to finish, then continue async
            static async ValueTask WriteColumnAsync_ContinueAfterWriteAsync(AsyncWriter<T> self, ValueTask waitFor, CancellationToken cancellationToken)
            {
                await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                self.Buffer.Reset();
            }
        }

        // returns true if it did write out headers,
        //   so we need to end a record before
        //   writing the next one
        private ValueTask<bool> CheckHeadersAsync(CancellationToken cancellationToken)
        {
            // make a note of what the columns to write actually are
            Columns = Configuration.SerializeColumns;
            CreateWriteContexts();

            IsFirstRow = false;

            if (Configuration.Options.WriteHeader == WriteHeader.Never)
            {
                // nothing to write, so bail
                return new ValueTask<bool>(false);
            }

            var writeTask = WriteHeadersAsync(cancellationToken);
            if (!writeTask.IsCompletedSuccessfully(this))
            {
                return CheckHeadersAsync_CompleteAsync(this, writeTask, cancellationToken);
            }

            return new ValueTask<bool>(true);

            // wait for the write to complete, then return true
            static async ValueTask<bool> CheckHeadersAsync_CompleteAsync(AsyncWriter<T> self, ValueTask waitFor, CancellationToken cancellationToken)
            {
                await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                return true;
            }
        }

        private ValueTask WriteHeadersAsync(CancellationToken cancellationToken)
        {
            var needsEscape = Configuration.SerializeColumnsNeedEscape;

            var columnsValue = Columns;
            var valueSeparator = Configuration.ValueSeparatorMemory;

            for (var i = 0; i < columnsValue.Length; i++)
            {
                // for the separator
                if (i != 0)
                {
                    var sepTask = PlaceAllInStagingAsync(valueSeparator, cancellationToken);
                    if (!sepTask.IsCompletedSuccessfully(this))
                    {
                        return WriteHeadersAsync_CompleteAfterFlushAsync(this, sepTask, needsEscape, valueSeparator, i, cancellationToken);
                    }
                }

                var writeTask = WriteSingleHeaderAsync(columnsValue[i], needsEscape[i], cancellationToken);
                if (!writeTask.IsCompletedSuccessfully(this))
                {
                    return WriteHeadersAsync_CompleteAfterHeaderWriteAsync(this, writeTask, needsEscape, valueSeparator, i, cancellationToken);
                }
            }

            return default;

            // waits for a flush to finish, then proceeds with writing headers
            static async ValueTask WriteHeadersAsync_CompleteAfterFlushAsync(AsyncWriter<T> self, ValueTask waitFor, bool[] needsEscape, ReadOnlyMemory<char> valueSeparator, int i, CancellationToken cancellationToken)
            {
                await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                var selfColumnsValue = self.Columns;
                var headerTask = self.WriteSingleHeaderAsync(selfColumnsValue[i], needsEscape[i], cancellationToken);
                await ConfigureCancellableAwait(self, headerTask, cancellationToken);

                // implicit increment at the end of the calling loop
                i++;

                for (; i < selfColumnsValue.Length; i++)
                {
                    // by definition we've always wrote at least one column here
                    var placeTask = self.PlaceAllInStagingAsync(valueSeparator, cancellationToken);
                    await ConfigureCancellableAwait(self, placeTask, cancellationToken);

                    var writeTask = self.WriteSingleHeaderAsync(selfColumnsValue[i], needsEscape[i], cancellationToken);
                    await ConfigureCancellableAwait(self, writeTask, cancellationToken);
                }
            }

            // waits for a header write to finish, then proceeds with the rest
            static async ValueTask WriteHeadersAsync_CompleteAfterHeaderWriteAsync(AsyncWriter<T> self, ValueTask waitFor, bool[] needsEscape, ReadOnlyMemory<char> valueSeparator, int i, CancellationToken cancellationToken)
            {
                await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                // implicit increment at the end of the calling loop
                i++;

                var selfColumnsValue = self.Columns;
                for (; i < selfColumnsValue.Length; i++)
                {
                    // by definition we've always wrote at least one column here
                    var placeTask = self.PlaceAllInStagingAsync(valueSeparator, cancellationToken);
                    await ConfigureCancellableAwait(self, placeTask, cancellationToken);

                    var writeTask = self.WriteSingleHeaderAsync(selfColumnsValue[i], needsEscape[i], cancellationToken);
                    await ConfigureCancellableAwait(self, writeTask, cancellationToken);
                }
            }
        }

        private ValueTask WriteSingleHeaderAsync(Column column, bool escape, CancellationToken cancellationToken)
        {
            var colName = column.Name;

            if (!escape)
            {
                var write = colName.AsMemory();

                return PlaceAllInStagingAsync(write, cancellationToken);
            }
            else
            {
                var options = Configuration.Options;

                // try and blit everything in relatively few calls
                var escapedValueStartAndStop = Utils.NonNullValue(options.EscapedValueStartAndEnd);
                var escapeValueEscapeChar = Utils.NonNullValue(options.EscapedValueEscapeCharacter);

                var colMem = colName.AsMemory();

                // start with the escape char
                var startEscapeTask = PlaceCharInStagingAsync(escapedValueStartAndStop, cancellationToken);
                if (!startEscapeTask.IsCompletedSuccessfully(this))
                {
                    return WriteSingleHeaderAsync_CompleteAfterFirstCharAsync(this, startEscapeTask, escapedValueStartAndStop, escapeValueEscapeChar, colMem, cancellationToken);
                }

                var start = 0;
                var end = Utils.FindChar(colMem, start, escapedValueStartAndStop);
                while (end != -1)
                {
                    var len = end - start;
                    var toWrite = colMem.Slice(start, len);

                    var writeTask = PlaceAllInStagingAsync(toWrite, cancellationToken);
                    if (!writeTask.IsCompletedSuccessfully(this))
                    {
                        return WriteSingleHeaderAsync_CompleteAfterWriteAsync(this, writeTask, escapedValueStartAndStop, escapeValueEscapeChar, colMem, end, cancellationToken);
                    }

                    // place the escape char
                    var escapeTask = PlaceCharInStagingAsync(escapeValueEscapeChar, cancellationToken);
                    if (!escapeTask.IsCompletedSuccessfully(this))
                    {
                        return WriteSingleHeaderAsync_CompleteAfterEscapeAsync(this, escapeTask, escapedValueStartAndStop, escapeValueEscapeChar, colMem, end, cancellationToken);
                    }

                    start = end;
                    end = Utils.FindChar(colMem, start + 1, escapedValueStartAndStop);
                }

                // copy the last bit
                if (start != colMem.Length)
                {
                    var toWrite = colMem.Slice(start);

                    var writeTask = PlaceAllInStagingAsync(toWrite, cancellationToken);
                    if (!writeTask.IsCompletedSuccessfully(this))
                    {
                        return WriteSingleHeaderAsync_CompleteAfterLastWriteAsync(this, writeTask, escapedValueStartAndStop, cancellationToken);
                    }
                }

                // end with the escape char
                var endEscapeTask = PlaceCharInStagingAsync(escapedValueStartAndStop, cancellationToken);
                if (!endEscapeTask.IsCompletedSuccessfully(this))
                {
                    return endEscapeTask;
                }

                return default;
            }

            // waits for the first char to write, then does the rest asynchronously
            static async ValueTask WriteSingleHeaderAsync_CompleteAfterFirstCharAsync(AsyncWriter<T> self, ValueTask waitFor, char escapedValueStartAndStop, char escapeValueEscapeChar, ReadOnlyMemory<char> colMem, CancellationToken cancellationToken)
            {
                await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                var start = 0;
                var end = Utils.FindChar(colMem, start, escapedValueStartAndStop);
                while (end != -1)
                {
                    var len = end - start;
                    var toWrite = colMem.Slice(start, len);

                    var placeTask = self.PlaceAllInStagingAsync(toWrite, cancellationToken);
                    await ConfigureCancellableAwait(self, placeTask, cancellationToken);

                    // place the escape char
                    var secondPlaceTask = self.PlaceCharInStagingAsync(escapeValueEscapeChar, cancellationToken);
                    await ConfigureCancellableAwait(self, secondPlaceTask, cancellationToken);

                    start = end;
                    end = Utils.FindChar(colMem, start + 1, escapedValueStartAndStop);
                }

                // copy the last bit
                if (start != colMem.Length)
                {
                    var toWrite = colMem.Slice(start);

                    var thirdPlaceTask = self.PlaceAllInStagingAsync(toWrite, cancellationToken);
                    await ConfigureCancellableAwait(self, thirdPlaceTask, cancellationToken);
                }

                // end with the escape char
                var fourthPlaceTask = self.PlaceCharInStagingAsync(escapedValueStartAndStop, cancellationToken);
                await ConfigureCancellableAwait(self, fourthPlaceTask, cancellationToken);
            }

            // waits for a write to finish, then complete the rest of the while loop and method async
            static async ValueTask WriteSingleHeaderAsync_CompleteAfterWriteAsync(AsyncWriter<T> self, ValueTask waitFor, char escapedValueStartAndStop, char escapeValueEscapeChar, ReadOnlyMemory<char> colMem, int end, CancellationToken cancellationToken)
            {
                await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                var placeTask = self.PlaceCharInStagingAsync(escapedValueStartAndStop, cancellationToken);
                await ConfigureCancellableAwait(self, placeTask, cancellationToken);

                var start = end;
                end = Utils.FindChar(colMem, start + 1, escapedValueStartAndStop);

                while (end != -1)
                {
                    var len = end - start;
                    var toWrite = colMem.Slice(start, len);

                    var secondPlaceTask = self.PlaceAllInStagingAsync(toWrite, cancellationToken);
                    await ConfigureCancellableAwait(self, secondPlaceTask, cancellationToken);

                    // place the escape char
                    var thirdPlaceTask = self.PlaceCharInStagingAsync(escapeValueEscapeChar, cancellationToken);
                    await ConfigureCancellableAwait(self, thirdPlaceTask, cancellationToken);

                    start = end;
                    end = Utils.FindChar(colMem, start + 1, escapedValueStartAndStop);
                }

                // copy the last bit
                if (start != colMem.Length)
                {
                    var toWrite = colMem.Slice(start);

                    var fourthPlaceTask = self.PlaceAllInStagingAsync(toWrite, cancellationToken);
                    await ConfigureCancellableAwait(self, fourthPlaceTask, cancellationToken);
                }

                // end with the escape char
                var fifthPlaceTask = self.PlaceCharInStagingAsync(escapedValueStartAndStop, cancellationToken);
                await ConfigureCancellableAwait(self, fifthPlaceTask, cancellationToken);
            }

            // waits for an escape to finish, then completes the rest of the while loop and method async
            static async ValueTask WriteSingleHeaderAsync_CompleteAfterEscapeAsync(AsyncWriter<T> self, ValueTask waitFor, char escapedValueStartAndStop, char escapeValueEscapeChar, ReadOnlyMemory<char> colMem, int end, CancellationToken cancellationToken)
            {
                await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                var start = end;
                end = Utils.FindChar(colMem, start + 1, escapedValueStartAndStop);

                while (end != -1)
                {
                    var len = end - start;
                    var toWrite = colMem.Slice(start, len);

                    var placeTask = self.PlaceAllInStagingAsync(toWrite, cancellationToken);
                    await ConfigureCancellableAwait(self, placeTask, cancellationToken);

                    // place the escape char
                    var secondPlaceTask = self.PlaceCharInStagingAsync(escapeValueEscapeChar, cancellationToken);
                    await ConfigureCancellableAwait(self, secondPlaceTask, cancellationToken);

                    start = end;
                    end = Utils.FindChar(colMem, start + 1, escapedValueStartAndStop);
                }

                // copy the last bit
                if (start != colMem.Length)
                {
                    var toWrite = colMem.Slice(start);

                    var thirdPlaceTask = self.PlaceAllInStagingAsync(toWrite, cancellationToken);
                    await ConfigureCancellableAwait(self, thirdPlaceTask, cancellationToken);
                }

                // end with the escape char
                var fourthPlaceTask = self.PlaceCharInStagingAsync(escapedValueStartAndStop, cancellationToken);
                await ConfigureCancellableAwait(self, fourthPlaceTask, cancellationToken);
            }

            // waits for a write to finish, then writes out the final char and maybe flushes async
            static async ValueTask WriteSingleHeaderAsync_CompleteAfterLastWriteAsync(AsyncWriter<T> self, ValueTask waitFor, char escapedValueStartAndStop, CancellationToken cancellationToken)
            {
                await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                // end with the escape char
                var placeTask = self.PlaceCharInStagingAsync(escapedValueStartAndStop, cancellationToken);
                await ConfigureCancellableAwait(self, placeTask, cancellationToken);
            }
        }

        public override ValueTask DisposeAsync()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;

                try
                {

                    var writeTrailingNewLine = Configuration.Options.WriteTrailingRowEnding;

                    if (IsFirstRow)
                    {
                        var headersTask = CheckHeadersAsync(CancellationToken.None);
                        if (!headersTask.IsCompletedSuccessfully(this))
                        {
                            return DisposeAsync_ContinueAfterHeadersAsync(this, headersTask, writeTrailingNewLine);
                        }
                    }

                    if (writeTrailingNewLine == WriteTrailingRowEnding.Always)
                    {
                        var endRecordTask = EndRecordAsync(CancellationToken.None);
                        if (!endRecordTask.IsCompletedSuccessfully(this))
                        {
                            return DisposeAsync_ContinueAfterEndRecordAsync(this, endRecordTask);
                        }
                    }

                    if (HasStaging)
                    {
                        if (InStaging > 0)
                        {
                            var flushTask = FlushStagingAsync(CancellationToken.None);
                            if (!flushTask.IsCompletedSuccessfully(this))
                            {
                                return DisposeAsync_ContinueAfterFlushAsync(this, flushTask);
                            }
                        }

                        Staging.Dispose();
                        Staging = EmptyMemoryOwner.Singleton;
                        StagingMemory = Memory<char>.Empty;
                    }

                    var ret = Inner.DisposeAsync();
                    if (!ret.IsCompletedSuccessfully(this))
                    {
                        return DisposeAsync_ContinueAfterInnerDisposedAsync(this, ret);
                    }

                    if (OneCharOwner.HasValue)
                    {
                        OneCharOwner.Value.Dispose();
                        OneCharOwner.Clear();
                    }

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

                    if (OneCharOwner.HasValue)
                    {
                        OneCharOwner.Value.Dispose();
                        OneCharOwner.Clear();
                    }

                    Buffer.Dispose();

                    Throw.PoisonAndRethrow(this, e);
                }
            }

            return default;

            // wait on headers, then continue asynchronously
            static async ValueTask DisposeAsync_ContinueAfterHeadersAsync(AsyncWriter<T> self, ValueTask<bool> waitFor, WriteTrailingRowEnding writeTrailingNewLine)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, CancellationToken.None);

                    if (writeTrailingNewLine == WriteTrailingRowEnding.Always)
                    {
                        var endTask = self.EndRecordAsync(CancellationToken.None);
                        await ConfigureCancellableAwait(self, endTask, CancellationToken.None);
                    }

                    if (self.HasStaging)
                    {
                        if (self.InStaging > 0)
                        {
                            var flushTask = self.FlushStagingAsync(CancellationToken.None);
                            await ConfigureCancellableAwait(self, flushTask, CancellationToken.None);
                        }

                        self.Staging.Dispose();
                        self.Staging = EmptyMemoryOwner.Singleton;
                        self.StagingMemory = Memory<char>.Empty;
                    }

                    var disposeTask = self.Inner.DisposeAsync();
                    await ConfigureCancellableAwait(self, disposeTask, CancellationToken.None);

                    if (self.OneCharOwner.HasValue)
                    {
                        self.OneCharOwner.Value.Dispose();
                        self.OneCharOwner.Clear();
                    }
                    self.Buffer.Dispose();
                }
                catch (Exception e)
                {
                    if (self.HasStaging)
                    {
                        self.Staging.Dispose();
                        self.Staging = EmptyMemoryOwner.Singleton;
                        self.StagingMemory = Memory<char>.Empty;
                    }

                    if (self.OneCharOwner.HasValue)
                    {
                        self.OneCharOwner.Value.Dispose();
                        self.OneCharOwner.Clear();
                    }

                    self.Buffer.Dispose();

                    Throw.PoisonAndRethrow(self, e);
                }
            }

            // wait on end record, then continue asynchronously
            static async ValueTask DisposeAsync_ContinueAfterEndRecordAsync(AsyncWriter<T> self, ValueTask waitFor)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, CancellationToken.None);

                    if (self.HasStaging)
                    {
                        if (self.InStaging > 0)
                        {
                            var flushTask = self.FlushStagingAsync(CancellationToken.None);
                            await ConfigureCancellableAwait(self, flushTask, CancellationToken.None);
                        }

                        self.Staging.Dispose();
                        self.Staging = EmptyMemoryOwner.Singleton;
                        self.StagingMemory = Memory<char>.Empty;
                    }

                    var disposeTask = self.Inner.DisposeAsync();
                    await ConfigureCancellableAwait(self, disposeTask, CancellationToken.None);

                    if (self.OneCharOwner.HasValue)
                    {
                        self.OneCharOwner.Value.Dispose();
                        self.OneCharOwner.Clear();
                    }
                    self.Buffer.Dispose();
                }
                catch (Exception e)
                {
                    if (self.HasStaging)
                    {
                        self.Staging.Dispose();
                        self.Staging = EmptyMemoryOwner.Singleton;
                        self.StagingMemory = Memory<char>.Empty;
                    }

                    if (self.OneCharOwner.HasValue)
                    {
                        self.OneCharOwner.Value.Dispose();
                        self.OneCharOwner.Clear();
                    }

                    self.Buffer.Dispose();

                    Throw.PoisonAndRethrow(self, e);
                }
            }

            // wait on flush, then continue asynchronously
            static async ValueTask DisposeAsync_ContinueAfterFlushAsync(AsyncWriter<T> self, ValueTask waitFor)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, CancellationToken.None);

                    if (self.HasStaging)
                    {
                        self.Staging.Dispose();
                        self.Staging = EmptyMemoryOwner.Singleton;
                        self.StagingMemory = Memory<char>.Empty;
                    }

                    var disposeTask = self.Inner.DisposeAsync();
                    await ConfigureCancellableAwait(self, disposeTask, CancellationToken.None);

                    if (self.OneCharOwner.HasValue)
                    {
                        self.OneCharOwner.Value.Dispose();
                        self.OneCharOwner.Clear();
                    }

                    self.Buffer.Dispose();
                }
                catch (Exception e)
                {
                    if (self.HasStaging)
                    {
                        self.Staging.Dispose();
                        self.Staging = EmptyMemoryOwner.Singleton;
                        self.StagingMemory = Memory<char>.Empty;
                    }

                    if (self.OneCharOwner.HasValue)
                    {
                        self.OneCharOwner.Value.Dispose();
                        self.OneCharOwner.Clear();
                    }

                    self.Buffer.Dispose();

                    Throw.PoisonAndRethrow(self, e);
                }
            }

            // wait on Inner.DisposeAsync
            static async ValueTask DisposeAsync_ContinueAfterInnerDisposedAsync(AsyncWriter<T> self, ValueTask waitFor)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, CancellationToken.None);

                    if (self.OneCharOwner.HasValue)
                    {
                        self.OneCharOwner.Value.Dispose();
                        self.OneCharOwner.Clear();
                    }
                    self.Buffer.Dispose();

                    self.IsDisposed = true;
                }
                catch (Exception e)
                {
                    if (self.OneCharOwner.HasValue)
                    {
                        self.OneCharOwner.Value.Dispose();
                        self.OneCharOwner.Clear();
                    }

                    self.Buffer.Dispose();

                    Throw.PoisonAndRethrow(self, e);
                }
            }
        }

        public override string ToString()
        {
            return $"{nameof(AsyncWriter<T>)} with {Configuration}";
        }
    }
}