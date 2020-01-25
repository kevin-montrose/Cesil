using System.Buffers;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Cesil.Tests
{
    public class RowConstructorTests
    {
        private sealed class _Simple
        {
            public int Foo { get; set; }
            public string Bar { get; set; }
        }

        [Fact]
        public void Simple()
        {
            var t = typeof(_Simple).GetTypeInfo();

            var ip = InstanceProvider.ForDelegate((in ReadContext ctx, out _Simple val) => { val = new _Simple(); return true; });

            var sFooInvoked = false;
            var sFoo = Setter.ForDelegate((_Simple row, int val, in ReadContext ctx) => { sFooInvoked = true; row.Foo = val; });
            var sBarInvoked = false;
            var sBar = Setter.ForDelegate((_Simple row, string val, in ReadContext ctx) => { sBarInvoked = true; row.Bar = val; });

            var pFoo = Parser.GetDefault(typeof(int).GetTypeInfo());
            var pBar = Parser.GetDefault(typeof(string).GetTypeInfo());

            var dmFoo = DeserializableMember.Create(t, nameof(_Simple.Foo), sFoo, pFoo, MemberRequired.No, null);
            var dmBar = DeserializableMember.Create(t, nameof(_Simple.Bar), sBar, pBar, MemberRequired.No, null);

            using var builder = RowConstructor.Create<_Simple>(MemoryPool<char>.Shared, ip, new[] { dmFoo, dmBar });

            _Simple _ = null;

            // in order
            Assert.True(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 0, null), ref _));
            builder.StartRow(ReadContext.ReadingRow(Options.Default, 0, null));
            sFooInvoked = false;
            builder.ColumnAvailable(Options.Default, 0, 0, null, "123");
            Assert.True(sFooInvoked);
            sBarInvoked = false;
            builder.ColumnAvailable(Options.Default, 0, 1, null, "hello");
            Assert.True(sBarInvoked);
            var r1 = builder.FinishRow();
            Assert.NotNull(r1);
            Assert.Equal(123, r1.Foo);
            Assert.Equal("hello", r1.Bar);


            // reverse
            _ = null;
            Assert.True(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 1, null), ref _));
            builder.StartRow(ReadContext.ReadingRow(Options.Default, 1, null));
            sBarInvoked = false;
            builder.ColumnAvailable(Options.Default, 1, 1, null, "world");
            Assert.True(sBarInvoked);
            sFooInvoked = false;
            builder.ColumnAvailable(Options.Default, 1, 0, null, "456");
            Assert.True(sFooInvoked);
            var r2 = builder.FinishRow();
            Assert.NotNull(r2);
            Assert.Equal(456, r2.Foo);
            Assert.Equal("world", r2.Bar);

            // just Foo
            _ = null;
            Assert.True(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 2, null), ref _));
            builder.StartRow(ReadContext.ReadingRow(Options.Default, 2, null));
            sFooInvoked = false;
            sBarInvoked = false;
            builder.ColumnAvailable(Options.Default, 2, 0, null, "789");
            Assert.True(sFooInvoked);
            var r3 = builder.FinishRow();
            Assert.NotNull(r3);
            Assert.Equal(789, r3.Foo);
            Assert.Null(r3.Bar);
            Assert.False(sBarInvoked);

            // just Bar
            _ = null;
            Assert.True(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 3, null), ref _));
            builder.StartRow(ReadContext.ReadingRow(Options.Default, 3, null));
            sFooInvoked = false;
            sBarInvoked = false;
            builder.ColumnAvailable(Options.Default, 3, 1, null, "fizz");
            Assert.True(sBarInvoked);
            var r4 = builder.FinishRow();
            Assert.NotNull(r4);
            Assert.Equal("fizz", r4.Bar);
            Assert.Equal(0, r4.Foo);
            Assert.False(sFooInvoked);

            // empty
            _ = null;
            Assert.True(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 4, null), ref _));
            builder.StartRow(ReadContext.ReadingRow(Options.Default, 4, null));
            sFooInvoked = false;
            sBarInvoked = false;
            var r5 = builder.FinishRow();
            Assert.NotNull(r5);
            Assert.Equal(0, r5.Foo);
            Assert.Null(r5.Bar);
            Assert.False(sFooInvoked);
            Assert.False(sBarInvoked);
        }

        private static HeadersReader<T>.HeaderEnumerator CreateHeadersReader<T>(string header)
        {
            var config = (BoundConfigurationBase<T>)Configuration.For<T>();

            var str = new StringReader(header);

            var buffer = new BufferWithPushback(MemoryPool<char>.Shared, 500);

            var charLookup = CharacterLookup.MakeCharacterLookup(Options.Default, out _);
            var reader = new HeadersReader<T>(new ReaderStateMachine(), config, charLookup, new TextReaderAdapter(str), buffer, RowEnding.CarriageReturnLineFeed);

            var res = reader.Read();
            return res.Headers;
        }

        [Fact]
        public void SetColumnOrder_Simple()
        {
            // no change
            {
                using var builder = MakeConstructor();
                builder.SetColumnOrder(CreateHeadersReader<_Simple>("Foo,Bar"));

                _Simple _ = null;
                Assert.True(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 0, null), ref _));
                builder.StartRow(ReadContext.ReadingRow(Options.Default, 0, null));
                builder.ColumnAvailable(Options.Default, 0, 0, null, "123");
                builder.ColumnAvailable(Options.Default, 0, 1, null, "hello");
                var row = builder.FinishRow();
                Assert.NotNull(row);
                Assert.Equal(123, row.Foo);
                Assert.Equal("hello", row.Bar);
            }

            // reverse
            {
                using var builder = MakeConstructor();
                builder.SetColumnOrder(CreateHeadersReader<_Simple>("Bar,Foo"));

                _Simple _ = null;
                Assert.True(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 0, null), ref _));
                builder.StartRow(ReadContext.ReadingRow(Options.Default, 0, null));
                builder.ColumnAvailable(Options.Default, 0, 0, null, "world");
                builder.ColumnAvailable(Options.Default, 0, 1, null, "456");
                var row = builder.FinishRow();
                Assert.NotNull(row);
                Assert.Equal(456, row.Foo);
                Assert.Equal("world", row.Bar);
            }

            // missing Foo
            {
                using var builder = MakeConstructor();
                builder.SetColumnOrder(CreateHeadersReader<_Simple>("Foo"));

                _Simple _ = null;
                Assert.True(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 0, null), ref _));
                builder.StartRow(ReadContext.ReadingRow(Options.Default, 0, null));
                builder.ColumnAvailable(Options.Default, 0, 0, null, "789");
                var row = builder.FinishRow();
                Assert.NotNull(row);
                Assert.Equal(789, row.Foo);
                Assert.Null(row.Bar);
            }

            // missing Bar
            {
                using var builder = MakeConstructor();
                builder.SetColumnOrder(CreateHeadersReader<_Simple>("Bar"));

                _Simple _ = null;
                Assert.True(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 0, null), ref _));
                builder.StartRow(ReadContext.ReadingRow(Options.Default, 0, null));
                builder.ColumnAvailable(Options.Default, 0, 0, null, "Fizz");
                var row = builder.FinishRow();
                Assert.NotNull(row);
                Assert.Equal(0, row.Foo);
                Assert.Equal("Fizz", row.Bar);
            }

            // empty
            {
                using var builder = MakeConstructor();
                builder.SetColumnOrder(CreateHeadersReader<_Simple>(""));

                _Simple _ = null;
                Assert.True(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 0, null), ref _));
                builder.StartRow(ReadContext.ReadingRow(Options.Default, 0, null));
                var row = builder.FinishRow();
                Assert.NotNull(row);
                Assert.Equal(0, row.Foo);
                Assert.Null(row.Bar);
            }

            static IRowConstructor<_Simple> MakeConstructor()
            {
                var t = typeof(_Simple).GetTypeInfo();

                var ip = InstanceProvider.ForDelegate((in ReadContext ctx, out _Simple val) => { val = new _Simple(); return true; });

                var sFoo = Setter.ForDelegate((_Simple row, int val, in ReadContext ctx) => { row.Foo = val; });
                var sBar = Setter.ForDelegate((_Simple row, string val, in ReadContext ctx) => { row.Bar = val; });

                var pFoo = Parser.GetDefault(typeof(int).GetTypeInfo());
                var pBar = Parser.GetDefault(typeof(string).GetTypeInfo());

                var dmFoo = DeserializableMember.Create(t, nameof(_Simple.Foo), sFoo, pFoo, MemberRequired.No, null);
                var dmBar = DeserializableMember.Create(t, nameof(_Simple.Bar), sBar, pBar, MemberRequired.No, null);

                var builder = RowConstructor.Create<_Simple>(MemoryPool<char>.Shared, ip, new[] { dmFoo, dmBar });

                return builder;
            }
        }

        private sealed class _ConstructorParameters
        {
            public int Foo { get; }
            public string Bar { get; }

            public _ConstructorParameters(int a, string b)
            {
                Foo = a;
                Bar = b;
            }
        }

        [Fact]
        public void ConstructorParameters()
        {
            var t = typeof(_ConstructorParameters).GetTypeInfo();

            var cons = typeof(_ConstructorParameters).GetConstructors()[0];
            var consPs = cons.GetParameters();

            var ip = InstanceProvider.ForConstructorWithParameters(cons);

            var sFoo = Setter.ForConstructorParameter(consPs.Single(s => s.Name == "a"));
            var sBar = Setter.ForConstructorParameter(consPs.Single(s => s.Name == "b"));

            var pFoo = Parser.GetDefault(typeof(int).GetTypeInfo());
            var pBar = Parser.GetDefault(typeof(string).GetTypeInfo());

            var dmFoo = DeserializableMember.Create(t, nameof(_ConstructorParameters.Foo), sFoo, pFoo, MemberRequired.Yes, null);
            var dmBar = DeserializableMember.Create(t, nameof(_ConstructorParameters.Bar), sBar, pBar, MemberRequired.Yes, null);

            using var builder = RowConstructor.Create<_ConstructorParameters>(MemoryPool<char>.Shared, ip, new[] { dmFoo, dmBar });

            _ConstructorParameters _ = null;

            // in order
            _ = null;
            Assert.False(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 0, null), ref _));
            builder.StartRow(ReadContext.ReadingRow(Options.Default, 0, null));
            builder.ColumnAvailable(Options.Default, 0, 0, null, "123");
            builder.ColumnAvailable(Options.Default, 0, 1, null, "hello");
            var r1 = builder.FinishRow();
            Assert.NotNull(r1);
            Assert.Equal(123, r1.Foo);
            Assert.Equal("hello", r1.Bar);

            // reverse
            _ = null;
            Assert.False(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 1, null), ref _));
            builder.StartRow(ReadContext.ReadingRow(Options.Default, 1, null));
            builder.ColumnAvailable(Options.Default, 1, 1, null, "world");
            builder.ColumnAvailable(Options.Default, 1, 0, null, "456");
            var r2 = builder.FinishRow();
            Assert.NotNull(r2);
            Assert.Equal(456, r2.Foo);
            Assert.Equal("world", r2.Bar);
        }

        [Fact]
        public void SetColumnOrder_ConstructorParameters()
        {
            // no change
            {
                using var builder = MakeConstructor();
                builder.SetColumnOrder(CreateHeadersReader<_ConstructorParameters>("Foo,Bar"));

                _ConstructorParameters _ = null;
                Assert.False(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 0, null), ref _));
                builder.StartRow(ReadContext.ReadingRow(Options.Default, 0, null));
                builder.ColumnAvailable(Options.Default, 0, 0, null, "123");
                builder.ColumnAvailable(Options.Default, 0, 1, null, "hello");
                var row = builder.FinishRow();
                Assert.NotNull(row);
                Assert.Equal(123, row.Foo);
                Assert.Equal("hello", row.Bar);
            }

            // reverse
            {
                using var builder = MakeConstructor();
                builder.SetColumnOrder(CreateHeadersReader<_ConstructorParameters>("Bar,Foo"));

                _ConstructorParameters _ = null;
                Assert.False(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 0, null), ref _));
                builder.StartRow(ReadContext.ReadingRow(Options.Default, 0, null));
                builder.ColumnAvailable(Options.Default, 0, 0, null, "world");
                builder.ColumnAvailable(Options.Default, 0, 1, null, "456");
                var row = builder.FinishRow();
                Assert.NotNull(row);
                Assert.Equal(456, row.Foo);
                Assert.Equal("world", row.Bar);
            }

            static IRowConstructor<_ConstructorParameters> MakeConstructor()
            {
                var t = typeof(_ConstructorParameters).GetTypeInfo();

                var cons = typeof(_ConstructorParameters).GetConstructors()[0];
                var consPs = cons.GetParameters();

                var ip = InstanceProvider.ForConstructorWithParameters(cons);

                var sFoo = Setter.ForConstructorParameter(consPs.Single(s => s.Name == "a"));
                var sBar = Setter.ForConstructorParameter(consPs.Single(s => s.Name == "b"));

                var pFoo = Parser.GetDefault(typeof(int).GetTypeInfo());
                var pBar = Parser.GetDefault(typeof(string).GetTypeInfo());

                var dmFoo = DeserializableMember.Create(t, nameof(_ConstructorParameters.Foo), sFoo, pFoo, MemberRequired.Yes, null);
                var dmBar = DeserializableMember.Create(t, nameof(_ConstructorParameters.Bar), sBar, pBar, MemberRequired.Yes, null);

                var builder = RowConstructor.Create<_ConstructorParameters>(MemoryPool<char>.Shared, ip, new[] { dmFoo, dmBar });

                return builder;
            }
        }

        private sealed class _Mixed
        {
            public int Foo { get; }
            public string Bar { get; }

            public char Fizz { get; set; }
            public byte Buzz { get; set; }

            public _Mixed(int a, string b)
            {
                Foo = a;
                Bar = b;
            }
        }

        [Fact]
        public void Mixed()
        {
            var t = typeof(_Mixed).GetTypeInfo();

            var cons = t.GetConstructors()[0];
            var consPs = cons.GetParameters();

            var ip = InstanceProvider.ForConstructorWithParameters(cons);

            var sFoo = Setter.ForConstructorParameter(consPs.Single(s => s.Name == "a"));
            var sBar = Setter.ForConstructorParameter(consPs.Single(s => s.Name == "b"));

            var sFizzInvoked = false;
            var sFizz = Setter.ForDelegate((_Mixed row, char val, in ReadContext ctx) => { sFizzInvoked = true; row.Fizz = val; });
            var sBuzzInvoked = false;
            var sBuzz = Setter.ForDelegate((_Mixed row, byte val, in ReadContext ctx) => { sBuzzInvoked = true; row.Buzz = val; });

            var pFoo = Parser.GetDefault(typeof(int).GetTypeInfo());
            var pBar = Parser.GetDefault(typeof(string).GetTypeInfo());
            var pFizz = Parser.GetDefault(typeof(char).GetTypeInfo());
            var pBuzz = Parser.GetDefault(typeof(byte).GetTypeInfo());

            var dmFoo = DeserializableMember.Create(t, nameof(_Mixed.Foo), sFoo, pFoo, MemberRequired.Yes, null);
            var dmBar = DeserializableMember.Create(t, nameof(_Mixed.Bar), sBar, pBar, MemberRequired.Yes, null);
            var dmFizz = DeserializableMember.Create(t, nameof(_Mixed.Fizz), sFizz, pFizz, MemberRequired.No, null);
            var dmBuzz = DeserializableMember.Create(t, nameof(_Mixed.Buzz), sBuzz, pBuzz, MemberRequired.No, null);

            using var builder =
                RowConstructor.Create<_Mixed>(
                    MemoryPool<char>.Shared,
                    ip,
                    new[]
                    {
                        dmFoo,
                        dmBar,
                        dmFizz,
                        dmBuzz
                    }
                );

            _Mixed _ = null;

            // in order
            sFizzInvoked = false;
            sBuzzInvoked = false;
            Assert.False(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 0, null), ref _));
            builder.StartRow(ReadContext.ReadingRow(Options.Default, 0, null));
            builder.ColumnAvailable(Options.Default, 0, 0, null, "123");
            builder.ColumnAvailable(Options.Default, 0, 1, null, "hello");
            builder.ColumnAvailable(Options.Default, 0, 2, null, "x");
            Assert.True(sFizzInvoked);
            builder.ColumnAvailable(Options.Default, 0, 3, null, "255");
            Assert.True(sBuzzInvoked);
            var r1 = builder.FinishRow();
            Assert.NotNull(r1);
            Assert.Equal(123, r1.Foo);
            Assert.Equal("hello", r1.Bar);
            Assert.Equal('x', r1.Fizz);
            Assert.Equal(255, r1.Buzz);

            // reverse
            _ = null;
            sFizzInvoked = false;
            sBuzzInvoked = false;
            Assert.False(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 1, null), ref _));
            builder.StartRow(ReadContext.ReadingRow(Options.Default, 1, null));
            builder.ColumnAvailable(Options.Default, 1, 3, null, "128");
            Assert.False(sFizzInvoked);
            Assert.False(sBuzzInvoked);
            builder.ColumnAvailable(Options.Default, 1, 2, null, "y");
            Assert.False(sFizzInvoked);
            Assert.False(sBuzzInvoked);
            builder.ColumnAvailable(Options.Default, 1, 1, null, "world");
            Assert.False(sFizzInvoked);
            Assert.False(sBuzzInvoked);
            builder.ColumnAvailable(Options.Default, 1, 0, null, "456");
            Assert.True(sFizzInvoked);
            Assert.True(sBuzzInvoked);
            var r2 = builder.FinishRow();
            Assert.NotNull(r2);
            Assert.Equal(456, r2.Foo);
            Assert.Equal("world", r2.Bar);
            Assert.Equal('y', r2.Fizz);
            Assert.Equal(128, r2.Buzz);

            // missing Fizz
            _ = null;
            sFizzInvoked = false;
            sBuzzInvoked = false;
            Assert.False(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 2, null), ref _));
            builder.StartRow(ReadContext.ReadingRow(Options.Default, 2, null));
            builder.ColumnAvailable(Options.Default, 2, 0, null, "789");
            builder.ColumnAvailable(Options.Default, 2, 1, null, "fix");
            builder.ColumnAvailable(Options.Default, 2, 3, null, "32");
            Assert.True(sBuzzInvoked);
            Assert.False(sFizzInvoked);
            var r3 = builder.FinishRow();
            Assert.NotNull(r3);
            Assert.Equal(789, r3.Foo);
            Assert.Equal("fix", r3.Bar);
            Assert.Equal('\0', r3.Fizz);
            Assert.Equal(32, r3.Buzz);

            // missing Fizz, reverse
            _ = null;
            sFizzInvoked = false;
            sBuzzInvoked = false;
            Assert.False(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 3, null), ref _));
            builder.StartRow(ReadContext.ReadingRow(Options.Default, 3, null));
            builder.ColumnAvailable(Options.Default, 3, 3, null, "16");
            Assert.False(sFizzInvoked);
            Assert.False(sBuzzInvoked);
            builder.ColumnAvailable(Options.Default, 3, 1, null, "it");
            Assert.False(sFizzInvoked);
            Assert.False(sBuzzInvoked);
            builder.ColumnAvailable(Options.Default, 3, 0, null, "012");
            Assert.False(sFizzInvoked);
            Assert.True(sBuzzInvoked);
            var r4 = builder.FinishRow();
            Assert.NotNull(r4);
            Assert.Equal(012, r4.Foo);
            Assert.Equal("it", r4.Bar);
            Assert.Equal('\0', r4.Fizz);
            Assert.Equal(16, r4.Buzz);

            // missing Buzz
            _ = null;
            sFizzInvoked = false;
            sBuzzInvoked = false;
            Assert.False(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 4, null), ref _));
            builder.StartRow(ReadContext.ReadingRow(Options.Default, 4, null));
            builder.ColumnAvailable(Options.Default, 4, 0, null, "345");
            builder.ColumnAvailable(Options.Default, 4, 1, null, "exit");
            builder.ColumnAvailable(Options.Default, 4, 2, null, "z");
            Assert.True(sFizzInvoked);
            Assert.False(sBuzzInvoked);
            var r5 = builder.FinishRow();
            Assert.NotNull(r5);
            Assert.Equal(345, r5.Foo);
            Assert.Equal("exit", r5.Bar);
            Assert.Equal('z', r5.Fizz);
            Assert.Equal(0, r5.Buzz);

            // missing Buzz, reverse
            _ = null;
            sFizzInvoked = false;
            sBuzzInvoked = false;
            Assert.False(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 5, null), ref _));
            builder.StartRow(ReadContext.ReadingRow(Options.Default, 5, null));
            builder.ColumnAvailable(Options.Default, 5, 2, null, "a");
            Assert.False(sFizzInvoked);
            Assert.False(sBuzzInvoked);
            builder.ColumnAvailable(Options.Default, 5, 1, null, "-1");
            Assert.False(sFizzInvoked);
            Assert.False(sBuzzInvoked);
            builder.ColumnAvailable(Options.Default, 5, 0, null, "678");
            Assert.True(sFizzInvoked);
            Assert.False(sBuzzInvoked);
            var r6 = builder.FinishRow();
            Assert.NotNull(r6);
            Assert.Equal(678, r6.Foo);
            Assert.Equal("-1", r6.Bar);
            Assert.Equal('a', r6.Fizz);
            Assert.Equal(0, r6.Buzz);

            // missing both
            _ = null;
            sFizzInvoked = false;
            sBuzzInvoked = false;
            Assert.False(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 6, null), ref _));
            builder.StartRow(ReadContext.ReadingRow(Options.Default, 6, null));
            builder.ColumnAvailable(Options.Default, 6, 0, null, "901");
            builder.ColumnAvailable(Options.Default, 6, 1, null, "wat");
            Assert.False(sFizzInvoked);
            Assert.False(sBuzzInvoked);
            var r7 = builder.FinishRow();
            Assert.NotNull(r7);
            Assert.Equal(901, r7.Foo);
            Assert.Equal("wat", r7.Bar);
            Assert.Equal('\0', r7.Fizz);
            Assert.Equal(0, r7.Buzz);

            // missing both, reverse
            _ = null;
            sFizzInvoked = false;
            sBuzzInvoked = false;
            Assert.False(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 7, null), ref _));
            builder.StartRow(ReadContext.ReadingRow(Options.Default, 7, null));
            builder.ColumnAvailable(Options.Default, 7, 1, null, "wut");
            builder.ColumnAvailable(Options.Default, 7, 0, null, "234");
            Assert.False(sFizzInvoked);
            Assert.False(sBuzzInvoked);
            var r8 = builder.FinishRow();
            Assert.NotNull(r8);
            Assert.Equal(234, r8.Foo);
            Assert.Equal("wut", r8.Bar);
            Assert.Equal('\0', r8.Fizz);
            Assert.Equal(0, r8.Buzz);
        }

        [Fact]
        public void SetColumnOrder_Mixed()
        {
            // in order
            {
                _Mixed _ = null;

                var builder = MakeConstructor();
                builder.SetColumnOrder(CreateHeadersReader<_Mixed>("Foo,Bar,Fizz,Buzz"));

                Assert.False(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 0, null), ref _));
                builder.StartRow(ReadContext.ReadingRow(Options.Default, 0, null));
                builder.ColumnAvailable(Options.Default, 0, 0, null, "123");
                builder.ColumnAvailable(Options.Default, 0, 1, null, "hello");
                builder.ColumnAvailable(Options.Default, 0, 2, null, "x");
                builder.ColumnAvailable(Options.Default, 0, 3, null, "255");
                var row = builder.FinishRow();
                Assert.NotNull(row);
                Assert.Equal(123, row.Foo);
                Assert.Equal("hello", row.Bar);
                Assert.Equal('x', row.Fizz);
                Assert.Equal(255, row.Buzz);
            }

            // reversed
            {
                _Mixed _ = null;

                var builder = MakeConstructor();
                builder.SetColumnOrder(CreateHeadersReader<_Mixed>("Buzz,Fizz,Bar,Foo"));

                Assert.False(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 0, null), ref _));
                builder.StartRow(ReadContext.ReadingRow(Options.Default, 0, null));
                builder.ColumnAvailable(Options.Default, 0, 0, null, "100");
                builder.ColumnAvailable(Options.Default, 0, 1, null, "z");
                builder.ColumnAvailable(Options.Default, 0, 2, null, "world");
                builder.ColumnAvailable(Options.Default, 0, 3, null, "456");
                var row = builder.FinishRow();
                Assert.NotNull(row);
                Assert.Equal(456, row.Foo);
                Assert.Equal("world", row.Bar);
                Assert.Equal('z', row.Fizz);
                Assert.Equal(100, row.Buzz);
            }

            // missing Fizz
            {
                _Mixed _ = null;

                var builder = MakeConstructor();
                builder.SetColumnOrder(CreateHeadersReader<_Mixed>("Foo,Bar,Buzz"));

                Assert.False(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 0, null), ref _));
                builder.StartRow(ReadContext.ReadingRow(Options.Default, 0, null));
                builder.ColumnAvailable(Options.Default, 0, 0, null, "123");
                builder.ColumnAvailable(Options.Default, 0, 1, null, "hello");
                builder.ColumnAvailable(Options.Default, 0, 2, null, "255");
                var row = builder.FinishRow();
                Assert.NotNull(row);
                Assert.Equal(123, row.Foo);
                Assert.Equal("hello", row.Bar);
                Assert.Equal('\0', row.Fizz);
                Assert.Equal(255, row.Buzz);
            }

            // missing Fizz, reverse
            {
                _Mixed _ = null;

                var builder = MakeConstructor();
                builder.SetColumnOrder(CreateHeadersReader<_Mixed>("Buzz,Bar,Foo"));

                Assert.False(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 0, null), ref _));
                builder.StartRow(ReadContext.ReadingRow(Options.Default, 0, null));
                builder.ColumnAvailable(Options.Default, 0, 0, null, "111");
                builder.ColumnAvailable(Options.Default, 0, 1, null, "world");
                builder.ColumnAvailable(Options.Default, 0, 2, null, "456");
                var row = builder.FinishRow();
                Assert.NotNull(row);
                Assert.Equal(456, row.Foo);
                Assert.Equal("world", row.Bar);
                Assert.Equal('\0', row.Fizz);
                Assert.Equal(111, row.Buzz);
            }

            // missing Buzz
            {
                _Mixed _ = null;

                var builder = MakeConstructor();
                builder.SetColumnOrder(CreateHeadersReader<_Mixed>("Foo,Bar,Fizz"));

                Assert.False(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 0, null), ref _));
                builder.StartRow(ReadContext.ReadingRow(Options.Default, 0, null));
                builder.ColumnAvailable(Options.Default, 0, 0, null, "123");
                builder.ColumnAvailable(Options.Default, 0, 1, null, "hello");
                builder.ColumnAvailable(Options.Default, 0, 2, null, "x");
                var row = builder.FinishRow();
                Assert.NotNull(row);
                Assert.Equal(123, row.Foo);
                Assert.Equal("hello", row.Bar);
                Assert.Equal('x', row.Fizz);
                Assert.Equal(0, row.Buzz);
            }

            // missing Buzz, reverse
            {
                _Mixed _ = null;

                var builder = MakeConstructor();
                builder.SetColumnOrder(CreateHeadersReader<_Mixed>("Fizz,Bar,Foo"));

                Assert.False(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 0, null), ref _));
                builder.StartRow(ReadContext.ReadingRow(Options.Default, 0, null));
                builder.ColumnAvailable(Options.Default, 0, 0, null, "q");
                builder.ColumnAvailable(Options.Default, 0, 1, null, "world");
                builder.ColumnAvailable(Options.Default, 0, 2, null, "456");


                var row = builder.FinishRow();
                Assert.NotNull(row);
                Assert.Equal(456, row.Foo);
                Assert.Equal("world", row.Bar);
                Assert.Equal('q', row.Fizz);
                Assert.Equal(0, row.Buzz);
            }

            // missing both
            {
                _Mixed _ = null;

                var builder = MakeConstructor();
                builder.SetColumnOrder(CreateHeadersReader<_Mixed>("Foo,Bar"));

                Assert.False(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 0, null), ref _));
                builder.StartRow(ReadContext.ReadingRow(Options.Default, 0, null));
                builder.ColumnAvailable(Options.Default, 0, 0, null, "123");
                builder.ColumnAvailable(Options.Default, 0, 1, null, "hello");
                var row = builder.FinishRow();
                Assert.NotNull(row);
                Assert.Equal(123, row.Foo);
                Assert.Equal("hello", row.Bar);
                Assert.Equal('\0', row.Fizz);
                Assert.Equal(0, row.Buzz);
            }

            // missing both, reverse
            {
                _Mixed _ = null;

                var builder = MakeConstructor();
                builder.SetColumnOrder(CreateHeadersReader<_Mixed>("Bar,Foo"));

                Assert.False(builder.TryPreAllocate(ReadContext.ReadingRow(Options.Default, 0, null), ref _));
                builder.StartRow(ReadContext.ReadingRow(Options.Default, 0, null));
                builder.ColumnAvailable(Options.Default, 0, 0, null, "world");
                builder.ColumnAvailable(Options.Default, 0, 1, null, "456");

                var row = builder.FinishRow();
                Assert.NotNull(row);
                Assert.Equal(456, row.Foo);
                Assert.Equal("world", row.Bar);
                Assert.Equal('\0', row.Fizz);
                Assert.Equal(0, row.Buzz);
            }

            static IRowConstructor<_Mixed> MakeConstructor()
            {
                var t = typeof(_Mixed).GetTypeInfo();

                var cons = t.GetConstructors()[0];
                var consPs = cons.GetParameters();

                var ip = InstanceProvider.ForConstructorWithParameters(cons);

                var sFoo = Setter.ForConstructorParameter(consPs.Single(s => s.Name == "a"));
                var sBar = Setter.ForConstructorParameter(consPs.Single(s => s.Name == "b"));

                var sFizz = Setter.ForDelegate((_Mixed row, char val, in ReadContext ctx) => { row.Fizz = val; });
                var sBuzz = Setter.ForDelegate((_Mixed row, byte val, in ReadContext ctx) => { row.Buzz = val; });

                var pFoo = Parser.GetDefault(typeof(int).GetTypeInfo());
                var pBar = Parser.GetDefault(typeof(string).GetTypeInfo());
                var pFizz = Parser.GetDefault(typeof(char).GetTypeInfo());
                var pBuzz = Parser.GetDefault(typeof(byte).GetTypeInfo());

                var dmFoo = DeserializableMember.Create(t, nameof(_Mixed.Foo), sFoo, pFoo, MemberRequired.Yes, null);
                var dmBar = DeserializableMember.Create(t, nameof(_Mixed.Bar), sBar, pBar, MemberRequired.Yes, null);
                var dmFizz = DeserializableMember.Create(t, nameof(_Mixed.Fizz), sFizz, pFizz, MemberRequired.No, null);
                var dmBuzz = DeserializableMember.Create(t, nameof(_Mixed.Buzz), sBuzz, pBuzz, MemberRequired.No, null);

                var builder =
                    RowConstructor.Create<_Mixed>(
                        MemoryPool<char>.Shared,
                        ip,
                        new[]
                        {
                        dmFoo,
                        dmBar,
                        dmFizz,
                        dmBuzz
                        }
                    );

                return builder;
            }
        }
    }
}
