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
            foreach(var state in Enum.GetValues(typeof(ReaderStateMachine.State)).Cast<ReaderStateMachine.State>())
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
            Assert.Throws<ArgumentException>(() => Configuration.ForDynamic(Options.DynamicDefault.NewBuilder().WithReadHeader(ReadHeaders.Detect).Build()));

            Assert.Throws<ArgumentNullException>(() => Configuration.For<_BadCreateCalls>(null));
            Assert.Throws<InvalidOperationException>(() => Configuration.For<object>());
        }

        class _OptionsEquality_MemoryPool : MemoryPool<char>
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
            foreach(DynamicRowDisposal drd in Enum.GetValues(typeof(DynamicRowDisposal)))
            foreach(var escapeChar in new char[] { '"', '\\'})
            foreach(var escapeStartChar in new char[] {  '"', '!'})
            foreach(var memPool in new [] {  MemoryPool<char>.Shared, new _OptionsEquality_MemoryPool() })
            foreach(var readHint in new [] { 1, 10 })
            foreach(ReadHeaders rh in Enum.GetValues(typeof(ReadHeaders)))
            foreach(RowEndings re in Enum.GetValues(typeof(RowEndings)))
            foreach(var typeDesc in new [] { TypeDescribers.Default, new ManualTypeDescriber()})
            foreach(var valSepChar in new char[] {  ',', ';'})
            foreach(var writeHint in new int? [] {  null, 10})
            foreach(WriteHeaders wh in Enum.GetValues(typeof(WriteHeaders)))
            foreach(WriteTrailingNewLines wt in Enum.GetValues(typeof(WriteTrailingNewLines)))
            {
                var builder = OptionsBuilder.NewEmptyBuilder();
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
                        .Build();

                opts.Add(opt);
            }

            for(var i = 0; i < opts.Count; i++)
            {
                var a = opts[i];

                var eqNull = a == null;
                var neqNull = a != null;

                Assert.False(eqNull);
                Assert.True(neqNull);

                for(var j = i; j < opts.Count; j++)
                {
                    var b = opts[j];

                    var eq = a == b;
                    var neq = a != b;
                    var hashEq = a.GetHashCode() == b.GetHashCode();

                    if(i == j)
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

            Assert.Throws<InvalidOperationException>(() => Options.Default.NewBuilder().WithRowEndingInternal(default).Build());
            Assert.Throws<InvalidOperationException>(() => Options.Default.NewBuilder().WithRowEndingInternal((RowEndings)99).Build());

            Assert.Throws<InvalidOperationException>(() => Options.Default.NewBuilder().WithReadHeaderInternal(default).Build());
            Assert.Throws<InvalidOperationException>(() => Options.Default.NewBuilder().WithReadHeaderInternal((ReadHeaders)99).Build());

            Assert.Throws<InvalidOperationException>(() => Options.Default.NewBuilder().WithWriteHeaderInternal(default).Build());
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

            Assert.Throws<InvalidOperationException>(() => Options.Default.NewBuilder().WithWriteTrailingNewLineInternal(default).Build());
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

            Assert.Throws<InvalidOperationException>(() => Options.Default.NewBuilder().WithDynamicRowDisposalInternal(default).Build());
            Assert.Throws<InvalidOperationException>(() => Options.Default.NewBuilder().WithDynamicRowDisposalInternal((DynamicRowDisposal)99).Build());

            Assert.Throws<InvalidOperationException>(() => Options.Default.NewBuilder().WithCommentCharacter('"').Build());

            Assert.Throws<ArgumentException>(() => Options.Default.NewBuilder().WithRowEnding(default));

            Assert.Throws<ArgumentException>(() => Options.Default.NewBuilder().WithReadHeader(default));

            Assert.Throws<ArgumentException>(() => Options.Default.NewBuilder().WithWriteHeader(default));

            Assert.Throws<ArgumentException>(() => Options.Default.NewBuilder().WithWriteTrailingNewLine(default));

            Assert.Throws<ArgumentException>(() => Options.Default.NewBuilder().WithWriteBufferSizeHint(-12));

            Assert.Throws<ArgumentException>(() => Options.Default.NewBuilder().WithReadBufferSizeHint(-12));

            Assert.Throws<ArgumentException>(() => Options.Default.NewBuilder().WithDynamicRowDisposal(default));
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
