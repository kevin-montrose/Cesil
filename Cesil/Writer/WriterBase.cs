using System;
using System.Buffers;

namespace Cesil
{
    internal abstract class WriterBase<T>
    {
        internal BoundConfigurationBase<T> Config { get; }

        internal readonly MaxSizedBufferWriter Buffer;

        private Column[]? _Columns;
        internal Column[] Columns
        {
            get => Utils.NonNull(_Columns);
            set => _Columns = value;
        }

        internal bool IsFirstRow => _Columns == null;

        internal bool HasBuffer => _Staging != null;

        private IMemoryOwner<char>? _Staging;
        internal IMemoryOwner<char> Staging => Utils.NonNull(_Staging);
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
                _Staging = null;
                InStaging = -1;
            }
            else
            {
                InStaging = 0;
                _Staging = Config.MemoryPool.Rent(Config.WriteBufferSizeHint ?? MaxSizedBufferWriter.DEFAULT_STAGING_SIZE);
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
            Staging.Memory.Span[InStaging] = c;
            InStaging++;

            return InStaging == Staging.Memory.Length;
        }

        internal (char CommentChar, ReadOnlySequence<char> CommentLines) SplitCommentIntoLines(string comment)
        {
            if (Config.CommentChar == null)
            {
                return Throw.InvalidOperationException<(char CommentChar, ReadOnlySequence<char> CommentLines)>($"No {nameof(Options.CommentCharacter)} configured, cannot write a comment line");
            }

            var commentMem = comment.AsMemory();

            var seq = Utils.Split(commentMem, Config.RowEndingMemory);
            var c = Config.CommentChar.Value;

            return (c, seq);
        }
    }
}
