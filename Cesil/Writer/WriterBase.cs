using System;
using System.Buffers;

namespace Cesil
{
    internal abstract class WriterBase<T>
    {
        internal readonly BoundConfigurationBase<T> Configuration;

        internal readonly MaxSizedBufferWriter Buffer;

        internal NonNull<Column[]> Columns;

        internal bool IsFirstRow => !Columns.HasValue;

        internal NonNull<IMemoryOwner<char>> Staging;
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
                Staging.Clear();
                InStaging = -1;
            }
            else
            {
                InStaging = 0;
                Staging.Value = memPool.Rent(writeSizeHint ?? MaxSizedBufferWriter.DEFAULT_STAGING_SIZE);
            }
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
            var stagingValue = Staging.Value;

            stagingValue.Memory.Span[InStaging] = c;
            InStaging++;

            return InStaging == stagingValue.Memory.Length;
        }

        internal (char CommentChar, ReadOnlySequence<char> CommentLines) SplitCommentIntoLines(string comment)
        {
            var options = Configuration.Options;
            var commentChar = options.CommentCharacter;

            if (commentChar == null)
            {
                return Throw.InvalidOperationException<(char CommentChar, ReadOnlySequence<char> CommentLines)>($"No {nameof(Options.CommentCharacter)} configured, cannot write a comment line");
            }

            var commentMem = comment.AsMemory();

            var seq = Utils.Split(commentMem, Configuration.RowEndingMemory);
            var c = commentChar.Value;

            return (c, seq);
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
                var separatorIx = Utils.FindChar(chars, 0, options.ValueSeparator);

                var commentChar = options.CommentCharacter;
                var commentIx = commentChar != null ? Utils.FindChar(chars, 0, commentChar.Value) : -1;

                if (carriageReturnIx == -1) carriageReturnIx = int.MaxValue;
                if (newLineIx == -1) newLineIx = int.MaxValue;
                if (separatorIx == -1) separatorIx = int.MaxValue;
                if (commentIx == -1) commentIx = int.MaxValue;

                var offendingIx = Math.Min(carriageReturnIx, Math.Min(newLineIx, Math.Min(separatorIx, commentIx)));
                var offendingChar = chars[offendingIx];

                Throw.InvalidOperationException<object>($"Tried to write a value contain '{offendingChar}' which requires escaping a value, but no way to escape a value is configured");
                return;
            }

            // we're only in trouble if the value contains EscapedValueStartAndStop
            var escapeStartIx = Utils.FindChar(chars, 0, escapedValueStartAndEnd!.Value);
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
                var separatorIx = Utils.FindChar(chars, 0, options.ValueSeparator);

                var commentChar = options.CommentCharacter;
                var commentIx = commentChar != null ? Utils.FindChar(chars, 0, commentChar.Value) : -1;

                if (carriageReturnIx == -1) carriageReturnIx = int.MaxValue;
                if (newLineIx == -1) newLineIx = int.MaxValue;
                if (separatorIx == -1) separatorIx = int.MaxValue;
                if (commentIx == -1) commentIx = int.MaxValue;

                var offendingIx = Math.Min(carriageReturnIx, Math.Min(newLineIx, Math.Min(separatorIx, commentIx)));

                char offendingChar;
                if (offendingIx == carriageReturnIx)
                {
                    offendingChar = '\r';
                }
                else if (offendingIx == newLineIx)
                {
                    offendingChar = '\n';
                }
                else if (offendingIx == separatorIx)
                {
                    offendingChar = options.ValueSeparator;
                }
                else
                {
                    offendingChar = commentChar!.Value;
                }

                Throw.InvalidOperationException<object>($"Tried to write a value contain '{offendingChar}' which requires escaping a value, but no way to escape a value is configured");
                return;
            }

            // we're only in trouble if the value contains EscapedValueStartAndStop
            var escapeStartIx = Utils.FindChar(chars, 0, escapedValueStartAndEnd!.Value);
            if (escapeStartIx == -1) return;

            Throw.InvalidOperationException<object>($"Tried to write a value contain '{escapedValueStartAndEnd}' which requires escaping the character in an escaped value, but no way to escape inside an escaped value is configured");
        }
    }
}
