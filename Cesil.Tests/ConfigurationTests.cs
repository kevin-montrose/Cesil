using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;

namespace Cesil.Tests
{
#pragma warning disable IDE1006
    public class ConfigurationTests
    {
        [Fact]
        public void TransitionMatrixInBounds()
        {
            foreach (var state in Enum.GetValues(typeof(ReaderStateMachine.State)).Cast<ReaderStateMachine.State>())
            {
                foreach (var type in Enum.GetValues(typeof(ReaderStateMachine.CharacterType)).Cast<ReaderStateMachine.CharacterType>())
                {
                    var ix = ReaderStateMachine.GetTransitionMatrixOffset(state, type);

                    Assert.True(ix >= 0);
                    Assert.True(ix < ReaderStateMachine.RuleCacheConfigSize);
                }
            }

        }

        [Fact]
        public void CharLookupInBounds()
        {
            // test that no combination of characters and state transitions
            //   will reach outside the size of the backing pin

            using (var c = CharacterLookup.MakeCharacterLookup(MemoryPool<char>.Shared, char.MinValue, char.MaxValue, 'c', 'd', out var maxSize1))
            using (var d = CharacterLookup.MakeCharacterLookup(MemoryPool<char>.Shared, '"', ',', '"', '#', out var maxSize2))
            {
                foreach (var state in Enum.GetValues(typeof(ReaderStateMachine.State)).Cast<ReaderStateMachine.State>())
                {
                    for (var x = 0; x <= char.MaxValue; x++)
                    {
                        var offset1 = ReaderStateMachine.GetCharLookupOffset(in c, state, (char)x);
                        if (offset1 != null)
                        {
                            Assert.True(offset1 >= 0);
                            Assert.True(offset1 < maxSize1);
                        }

                        var offset2 = ReaderStateMachine.GetCharLookupOffset(in d, state, (char)x);
                        if (offset2 != null)
                        {
                            Assert.True(offset2 >= 0);
                            Assert.True(offset2 < maxSize2);
                        }
                    }
                }
            }
        }

        [Fact]
        public void ConfigurationErrors()
        {
            Assert.Throws<ArgumentNullException>(() => Configuration.ForDynamic(null));
            Assert.Throws<ArgumentException>(() => Configuration.ForDynamic(Options.CreateBuilder(Options.DynamicDefault).WithReadHeader(ReadHeader.Detect).ToOptions()));

            Assert.Throws<ArgumentNullException>(() => Configuration.For<_BadCreateCalls>(null));
            Assert.Throws<InvalidOperationException>(() => Configuration.For<object>());
        }

        private class _OptionsEquality_MemoryPool : MemoryPool<char>
        {
            public override int MaxBufferSize => Shared.MaxBufferSize;

            public override IMemoryOwner<char> Rent(int minBufferSize = -1)
            => Shared.Rent(minBufferSize);

            protected override void Dispose(bool disposing) { }
        }

        [Fact]
        public void OptionsEquality()
        {
            var opts = new List<Options>();

            foreach (var commentChar in new char?[] { null, '#', })
                foreach (DynamicRowDisposal drd in Enum.GetValues(typeof(DynamicRowDisposal)))
                    foreach (var escapeChar in new char[] { '"', '\\' })
                        foreach (var escapeStartChar in new char[] { '"', '!' })
                            foreach (var memPool in new[] { MemoryPool<char>.Shared, new _OptionsEquality_MemoryPool() })
                                foreach (var readHint in new[] { 1, 10 })
                                    foreach (ReadHeader rh in Enum.GetValues(typeof(ReadHeader)))
                                        foreach (RowEnding re in Enum.GetValues(typeof(RowEnding)))
                                            foreach (var typeDesc in new[] { TypeDescribers.Default, ManualTypeDescriberBuilder.CreateBuilder().ToManualTypeDescriber() })
                                                foreach (var valSepChar in new char[] { ',', ';' })
                                                    foreach (var writeHint in new int?[] { null, 10 })
                                                        foreach (WriteHeader wh in Enum.GetValues(typeof(WriteHeader)))
                                                            foreach (WriteTrailingNewLine wt in Enum.GetValues(typeof(WriteTrailingNewLine)))
                                                            {
                                                                var builder = OptionsBuilder.CreateBuilder();
                                                                var opt =
                                                                    builder
                                                                        .WithCommentCharacter(commentChar)
                                                                        .WithDynamicRowDisposal(drd)
                                                                        .WithEscapedValueEscapeCharacter(escapeChar)
                                                                        .WithEscapedValueStartAndEnd(escapeStartChar)
                                                                        .WithMemoryPool(memPool)
                                                                        .WithReadBufferSizeHint(readHint)
                                                                        .WithReadHeader(rh)
                                                                        .WithRowEnding(re)
                                                                        .WithTypeDescriber(typeDesc)
                                                                        .WithValueSeparator(valSepChar)
                                                                        .WithWriteBufferSizeHint(writeHint)
                                                                        .WithWriteHeader(wh)
                                                                        .WithWriteTrailingNewLine(wt)
                                                                        .ToOptions();

                                                                opts.Add(opt);
                                                            }

            for (var i = 0; i < opts.Count; i++)
            {
                var a = opts[i];

                var eqNull = a == null;
                var neqNull = a != null;

                Assert.False(eqNull);
                Assert.True(neqNull);

                for (var j = i; j < opts.Count; j++)
                {
                    var b = opts[j];

                    var eq = a == b;
                    var neq = a != b;
                    var hashEq = a.GetHashCode() == b.GetHashCode();

                    if (i == j)
                    {
                        Assert.True(eq);
                        Assert.False(neq);
                        Assert.True(hashEq);
                    }
                    else
                    {
                        Assert.False(eq);
                        Assert.True(neq);
                    }
                }
            }
        }

