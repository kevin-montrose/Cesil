﻿using System;
using System.Buffers;

namespace Cesil
{
    internal sealed class ReadOnlyCharSegment : ReadOnlySequenceSegment<char>
    {
        internal ReadOnlyCharSegment(ReadOnlyMemory<char> allocation, int bytesUsed)
        {
            Memory = allocation.Slice(0, bytesUsed);
            RunningIndex = 0;
            Next = null;
        }

        internal ReadOnlyCharSegment Append(ReadOnlyMemory<char> allocation, int bytesUsed)
        {
            var ret =
                new ReadOnlyCharSegment(allocation, bytesUsed)
                {
                    RunningIndex = RunningIndex + Memory.Length
                };
            Next = ret;

            return ret;
        }
    }
}
