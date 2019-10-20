using System;
using System.Buffers;
using System.Text;

namespace Cesil
{
    internal abstract class ByteSequenceAdapterBase
    {
        internal bool IsComplete;
        private readonly Decoder Decoder;

        internal ByteSequenceAdapterBase(Encoding encoding)
        {
            Decoder = encoding.GetDecoder();
        }

        internal void MapByteSequenceToChar(
            ReadOnlySequence<byte> buffer,
            bool endOfBuffer,
            Span<char> into,
            out long processedBytes,
            out long examinedBytes,
            out int countChars
        )
        {
            var handledAny = false;

            var copyTo = into;
            var handledAll = true;

            var handledBytes = 0;
            var readChars = 0;

            foreach (var seq in buffer)
            {
                if (copyTo.IsEmpty)
                {
                    handledAll = false;
                    break;
                }

                var seqBytes = seq.Span;
                Decoder.Convert(seqBytes, copyTo, false, out var bytesUsed, out var charsUsed, out _);

                if (bytesUsed > 0)
                {
                    handledAny = true;
                }

                handledBytes += bytesUsed;
                readChars += charsUsed;

                copyTo = copyTo.Slice(charsUsed);

                if (bytesUsed != seqBytes.Length)
                {
                    handledAll = false;
                    break;
                }
            }

            // if we read the whole buffer, but couldn't extract a single char then we need to wait for more data to come in,
            //    so mark the whole buffer as examined
            // otherwise, we just ran out of space and want the next call to immediately return the old data
            var examined = handledAny ? handledBytes : buffer.Length;

            processedBytes = handledBytes;
            examinedBytes = examined;

            if (handledAll)
            {
                // record if we're done for the next caller
                IsComplete = endOfBuffer;
            }

            countChars = readChars;
        }
    }
}