        private class _NoColumns { }

        [Fact]
        public void NoColumns()
        {
            Assert.Throws<InvalidOperationException>(
                delegate
                {
                    Configuration.For<_NoColumns>();
                }
            );
        }

        private class _NoReadColumns
        {
            public string Foo { private set; get; }
        }

        [Fact]
        public void NoReadColumns()
        {
            // sync
            Assert.Throws<InvalidOperationException>(
                delegate
                {
                    var config = Configuration.For<_NoReadColumns>();

                    config.CreateReader(TextReader.Null);
                }
            );

            // async
            Assert.Throws<InvalidOperationException>(
                delegate
                {
                    var config = Configuration.For<_NoReadColumns>();

                    config.CreateAsyncReader(TextReader.Null);
                }
            );
        }

        private class _NoWriteColumns
        {
            public string Bar { private get; set; }
        }

        [Fact]
        public void NoWriteColumns()
        {
            // sync
            Assert.Throws<InvalidOperationException>(
                delegate
                {
                    var config = Configuration.For<_NoWriteColumns>();

                    config.CreateWriter(TextWriter.Null);
                }
            );

            // async
            Assert.Throws<InvalidOperationException>(
                delegate
                {
                    var config = Configuration.For<_NoWriteColumns>();

                    config.CreateAsyncWriter(TextWriter.Null);
                }
            );
        }

        private class _SingleColumn_Int
        {
            public int Col { get; set; }
        }

        private class _SingleColumn_Float
        {
            public double ColF { get; set; }
        }

        private class _SingleColumn_String
        {
            public string ColS { get; set; }
        }

        private class _SingleColumn_Date
        {
            public DateTime ColDt { get; set; }
        }

        [Theory]
        [InlineData(typeof(_SingleColumn_Int), nameof(_SingleColumn_Int.Col))]
        [InlineData(typeof(_SingleColumn_Float), nameof(_SingleColumn_Float.ColF))]
        [InlineData(typeof(_SingleColumn_String), nameof(_SingleColumn_String.ColS))]
        [InlineData(typeof(_SingleColumn_Date), nameof(_SingleColumn_Date.ColDt))]
        internal void SingleColumn(Type t, string n)
        {
            var mtd = this.GetType().GetMethod(nameof(_SingleColumn), BindingFlags.Static | BindingFlags.NonPublic);
            var mtdGen = mtd.MakeGenericMethod(t);
            mtdGen.Invoke(null, new object[] { n });
        }

        private static void _SingleColumn<T>(string n)
        {
            var config = (ConcreteBoundConfiguration<T>)Configuration.For<T>();
            Assert.True(config.NewCons.HasValue);

            Assert.Collection(
                config.DeserializeColumns,
                c =>
                {
                    Assert.Equal(n, c.Name.Value);
                }
            );
        }

