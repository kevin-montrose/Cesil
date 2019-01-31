using System;
using System.Buffers;

namespace Cesil
{
    internal abstract class WriterBase<T>
        where T : new()
    {
        internal BoundConfiguration<T> Config { get; }

        internal readonly MaxSizedBufferWriter Buffer;

        internal Column[] Columns;

        internal bool IsFirstRow => Columns == null;

        internal bool HasBuffer => Staging != null;

        internal readonly IMemoryOwner<char> Staging;
        internal int InStaging;

        protected WriterBase(BoundConfiguration<T> config)
        {
            Config = config;
            Buffer = new MaxSizedBufferWriter(Config.MemoryPool, config.WriteBufferSizeHint);

            // buffering is configurable
            if (Config.WriteBufferSizeHint == 0)
            {
                Staging = null;
                InStaging = -1;
            }
            else
            {
                InStaging = 0;
                Staging = Config.MemoryPool.Rent(Config.WriteBufferSizeHint ?? MaxSizedBufferWriter.DEFAULT_STAGING_SIZE);
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
    }
}
