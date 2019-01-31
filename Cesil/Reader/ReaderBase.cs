using System;

namespace Cesil
{
    internal abstract class ReaderBase<T>
        where T : new()
    {
        // try and size the buffers so we get a whole page to ourselves
        private const int OVERHEAD_BYTES = 16;
        private const int PAGE_SIZE_BYTES = 4098;
        internal const int DEFAULT_BUFFER_SIZE = (PAGE_SIZE_BYTES / sizeof(char)) - OVERHEAD_BYTES;

        internal readonly BufferWithPushback Buffer;
        internal readonly Partial<T> Partial;

        internal readonly ReaderStateMachine.CharacterLookup SharedCharacterLookup;

        internal ReaderStateMachine StateMachine;

        internal BoundConfiguration<T> Configuration { get; }
        internal Column[] Columns;

        internal RowEndings? RowEndings { get; set; }
        internal ReadHeaders? ReadHeaders { get; set; }

        internal bool HasValueToReturn => Partial.HasPending;

        protected ReaderBase(BoundConfiguration<T> config)
        {
            Configuration = config;
            
            var bufferSize = config.ReadBufferSizeHint;
            if(bufferSize == 0)
            {
                bufferSize = DEFAULT_BUFFER_SIZE;
            }

            Buffer = new BufferWithPushback(config.MemoryPool, bufferSize);
            Partial = new Partial<T>(config.MemoryPool);

            SharedCharacterLookup =
                ReaderStateMachine.MakeCharacterLookup(
                    config.MemoryPool,
                    config.EscapedValueStartAndStop,
                    config.ValueSeparator,
                    config.EscapeValueEscapeChar,
                    config.CommentChar
                );
        }
        
        protected internal bool AdvanceWork(int numInBuffer)
        {
            var res = ProcessBuffer(numInBuffer, out var pushBack);
            if (pushBack > 0)
            {
                PreparingToWriteToBuffer();
                Buffer.PushBackFromBuffer(numInBuffer, pushBack);
            }

            return res;
        }

        private bool ProcessBuffer(int bufferLen, out int unprocessedCharacters)
        {
            var buffSpan = Buffer.Buffer.Span;

            ReaderStateMachine.AdvanceResult? inBatchableResult = null;
            var consistentResultSince = -1;

            for (var i = 0; i < bufferLen; i++)
            {
                var c = buffSpan[i];
                var res = StateMachine.Advance(c);

                // try and batch skips and appends
                //   to save time on copying AND on 
                //   basically pointless method calls
                if (inBatchableResult != null)
                {
                    if (res == inBatchableResult)
                    {
                        continue;
                    }
                    else
                    {
                        switch(inBatchableResult.Value)
                        {
                            case ReaderStateMachine.AdvanceResult.Skip_Character:

                                // there's no distinction between skipping several characters and skipping one
                                //    so this doesn't need the length
                                Partial.SkipCharacter();
                                break;
                            case ReaderStateMachine.AdvanceResult.Append_Character:
                                var length = i - consistentResultSince;

                                Partial.AppendCharacters(buffSpan, consistentResultSince, length);
                                break;
                            default:
                                Throw.Exception($"Unexpected {nameof(ReaderStateMachine.AdvanceResult)}: {inBatchableResult.Value}");
                                break;
                        }

                        inBatchableResult = null;
                        consistentResultSince = -1;

                        // fall through into the switch to handle the current character
                    }
                }
        
                switch (res)
                {
                    case ReaderStateMachine.AdvanceResult.Skip_Character:
                        if(inBatchableResult == null)
                        {
                            inBatchableResult = ReaderStateMachine.AdvanceResult.Skip_Character;
                            consistentResultSince = i;
                            continue;
                        }

                        Partial.SkipCharacter();
                        break;

                    case ReaderStateMachine.AdvanceResult.Append_Character:
                        if (inBatchableResult == null)
                        {
                            inBatchableResult = ReaderStateMachine.AdvanceResult.Append_Character;
                            consistentResultSince = i;
                            continue;
                        }

                        Partial.AppendCharacters(buffSpan, i, 1);
                        break;

                    case ReaderStateMachine.AdvanceResult.Finished_Value:
                        PushPendingCharactersToValue();
                        break;
                    case ReaderStateMachine.AdvanceResult.Finished_Record:
                        if (Partial.PendingCharsCount > 0)
                        {
                            PushPendingCharactersToValue();
                        }

                        unprocessedCharacters = bufferLen - i - 1;
                        return true;

                    case ReaderStateMachine.AdvanceResult.Exception_ExpectedEndOfRecord:
                        Throw.InvalidOperation($"Encountered '{c}' when expecting end of record");
                        break;
                    case ReaderStateMachine.AdvanceResult.Exception_InvalidState:
                        Throw.InvalidOperation($"Internal state machine is in an invalid state due to a previous error");
                        break;
                    case ReaderStateMachine.AdvanceResult.Exception_StartEscapeInValue:
                        Throw.InvalidOperation($"Encountered '{c}', starting an escaped value, when already in a value");
                        break;
                    case ReaderStateMachine.AdvanceResult.Exception_UnexpectedCharacterInEscapeSequence:
                        Throw.InvalidOperation($"Encountered '{c}' in an escape sequence, which is invalid");
                        break;
                    case ReaderStateMachine.AdvanceResult.Exception_UnexpectedLineEnding:
                        Throw.Exception($"Unexpected {nameof(Cesil.RowEndings)} value encountered");
                        break;
                    case ReaderStateMachine.AdvanceResult.Exception_UnexpectedState:
                        Throw.Exception($"Unexpected state value entered");
                        break;
                    case ReaderStateMachine.AdvanceResult.Exception_ExpectedEndOfRecordOrValue:
                        Throw.InvalidOperation($"Encountered '{c}' when expecting the end of a record or value");
                        break;

                    default:
                        Throw.Exception($"Unexpected {nameof(ReaderStateMachine.AdvanceResult)}: {res}");
                        break;
                }
            }

            // handle any batch that was still pending
            if (inBatchableResult != null)
            {
                switch (inBatchableResult.Value)
                {
                    case ReaderStateMachine.AdvanceResult.Skip_Character:

                        // there's no distinction between skipping several characters and skipping one
                        //    so this doesn't need the length
                        Partial.SkipCharacter();
                        break;
                    case ReaderStateMachine.AdvanceResult.Append_Character:
                        // we read all the up to the end, so length needs to include the last character
                        var length = bufferLen - consistentResultSince;

                        Partial.AppendCharacters(buffSpan, consistentResultSince, length);
                        break;
                    default:
                        Throw.Exception($"Unexpected {nameof(ReaderStateMachine.AdvanceResult)}: {inBatchableResult.Value}");
                        break;
                }
            }

            unprocessedCharacters = 0;
            return false;
        }

        protected internal void EndOfData()
        {
            var state = StateMachine.CurrentState;
            var inComment = (((byte)state) & ReaderStateMachine.IN_COMMENT_MASK) == ReaderStateMachine.IN_COMMENT_MASK;

            if (inComment)
            {
                Partial.ClearValue();
                return;
            }

            if (HasValueToReturn)
            {
                if (Partial.PendingCharsCount > 0)
                {
                    PushPendingCharactersToValue();
                }
            }
        }

        protected internal T GetValueForReturn()
        {
            for (var i = Partial.CurrentColumnIndex; i < Columns.Length; i++)
            {
                var col = Columns[i];
                if (col.IsRequired)
                {
                    Throw.SerializationException($"Column [{col.Name}] is required, but was not found in row");
                }
            }

            var ret = Partial.Value;
            Partial.ClearValue();
            return ret;
        }

        protected void SetValueToPopulate(T val)
        {
            Partial.SetValueAndResetColumn(val);
        }

        protected internal void PreparingToWriteToBuffer()
        {
            Partial.BufferToBeReused(Buffer.Buffer.Span);
        }

        private void PushPendingCharactersToValue()
        {
            if (Partial.CurrentColumnIndex >= Columns.Length)
            {
                Throw.InvalidOperation($"Unexpected column (Index={Partial.CurrentColumnIndex})");
            }

            var dataSpan = Partial.PendingAsMemory(Buffer.Buffer);

            var column = Columns[Partial.CurrentColumnIndex];

            if(column.IsRequired && dataSpan.Length == 0)
            {
                Throw.SerializationException($"Column [{column.Name}] is required, but was not found in row");
            }

            if(!column.Set(dataSpan.Span, Partial.Value))
            {
                Throw.InvalidOperation($"Could not assign value \"{Partial.PendingAsString(Buffer.Buffer)}\" to column \"{column.Name}\" (Index={Partial.CurrentColumnIndex})");
            }

            Partial.ClearBufferAndAdvanceColumnIndex();
        }

        protected void HandleHeadersReaderResult((HeadersReader<T>.HeaderEnumerator Headers, bool IsHeader, Memory<char> PushBack) headers)
        {
            if (!headers.IsHeader)
            {
                if (Configuration.ReadHeader == Cesil.ReadHeaders.Always)
                {
                    Throw.InvalidOperation("First row of input was not a row of headers");
                    return;
                }
            }

            // what are we _actually_ doing?
            this.ReadHeaders = headers.IsHeader ? Cesil.ReadHeaders.Always : Cesil.ReadHeaders.Never;
            TryMakeStateMachine();

            if (this.ReadHeaders == Cesil.ReadHeaders.Always)
            {
                var columnsInDiscoveredOrder = new Column[headers.Headers.Count];
                foreach (var col in Configuration.DeserializeColumns)
                {
                    var isRequired = col.IsRequired;
                    var found = false;

                    using (var e = headers.Headers)
                    {
                        var i = 0;
                        while(e.MoveNext())
                        {
                            var header = e.Current;
                            var colNameMem = col.Name.AsMemory();
                            if (Utils.AreEqual(colNameMem, header))
                            {
                                columnsInDiscoveredOrder[i] = col;
                                found = true;
                                break;
                            }

                            i++;
                        }
                    }

                    if(isRequired && !found)
                    {
                        Throw.SerializationException($"Column [{col.Name}] is required, but was not found in the header");
                    }
                }

                for (var i = 0; i < columnsInDiscoveredOrder.Length; i++)
                {
                    if (columnsInDiscoveredOrder[i] == null)
                    {
                        columnsInDiscoveredOrder[i] = Column.Ignored;
                    }
                }
                
                Columns = columnsInDiscoveredOrder;
            }
            else
            {
                Columns = Configuration.DeserializeColumns;
            }

            Buffer.PushBackFromOutsideBuffer(headers.PushBack);
        }

        protected void HandleLineEndingsDetectionResult((RowEndings Ending, Memory<char> PushBack)? res)
        {
            if (res == null)
            {
                Throw.InvalidOperation($"Unable to automatically detect row endings");
            }

            RowEndings = res.Value.Ending;
            TryMakeStateMachine();

            Buffer.PushBackFromOutsideBuffer(res.Value.PushBack);
        }

        internal void TryMakeStateMachine()
        {
            if (StateMachine != null) return;

            if (RowEndings == null || ReadHeaders == null) return;

            StateMachine =
                new ReaderStateMachine(
                    SharedCharacterLookup,
                    Configuration.EscapedValueStartAndStop,
                    Configuration.EscapeValueEscapeChar,
                    RowEndings.Value,
                    ReadHeaders.Value
                );
        }
    }
}
