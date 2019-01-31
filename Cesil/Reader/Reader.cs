using System;
using System.Collections.Generic;
using System.IO;

namespace Cesil
{
    internal sealed class Reader<T>: ReaderBase<T>, IReader<T>, ITestableDisposable
        where T:new()
    {
        public bool IsDisposed => Inner == null;
        private TextReader Inner;

        internal Reader(TextReader inner, BoundConfiguration<T> config): base(config)
        {
            Inner = inner;
        }

        public List<T> ReadAll(List<T> into)
        {
            AssertNotDisposed();

            if (into == null)
            {
                Throw.ArgumentNullException(nameof(into));
            }

            while (TryRead(out var t))
            {
                into.Add(t);
            }

            return into;
        }

        public List<T> ReadAll()
        => ReadAll(new List<T>());

        public IEnumerable<T> EnumerateAll()
        {
            AssertNotDisposed();

            return EnumerateAll_Enumerable();

            // make the actually enumerable
            IEnumerable<T> EnumerateAll_Enumerable()
            {
                while (TryRead(out var t))
                {
                    yield return t;
                }
            }
        }

        public bool TryRead(out T record)
        {
            AssertNotDisposed();

            record = new T();
            return TryReadWithReuse(ref record);
        }

        public bool TryReadWithReuse(ref T record)
        {
            AssertNotDisposed();

            if (RowEndings == null)
            {
                HandleLineEndings();
            }

            if (ReadHeaders == null)
            {
                HandleHeaders();
            }

            while (true)
            {
                PreparingToWriteToBuffer();
                var available = Buffer.Read(Inner);
                if (available == 0)
                {
                    EndOfData();

                    if (HasValueToReturn)
                    {
                        record = GetValueForReturn();
                        return true;
                    }

                    // intentionally _not_ modifying record here
                    return false;
                }

                if (!HasValueToReturn)
                {
                    if(record == null)
                    {
                        record = new T();
                    }
                    SetValueToPopulate(record);
                }

                var res = AdvanceWork(available);
                if (res)
                {
                    record = GetValueForReturn();
                    return true;
                }
            }
        }

        private void HandleHeaders()
        {
            if (Configuration.ReadHeader == Cesil.ReadHeaders.Never)
            {
                // can just use the discovered copy from source
                ReadHeaders = Cesil.ReadHeaders.Never;
                TryMakeStateMachine();
                Columns = Configuration.DeserializeColumns;
                
                return;
            }

            var headerConfig =
                new BoundConfiguration<T>(
                    Configuration.NewCons,
                    Configuration.DeserializeColumns,
                    Array.Empty<Column>(),
                    Array.Empty<bool>(),
                    Configuration.ValueSeparator,
                    Configuration.EscapedValueStartAndStop,
                    Configuration.EscapeValueEscapeChar,
                    RowEndings.Value,
                    Configuration.ReadHeader,
                    Configuration.WriteHeader,
                    Configuration.WriteTrailingNewLine,
                    Configuration.MemoryPool,
                    Configuration.CommentChar,
                    null,
                    Configuration.ReadBufferSizeHint
                );

            using (
                var headerReader = new HeadersReader<T>(
                    headerConfig,
                    SharedCharacterLookup,
                    Inner,
                    Buffer
                )
            )
            {
                var headers = headerReader.Read();

                HandleHeadersReaderResult(headers);
            }
        }
        
        private void HandleLineEndings()
        {
            if (Configuration.RowEnding != Cesil.RowEndings.Detect)
            {
                RowEndings = Configuration.RowEnding;
                TryMakeStateMachine();
                return;
            }

            using (var detector = new RowEndingDetector<T>(Configuration, SharedCharacterLookup, Inner))
            {
                var res = detector.Detect();
                HandleLineEndingsDetectionResult(res);
            }
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                Inner.Dispose();
                Buffer.Dispose();
                Partial.Dispose();
                StateMachine?.Dispose();
                SharedCharacterLookup.Dispose();

                Inner = null;
            }
        }

        public void AssertNotDisposed()
        {
            if (IsDisposed)
            {
                Throw.ObjectDisposed(nameof(Reader<T>));
            }
        }
    }
}
