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
        private sealed class _BadTypeDescribers_Row3
        {
            public string Foo { get; set; }

            public _BadTypeDescribers_Row3(string foo)
            {
                Foo = foo;
            }
        }

        private sealed class _BadTypeDescribers_Row2
        {
            public string Foo { get; set; }
            public string Bar { get; set; }

            public _BadTypeDescribers_Row2(string foo)
            {
                Foo = foo;
            }
        }

        private sealed class _BadTypeDescribers_Row
        {
            public int Foo { get; set; }
        }

        private sealed class _BadTypeDescribers : ITypeDescriber
        {
            private readonly InstanceProvider InstanceProvider;
            private readonly IEnumerable<DeserializableMember> DeserializableMembers;
            private readonly IEnumerable<SerializableMember> SerializableMembers;

            public _BadTypeDescribers(InstanceProvider ip, IEnumerable<DeserializableMember> dm, IEnumerable<SerializableMember> sm)
            {
                InstanceProvider = ip;
                DeserializableMembers = dm;
                SerializableMembers = sm;
            }

            public IEnumerable<DeserializableMember> EnumerateMembersToDeserialize(TypeInfo forType)
            => DeserializableMembers;

            public IEnumerable<SerializableMember> EnumerateMembersToSerialize(TypeInfo forType)
            => SerializableMembers;

            public int GetCellsForDynamicRow(in WriteContext context, object row, Span<DynamicCellValue> cells)
            => 0;

            public Parser GetDynamicCellParserFor(in ReadContext context, TypeInfo targetType)
            => null;

            public DynamicRowConverter GetDynamicRowConverter(in ReadContext context, IEnumerable<ColumnIdentifier> columns, TypeInfo targetType)
            => null;

            public InstanceProvider GetInstanceProvider(TypeInfo forType)
            => InstanceProvider;
        }

        [Fact]
        public void BadTypeDescribers()
        {
            var t = typeof(_BadTypeDescribers_Row).GetTypeInfo();

            // null provider, non-null columns
            {
                var describer = new _BadTypeDescribers(null, TypeDescribers.Default.EnumerateMembersToDeserialize(t), TypeDescribers.Default.EnumerateMembersToSerialize(t));
                var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(describer).ToOptions();

                Assert.Throws<InvalidOperationException>(() => Configuration.For<_BadTypeDescribers_Row>(opts));
            }

            // null deserialize
            {
                var describer = new _BadTypeDescribers(TypeDescribers.Default.GetInstanceProvider(t), null, TypeDescribers.Default.EnumerateMembersToSerialize(t));
                var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(describer).ToOptions();

                Assert.Throws<InvalidOperationException>(() => Configuration.For<_BadTypeDescribers_Row>(opts));
            }

            // null serialize
            {
                var describer = new _BadTypeDescribers(TypeDescribers.Default.GetInstanceProvider(t), TypeDescribers.Default.EnumerateMembersToDeserialize(t), null);
                var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(describer).ToOptions();

                Assert.Throws<InvalidOperationException>(() => Configuration.For<_BadTypeDescribers_Row>(opts));
            }

            // missing constructor parameter
            {
                var t2 = typeof(_BadTypeDescribers_Row2).GetTypeInfo();

                var cons = t2.GetConstructor(new[] { typeof(string) });
                var ip = InstanceProvider.ForConstructorWithParameters(cons);
                var ds = TypeDescribers.Default.EnumerateMembersToDeserialize(t2);

                var describer = new _BadTypeDescribers(ip, ds, TypeDescribers.Default.EnumerateMembersToSerialize(t2));
                var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(describer).ToOptions();

                Assert.Throws<InvalidOperationException>(() => Configuration.For<_BadTypeDescribers_Row2>(opts));
            }

            // constructor parameter, not constructor provider
            {
                var t2 = typeof(_BadTypeDescribers_Row2).GetTypeInfo();

                var cons = t2.GetConstructor(new[] { typeof(string) });
                var p = cons.GetParameters().Single();
                var s = Setter.ForConstructorParameter(p);
                var dm = DeserializableMember.Create(t, "foo", s, Parser.GetDefault(typeof(string).GetTypeInfo()), MemberRequired.Yes, null);

                var ip = TypeDescribers.Default.GetInstanceProvider(t);
                var ds = TypeDescribers.Default.EnumerateMembersToDeserialize(t).Concat(new[] { dm }).ToArray();

                var describer = new _BadTypeDescribers(ip, ds, TypeDescribers.Default.EnumerateMembersToSerialize(t));
                var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(describer).ToOptions();

                Assert.Throws<InvalidOperationException>(() => Configuration.For<_BadTypeDescribers_Row>(opts));
            }

            // parameter for different constructor
            {
                var t2 = typeof(_BadTypeDescribers_Row2).GetTypeInfo();
                var t3 = typeof(_BadTypeDescribers_Row3).GetTypeInfo();

                var cons2 = t2.GetConstructor(new[] { typeof(string) });
                var p2 = cons2.GetParameters().Single();
                var s2 = Setter.ForConstructorParameter(p2);
                var dm2 = DeserializableMember.Create(t, "bar", s2, Parser.GetDefault(typeof(string).GetTypeInfo()), MemberRequired.Yes, null);

                var cons3 = t3.GetConstructor(new[] { typeof(string) });
                var p3 = cons3.GetParameters().Single();
                var s3 = Setter.ForConstructorParameter(p3);
                var dm3 = DeserializableMember.Create(t, "foo", s3, Parser.GetDefault(typeof(string).GetTypeInfo()), MemberRequired.Yes, null);

                var ip = InstanceProvider.ForConstructorWithParameters(cons2);
                var ds = new[] { dm2, dm3 };

                var describer = new _BadTypeDescribers(ip, ds, TypeDescribers.Default.EnumerateMembersToSerialize(t2));
                var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(describer).ToOptions();

                Assert.Throws<InvalidOperationException>(() => Configuration.For<_BadTypeDescribers_Row2>(opts));
            }
        }

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

            var cOpts =
                Options.CreateBuilder(Options.Default)
                    .WithEscapedValueStartAndEnd(char.MinValue)
                    .WithValueSeparator(char.MaxValue.ToString())
                    .WithEscapedValueEscapeCharacter('c')
                    .WithCommentCharacter('d')
                    .ToOptions();

            var dOpts =
                Options.CreateBuilder(Options.Default)
                    .WithEscapedValueStartAndEnd('"')
                    .WithValueSeparator(','.ToString())
                    .WithEscapedValueEscapeCharacter('"')
                    .WithCommentCharacter('#')
                    .ToOptions();

            var eOpts =
                Options.CreateBuilder(cOpts)
                    .WithWhitespaceTreatment(WhitespaceTreatments.Trim)
                    .ToOptions();

            var fOpts =
                Options.CreateBuilder(dOpts)
                    .WithWhitespaceTreatment(WhitespaceTreatments.Trim)
                    .ToOptions();

            using (var c = CharacterLookup.MakeCharacterLookup(cOpts, out var maxSize1))
            using (var d = CharacterLookup.MakeCharacterLookup(dOpts, out var maxSize2))
            using (var e = CharacterLookup.MakeCharacterLookup(eOpts, out var maxSize3))
            using (var f = CharacterLookup.MakeCharacterLookup(fOpts, out var maxSize4))
            {
                for (var x = 0; x <= char.MaxValue; x++)
                {
                    var offset1 = ReaderStateMachine.GetCharLookupOffset(in c, (char)x);
                    if (offset1 != null)
                    {
                        Assert.True(offset1 >= 0);
                        Assert.True(offset1 < maxSize1);
                    }

                    var offset2 = ReaderStateMachine.GetCharLookupOffset(in d, (char)x);
                    if (offset2 != null)
                    {
                        Assert.True(offset2 >= 0);
                        Assert.True(offset2 < maxSize2);
                    }

                    var offset3 = ReaderStateMachine.GetCharLookupOffset(in e, (char)x);
                    if (offset3 != null)
                    {
                        Assert.True(offset3 >= 0);
                        Assert.True(offset3 < maxSize3);
                    }

                    var offset4 = ReaderStateMachine.GetCharLookupOffset(in f, (char)x);
                    if (offset4 != null)
                    {
                        Assert.True(offset4 >= 0);
                        Assert.True(offset4 < maxSize4);
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

        private sealed class _OptionsEquality_ArrayPool : ArrayPool<DynamicCellValue>
        {
            public override DynamicCellValue[] Rent(int minimumLength)
            => Shared.Rent(minimumLength);

            public override void Return(DynamicCellValue[] array, bool clearArray = false)
            => Shared.Return(array, clearArray);
        }

        [Fact]
        public void OptionsEquality()
        {
            var opts = new List<Options>();

            foreach (var commentChar in new char?[] { null, '#', })
                foreach (var drd in new[] { DynamicRowDisposal.OnExplicitDispose, DynamicRowDisposal.OnReaderDispose })
                    foreach (var escapeChar in new char[] { '"', '\\' })
                        foreach (var escapeStartChar in new char[] { '"', '!' })
                            foreach (var memPool in new[] { MemoryPool<char>.Shared, new _OptionsEquality_MemoryPool() })
                                foreach (var readHint in new[] { 1, 10 })
                                    foreach (var rh in new[] { ReadHeader.Always, ReadHeader.Never })
                                        foreach (var re in new[] { RowEnding.CarriageReturn, RowEnding.Detect })
                                            foreach (var typeDesc in new[] { TypeDescribers.Default, ManualTypeDescriberBuilder.CreateBuilder().ToManualTypeDescriber() })
                                                foreach (var valSepChar in new char[] { ',', ';' })
                                                    foreach (var writeHint in new int?[] { null, 10 })
                                                        foreach (var wh in new[] { WriteHeader.Always, WriteHeader.Never })
                                                            foreach (var wt in new[] { WriteTrailingRowEnding.Always, WriteTrailingRowEnding.Never })
                                                                foreach (var ect in new[] { ExtraColumnTreatment.Ignore, ExtraColumnTreatment.ThrowException })
                                                                    foreach (var ap in new[] { ArrayPool<DynamicCellValue>.Shared, new _OptionsEquality_ArrayPool() })
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
                                                                                .WithValueSeparator(valSepChar.ToString())
                                                                                .WithWriteBufferSizeHint(writeHint)
                                                                                .WithWriteHeader(wh)
                                                                                .WithWriteTrailingRowEnding(wt)
                                                                                .WithExtraColumnTreatment(ect)
                                                                                .WithArrayPool(ap)
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
            Assert.True(config.RowBuilder.HasValue);

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
            Assert.Throws<ArgumentNullException>(() => Options.CreateBuilder(Options.Default).WithValueSeparator(null));
            Assert.Throws<ArgumentException>(() => Options.CreateBuilder(Options.Default).WithValueSeparator(""));

            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithValueSeparatorInternal(null).ToOptions());
            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithValueSeparatorInternal("").ToOptions());

            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithValueSeparator(','.ToString()).WithEscapedValueStartAndEnd(',').ToOptions());

            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithValueSeparator(','.ToString()).WithCommentCharacter(',').ToOptions());

            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithEscapedValueStartAndEnd(',').WithCommentCharacter(',').ToOptions());

            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithRowEndingInternal(default).ToOptions());
            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithRowEndingInternal((RowEnding)99).ToOptions());

            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithReadHeaderInternal(default).ToOptions());
            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithReadHeaderInternal((ReadHeader)99).ToOptions());

            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithWriteHeaderInternal(default).ToOptions());
            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithWriteHeaderInternal((WriteHeader)99).ToOptions());

            Assert.Throws<InvalidOperationException>(
                () =>
                    Options.CreateBuilder().WithValueSeparator(','.ToString())
                    .WithRowEnding(RowEnding.CarriageReturnLineFeed)
                    .WithEscapedValueStartAndEnd('"')
                    .WithEscapedValueEscapeCharacter('"')
                    .WithReadHeader(ReadHeader.Detect)
                    .WithWriteHeader(WriteHeader.Always)
                    //.WithTypeDescriber(TypeDescribers.Default)
                    .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Never)
                    .WithMemoryPool(MemoryPool<char>.Shared)
                    .WithWriteBufferSizeHint(null)
                    .WithCommentCharacter(null)
                    .WithReadBufferSizeHint(0)
                    .WithArrayPool(ArrayPool<DynamicCellValue>.Shared)
                    .ToOptions()
            );

            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithWriteTrailingRowEndingInternal(default).ToOptions());
            Assert.Throws<InvalidOperationException>(() => Options.CreateBuilder(Options.Default).WithWriteTrailingRowEndingInternal((WriteTrailingRowEnding)99).ToOptions());

            Assert.Throws<InvalidOperationException>(
                () =>
                    Options.CreateBuilder().WithValueSeparator(','.ToString())
                    .WithRowEnding(RowEnding.CarriageReturnLineFeed)
                    .WithEscapedValueStartAndEnd('"')
                    .WithEscapedValueEscapeCharacter('"')
                    .WithReadHeader(ReadHeader.Detect)
                    .WithWriteHeader(WriteHeader.Always)
                    .WithTypeDescriber(TypeDescribers.Default)
                    .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Never)
                    //.WithMemoryPool(MemoryPool<char>.Shared)
                    .WithWriteBufferSizeHint(null)
                    .WithCommentCharacter(null)
                    .WithReadBufferSizeHint(0)
                    .WithArrayPool(ArrayPool<DynamicCellValue>.Shared)
                    .ToOptions()
            );

            Assert.Throws<InvalidOperationException>(
                () =>
                    Options.CreateBuilder().WithValueSeparator(','.ToString())
                    .WithRowEnding(RowEnding.CarriageReturnLineFeed)
                    .WithEscapedValueStartAndEnd('"')
                    .WithEscapedValueEscapeCharacter('"')
                    .WithReadHeader(ReadHeader.Detect)
                    .WithWriteHeader(WriteHeader.Always)
                    .WithTypeDescriber(TypeDescribers.Default)
                    .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Never)
                    .WithMemoryPool(MemoryPool<char>.Shared)
                    .WithWriteBufferSizeHint(null)
                    .WithCommentCharacter(null)
                    .WithReadBufferSizeHint(0)
                    //.WithArrayPool(ArrayPool<DynamicCellValue>.Shared)
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

            Assert.Throws<ArgumentException>(() => Options.CreateBuilder(Options.Default).WithWriteTrailingRowEnding(default));

            Assert.Throws<ArgumentException>(() => Options.CreateBuilder(Options.Default).WithWriteBufferSizeHint(-12));

            Assert.Throws<ArgumentException>(() => Options.CreateBuilder(Options.Default).WithReadBufferSizeHint(-12));

            Assert.Throws<ArgumentException>(() => Options.CreateBuilder(Options.Default).WithDynamicRowDisposal(default));

            Assert.Throws<ArgumentException>(() => Options.CreateBuilder(Options.Default).WithWhitespaceTreatment((WhitespaceTreatments)255));

            Assert.Throws<InvalidOperationException>(
                () =>
                    Options.CreateBuilder(Options.Default)
                        .WithWhitespaceTreatmentInternal((WhitespaceTreatments)255)
                        .ToOptions()
            );

            Assert.Throws<InvalidOperationException>(
                () =>
                    Options.CreateBuilder(Options.Default)
                        .WithWhitespaceTreatmentInternal(WhitespaceTreatments.Trim)
                        .WithCommentCharacter(' ')
                        .ToOptions()
            );

            Assert.Throws<InvalidOperationException>(
                () =>
                    Options.CreateBuilder(Options.Default)
                        .WithWhitespaceTreatmentInternal(WhitespaceTreatments.Trim)
                        .WithEscapedValueStartAndEnd(' ')
                        .ToOptions()
            );

            Assert.Throws<InvalidOperationException>(
               () =>
                   Options.CreateBuilder(Options.Default)
                       .WithWhitespaceTreatmentInternal(WhitespaceTreatments.Trim)
                       .WithEscapedValueEscapeCharacter(' ')
                       .ToOptions()
           );

            Assert.Throws<InvalidOperationException>(
               () =>
                   Options.CreateBuilder(Options.Default)
                       .WithWhitespaceTreatmentInternal(WhitespaceTreatments.Trim)
                       .WithValueSeparator(' '.ToString())
                       .ToOptions()
           );

            Assert.Throws<InvalidOperationException>(
               () =>
                   Options.CreateBuilder(Options.Default)
                       .WithExtraColumnTreatmentInternal(0)
                       .WithValueSeparator(' '.ToString())
                       .ToOptions()
           );

            Assert.Throws<InvalidOperationException>(
                () =>
                    Options.CreateBuilder(Options.Default)
                        .WithEscapedValueStartAndEnd(null)
                        .WithEscapedValueEscapeCharacter('\\')
                        .ToOptions()
            );

            // \r clashing
            {
                Options.CreateBuilder(Options.Default).WithValueSeparator("\r").WithRowEnding(RowEnding.LineFeed).ToOptions();
                Assert.Throws<InvalidOperationException>(
                    () =>
                        Options.CreateBuilder(Options.Default)
                            .WithValueSeparator("\r")
                            .WithRowEnding(RowEnding.CarriageReturn)
                            .ToOptions()
                );
                Assert.Throws<InvalidOperationException>(
                    () =>
                        Options.CreateBuilder(Options.Default)
                            .WithValueSeparator("\r")
                            .WithRowEnding(RowEnding.CarriageReturnLineFeed)
                            .ToOptions()
                );
                Assert.Throws<InvalidOperationException>(
                    () =>
                        Options.CreateBuilder(Options.Default)
                            .WithValueSeparator("\r")
                            .WithRowEnding(RowEnding.Detect)
                            .ToOptions()
                );
            }

            // \n clashing
            {
                Options.CreateBuilder(Options.Default).WithValueSeparator("\n").WithRowEnding(RowEnding.CarriageReturn).ToOptions();
                Options.CreateBuilder(Options.Default).WithValueSeparator("\n").WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();
                Assert.Throws<InvalidOperationException>(
                    () =>
                        Options.CreateBuilder(Options.Default)
                            .WithValueSeparator("\n")
                            .WithRowEnding(RowEnding.LineFeed)
                            .ToOptions()
                );
                Assert.Throws<InvalidOperationException>(
                    () =>
                        Options.CreateBuilder(Options.Default)
                            .WithValueSeparator("\n")
                            .WithRowEnding(RowEnding.Detect)
                            .ToOptions()
                );
            }

            // \r\n clashing
            {
                Options.CreateBuilder(Options.Default).WithValueSeparator("\n").WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();
                Assert.Throws<InvalidOperationException>(
                    () =>
                        Options.CreateBuilder(Options.Default)
                            .WithValueSeparator("\r\n")
                            .WithRowEnding(RowEnding.CarriageReturnLineFeed)
                            .ToOptions()
                );
                Assert.Throws<InvalidOperationException>(
                    () =>
                        Options.CreateBuilder(Options.Default)
                            .WithValueSeparator("\r")
                            .WithRowEnding(RowEnding.CarriageReturnLineFeed)
                            .ToOptions()
                );
                Assert.Throws<InvalidOperationException>(
                    () =>
                        Options.CreateBuilder(Options.Default)
                            .WithValueSeparator("\r\n")
                            .WithRowEnding(RowEnding.Detect)
                            .ToOptions()
                );
            }
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
                var defaultConfig = Configuration.For<_BadCreateCalls>();

                Assert.Throws<ArgumentNullException>(() => defaultConfig.CreateAsyncReader(null));
                Assert.Throws<ArgumentNullException>(() => defaultConfig.CreateAsyncReader(default, Encoding.UTF8));
                Assert.Throws<ArgumentNullException>(() => defaultConfig.CreateAsyncReader(new Pipe().Reader, null));
                Assert.Throws<ArgumentNullException>(() => defaultConfig.CreateAsyncWriter(null));
                Assert.Throws<ArgumentNullException>(() => defaultConfig.CreateAsyncWriter(default, Encoding.UTF8));
                Assert.Throws<ArgumentNullException>(() => defaultConfig.CreateAsyncWriter(new Pipe().Writer, null));
                Assert.Throws<ArgumentNullException>(() => defaultConfig.CreateReader(null));
                Assert.Throws<ArgumentNullException>(() => defaultConfig.CreateReader(default, null));
                Assert.Throws<ArgumentNullException>(() => defaultConfig.CreateWriter(default(TextWriter)));
                Assert.Throws<ArgumentNullException>(() => defaultConfig.CreateWriter(default(IBufferWriter<char>)));
                Assert.Throws<ArgumentNullException>(() => defaultConfig.CreateWriter(null, Encoding.UTF8));
                Assert.Throws<ArgumentNullException>(() => defaultConfig.CreateWriter(new Pipe().Writer, null));
            }

            // dynamic
            {
                var defaultConfig = Configuration.ForDynamic();

                Assert.Throws<ArgumentNullException>(() => defaultConfig.CreateAsyncReader(null));
                Assert.Throws<ArgumentNullException>(() => defaultConfig.CreateAsyncReader(default, Encoding.UTF8));
                Assert.Throws<ArgumentNullException>(() => defaultConfig.CreateAsyncReader(new Pipe().Reader, null));
                Assert.Throws<ArgumentNullException>(() => defaultConfig.CreateAsyncWriter(null));
                Assert.Throws<ArgumentNullException>(() => defaultConfig.CreateAsyncWriter(default, Encoding.UTF8));
                Assert.Throws<ArgumentNullException>(() => defaultConfig.CreateAsyncWriter(new Pipe().Writer, null));
                Assert.Throws<ArgumentNullException>(() => defaultConfig.CreateReader(null));
                Assert.Throws<ArgumentNullException>(() => defaultConfig.CreateReader(default, null));
                Assert.Throws<ArgumentNullException>(() => defaultConfig.CreateWriter(default(TextWriter)));
                Assert.Throws<ArgumentNullException>(() => defaultConfig.CreateWriter(default(IBufferWriter<char>)));
                Assert.Throws<ArgumentNullException>(() => defaultConfig.CreateWriter(null, Encoding.UTF8));
                Assert.Throws<ArgumentNullException>(() => defaultConfig.CreateWriter(new Pipe().Writer, null));
            }
        }

        [Fact]
        public void RowEndingDetectWhileWriting()
        {
            // concrete
            {
                var detectOpts = Options.CreateBuilder(Options.Default).WithRowEnding(RowEnding.Detect).ToOptions();
                var detectConfig = Configuration.For<_BadCreateCalls>(detectOpts);

                Assert.Throws<InvalidOperationException>(() => detectConfig.CreateWriter(TextWriter.Null));
                Assert.Throws<InvalidOperationException>(() => detectConfig.CreateAsyncWriter(TextWriter.Null));
            }

            // dynamic
            {
                var detectOpts = Options.CreateBuilder(Options.DynamicDefault).WithRowEnding(RowEnding.Detect).ToOptions();
                var detectConfig = Configuration.ForDynamic(detectOpts);

                Assert.Throws<InvalidOperationException>(() => detectConfig.CreateWriter(TextWriter.Null));
                Assert.Throws<InvalidOperationException>(() => detectConfig.CreateAsyncWriter(TextWriter.Null));
            }
        }
    }
#pragma warning restore IDE1006
}
