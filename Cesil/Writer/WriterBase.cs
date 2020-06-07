using System;
using System.Buffers;

namespace Cesil
{
    internal abstract class WriterBase<T> : PoisonableBase
    {
        internal readonly BoundConfigurationBase<T> Configuration;

        internal readonly MaxSizedBufferWriter Buffer;

        internal Column[] Columns;
        internal WriteContext[] WriteContexts;

        internal bool IsFirstRow;

        internal readonly bool HasStaging;
        internal IMemoryOwner<char> Staging;
        internal Memory<char> StagingMemory;
        internal int InStaging;

        internal int RowNumber;

        internal readonly object? Context;

        protected WriterBase(BoundConfigurationBase<T> config, object? context)
        {
            RowNumber = 0;
            Configuration = config;

            var options = Configuration.Options;
            var memPool = options.MemoryPool;
            var writeSizeHint = options.WriteBufferSizeHint;

            Buffer = new MaxSizedBufferWriter(memPool, writeSizeHint);
            Context = context;

            // buffering is configurable
            if (writeSizeHint == 0)
            {
                HasStaging = false;
                Staging = EmptyMemoryOwner.Singleton;
                StagingMemory = Memory<char>.Empty;
                InStaging = -1;
            }
            else
            {
                HasStaging = true;
                Staging = memPool.Rent(writeSizeHint ?? MaxSizedBufferWriter.DEFAULT_STAGING_SIZE);
                StagingMemory = Staging.Memory;
                InStaging = 0;
            }

            IsFirstRow = true;
            Columns = Array.Empty<Column>();
            WriteContexts = Array.Empty<WriteContext>();
        }

        internal bool NeedsEncode(ReadOnlyMemory<char> charMem)
        => Utils.FindNeedsEncode(charMem, 0, Configuration) != -1;

        internal bool NeedsEncode(ReadOnlySpan<char> charSpan)
        => Utils.FindNeedsEncode(charSpan, 0, Configuration) != -1;

        internal bool NeedsEncode(ReadOnlySequence<char> head)
        => Utils.FindNeedsEncode(head, 0, Configuration) != -1;

        // returns true if we need to flush staging
        internal bool PlaceInStaging(char c)
        {
            StagingMemory.Span[InStaging] = c;
            InStaging++;

            return InStaging == StagingMemory.Length;
        }

        internal static void CheckCanEncode(ReadOnlySpan<char> chars, Options options)
        {
            var escapedValueStartAndEnd = options.EscapedValueStartAndEnd;
            var hasEscapedValueStartAndStop = escapedValueStartAndEnd != null;
            var hasEscapeValueEscapeChar = options.EscapedValueEscapeCharacter != null;

            // we can always encode if we have both (the common case)
            if (hasEscapedValueStartAndStop && hasEscapeValueEscapeChar)
            {
                return;
            }

            // we can NEVER encode if we don't have the ability to start an escaped value
            if (!hasEscapedValueStartAndStop)
            {
                // we can be slow here, we're about to throw an exception
                var carriageReturnIx = Utils.FindChar(chars, 0, '\r');
                var newLineIx = Utils.FindChar(chars, 0, '\n');

                var separatorIx = Utils.Find(chars, 0, options.ValueSeparator);
                var commentChar = options.CommentCharacter;
                var commentIx = commentChar != null ? Utils.FindChar(chars, 0, commentChar.Value) : -1;

                if (carriageReturnIx == -1) carriageReturnIx = int.MaxValue;
                if (newLineIx == -1) newLineIx = int.MaxValue;
                if (separatorIx == -1) separatorIx = int.MaxValue;
                if (commentIx == -1) commentIx = int.MaxValue;

                var offendingIx = Math.Min(carriageReturnIx, Math.Min(newLineIx, Math.Min(separatorIx, commentIx)));

                var take =
                    carriageReturnIx == offendingIx ||
                    newLineIx == offendingIx ||
                    commentIx == offendingIx ? 1 : options.ValueSeparator.Length;

                var offendingText = new string(chars[offendingIx..(offendingIx + take)]);

                Throw.InvalidOperationException<object>($"Tried to write a value contain '{offendingText}' which requires escaping a value, but no way to escape a value is configured");
                return;
            }

            // we're only in trouble if the value contains EscapedValueStartAndStop
            var escapeStartIx = Utils.FindChar(chars, 0, Utils.NonNullValue(escapedValueStartAndEnd));
            if (escapeStartIx == -1) return;

            Throw.InvalidOperationException<object>($"Tried to write a value contain '{escapedValueStartAndEnd}' which requires escaping the character in an escaped value, but no way to escape inside an escaped value is configured");
        }

        internal static void CheckCanEncode(ReadOnlySequence<char> chars, Options options)
        {
            if (chars.IsSingleSegment)
            {
                CheckCanEncode(chars.FirstSpan, options);
                return;
            }

            var escapedValueStartAndEnd = options.EscapedValueStartAndEnd;
            var hasEscapedValueStartAndStop = escapedValueStartAndEnd != null;
            var hasEscapeValueEscapeChar = options.EscapedValueEscapeCharacter != null;

            // we can always encode if we have both (the common case)
            if (hasEscapedValueStartAndStop && hasEscapeValueEscapeChar)
            {
                return;
            }

            // we can NEVER encode if we don't have the ability to start an escaped value
            if (!hasEscapedValueStartAndStop)
            {
                // we can be slow here, we're about to throw an exception
                var carriageReturnIx = Utils.FindChar(chars, 0, '\r');
                var newLineIx = Utils.FindChar(chars, 0, '\n');

                var separatorIx = Utils.Find(chars, options.ValueSeparator);

                var commentChar = options.CommentCharacter;
                var commentIx = commentChar != null ? Utils.FindChar(chars, 0, commentChar.Value) : -1;

                if (carriageReturnIx == -1) carriageReturnIx = int.MaxValue;
                if (newLineIx == -1) newLineIx = int.MaxValue;
                if (separatorIx == -1) separatorIx = int.MaxValue;
                if (commentIx == -1) commentIx = int.MaxValue;

                var offendingIx = Math.Min(carriageReturnIx, Math.Min(newLineIx, Math.Min(separatorIx, commentIx)));

                var take =
                    carriageReturnIx == offendingIx ||
                    newLineIx == offendingIx ||
                    commentIx == offendingIx ? 1 : options.ValueSeparator.Length;

                var offendingText = new string(chars.Slice(offendingIx).FirstSpan[0..take]);

                Throw.InvalidOperationException<object>($"Tried to write a value contain '{offendingText}' which requires escaping a value, but no way to escape a value is configured");
                return;
            }

            // we're only in trouble if the value contains EscapedValueStartAndStop
            var escapeStartIx = Utils.FindChar(chars, 0, Utils.NonNullValue(escapedValueStartAndEnd));
            if (escapeStartIx == -1) return;

            Throw.InvalidOperationException<object>($"Tried to write a value contain '{escapedValueStartAndEnd}' which requires escaping the character in an escaped value, but no way to escape inside an escaped value is configured");
        }

        // can't do this in Configuration because we need the _Context_ object
        internal void CreateWriteContexts()
        {
            WriteContexts = new WriteContext[Columns.Length];
            for (var i = 0; i < WriteContexts.Length; i++)
            {
                WriteContexts[i] = WriteContext.WritingColumn(Configuration.Options, 0, ColumnIdentifier.CreateInner(i, Columns[i].Name), Context);
            }
        }
    }
}
