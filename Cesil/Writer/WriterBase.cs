using System;
using System.Buffers;

namespace Cesil
{
    internal abstract class WriterBase<T>
    {
        internal BoundConfigurationBase<T> Config { get; }

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
            Config = config;
            Buffer = new MaxSizedBufferWriter(Config.MemoryPool, config.WriteBufferSizeHint);
            Context = context;

            // buffering is configurable
            if (Config.WriteBufferSizeHint == 0)
            {
                Staging.Clear();
                InStaging = -1;
            }
            else
            {
                InStaging = 0;
                Staging.Value = Config.MemoryPool.Rent(Config.WriteBufferSizeHint ?? MaxSizedBufferWriter.DEFAULT_STAGING_SIZE);
            }
        }

        internal bool NeedsEncode(ReadOnlyMemory<char> charMem)
        => Utils.FindNeedsEncode(charMem, 0, Config) != -1;

        internal bool NeedsEncode(ReadOnlySpan<char> charSpan)
        => Utils.FindNeedsEncode(charSpan, 0, Config) != -1;

        internal bool NeedsEncode(ReadOnlySequence<char> head)
        => Utils.FindNeedsEncode(head, 0, Config) != -1;

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
            if (!Config.HasCommentChar)
            {
                return Throw.InvalidOperationException<(char CommentChar, ReadOnlySequence<char> CommentLines)>($"No {nameof(Options.CommentCharacter)} configured, cannot write a comment line");
            }

            var commentMem = comment.AsMemory();

            var seq = Utils.Split(commentMem, Config.RowEndingMemory);
            var c = Config.CommentChar;

            return (c, seq);
        }

        internal void CheckCanEncode(ReadOnlySpan<char> chars)
        {
            // we can always encode if we have both (the common case)
            if (Config.HasEscapedValueStartAndStop && Config.HasEscapeValueEscapeChar)
            {
                return;
            }

            // we can NEVER encode if we don't have the ability to start an escaped value
            if (!Config.HasEscapedValueStartAndStop)
            {
                // we can be slow here, we're about to throw an exception
                var carriageReturnIx = Utils.FindChar(chars, 0, '\r');
                var newLineIx = Utils.FindChar(chars, 0, '\n');
                var separatorIx = Utils.FindChar(chars, 0, Config.ValueSeparator);
                var commentIx = Config.HasCommentChar ? Utils.FindChar(chars, 0, Config.CommentChar) : -1;

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
            var escapeStartIx = Utils.FindChar(chars, 0, Config.EscapedValueStartAndStop);
            if (escapeStartIx == -1) return;

            Throw.InvalidOperationException<object>($"Tried to write a value contain '{Config.EscapedValueStartAndStop}' which requires escaping the character in an escaped value, but no way to escape inside an escaped value is configured");
        }

        internal void CheckCanEncode(ReadOnlySequence<char> chars)
        {
            // we can always encode if we have both (the common case)
            if (Config.HasEscapedValueStartAndStop && Config.HasEscapeValueEscapeChar)
            {
                return;
            }

            if (chars.IsSingleSegment)
            {
                CheckCanEncode(chars.FirstSpan);
                return;
            }

            // we can NEVER encode if we don't have the ability to start an escaped value
            if (!Config.HasEscapedValueStartAndStop)
            {
                // we can be slow here, we're about to throw an exception
                var carriageReturnIx = Utils.FindChar(chars, 0, '\r');
                var newLineIx = Utils.FindChar(chars, 0, '\n');
                var separatorIx = Utils.FindChar(chars, 0, Config.ValueSeparator);
                var commentIx = Config.HasCommentChar ? Utils.FindChar(chars, 0, Config.CommentChar) : -1;

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
                    offendingChar = Config.ValueSeparator;
                }
                else
                {
                    offendingChar = Config.CommentChar;
                }

                Throw.InvalidOperationException<object>($"Tried to write a value contain '{offendingChar}' which requires escaping a value, but no way to escape a value is configured");
                return;
            }

            // we're only in trouble if the value contains EscapedValueStartAndStop
            var escapeStartIx = Utils.FindChar(chars, 0, Config.EscapedValueStartAndStop);
            if (escapeStartIx == -1) return;

            Throw.InvalidOperationException<object>($"Tried to write a value contain '{Config.EscapedValueStartAndStop}' which requires escaping the character in an escaped value, but no way to escape inside an escaped value is configured");
        }
    }
}