        [Fact]
        public void OptionsValidation()
        {
            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithValueSeparator(',').WithEscapedValueStartAndEnd(',').ToOptions());

            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithValueSeparator(',').WithCommentCharacter(',').ToOptions());

            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithEscapedValueStartAndEnd(',').WithCommentCharacter(',').ToOptions());

            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithRowEndingInternal(default).ToOptions());
            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithRowEndingInternal((RowEnding)99).ToOptions());

            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithReadHeaderInternal(default).ToOptions());
            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithReadHeaderInternal((ReadHeader)99).ToOptions());

            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithWriteHeaderInternal(default).ToOptions());
            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithWriteHeaderInternal((WriteHeader)99).ToOptions());

            Assert.Throws<InvalidOperationException>(
                () =>
                    Options.CreateBuilder().WithValueSeparator(',')
                    .WithRowEnding(RowEnding.CarriageReturnLineFeed)
                    .WithEscapedValueStartAndEnd('"')
                    .WithEscapedValueEscapeCharacter('"')
                    .WithReadHeader(ReadHeader.Detect)
                    .WithWriteHeader(WriteHeader.Always)
                    //.WithTypeDescriber(TypeDescribers.Default)
                    .WithWriteTrailingNewLine(WriteTrailingNewLine.Never)
                    .WithMemoryPool(MemoryPool<char>.Shared)
                    .WithWriteBufferSizeHint(null)
                    .WithCommentCharacter(null)
                    .WithReadBufferSizeHint(0)
                    .ToOptions()
            );

            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithWriteTrailingNewLineInternal(default).ToOptions());
            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithWriteTrailingNewLineInternal((WriteTrailingNewLine)99).ToOptions());

            Assert.Throws<InvalidOperationException>(
                () =>
                    Options.CreateBuilder().WithValueSeparator(',')
                    .WithRowEnding(RowEnding.CarriageReturnLineFeed)
                    .WithEscapedValueStartAndEnd('"')
                    .WithEscapedValueEscapeCharacter('"')
                    .WithReadHeader(ReadHeader.Detect)
                    .WithWriteHeader(WriteHeader.Always)
                    .WithTypeDescriber(TypeDescribers.Default)
                    .WithWriteTrailingNewLine(WriteTrailingNewLine.Never)
                    //.WithMemoryPool(MemoryPool<char>.Shared)
                    .WithWriteBufferSizeHint(null)
                    .WithCommentCharacter(null)
                    .WithReadBufferSizeHint(0)
                    .ToOptions()
            );

            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithWriteBufferSizeHintInternal(-1).ToOptions());

            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithReadBufferSizeHintInternal(-1).ToOptions());

            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithDynamicRowDisposalInternal(default).ToOptions());
            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithDynamicRowDisposalInternal((DynamicRowDisposal)99).ToOptions());

            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithCommentCharacter('"').ToOptions());

            Assert.Throws<ArgumentException>(() => Options.CreateBuilder(Options.Default).WithRowEnding(default));

            Assert.Throws<ArgumentException>(() => Options.CreateBuilder(Options.Default).WithReadHeader(default));

            Assert.Throws<ArgumentException>(() => Options.CreateBuilder(Options.Default).WithWriteHeader(default));

            Assert.Throws<ArgumentException>(() => Options.CreateBuilder(Options.Default).WithWriteTrailingNewLine(default));

            Assert.Throws<ArgumentException>(() => Options.CreateBuilder(Options.Default).WithWriteBufferSizeHint(-12));

            Assert.Throws<ArgumentException>(() => Options.CreateBuilder(Options.Default).WithReadBufferSizeHint(-12));

            Assert.Throws<ArgumentException>(() => Options.CreateBuilder(Options.Default).WithDynamicRowDisposal(default));
        }

        private class _BadCreateCalls
        {
            public int Foo { get; set; }
        }

        [Fact]
        public void BadCreateCalls()
        {
            // concrete types
            {
                var opts = Configuration.For<_BadCreateCalls>();

                Assert.Throws<ArgumentNullException>(() => opts.CreateAsyncReader(null));
                Assert.Throws<ArgumentNullException>(() => opts.CreateAsyncReader(default, Encoding.UTF8));
                Assert.Throws<ArgumentNullException>(() => opts.CreateAsyncReader(new Pipe().Reader, null));
                Assert.Throws<ArgumentNullException>(() => opts.CreateAsyncWriter(null));
                Assert.Throws<ArgumentNullException>(() => opts.CreateAsyncWriter(default, Encoding.UTF8));
                Assert.Throws<ArgumentNullException>(() => opts.CreateAsyncWriter(new Pipe().Writer, null));
                Assert.Throws<ArgumentNullException>(() => opts.CreateReader(null));
                Assert.Throws<ArgumentNullException>(() => opts.CreateReader(default, null));
                Assert.Throws<ArgumentNullException>(() => opts.CreateWriter(default(TextWriter)));
                Assert.Throws<ArgumentNullException>(() => opts.CreateWriter(default(IBufferWriter<char>)));
                Assert.Throws<ArgumentNullException>(() => opts.CreateWriter(null, Encoding.UTF8));
                Assert.Throws<ArgumentNullException>(() => opts.CreateWriter(new Pipe().Writer, null));
            }

            // dynamic
            {
                var opts = Configuration.ForDynamic();

                Assert.Throws<ArgumentNullException>(() => opts.CreateAsyncReader(null));
                Assert.Throws<ArgumentNullException>(() => opts.CreateAsyncReader(default, Encoding.UTF8));
                Assert.Throws<ArgumentNullException>(() => opts.CreateAsyncReader(new Pipe().Reader, null));
                Assert.Throws<ArgumentNullException>(() => opts.CreateAsyncWriter(null));
                Assert.Throws<ArgumentNullException>(() => opts.CreateAsyncWriter(default, Encoding.UTF8));
                Assert.Throws<ArgumentNullException>(() => opts.CreateAsyncWriter(new Pipe().Writer, null));
                Assert.Throws<ArgumentNullException>(() => opts.CreateReader(null));
                Assert.Throws<ArgumentNullException>(() => opts.CreateReader(default, null));
                Assert.Throws<ArgumentNullException>(() => opts.CreateWriter(default(TextWriter)));
                Assert.Throws<ArgumentNullException>(() => opts.CreateWriter(default(IBufferWriter<char>)));
                Assert.Throws<ArgumentNullException>(() => opts.CreateWriter(null, Encoding.UTF8));
                Assert.Throws<ArgumentNullException>(() => opts.CreateWriter(new Pipe().Writer, null));
            }
        }
    }
#pragma warning restore IDE1006
}
