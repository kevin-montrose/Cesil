using System;
using System.Buffers;
using System.IO;
using System.Reflection;
using Xunit;

namespace Cesil.Tests
{
#pragma warning disable IDE1006
    public class ConfigurationTests
    {
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

        class _NoReadColumns
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

        class _NoWriteColumns
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

        private static void _SingleColumn<T>( string n)
        {
            var config = (ConcreteBoundConfiguration<T>)Configuration.For<T>();
            Assert.NotNull(config.NewCons);

            Assert.Collection(
                config.DeserializeColumns,
                c =>
                {
                    Assert.Equal(n, c.Name);
                }
            );
        }

        [Fact]
        public void OptionsValidation()
        {
            Assert.Throws<InvalidOperationException>(() => Options.Default.NewBuilder().WithValueSeparator(',').WithEscapedValueStartAndEnd(',').Build());

            Assert.Throws<InvalidOperationException>(() => Options.Default.NewBuilder().WithValueSeparator(',').WithCommentCharacter(',').Build());

            Assert.Throws<InvalidOperationException>(() => Options.Default.NewBuilder().WithEscapedValueStartAndEnd(',').WithCommentCharacter(',').Build());

            Assert.Throws<InvalidOperationException>(() => Options.Default.NewBuilder().WithRowEndingInternal(RowEndings.None).Build());
            Assert.Throws<InvalidOperationException>(() => Options.Default.NewBuilder().WithRowEndingInternal((RowEndings)99).Build());

            Assert.Throws<InvalidOperationException>(() => Options.Default.NewBuilder().WithReadHeaderInternal(ReadHeaders.None).Build());
            Assert.Throws<InvalidOperationException>(() => Options.Default.NewBuilder().WithReadHeaderInternal((ReadHeaders)99).Build());

            Assert.Throws<InvalidOperationException>(() => Options.Default.NewBuilder().WithWriteHeaderInternal(WriteHeaders.None).Build());
            Assert.Throws<InvalidOperationException>(() => Options.Default.NewBuilder().WithWriteHeaderInternal((WriteHeaders)99).Build());

            Assert.Throws<InvalidOperationException>(
                () =>
                    Options.NewEmptyBuilder().WithValueSeparator(',')
                    .WithRowEnding(RowEndings.CarriageReturnLineFeed)
                    .WithEscapedValueStartAndEnd('"')
                    .WithEscapedValueEscapeCharacter('"')
                    .WithReadHeader(ReadHeaders.Detect)
                    .WithWriteHeader(WriteHeaders.Always)
                    //.WithTypeDescriber(TypeDescribers.Default)
                    .WithWriteTrailingNewLine(WriteTrailingNewLines.Never)
                    .WithMemoryPool(MemoryPool<char>.Shared)
                    .WithWriteBufferSizeHint(null)
                    .WithCommentCharacter(null)
                    .WithReadBufferSizeHint(0)
                    .Build()
            );

            Assert.Throws<InvalidOperationException>(() => Options.Default.NewBuilder().WithWriteTrailingNewLineInternal(WriteTrailingNewLines.None).Build());
            Assert.Throws<InvalidOperationException>(() => Options.Default.NewBuilder().WithWriteTrailingNewLineInternal((WriteTrailingNewLines)99).Build());

            Assert.Throws<InvalidOperationException>(
                () =>
                    Options.NewEmptyBuilder().WithValueSeparator(',')
                    .WithRowEnding(RowEndings.CarriageReturnLineFeed)
                    .WithEscapedValueStartAndEnd('"')
                    .WithEscapedValueEscapeCharacter('"')
                    .WithReadHeader(ReadHeaders.Detect)
                    .WithWriteHeader(WriteHeaders.Always)
                    .WithTypeDescriber(TypeDescribers.Default)
                    .WithWriteTrailingNewLine(WriteTrailingNewLines.Never)
                    //.WithMemoryPool(MemoryPool<char>.Shared)
                    .WithWriteBufferSizeHint(null)
                    .WithCommentCharacter(null)
                    .WithReadBufferSizeHint(0)
                    .Build()
            );

            Assert.Throws<InvalidOperationException>(() => Options.Default.NewBuilder().WithWriteBufferSizeHintInternal(-1).Build());

            Assert.Throws<InvalidOperationException>(() => Options.Default.NewBuilder().WithReadBufferSizeHintInternal(-1).Build());

            Assert.Throws<InvalidOperationException>(() => Options.Default.NewBuilder().WithDynamicRowDisposalInternal(DynamicRowDisposal.None).Build());
            Assert.Throws<InvalidOperationException>(() => Options.Default.NewBuilder().WithDynamicRowDisposalInternal((DynamicRowDisposal)99).Build());
        }
    }
#pragma warning restore IDE1006
}
