using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Pipelines;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Xunit;

using static Cesil.Tests.Helpers;

namespace Cesil.Tests
{
#pragma warning disable IDE1006
    public class ReaderTests
    {
        private sealed class _MultiCharacterSeparatorInHeaders
        {
            [DataMember(Name = "Foo#|#Bar")]
            public string A { get; set; }
            public int B { get; set; }
        }

        [Fact]
        public void MultiCharacterSeparatorInHeaders()
        {
            // always
            {
                var opts = Options.CreateBuilder(Options.Default).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Always).ToOptions();

                RunSyncReaderVariants<_MultiCharacterSeparatorInHeaders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("B#|#\"Foo#|#Bar\"\r\n123#|#hello"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(
                                rows,
                                a =>
                                {
                                    Assert.Equal("hello", a.A);
                                    Assert.Equal(123, a.B);
                                }
                            );
                        }
                    }
                );
            }

            // detect
            {
                var opts = Options.CreateBuilder(Options.Default).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Detect).ToOptions();

                RunSyncReaderVariants<_MultiCharacterSeparatorInHeaders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("B#|#\"Foo#|#Bar\"\r\n123#|#hello"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(
                                rows,
                                a =>
                                {
                                    Assert.Equal("hello", a.A);
                                    Assert.Equal(123, a.B);
                                }
                            );
                        }
                    }
                );
            }

            // detect rows endings
            {
                var opts = Options.CreateBuilder(Options.Default).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Detect).WithRowEnding(RowEnding.Detect).ToOptions();

                // \r\n
                {
                    RunSyncReaderVariants<_MultiCharacterSeparatorInHeaders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("B#|#\"Foo#|#Bar\"\r\n123#|#hello"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(
                                rows,
                                a =>
                                {
                                    Assert.Equal("hello", a.A);
                                    Assert.Equal(123, a.B);
                                }
                            );
                        }
                    }
                );
                }

                // \r
                {
                    RunSyncReaderVariants<_MultiCharacterSeparatorInHeaders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("B#|#\"Foo#|#Bar\"\r123#|#hello"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(
                                rows,
                                a =>
                                {
                                    Assert.Equal("hello", a.A);
                                    Assert.Equal(123, a.B);
                                }
                            );
                        }
                    }
                );
                }

                // \n
                {
                    RunSyncReaderVariants<_MultiCharacterSeparatorInHeaders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("B#|#\"Foo#|#Bar\"\n123#|#hello"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(
                                rows,
                                a =>
                                {
                                    Assert.Equal("hello", a.A);
                                    Assert.Equal(123, a.B);
                                }
                            );
                        }
                    }
                );
                }
            }
        }

        private sealed class _MultiCharacterSeparators
        {
            public string A { get; set; }
            public int B { get; set; }
        }

        [Fact]
        public void MultiCharacterSeparators()
        {
            // header variants
            {
                // no headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Never).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

                    RunSyncReaderVariants<_MultiCharacterSeparators>(
                        opts,
                        (config, getReader) =>
                        {
                            using (var reader = getReader("hello#|#123\r\n\"world\"#|#456\r\n\"f#|#b\"#|#789"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.A);
                                        Assert.Equal(123, a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", b.A);
                                        Assert.Equal(456, b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", c.A);
                                        Assert.Equal(789, c.B);
                                    }
                                );
                            }
                        }
                    );
                }

                // always headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Always).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

                    RunSyncReaderVariants<_MultiCharacterSeparators>(
                        opts,
                        (config, getReader) =>
                        {
                            using (var reader = getReader("A#|#B\r\nhello#|#123\r\n\"world\"#|#456\r\n\"f#|#b\"#|#789"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.A);
                                        Assert.Equal(123, a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", b.A);
                                        Assert.Equal(456, b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", c.A);
                                        Assert.Equal(789, c.B);
                                    }
                                );
                            }
                        }
                    );
                }

                // detect headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Detect).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

                    // not present
                    RunSyncReaderVariants<_MultiCharacterSeparators>(
                        opts,
                        (config, getReader) =>
                        {
                            using (var reader = getReader("hello#|#123\r\n\"world\"#|#456\r\n\"f#|#b\"#|#789"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.A);
                                        Assert.Equal(123, a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", b.A);
                                        Assert.Equal(456, b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", c.A);
                                        Assert.Equal(789, c.B);
                                    }
                                );
                            }
                        }
                    );

                    // present
                    RunSyncReaderVariants<_MultiCharacterSeparators>(
                        opts,
                        (config, getReader) =>
                        {
                            using (var reader = getReader("B#|#A\r\n123#|#hello\r\n456#|#\"world\"\r\n789#|#\"f#|#b\""))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.A);
                                        Assert.Equal(123, a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", b.A);
                                        Assert.Equal(456, b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", c.A);
                                        Assert.Equal(789, c.B);
                                    }
                                );
                            }
                        }
                    );
                }
            }

            // detect line endings
            {
                var opts = Options.CreateBuilder(Options.Default).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Detect).WithRowEnding(RowEnding.Detect).ToOptions();

                // \r\n
                {
                    // not present
                    RunSyncReaderVariants<_MultiCharacterSeparators>(
                        opts,
                        (config, getReader) =>
                        {
                            using (var reader = getReader("hello#|#123\r\n\"world\"#|#456\r\n\"f#|#b\"#|#789"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.A);
                                        Assert.Equal(123, a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", b.A);
                                        Assert.Equal(456, b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", c.A);
                                        Assert.Equal(789, c.B);
                                    }
                                );
                            }
                        }
                    );

                    // present
                    RunSyncReaderVariants<_MultiCharacterSeparators>(
                        opts,
                        (config, getReader) =>
                        {
                            using (var reader = getReader("B#|#A\r\n123#|#hello\r\n456#|#\"world\"\r\n789#|#\"f#|#b\""))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.A);
                                        Assert.Equal(123, a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", b.A);
                                        Assert.Equal(456, b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", c.A);
                                        Assert.Equal(789, c.B);
                                    }
                                );
                            }
                        }
                    );
                }

                // \r
                {
                    // not present
                    RunSyncReaderVariants<_MultiCharacterSeparators>(
                        opts,
                        (config, getReader) =>
                        {
                            using (var reader = getReader("hello#|#123\r\"world\"#|#456\r\"f#|#b\"#|#789"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.A);
                                        Assert.Equal(123, a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", b.A);
                                        Assert.Equal(456, b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", c.A);
                                        Assert.Equal(789, c.B);
                                    }
                                );
                            }
                        }
                    );

                    // present
                    RunSyncReaderVariants<_MultiCharacterSeparators>(
                        opts,
                        (config, getReader) =>
                        {
                            using (var reader = getReader("B#|#A\r123#|#hello\r456#|#\"world\"\r789#|#\"f#|#b\""))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.A);
                                        Assert.Equal(123, a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", b.A);
                                        Assert.Equal(456, b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", c.A);
                                        Assert.Equal(789, c.B);
                                    }
                                );
                            }
                        }
                    );
                }

                // \n
                {
                    // not present
                    RunSyncReaderVariants<_MultiCharacterSeparators>(
                        opts,
                        (config, getReader) =>
                        {
                            using (var reader = getReader("hello#|#123\n\"world\"#|#456\n\"f#|#b\"#|#789"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.A);
                                        Assert.Equal(123, a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", b.A);
                                        Assert.Equal(456, b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", c.A);
                                        Assert.Equal(789, c.B);
                                    }
                                );
                            }
                        }
                    );

                    // present
                    RunSyncReaderVariants<_MultiCharacterSeparators>(
                        opts,
                        (config, getReader) =>
                        {
                            using (var reader = getReader("B#|#A\n123#|#hello\n456#|#\"world\"\n789#|#\"f#|#b\""))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.A);
                                        Assert.Equal(123, a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", b.A);
                                        Assert.Equal(456, b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", c.A);
                                        Assert.Equal(789, c.B);
                                    }
                                );
                            }
                        }
                    );
                }
            }
        }

        private struct _ValueTypeInstanceProviders
        {
            public int A { get; set; }
        }

        [Fact]
        public void ValueTypeInstanceProviders()
        {
            var ip =
                InstanceProvider.ForDelegate(
                    (in ReadContext _, out _ValueTypeInstanceProviders val) =>
                    {
                        val = new _ValueTypeInstanceProviders { A = 4 };
                        return true;
                    }
                );
            var setter =
                Setter.ForDelegate(
                    (ref _ValueTypeInstanceProviders row, int value, in ReadContext _) =>
                    {
                        row.A *= value;
                    }
                );

            var tdb = ManualTypeDescriber.CreateBuilder(ManualTypeDescriberFallbackBehavior.UseFallback, TypeDescribers.Default);
            tdb.WithInstanceProvider(ip);
            tdb.WithExplicitSetter(typeof(_ValueTypeInstanceProviders).GetTypeInfo(), "A", setter);
            var td = tdb.ToManualTypeDescriber();

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(td).WithCommentCharacter('#').ToOptions();

            // always called
            {
                RunSyncReaderVariants<_ValueTypeInstanceProviders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("A\r\n1\r\n2\r\n3\r\n4"))
                        using (var csv = config.CreateReader(reader))
                        {
                            Assert.True(csv.TryRead(out var r1));
                            Assert.Equal(4, r1.A);
                            Assert.True(csv.TryRead(out var r2));
                            Assert.Equal(8, r2.A);
                            Assert.True(csv.TryRead(out var r3));
                            Assert.Equal(12, r3.A);
                            Assert.True(csv.TryRead(out var r4));
                            Assert.Equal(16, r4.A);

                            Assert.False(csv.TryRead(out _));
                        }
                    }
                );
            }

            // always called, comments
            {
                RunSyncReaderVariants<_ValueTypeInstanceProviders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("A\r\n1\r\n2\r\n#hello\r\n3\r\n4"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.True(res1.HasValue);
                            var r1 = res1.Value;
                            Assert.Equal(4, r1.A);

                            var res2 = csv.TryReadWithComment();
                            Assert.True(res2.HasValue);
                            var r2 = res2.Value;
                            Assert.Equal(8, r2.A);

                            var res3 = csv.TryReadWithComment();
                            Assert.True(res3.HasComment);
                            var com3 = res3.Comment;
                            Assert.Equal("hello", com3);

                            var res4 = csv.TryReadWithComment();
                            Assert.True(res4.HasValue);
                            var r4 = res4.Value;
                            Assert.Equal(12, r4.A);

                            var res5 = csv.TryReadWithComment();
                            Assert.True(res5.HasValue);
                            var r5 = res5.Value;
                            Assert.Equal(16, r5.A);

                            var res6 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res6.ResultType);
                        }
                    }
                );
            }

            // never called
            {
                RunSyncReaderVariants<_ValueTypeInstanceProviders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("A\r\n1\r\n2\r\n3\r\n4"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var r = new _ValueTypeInstanceProviders { A = -2 };

                            Assert.True(csv.TryReadWithReuse(ref r));
                            Assert.Equal(-2, r.A);
                            Assert.True(csv.TryReadWithReuse(ref r));
                            Assert.Equal(-4, r.A);
                            Assert.True(csv.TryReadWithReuse(ref r));
                            Assert.Equal(-12, r.A);
                            Assert.True(csv.TryReadWithReuse(ref r));
                            Assert.Equal(-48, r.A);

                            Assert.False(csv.TryReadWithReuse(ref r));
                        }
                    }
                );
            }

            // never called, comments
            {
                RunSyncReaderVariants<_ValueTypeInstanceProviders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("A\r\n1\r\n2\r\n#hello\r\n3\r\n4"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var r = new _ValueTypeInstanceProviders { A = -2 };

                            var res1 = csv.TryReadWithCommentReuse(ref r);
                            Assert.True(res1.HasValue);
                            r = res1.Value;
                            Assert.Equal(-2, r.A);

                            var res2 = csv.TryReadWithCommentReuse(ref r);
                            Assert.True(res2.HasValue);
                            r = res2.Value;
                            Assert.Equal(-4, r.A);

                            var res3 = csv.TryReadWithCommentReuse(ref r);
                            Assert.True(res3.HasComment);
                            var com3 = res3.Comment;
                            Assert.Equal("hello", com3);

                            var res4 = csv.TryReadWithCommentReuse(ref r);
                            Assert.True(res4.HasValue);
                            r = res4.Value;
                            Assert.Equal(-12, r.A);

                            var res5 = csv.TryReadWithCommentReuse(ref r);
                            Assert.True(res5.HasValue);
                            r = res5.Value;
                            Assert.Equal(-48, r.A);

                            var res6 = csv.TryReadWithCommentReuse(ref r);
                            Assert.Equal(ReadWithCommentResultType.NoValue, res6.ResultType);
                        }
                    }
                );
            }
        }

        private sealed class _InstanceSetterWithContext
        {
            public int A { get; private set; }

            public _InstanceSetterWithContext() { }

            public void Setter(int val, in ReadContext ctx)
            {
                A = val * 2;
            }
        }

        [Fact]
        public void InstanceSetterWithContext()
        {
            var t = typeof(_InstanceSetterWithContext).GetTypeInfo();

            var mtd = t.GetMethod(nameof(_InstanceSetterWithContext.Setter), BindingFlags.Public | BindingFlags.Instance);
            var setter = Setter.ForMethod(mtd);

            var tdb = ManualTypeDescriber.CreateBuilder(ManualTypeDescriberFallbackBehavior.UseFallback, TypeDescribers.Default);
            tdb.WithExplicitSetter(t, nameof(_InstanceSetterWithContext.A), setter);

            var td = tdb.ToManualTypeDescriber();

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(td).ToOptions();

            RunSyncReaderVariants<_InstanceSetterWithContext>(
                opts,
                (config, getReader) =>
                {
                    using (var reader = getReader("A\r\n1\r\n2\r\n3"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();
                        Assert.Collection(
                            rows,
                            a => Assert.Equal(2, a.A),
                            b => Assert.Equal(4, b.A),
                            c => Assert.Equal(6, c.A)
                        );
                    }
                }
            );
        }

        private enum _WellKnownSingleColumns
        {
            Foo,
            Bar
        }

        [Flags]
        private enum _WellKnownSingleColumns_Flags
        {
            Foo = 1,
            Bar = 2,
            Fizz = 4
        }

        [Fact]
        public void WellKnownSingleColumns()
        {
            // bool
            {
                RunSyncReaderVariants<bool>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("true\r\nfalse\r\ntrue"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new[] { true, false, true }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // bool?
            {
                RunSyncReaderVariants<bool?>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("\r\nfalse\r\ntrue"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new[] { default(bool?), false, true }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // char
            {
                RunSyncReaderVariants<char>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("a\r\nb\r\nc"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new[] { 'a', 'b', 'c' }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // char?
            {
                RunSyncReaderVariants<char?>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("\r\nb\r\nc"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new[] { default(char?), 'b', 'c' }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // byte
            {
                RunSyncReaderVariants<byte>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("0\r\n128\r\n255"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new byte[] { 0, 128, 255 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // byte?
            {
                RunSyncReaderVariants<byte?>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("0\r\n\r\n255"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new byte?[] { 0, null, 255 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // sbyte
            {
                RunSyncReaderVariants<sbyte>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("0\r\n-127\r\n-2"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new sbyte[] { 0, -127, -2 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // sbyte?
            {
                RunSyncReaderVariants<sbyte?>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("\r\n-127\r\n-2"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new sbyte?[] { null, -127, -2 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // short
            {
                RunSyncReaderVariants<short>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("0\r\n-9876\r\n-16000"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new short[] { 0, -9876, -16000 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // short?
            {
                RunSyncReaderVariants<short?>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("0\r\n\r\n-16000"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new short?[] { 0, null, -16000 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // ushort
            {
                RunSyncReaderVariants<ushort>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("0\r\n12345\r\n32000"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new ushort[] { 0, 12345, 32000 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // ushort?
            {
                RunSyncReaderVariants<ushort?>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("\r\n12345\r\n32000"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new ushort?[] { null, 12345, 32000 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // int
            {
                RunSyncReaderVariants<int>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("0\r\n2000000\r\n-15"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new[] { 0, 2000000, -15 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // int?
            {
                RunSyncReaderVariants<int?>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("\r\n2000000\r\n-15"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new int?[] { null, 2000000, -15 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // uint
            {
                RunSyncReaderVariants<uint>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("0\r\n2000000\r\n4000000000"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new uint[] { 0, 2000000, 4_000_000_000 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // uint?
            {
                RunSyncReaderVariants<uint?>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("\r\n2000000\r\n4000000000"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new uint?[] { null, 2000000, 4_000_000_000 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // long
            {
                RunSyncReaderVariants<long>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader($"0\r\n{long.MinValue}\r\n{long.MaxValue}"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new long[] { 0, long.MinValue, long.MaxValue }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // long?
            {
                RunSyncReaderVariants<long?>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader($"{long.MinValue}\r\n\r\n{long.MaxValue}"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new long?[] { long.MinValue, null, long.MaxValue }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // ulong
            {
                RunSyncReaderVariants<ulong>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader($"0\r\n123\r\n{ulong.MaxValue}"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new ulong[] { 0, 123, ulong.MaxValue }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // ulong?
            {
                RunSyncReaderVariants<ulong?>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader($"0\r\n\r\n{ulong.MaxValue}"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new ulong?[] { 0, null, ulong.MaxValue }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // float
            {
                RunSyncReaderVariants<float>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader($"0.12\r\n123456789.0123\r\n-999999.88888"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new float[] { 0.12f, 123456789.0123f, -999999.88888f }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // float?
            {
                RunSyncReaderVariants<float?>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader($"0.12\r\n\r\n-999999.88888"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new float?[] { 0.12f, null, -999999.88888f }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // double
            {
                RunSyncReaderVariants<double>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader($"0.12\r\n123456789.0123\r\n-999999.88888"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new double[] { 0.12, 123456789.0123, -999999.88888 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // double?
            {
                RunSyncReaderVariants<double?>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader($"0.12\r\n\r\n-999999.88888"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new double?[] { 0.12, null, -999999.88888 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // decimal
            {
                RunSyncReaderVariants<decimal>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader($"0.12\r\n123456789.0123\r\n-999999.88888"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new decimal[] { 0.12m, 123456789.0123m, -999999.88888m }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // decimal?
            {
                RunSyncReaderVariants<decimal?>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader($"0.12\r\n\r\n-999999.88888"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new decimal?[] { 0.12m, null, -999999.88888m }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // string
            {
                RunSyncReaderVariants<string>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader($"hello\r\n\r\nworld"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new string[] { "hello", null, "world" }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // Version
            {
                RunSyncReaderVariants<Version>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader($"1.2\r\n\r\n1.2.3.4"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new[] { new Version(1, 2), null, new Version(1, 2, 3, 4) }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // Uri
            {
                RunSyncReaderVariants<Uri>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader($"http://example.com/\r\n\r\nhttps://stackoverflow.com/questions"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new[] { new Uri("http://example.com/"), null, new Uri("https://stackoverflow.com/questions") }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // enum
            {
                RunSyncReaderVariants<_WellKnownSingleColumns>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader($"Foo\r\nBar\r\nFoo"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new[] { _WellKnownSingleColumns.Foo, _WellKnownSingleColumns.Bar, _WellKnownSingleColumns.Foo }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // enum?
            {
                RunSyncReaderVariants<_WellKnownSingleColumns?>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader($"Foo\r\nBar\r\n\r\n"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new _WellKnownSingleColumns?[] { _WellKnownSingleColumns.Foo, _WellKnownSingleColumns.Bar, null }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // flags enum
            {
                RunSyncReaderVariants<_WellKnownSingleColumns_Flags>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader($"\"Foo, Bar\"\r\nBar\r\nFizz"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new[] { _WellKnownSingleColumns_Flags.Foo | _WellKnownSingleColumns_Flags.Bar, _WellKnownSingleColumns_Flags.Bar, _WellKnownSingleColumns_Flags.Fizz }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // flags enum?
            {
                RunSyncReaderVariants<_WellKnownSingleColumns_Flags?>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader($"\"Foo, Bar\"\r\n\r\nFizz"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new _WellKnownSingleColumns_Flags?[] { _WellKnownSingleColumns_Flags.Foo | _WellKnownSingleColumns_Flags.Bar, null, _WellKnownSingleColumns_Flags.Fizz }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // DateTime
            {
                RunSyncReaderVariants<DateTime>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        var ci = CultureInfo.InvariantCulture;

                        using (var reader =
                            getReader(
                                $"\"{DateTime.MaxValue.ToString(ci)}\"\r\n\"{new DateTime(2020, 04, 23, 0, 0, 0, DateTimeKind.Unspecified).ToString(ci)}\"\r\n\"{DateTime.MinValue.ToString(ci)}\""
                            )
                        )
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            var shouldMatch =
                                new[]
                                {
                                    DateTime.Parse(DateTime.MaxValue.ToString(ci)),
                                    DateTime.Parse(new DateTime(2020, 04, 23, 0, 0, 0, DateTimeKind.Unspecified).ToString(ci)),
                                    DateTime.Parse(DateTime.MinValue.ToString(ci)),
                                };
                            Assert.True(shouldMatch.SequenceEqual(rows));
                        }
                    }
                );
            }

            // DateTime?
            {
                RunSyncReaderVariants<DateTime?>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        var ci = CultureInfo.InvariantCulture;

                        using (var reader =
                            getReader(
                                $"\"{DateTime.MaxValue.ToString(ci)}\"\r\n\r\n\"{DateTime.MinValue.ToString(ci)}\""
                            )
                        )
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            var shouldMatch =
                                new DateTime?[]
                                {
                                    DateTime.Parse(DateTime.MaxValue.ToString(ci)),
                                    null,
                                    DateTime.Parse(DateTime.MinValue.ToString(ci)),
                                };
                            Assert.True(shouldMatch.SequenceEqual(rows));
                        }
                    }
                );
            }

            // DateTimeOffset
            {
                RunSyncReaderVariants<DateTimeOffset>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        var ci = CultureInfo.InvariantCulture;

                        using (var reader =
                            getReader(
                                $"\"{DateTimeOffset.MaxValue.ToString(ci)}\"\r\n\"{new DateTimeOffset(2020, 04, 23, 0, 0, 0, TimeSpan.Zero).ToString(ci)}\"\r\n\"{DateTimeOffset.MinValue.ToString(ci)}\""
                            )
                        )
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            var shouldMatch =
                                new[]
                                {
                                    DateTimeOffset.Parse(DateTimeOffset.MaxValue.ToString(ci)),
                                    DateTimeOffset.Parse(new DateTimeOffset(2020, 04, 23, 0, 0, 0, TimeSpan.Zero).ToString(ci)),
                                    DateTimeOffset.Parse(DateTimeOffset.MinValue.ToString(ci)),
                                };
                            Assert.True(shouldMatch.SequenceEqual(rows));
                        }
                    }
                );
            }

            // DateTimeOffset?
            {
                RunSyncReaderVariants<DateTimeOffset?>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        var ci = CultureInfo.InvariantCulture;

                        using (var reader =
                            getReader(
                                $"\"{DateTimeOffset.MaxValue.ToString(ci)}\"\r\n\r\n\"{DateTimeOffset.MinValue.ToString(ci)}\""
                            )
                        )
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            var shouldMatch =
                                new DateTimeOffset?[]
                                {
                                    DateTimeOffset.Parse(DateTimeOffset.MaxValue.ToString(ci)),
                                    null,
                                    DateTimeOffset.Parse(DateTimeOffset.MinValue.ToString(ci)),
                                };
                            Assert.True(shouldMatch.SequenceEqual(rows));
                        }
                    }
                );
            }

            // Guid
            {
                RunSyncReaderVariants<Guid>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader($"2E9348A1-C3D9-4A9C-95FF-D97591F91542\r\nECB04C56-3042-4234-B757-6AC6E53E10C2"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new[] { Guid.Parse("2E9348A1-C3D9-4A9C-95FF-D97591F91542"), Guid.Parse("ECB04C56-3042-4234-B757-6AC6E53E10C2") }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // Guid?
            {
                RunSyncReaderVariants<Guid?>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader($"2E9348A1-C3D9-4A9C-95FF-D97591F91542\r\n\r\n"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new Guid?[] { Guid.Parse("2E9348A1-C3D9-4A9C-95FF-D97591F91542"), null }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // TimeSpan
            {
                RunSyncReaderVariants<TimeSpan>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader($"\"{TimeSpan.MaxValue}\"\r\n\"{TimeSpan.FromMilliseconds(123456)}\"\r\n\"{TimeSpan.MaxValue}\""))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new[] { TimeSpan.MaxValue, TimeSpan.FromMilliseconds(123456), TimeSpan.MaxValue }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // TimeSpan?
            {
                RunSyncReaderVariants<TimeSpan?>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader($"\"{TimeSpan.MaxValue}\"\r\n\r\n\"{TimeSpan.MaxValue}\""))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new TimeSpan?[] { TimeSpan.MaxValue, null, TimeSpan.MaxValue }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // Index
            {
                RunSyncReaderVariants<Index>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader($"{^1}\r\n{(Index)2}\r\n{^3}"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new[] { ^1, (Index)2, ^3 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // Index?
            {
                RunSyncReaderVariants<Index?>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader($"{^1}\r\n\r\n{^3}"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new Index?[] { ^1, null, ^3 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // Range
            {
                RunSyncReaderVariants<Range>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader($"{1..^1}\r\n{..^2}\r\n{^3..}"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new[] { 1..^1, ..^2, ^3.. }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // Range?
            {
                RunSyncReaderVariants<Range?>(
                    Options.Default,
                    (config, getReader) =>
                    {
                        using (var reader = getReader($"{1..^1}\r\n\r\n{^3..}"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.True(new Range?[] { 1..^1, null, ^3.. }.SequenceEqual(rows));
                        }
                    }
                );
            }
        }

        private struct _ByRefSetter
        {
            public int A { get; set; }
            public int B { get; set; }
            public int C { get; set; }
        }

        private static void _ByRefSetterStaticMethod(ref _ByRefSetter row, int a)
        {
            row.A = a * 2;
        }

        private delegate void _ByRefSetterDelegate(ref _ByRefSetter row, int c, in ReadContext ctx);

        [Fact]
        public void ByRefSetter()
        {
            var t = typeof(_ByRefSetter).GetTypeInfo();
            var byMethod = Setter.ForMethod(typeof(ReaderTests).GetMethod(nameof(_ByRefSetterStaticMethod), BindingFlags.Static | BindingFlags.NonPublic));
            var byKnownDelegate = Setter.ForDelegate((ref _ByRefSetter row, int b, in ReadContext ctx) => { row.B = b * 3; });

            _ByRefSetterDelegate otherDel = (ref _ByRefSetter row, int c, in ReadContext ctx) => { row.C = c * 4; };
            var byOtherDelegate = (Setter)otherDel;

            var m = ManualTypeDescriber.CreateBuilder(ManualTypeDescriberFallbackBehavior.UseFallback, TypeDescribers.Default);
            m.WithExplicitSetter(t, "A", byMethod);
            m.WithExplicitSetter(t, "B", byKnownDelegate);
            m.WithExplicitSetter(t, "C", byOtherDelegate);

            var td = m.ToManualTypeDescriber();

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(td).ToOptions();

            RunSyncReaderVariants<_ByRefSetter>(
                opts,
                (config, getReader) =>
                {
                    using (var reader = getReader("A,B,C\r\n1,2,3\r\n4,5,6\r\n7,8,9"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();

                        Assert.Collection(
                            rows,
                            a =>
                            {
                                Assert.Equal(1 * 2, a.A);
                                Assert.Equal(2 * 3, a.B);
                                Assert.Equal(3 * 4, a.C);
                            },
                            b =>
                            {
                                Assert.Equal(4 * 2, b.A);
                                Assert.Equal(5 * 3, b.B);
                                Assert.Equal(6 * 4, b.C);
                            },
                            c =>
                            {
                                Assert.Equal(7 * 2, c.A);
                                Assert.Equal(8 * 3, c.B);
                                Assert.Equal(9 * 4, c.C);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public void ShallowReadContexts()
        {
            var ccCtx = ReadContext.ConvertingColumn(Options.Default, 0, ColumnIdentifier.Create(1), "foo");
            var crCtx = ReadContext.ConvertingRow(Options.Default, 2, "bar");
            var rcCtx = ReadContext.ReadingColumn(Options.Default, 3, ColumnIdentifier.Create(4), "fizz");
            var rrCtx = ReadContext.ReadingRow(Options.Default, 5, "buzz");

            var shallowCC = new ShallowReadContext(in ccCtx);
            Assert.Equal(0, shallowCC.RowNumber);
            Assert.Equal(1, shallowCC.ColumnIndex);
            Assert.Equal(ReadContextMode.ConvertingColumn, shallowCC.Mode);

            var shallowCR = new ShallowReadContext(in crCtx);
            Assert.Equal(2, shallowCR.RowNumber);
            Assert.Equal(-1, shallowCR.ColumnIndex);
            Assert.Equal(ReadContextMode.ConvertingRow, shallowCR.Mode);

            var shallowRC = new ShallowReadContext(in rcCtx);
            Assert.Equal(3, shallowRC.RowNumber);
            Assert.Equal(4, shallowRC.ColumnIndex);
            Assert.Equal(ReadContextMode.ReadingColumn, shallowRC.Mode);

            var shallowRR = new ShallowReadContext(in rrCtx);
            Assert.Equal(5, shallowRR.RowNumber);
            Assert.Equal(-1, shallowRR.ColumnIndex);
            Assert.Equal(ReadContextMode.ReadingRow, shallowRR.Mode);
        }

        private sealed class _ThrowOnExcessColumns
        {
            public string A { get; set; }
            public string B { get; set; }
        }

        [Fact]
        public void ThrowOnExcessColumns()
        {
            var opts = Options.CreateBuilder(Options.Default).WithExtraColumnTreatment(ExtraColumnTreatment.ThrowException).ToOptions();

            // with headers
            {
                // fine, shouldn't throw
                RunSyncReaderVariants<_ThrowOnExcessColumns>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("A,B\r\nhello,world\r\n"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("hello", a.A); Assert.Equal("world", a.B); }
                            );
                        }
                    }
                );

                // should throw on second read
                RunSyncReaderVariants<_ThrowOnExcessColumns>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("A,B\r\nhello,world\r\nfizz,buzz,bazz"))
                        using (var csv = config.CreateReader(reader))
                        {
                            Assert.True(csv.TryRead(out var row));
                            Assert.Equal("hello", row.A);
                            Assert.Equal("world", row.B);

                            Assert.Throws<InvalidOperationException>(() => csv.TryRead(out var row2));
                        }
                    }
                );
            }

            // no headers
            {
                var noHeadersOpts = Options.CreateBuilder(opts).WithReadHeader(ReadHeader.Never).ToOptions();

                // fine, shouldn't throw
                RunSyncReaderVariants<_ThrowOnExcessColumns>(
                    noHeadersOpts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("hello,world\r\n"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("hello", a.A); Assert.Equal("world", a.B); }
                            );
                        }
                    }
                );

                // should throw on second read
                RunSyncReaderVariants<_ThrowOnExcessColumns>(
                    noHeadersOpts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("hello,world\r\nfizz,buzz,bazz"))
                        using (var csv = config.CreateReader(reader))
                        {
                            Assert.True(csv.TryRead(out var row));
                            Assert.Equal("hello", row.A);
                            Assert.Equal("world", row.B);

                            Assert.Throws<InvalidOperationException>(() => csv.TryRead(out var row2));
                        }
                    }
                );
            }
        }

        private sealed class _IgnoreExcessColumns
        {
            public string A { get; set; }
            public string B { get; set; }
        }

        [Fact]
        public void IgnoreExcessColumns()
        {
            // with headers
            RunSyncReaderVariants<_IgnoreExcessColumns>(
                Options.Default,
                (config, getReader) =>
                {
                    using (var reader = getReader("A,B\r\nhello,world\r\nfizz,buzz,bazz\r\nfe,fi,fo,fum"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();

                        Assert.Collection(
                            rows,
                            a => { Assert.Equal("hello", a.A); Assert.Equal("world", a.B); },
                            a => { Assert.Equal("fizz", a.A); Assert.Equal("buzz", a.B); },
                            a => { Assert.Equal("fe", a.A); Assert.Equal("fi", a.B); }
                        );
                    }
                }
            );

            // without headers
            var noHeadersOpts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();
            RunSyncReaderVariants<_IgnoreExcessColumns>(
                noHeadersOpts,
                (config, getReader) =>
                {
                    using (var reader = getReader("hello,world\r\nfizz,buzz,bazz\r\nfe,fi,fo,fum"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();

                        Assert.Collection(
                            rows,
                            a => { Assert.Equal("hello", a.A); Assert.Equal("world", a.B); },
                            a => { Assert.Equal("fizz", a.A); Assert.Equal("buzz", a.B); },
                            a => { Assert.Equal("fe", a.A); Assert.Equal("fi", a.B); }
                        );
                    }
                }
            );
        }

        private sealed class _VariousResets
        {
            private int _A;
            public int A
            {
                get => _A;
                set => _A = _A * value;
            }
            public static void ResetA_Row_Context(_VariousResets row, ref ReadContext _)
            => row._A = 3;

            public static int _B;
            public int B
            {
                get => _B;
                set => _B = _B + value;
            }
            public static void ResetB_NoRow_Context(ref ReadContext _)
            {
                _B = 4;
            }

            public int _C;
            public int C
            {
                get => _C;
                set => _C = _C * 2;
            }
            public void ResetC_Context(ref ReadContext _)
            {
                _C = 5;
            }

            private int _D;
            public int D
            {
                get => _D;
                set => _D += value;
            }

            public static void ResetD_Row_ByRef(ref _VariousResets row, ref ReadContext _)
            => (row = new _VariousResets()).D = 6;
        }

        [Fact]
        public void VariousResets()
        {
            var t = typeof(_VariousResets).GetTypeInfo();
            var cons = t.GetConstructors().Single();

            var a = t.GetPropertyNonNull(nameof(_VariousResets.A), BindingFlags.Public | BindingFlags.Instance);
            var aReset = t.GetMethodNonNull(nameof(_VariousResets.ResetA_Row_Context), BindingFlags.Public | BindingFlags.Static);
            var b = t.GetPropertyNonNull(nameof(_VariousResets.B), BindingFlags.Public | BindingFlags.Instance);
            var bReset = t.GetMethodNonNull(nameof(_VariousResets.ResetB_NoRow_Context), BindingFlags.Public | BindingFlags.Static);
            var c = t.GetPropertyNonNull(nameof(_VariousResets.C), BindingFlags.Public | BindingFlags.Instance);
            var cReset = t.GetMethodNonNull(nameof(_VariousResets.ResetC_Context), BindingFlags.Public | BindingFlags.Instance);
            var d = t.GetPropertyNonNull(nameof(_VariousResets.D), BindingFlags.Public | BindingFlags.Instance);
            var dReset = t.GetMethodNonNull(nameof(_VariousResets.ResetD_Row_ByRef), BindingFlags.Public | BindingFlags.Static);

            var m = ManualTypeDescriber.CreateBuilder();
            m.WithInstanceProvider(InstanceProvider.ForParameterlessConstructor(cons));
            m.WithExplicitSetter(t, "A", Setter.ForProperty(a), Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.No, Reset.ForMethod(aReset));
            m.WithExplicitSetter(t, "B", Setter.ForProperty(b), Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.No, Reset.ForMethod(bReset));
            m.WithExplicitSetter(t, "C", Setter.ForProperty(c), Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.No, Reset.ForMethod(cReset));

            var td = m.ToManualTypeDescriber();
            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(td).ToOptions();

            RunSyncReaderVariants<_VariousResets>(
                opts,
                (config, getReader) =>
                {
                    _VariousResets._B = 123;

                    using (var reader = getReader("A,B,C\r\n4,5,6"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();

                        Assert.Collection(
                            rows,
                            r =>
                            {
                                Assert.Equal(12, r.A);
                                Assert.Equal(9, r.B);
                                Assert.Equal(10, r.C);
                                Assert.Equal(0, r.D);
                            }
                        );
                    }
                }
            );

            // now with D
            m.WithExplicitSetter(t, "D", Setter.ForProperty(d), Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.No, Reset.ForMethod(dReset));
            var td2 = m.ToManualTypeDescriber();
            var opts2 = Options.CreateBuilder(Options.Default).WithTypeDescriber(td2).ToOptions();

            RunSyncReaderVariants<_VariousResets>(
                opts2,
                (config, getReader) =>
                {
                    _VariousResets._B = 8675;

                    using (var reader = getReader("A,B,C,D\r\n4,5,6,7"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var pre = new _VariousResets();
                        var oldPre = pre;
                        Assert.True(csv.TryReadWithReuse(ref pre));

                        Assert.NotSame(oldPre, pre);
                        Assert.Equal(0, pre.A);
                        Assert.Equal(9, pre.B);
                        Assert.Equal(0, pre.C);
                        Assert.Equal(13, pre.D);
                    }
                }
            );
        }

        private sealed class _VariousSetters
        {
            public int A { get; private set; }
            public string B { get; private set; }
            public int C { get; private set; }

            public _VariousSetters(int a)
            {
                A = -a;
            }


            public static void StaticSetter_Row_NoContext(_VariousSetters s, string b)
            {
                s.B = b + "." + b;
            }

            public static void StaticSetter_Row_Context(_VariousSetters s, int c, ref ReadContext ctx)
            {
                s.C = c + 1;
            }

            public static int D;
            public static void StaticSetter_NoRow_Context(int d, ref ReadContext ctx)
            {
                D = d + 2;
            }
        }

        [Fact]
        public void VariousSetters()
        {
            var t = typeof(_VariousSetters).GetTypeInfo();
            var cons = t.GetConstructors().Single();

            var m = ManualTypeDescriber.CreateBuilder();
            m.WithInstanceProvider(InstanceProvider.ForConstructorWithParameters(cons));
            m.WithExplicitSetter(t, "A", Setter.ForConstructorParameter(cons.GetParameters().Single()), Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes);
            m.WithExplicitSetter(t, "B", Setter.ForMethod(t.GetMethod(nameof(_VariousSetters.StaticSetter_Row_NoContext))));
            m.WithExplicitSetter(t, "C", Setter.ForMethod(t.GetMethod(nameof(_VariousSetters.StaticSetter_Row_Context))));
            m.WithExplicitSetter(t, "D", Setter.ForMethod(t.GetMethod(nameof(_VariousSetters.StaticSetter_NoRow_Context))));

            var td = m.ToManualTypeDescriber();
            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(td).ToOptions();

            RunSyncReaderVariants<_VariousSetters>(
                opts,
                (config, getReader) =>
                {
                    _VariousSetters.D = 0;

                    using (var reader = getReader("A,B,C,D\r\n1,foo,2,3"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();

                        Assert.Collection(
                            rows,
                            r =>
                            {
                                Assert.Equal(-1, r.A);
                                Assert.Equal("foo.foo", r.B);
                                Assert.Equal(3, r.C);
                                Assert.Equal(5, _VariousSetters.D);
                            }
                        );
                    }
                }
            );
        }

        private sealed class _PoisonedTryReadWithCommentReuse
        {
            public string Foo { get; set; }
        }

        [Fact]
        public void PoisonedTryReadWithCommentReuse()
        {
            var setter = Setter.ForDelegate((_PoisonedTryReadWithCommentReuse row, string val, in ReadContext _) => throw new Exception());

            var type = typeof(_PoisonedTryReadWithCommentReuse).GetTypeInfo();
            var cons = type.GetConstructor(Type.EmptyTypes);
            var provider = InstanceProvider.ForParameterlessConstructor(cons);

            var m = ManualTypeDescriberBuilder.CreateBuilder().WithInstanceProvider(provider).WithExplicitSetter(type, "Foo", setter).ToManualTypeDescriber();

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(m).ToOptions();

            RunSyncReaderVariants<_PoisonedTryReadWithCommentReuse>(
                opts,
                (config, getReader) =>
                {
                    using (var reader = getReader("Foo\r\nbar"))
                    using (var csv = config.CreateReader(reader))
                    {
                        Assert.Throws<Exception>(
                            () =>
                            {
                                _PoisonedTryReadWithCommentReuse row = null;
                                csv.TryReadWithCommentReuse(ref row);
                            }
                        );

                        var poisonable = csv as PoisonableBase;

                        Assert.Equal(PoisonType.Exception, poisonable.Poison.Value);
                    }
                }
            );
        }

        private sealed class _ChainedParsers
        {
            public int Foo { get; set; }
        }

        private sealed class _ChainedParsers_Context
        {
            public int Num { get; set; }
        }

        [Fact]
        public void ChainedParsers()
        {
            var p0 =
                Parser.ForDelegate(
                    (ReadOnlySpan<char> data, in ReadContext ctx, out int val) =>
                    {
                        var a = (_ChainedParsers_Context)ctx.Context;

                        if (a.Num != 1)
                        {
                            val = default;
                            return false;
                        }

                        val = int.Parse(data);
                        val *= 2;

                        return true;
                    }
                );

            var p1 =
                Parser.ForDelegate(
                    (ReadOnlySpan<char> data, in ReadContext ctx, out int val) =>
                    {
                        var a = (_ChainedParsers_Context)ctx.Context;

                        if (a.Num != 2)
                        {
                            val = default;
                            return false;
                        }

                        val = int.Parse(data);
                        val--;

                        return true;
                    }
                );

            var p2 =
                Parser.ForDelegate(
                    (ReadOnlySpan<char> data, in ReadContext ctx, out int val) =>
                    {
                        var a = (_ChainedParsers_Context)ctx.Context;

                        if (a.Num != 3)
                        {
                            val = default;
                            return false;
                        }

                        val = int.Parse(data);
                        val = -(val << 3);

                        return true;
                    }
                );

            var p = p0.Else(p1).Else(p2);

            var i = InstanceProvider.ForParameterlessConstructor(typeof(_ChainedParsers).GetConstructor(Type.EmptyTypes));

            var m = ManualTypeDescriber.CreateBuilder();
            m.WithInstanceProvider(i);
            m.WithExplicitSetter(
                typeof(_ChainedParsers).GetTypeInfo(),
                nameof(_ChainedParsers.Foo),
                Setter.ForMethod(typeof(_ChainedParsers).GetProperty(nameof(_ChainedParsers.Foo)).SetMethod),
                p
            );

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(m.ToManualTypeDescriber()).ToOptions();

            RunSyncReaderVariants<_ChainedParsers>(
                opts,
                (config, getReader) =>
                {
                    var ctx = new _ChainedParsers_Context();

                    using (var reader = getReader("Foo\r\n1\r\n2\r\n3\r\n4"))
                    using (var csv = config.CreateReader(reader, ctx))
                    {
                        ctx.Num = 1;
                        Assert.True(csv.TryRead(out var r1));
                        Assert.Equal(2, r1.Foo);

                        ctx.Num = 2;
                        Assert.True(csv.TryRead(out var r2));
                        Assert.Equal(1, r2.Foo);

                        ctx.Num = 3;
                        Assert.True(csv.TryRead(out var r3));
                        Assert.Equal(-(3 << 3), r3.Foo);

                        ctx.Num = 4;
                        Assert.Throws<SerializationException>(() => csv.TryRead(out _));
                    }
                }
            );
        }

        private sealed class _ChainedInstanceProviders
        {
            public int Cons { get; }
            public string Foo { get; set; }

            public _ChainedInstanceProviders(int v)
            {
                Cons = v;
            }
        }

        [Fact]
        public void ChainedInstanceProviders()
        {
            var num = 0;

            var i1 =
                InstanceProvider.ForDelegate(
                    (in ReadContext _, out _ChainedInstanceProviders res) =>
                    {
                        if (num == 1)
                        {
                            res = new _ChainedInstanceProviders(100);
                            return true;
                        }

                        res = null;
                        return false;
                    }
                );
            var i2 =
                InstanceProvider.ForDelegate(
                    (in ReadContext _, out _ChainedInstanceProviders res) =>
                    {
                        if (num == 2)
                        {
                            res = new _ChainedInstanceProviders(123);
                            return true;
                        }

                        res = null;
                        return false;
                    }
                );
            var i3 =
                InstanceProvider.ForDelegate(
                    (in ReadContext _, out _ChainedInstanceProviders res) =>
                    {
                        if (num == 3)
                        {
                            res = new _ChainedInstanceProviders(999);
                            return true;
                        }

                        res = null;
                        return false;
                    }
                );

            var i = i1.Else(i2).Else(i3);

            var m = ManualTypeDescriber.CreateBuilder();
            m.WithInstanceProvider(i);
            m.WithExplicitSetter(
                typeof(_ChainedInstanceProviders).GetTypeInfo(),
                nameof(_ChainedInstanceProviders.Foo),
                Setter.ForMethod(typeof(_ChainedInstanceProviders).GetProperty(nameof(_ChainedInstanceProviders.Foo)).SetMethod)
            );

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(m.ToManualTypeDescriber()).ToOptions();

            RunSyncReaderVariants<_ChainedInstanceProviders>(
                opts,
                (config, getReader) =>
                {
                    using (var reader = getReader("Foo\r\nabc\r\n123\r\neasy\r\nhard"))
                    using (var csv = config.CreateReader(reader))
                    {
                        num = 1;
                        Assert.True(csv.TryRead(out var r1));
                        Assert.Equal(100, r1.Cons);
                        Assert.Equal("abc", r1.Foo);

                        num = 3;
                        Assert.True(csv.TryRead(out var r2));
                        Assert.Equal(999, r2.Cons);
                        Assert.Equal("123", r2.Foo);

                        num = 2;
                        Assert.True(csv.TryRead(out var r3));
                        Assert.Equal(123, r3.Cons);
                        Assert.Equal("easy", r3.Foo);

                        num = -1;
                        Assert.Throws<InvalidOperationException>(() => csv.TryRead(out _));
                    }
                }
            );
        }

        private sealed class _WhitespaceTrimming
        {
            public string Foo { get; set; }
            public int Bar { get; set; }
        }

        [Fact]
        public void WhitespaceTrimming()
        {
            // in values
            {
                // leading
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWhitespaceTreatment(WhitespaceTreatments.TrimLeadingInValues).ToOptions();

                    RunSyncReaderVariants<_WhitespaceTrimming>(
                        opts,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("  Foo,  Bar\r\nhello,123\r\n   world,\t456\r\n\"\t \nfizz\",\"\t\t\t789\""))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.Foo);
                                        Assert.Equal(123, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", a.Foo);
                                        Assert.Equal(456, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("fizz", a.Foo);
                                        Assert.Equal(789, a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }

                // trailing
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWhitespaceTreatment(WhitespaceTreatments.TrimTrailingInValues).ToOptions();

                    RunSyncReaderVariants<_WhitespaceTrimming>(
                        opts,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("Foo   ,Bar   \r\nhello,123\r\nworld   ,456\t\r\n\"fizz\t \n\",\"789\t\t\t\""))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.Foo);
                                        Assert.Equal(123, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", a.Foo);
                                        Assert.Equal(456, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("fizz", a.Foo);
                                        Assert.Equal(789, a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }

                // both
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWhitespaceTreatment(WhitespaceTreatments.TrimInValues).ToOptions();

                    RunSyncReaderVariants<_WhitespaceTrimming>(
                        opts,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("   Foo   ,   Bar   \r\nhello,123\r\n\tworld   ,   456\t\r\n\"\tfizz\t \n\",\"\t789\t\t\t\""))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.Foo);
                                        Assert.Equal(123, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", a.Foo);
                                        Assert.Equal(456, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("fizz", a.Foo);
                                        Assert.Equal(789, a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }
            }

            // outside of values
            {
                // leading
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWhitespaceTreatment(WhitespaceTreatments.TrimBeforeValues).ToOptions();

                    RunSyncReaderVariants<_WhitespaceTrimming>(
                        opts,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("  Foo,  Bar\r\nhello,123\r\n   world,\t456\r\n\"\t \nfizz\", \t \"789\""))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.Foo);
                                        Assert.Equal(123, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", a.Foo);
                                        Assert.Equal(456, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("\t \nfizz", a.Foo);
                                        Assert.Equal(789, a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }

                // trailing
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWhitespaceTreatment(WhitespaceTreatments.TrimAfterValues).ToOptions();

                    RunSyncReaderVariants<_WhitespaceTrimming>(
                        opts,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("Foo  ,\"Bar\"  \r\nhello,123\r\nworld   ,456\t\r\n\"fizz\t \n\",\"789\" \t "))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.Foo);
                                        Assert.Equal(123, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", a.Foo);
                                        Assert.Equal(456, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("fizz\t \n", a.Foo);
                                        Assert.Equal(789, a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }

                // leading
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWhitespaceTreatment(WhitespaceTreatments.TrimBeforeValues).ToOptions();

                    RunSyncReaderVariants<_WhitespaceTrimming>(
                        opts,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("  Foo,  Bar\r\nhello,123\r\n   world,\t456\r\n\"\t \nfizz\", \t \"789\""))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.Foo);
                                        Assert.Equal(123, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", a.Foo);
                                        Assert.Equal(456, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("\t \nfizz", a.Foo);
                                        Assert.Equal(789, a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }

                // both
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWhitespaceTreatment(WhitespaceTreatments.TrimBeforeValues | WhitespaceTreatments.TrimAfterValues).ToOptions();

                    RunSyncReaderVariants<_WhitespaceTrimming>(
                        opts,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("  Foo  ,\t\"Bar\"  \r\nhello,123\r\n\t world   ,456\t\r\n  \"fizz\t \n\",  \"789\" \t "))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.Foo);
                                        Assert.Equal(123, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", a.Foo);
                                        Assert.Equal(456, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("fizz\t \n", a.Foo);
                                        Assert.Equal(789, a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }
            }

            // inside and outside of values
            {
                var opts = Options.CreateBuilder(Options.Default).WithWhitespaceTreatment(WhitespaceTreatments.Trim).ToOptions();

                RunSyncReaderVariants<_WhitespaceTrimming>(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("  \"  Foo  \"  ,\t\t\"\tBar\t\"  \r\nhello,123\r\n\t world   ,456\t\r\n  \"fizz\t \n\",  \"  789\r\n\" \t "))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(
                                rows,
                                a =>
                                {
                                    Assert.Equal("hello", a.Foo);
                                    Assert.Equal(123, a.Bar);
                                },
                                a =>
                                {
                                    Assert.Equal("world", a.Foo);
                                    Assert.Equal(456, a.Bar);
                                },
                                a =>
                                {
                                    Assert.Equal("fizz", a.Foo);
                                    Assert.Equal(789, a.Bar);
                                }
                            );
                        }
                    }
                );
            }

            // none
            {
                // no changes in values
                RunSyncReaderVariants<_WhitespaceTrimming>(
                    Options.Default,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("Foo,\"Bar\"\r\nhello\t,123\r\n  world,456\r\n\"\r\nfizz\",\"789\""))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(
                                rows,
                                a =>
                                {
                                    Assert.Equal("hello\t", a.Foo);
                                    Assert.Equal(123, a.Bar);
                                },
                                a =>
                                {
                                    Assert.Equal("  world", a.Foo);
                                    Assert.Equal(456, a.Bar);
                                },
                                a =>
                                {
                                    Assert.Equal("\r\nfizz", a.Foo);
                                    Assert.Equal(789, a.Bar);
                                }
                            );
                        }
                    }
                );

                // bad headers
                {
                    // leading value
                    RunSyncReaderVariants<_WhitespaceTrimming>(
                        Options.Default,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("  Foo,\"Bar\"\r\nfoo,123"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();

                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        // Foo is smashed
                                        Assert.Null(a.Foo);
                                        // Bar is fine
                                        Assert.Equal(123, a.Bar);
                                    }
                                );
                            }
                        }
                    );

                    // trailing value
                    RunSyncReaderVariants<_WhitespaceTrimming>(
                        Options.Default,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("Foo\t,\"Bar\"\r\nfoo,123"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();

                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        // Foo is smashed
                                        Assert.Null(a.Foo);
                                        // Bar is fine
                                        Assert.Equal(123, a.Bar);
                                    }
                                );
                            }
                        }
                    );

                    // leading value, escaped
                    RunSyncReaderVariants<_WhitespaceTrimming>(
                        Options.Default,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("Foo,\"  Bar\"\r\nfoo,123"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();

                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        // Foo is fine
                                        Assert.Equal("foo", a.Foo);
                                        // Bar is smashed
                                        Assert.Equal(0, a.Bar);
                                    }
                                );
                            }
                        }
                    );

                    // leading value, escaped, exceptional
                    RunSyncReaderVariants<_WhitespaceTrimming>(
                        Options.Default,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("Foo,\t\"  Bar\"\r\nfoo,123"))
                            using (var csv = config.CreateReader(reader))
                            {
                                Assert.Throws<InvalidOperationException>(() => csv.ReadAll());
                            }
                        }
                    );

                    // trailing value, escaped
                    RunSyncReaderVariants<_WhitespaceTrimming>(
                        Options.Default,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("Foo,\"Bar\r\n\"\r\nfoo,123"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();

                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        // Foo is fine
                                        Assert.Equal("foo", a.Foo);
                                        // Bar is smashed
                                        Assert.Equal(0, a.Bar);
                                    }
                                );
                            }
                        }
                    );

                    // trailing value, escaped, exceptional
                    RunSyncReaderVariants<_WhitespaceTrimming>(
                        Options.Default,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("Foo,\"Bar\r\n\"\t\t\r\nfoo,123"))
                            using (var csv = config.CreateReader(reader))
                            {
                                Assert.Throws<InvalidOperationException>(() => csv.ReadAll());
                            }
                        }
                    );
                }
            }
        }

        private sealed class _MissingHeaders
        {
            public string Foo { get; set; }
        }

        [Fact]
        public void MissingHeaders()
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions();

            RunSyncReaderVariants<_MissingHeaders>(
                opts,
                (config, getReader) =>
                {
                    using (var reader = getReader("fizz"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var exc = Assert.Throws<InvalidOperationException>(() => csv.TryRead(out _));

                        Assert.Equal("First row of input was not a row of headers", exc.Message);
                    }
                }
            );
        }

        private sealed class _ReadAllEmpty
        {
            public string Fizz { get; set; }
        }

        [Fact]
        public void ReadAllEmpty()
        {
            RunSyncReaderVariants<_ReadAllEmpty>(
                Options.Default,
                (config, getReader) =>
                {
                    using (var reader = getReader(""))
                    using (var csv = config.CreateReader(reader))
                    {
                        var res = csv.ReadAll();

                        Assert.Empty(res);
                    }
                }
            );
        }

        private sealed class _ReadOneThenAll
        {
            public string Foo { get; set; }
        }

        [Fact]
        public void ReadOneThenAll()
        {
            RunSyncReaderVariants<_ReadOneThenAll>(
                Options.Default,
                (config, getReader) =>
                {
                    using (var reader = getReader("Foo\r\nbar\r\nfizz\r\nbuzz"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = new List<_ReadOneThenAll>();

                        Assert.True(csv.TryRead(out var row));
                        rows.Add(row);

                        csv.ReadAll(rows);

                        Assert.Collection(
                            rows,
                            a => Assert.Equal("bar", a.Foo),
                            b => Assert.Equal("fizz", b.Foo),
                            c => Assert.Equal("buzz", c.Foo)
                        );
                    }
                }
            );
        }

        private sealed class _NullInto
        {
            public string A { get; set; }
        }

        [Fact]
        public void NullInto()
        {
            RunSyncReaderVariants<_NullInto>(
                Options.Default,
                (config, getReader) =>
                {
                    using (var reader = getReader(""))
                    using (var csv = config.CreateReader(reader))
                    {
                        Assert.Throws<ArgumentNullException>(() => csv.ReadAll(default(List<_NullInto>)));
                    }
                }
            );
        }

        private sealed class _UncommonAdvanceResults
        {
            public string A { get; set; }
        }

        [Fact]
        public void UncommonAdvanceResults()
        {
            {
                var opts = Options.CreateBuilder(Options.Default).WithRowEnding(RowEnding.CarriageReturnLineFeed).WithEscapedValueStartAndEnd('\\').ToOptions();

                // escape char after \r
                RunSyncReaderVariants<_UncommonAdvanceResults>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("hello\r\\"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var exc = Assert.Throws<InvalidOperationException>(() => csv.TryRead(out _));

                            Assert.Equal("Encountered '\\' when expecting end of record", exc.Message);
                        }
                    }
                );
            }

            {
                var opts = Options.CreateBuilder(Options.Default).WithRowEnding(RowEnding.CarriageReturnLineFeed).WithEscapedValueStartAndEnd('\\').WithReadHeader(ReadHeader.Never).ToOptions();

                // kept reading after things were busted
                RunSyncReaderVariants<_UncommonAdvanceResults>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("hello\r\\ " + string.Join(" ", Enumerable.Repeat('c', 1000))))
                        using (var csv = config.CreateReader(reader))
                        {
                            Assert.ThrowsAny<Exception>(() => csv.TryRead(out _));

                            Assert.Throws<InvalidOperationException>(() => csv.TryRead(out _));
                        }
                    }
                );
            }

            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

                // kept reading after things were busted
                RunSyncReaderVariants<_UncommonAdvanceResults>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("hel\"lo"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var exc = Assert.Throws<InvalidOperationException>(() => csv.TryRead(out _));
                            Assert.Equal("Encountered '\"', starting an escaped value, when already in a value", exc.Message);
                        }
                    }
                );
            }

            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

                // kept reading after things were busted
                RunSyncReaderVariants<_UncommonAdvanceResults>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("\"hel\"lo\""))
                        using (var csv = config.CreateReader(reader))
                        {
                            var exc = Assert.Throws<InvalidOperationException>(() => csv.TryRead(out _));
                            Assert.Equal("Encountered 'l' in an escape sequence, which is invalid", exc.Message);
                        }
                    }
                );
            }

            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions();

                // kept reading after things were busted
                RunSyncReaderVariants<_UncommonAdvanceResults>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("\"A"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var exc = Assert.Throws<InvalidOperationException>(() => csv.TryRead(out _));
                            Assert.Equal("Data ended unexpectedly", exc.Message);
                        }
                    }
                );
            }
        }

        private sealed class _CommentEndingInCarriageReturn
        {
            public string Foo { get; set; }
        }

        [Fact]
        public void CommentEndingInCarriageReturn()
        {
            var opt = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

            RunSyncReaderVariants<_CommentEndingInCarriageReturn>(
                opt,
                (config, getReader) =>
                {
                    using (var reader = getReader("#\r"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var res = csv.TryReadWithComment();
                        Assert.True(res.HasComment);
                        Assert.Equal("\r", res.Comment);

                        res = csv.TryReadWithComment();
                        Assert.Equal(ReadWithCommentResultType.NoValue, res.ResultType);
                    }
                }
            );
        }

        private sealed class _ReadOnlyByteSequence
        {
            public string Hello { get; set; }
            public string World { get; set; }
        }

        [Fact]
        public void ReadOnlyByteSequence()
        {
            var txt = Encoding.UTF32.GetBytes("Hello,World\r\nfoo,bar");

            var config = Configuration.For<_ReadOnlyByteSequence>();
            using (var csv = config.CreateReader(new ReadOnlySequence<byte>(txt.AsMemory()), Encoding.UTF32))
            {
                var rows = csv.ReadAll();

                Assert.Collection(
                    rows,
                    a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); }
                );
            }
        }

        private sealed class _ReadOnlyCharSequence
        {
            public string Hello { get; set; }
            public string World { get; set; }
        }

        [Fact]
        public void ReadOnlyCharSequence()
        {
            var txt = "Hello,World\r\nfoo,bar".ToArray();

            var config = Configuration.For<_ReadOnlyCharSequence>();
            using (var csv = config.CreateReader(new ReadOnlySequence<char>(txt.AsMemory())))
            {
                var rows = csv.ReadAll();

                Assert.Collection(
                    rows,
                    a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); }
                );
            }
        }

        private sealed class _FailingParser
        {
            public string Foo { get; set; }
        }

        [Fact]
        public void FailingParser()
        {
            var m = ManualTypeDescriberBuilder.CreateBuilder(ManualTypeDescriberFallbackBehavior.UseFallback);

            m.WithInstanceProvider(InstanceProvider.ForDelegate((in ReadContext _, out _FailingParser val) => { val = new _FailingParser(); return true; }));

            var t = typeof(_FailingParser).GetTypeInfo();
            var s = Setter.ForMethod(t.GetProperty(nameof(_FailingParser.Foo)).SetMethod);
            var p = Parser.ForDelegate((ReadOnlySpan<char> data, in ReadContext ctx, out string result) => { result = ""; return false; });

            m.WithExplicitSetter(t, "Foo", s, p);

            var opt = Options.CreateBuilder(Options.Default).WithTypeDescriber(m.ToManualTypeDescriber()).ToOptions();

            RunSyncReaderVariants<_FailingParser>(
                opt,
                (config, getReader) =>
                {
                    using (var r = getReader("hello"))
                    using (var csv = config.CreateReader(r))
                    {
                        Assert.Throws<SerializationException>(() => csv.ReadAll());
                    }
                }
            );
        }

        private class _NonGenericEnumerator
        {
            public string Foo { get; set; }
            public string Bar { get; set; }
        }

        [Fact]
        public void NonGenericEnumerator()
        {
            RunSyncReaderVariants<_NonGenericEnumerator>(
                Options.Default,
                (config, getReader) =>
                {
                    using (var reader = getReader("hello,world\r\nfizz,buzz"))
                    using (var csv = config.CreateReader(reader))
                    {
                        System.Collections.IEnumerable e = csv.EnumerateAll();

                        int ix = 0;
                        var i = e.GetEnumerator();
                        while (i.MoveNext())
                        {
                            object c = i.Current;
                            switch (ix)
                            {
                                case 0:
                                    {
                                        var a = (_NonGenericEnumerator)c;
                                        Assert.Equal("hello", a.Foo);
                                        Assert.Equal("world", a.Bar);
                                    }
                                    break;
                                case 1:
                                    {
                                        var a = (_NonGenericEnumerator)c;
                                        Assert.Equal("fizz", a.Foo);
                                        Assert.Equal("buzz", a.Bar);
                                    }
                                    break;
                                default:
                                    Assert.NotNull("Shouldn't be possible");
                                    break;
                            }

                            ix++;
                        }

                        Assert.Equal(2, ix);

                        Assert.Throws<NotSupportedException>(() => i.Reset());
                    }
                }
            );
        }

        private class _DeserializableMemberHelpers
        {
#pragma warning disable CS0649
            public int Field;
#pragma warning restore CS0649
            public string Prop { get; set; }
        }

        [Fact]
        public void DeserializableMemberHelpers()
        {
            var t = typeof(_DeserializableMemberHelpers).GetTypeInfo();

            // fields
            {
                var f = t.GetField(nameof(_DeserializableMemberHelpers.Field));

                // 1
                {
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForField(null));

                    var d1 = DeserializableMember.ForField(f);
                    Assert.False(d1.IsRequired);
                    Assert.Equal("Field", d1.Name);
                    Assert.Equal(Parser.GetDefault(typeof(int).GetTypeInfo()), d1.Parser);
                    Assert.False(d1.Reset.HasValue);
                    Assert.Equal(Setter.ForField(f), d1.Setter);
                }

                // 2
                {
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForField(null, "Foo"));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForField(f, null));

                    var d1 = DeserializableMember.ForField(f, "Foo");
                    Assert.False(d1.IsRequired);
                    Assert.Equal("Foo", d1.Name);
                    Assert.Equal(Parser.GetDefault(typeof(int).GetTypeInfo()), d1.Parser);
                    Assert.False(d1.Reset.HasValue);
                    Assert.Equal(Setter.ForField(f), d1.Setter);
                }

                var parser = Parser.ForDelegate<int>((ReadOnlySpan<char> _, in ReadContext rc, out int v) => { v = 1; return true; });

                // 3
                {
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForField(null, "Bar", parser));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForField(f, null, parser));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForField(f, "Bar", null));

                    var d1 = DeserializableMember.ForField(f, "Bar", parser);
                    Assert.False(d1.IsRequired);
                    Assert.Equal("Bar", d1.Name);
                    Assert.Equal(parser, d1.Parser);
                    Assert.False(d1.Reset.HasValue);
                    Assert.Equal(Setter.ForField(f), d1.Setter);
                }

                // 4
                {
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForField(null, "Baf", parser, MemberRequired.Yes));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForField(f, null, parser, MemberRequired.Yes));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForField(f, "Baf", null, MemberRequired.Yes));
                    // there's a separate test for bogus IsMemberRequired

                    var d1 = DeserializableMember.ForField(f, "Baf", parser, MemberRequired.Yes);
                    Assert.True(d1.IsRequired);
                    Assert.Equal("Baf", d1.Name);
                    Assert.Equal(parser, d1.Parser);
                    Assert.False(d1.Reset.HasValue);
                    Assert.Equal(Setter.ForField(f), d1.Setter);
                }

                var reset = Reset.ForDelegate((in ReadContext _) => { });

                // 5
                {
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForField(null, "Baz", parser, MemberRequired.Yes, reset));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForField(f, null, parser, MemberRequired.Yes, reset));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForField(f, "Baz", null, MemberRequired.Yes, reset));
                    // there's a separate test for bogus IsMemberRequired
                    // it's ok for reset = null

                    var d1 = DeserializableMember.ForField(f, "Baz", parser, MemberRequired.Yes, reset);
                    Assert.True(d1.IsRequired);
                    Assert.Equal("Baz", d1.Name);
                    Assert.Equal(parser, d1.Parser);
                    Assert.Equal(reset, d1.Reset.Value);
                    Assert.Equal(Setter.ForField(f), d1.Setter);
                }
            }

            // properties
            {
                var p = t.GetProperty(nameof(_DeserializableMemberHelpers.Prop));

                // 1
                {
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForProperty(null));

                    var d1 = DeserializableMember.ForProperty(p);
                    Assert.False(d1.IsRequired);
                    Assert.Equal("Prop", d1.Name);
                    Assert.Equal(Parser.GetDefault(typeof(string).GetTypeInfo()), d1.Parser);
                    Assert.False(d1.Reset.HasValue);
                    Assert.Equal(Setter.ForMethod(p.SetMethod), d1.Setter);
                }

                // 2
                {
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForProperty(null, "Foo"));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForProperty(p, null));

                    var d1 = DeserializableMember.ForProperty(p, "Foo");
                    Assert.False(d1.IsRequired);
                    Assert.Equal("Foo", d1.Name);
                    Assert.Equal(Parser.GetDefault(typeof(string).GetTypeInfo()), d1.Parser);
                    Assert.False(d1.Reset.HasValue);
                    Assert.Equal(Setter.ForMethod(p.SetMethod), d1.Setter);
                }

                var parser = Parser.ForDelegate<string>((ReadOnlySpan<char> _, in ReadContext rc, out string v) => { v = "1"; return true; });

                // 3
                {
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForProperty(null, "Bar", parser));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForProperty(p, null, parser));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForProperty(p, "Bar", null));

                    var d1 = DeserializableMember.ForProperty(p, "Bar", parser);
                    Assert.False(d1.IsRequired);
                    Assert.Equal("Bar", d1.Name);
                    Assert.Equal(parser, d1.Parser);
                    Assert.False(d1.Reset.HasValue);
                    Assert.Equal(Setter.ForMethod(p.SetMethod), d1.Setter);
                }

                // 4
                {
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForProperty(null, "Baf", parser, MemberRequired.Yes));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForProperty(p, null, parser, MemberRequired.Yes));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForProperty(p, "Baf", null, MemberRequired.Yes));
                    // there's a separate test for bogus IsMemberRequired

                    var d1 = DeserializableMember.ForProperty(p, "Baf", parser, MemberRequired.Yes);
                    Assert.True(d1.IsRequired);
                    Assert.Equal("Baf", d1.Name);
                    Assert.Equal(parser, d1.Parser);
                    Assert.False(d1.Reset.HasValue);
                    Assert.Equal(Setter.ForMethod(p.SetMethod), d1.Setter);
                }

                var reset = Reset.ForDelegate((in ReadContext _) => { });

                // 5
                {
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForProperty(null, "Baz", parser, MemberRequired.Yes, reset));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForProperty(p, null, parser, MemberRequired.Yes, reset));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForProperty(p, "Baz", null, MemberRequired.Yes, reset));
                    // there's a separate test for bogus IsMemberRequired
                    // it's ok for reset = null

                    var d1 = DeserializableMember.ForProperty(p, "Baz", parser, MemberRequired.Yes, reset);
                    Assert.True(d1.IsRequired);
                    Assert.Equal("Baz", d1.Name);
                    Assert.Equal(parser, d1.Parser);
                    Assert.Equal(reset, d1.Reset.Value);
                    Assert.Equal(Setter.ForMethod(p.SetMethod), d1.Setter);
                }
            }
        }

        private class _DeserializableMemberEquality
        {
            public int Foo { get; set; }
            public int Bar { get; set; }
        }

        [Fact]
        public void DeserializableMemberEquality()
        {
            var t = typeof(_DeserializableMemberEquality).GetTypeInfo();
            var names = new[] { nameof(_DeserializableMemberEquality.Foo), nameof(_DeserializableMemberEquality.Bar) };
            var setters = new[] { Setter.ForMethod(t.GetProperty(names[0]).SetMethod), Setter.ForMethod(t.GetProperty(names[1]).SetMethod) };
            IEnumerable<Parser> parsers;
            {
                var a = Parser.GetDefault(typeof(int).GetTypeInfo());
                var b = Parser.ForDelegate<int>((ReadOnlySpan<char> s, in ReadContext rc, out int val) => { val = 123; return true; });
                parsers = new[] { a, b };
            }
            var isMemberRequireds = new[] { MemberRequired.Yes, MemberRequired.No };
            IEnumerable<Reset> resets;
            {
                var a = Reset.ForDelegate((in ReadContext _) => { });
                var b = Reset.ForDelegate((_DeserializableMemberEquality _, in ReadContext __) => { });
                resets = new[] { a, b, null };
            }

            var members = new List<DeserializableMember>();

            foreach (var n in names)
            {
                foreach (var s in setters)
                {
                    foreach (var p in parsers)
                    {
                        foreach (var i in isMemberRequireds)
                        {
                            foreach (var r in resets)
                            {
                                members.Add(DeserializableMember.Create(t, n, s, p, i, r));
                            }
                        }
                    }
                }
            }

            var notSerializableMember = "";

            for (var i = 0; i < members.Count; i++)
            {
                var m1 = members[i];

                Assert.False(m1.Equals(notSerializableMember));

                for (var j = i; j < members.Count; j++)
                {
                    var m2 = members[j];

                    var eq = m1 == m2;
                    var neq = m1 != m2;
                    var hashEq = m1.GetHashCode() == m2.GetHashCode();
                    var objEq = m1.Equals((object)m2);

                    if (i == j)
                    {
                        Assert.True(eq);
                        Assert.False(neq);
                        Assert.True(hashEq);
                        Assert.True(objEq);
                    }
                    else
                    {
                        Assert.False(eq);
                        Assert.True(neq);
                        Assert.False(objEq);
                    }
                }
            }
        }

        private class _DeserializeMemberErrors
        {
            public string Foo { get; set; }
        }

        [Fact]
        public void DeserializableMemberErrors()
        {
            var type = typeof(_DeserializeMemberErrors).GetTypeInfo();
            var name = nameof(_DeserializeMemberErrors.Foo);
            var setter = Setter.ForMethod(type.GetProperty(name).SetMethod);
            var parser = Parser.GetDefault(typeof(string).GetTypeInfo());

            Assert.Throws<ArgumentNullException>(() => DeserializableMember.Create(null, name, setter, parser, MemberRequired.Yes, null));
            Assert.Throws<ArgumentNullException>(() => DeserializableMember.Create(type, null, setter, parser, MemberRequired.Yes, null));
            Assert.Throws<ArgumentNullException>(() => DeserializableMember.Create(type, name, null, parser, MemberRequired.Yes, null));
            Assert.Throws<ArgumentNullException>(() => DeserializableMember.Create(type, name, setter, null, MemberRequired.Yes, null));
            Assert.Throws<ArgumentException>(() => DeserializableMember.Create(type, name, setter, parser, 0, null));

            var badParser = Parser.GetDefault(typeof(int).GetTypeInfo());
            Assert.Throws<ArgumentException>(() => DeserializableMember.Create(type, name, setter, badParser, MemberRequired.Yes, null));

            var badReset = Reset.ForDelegate<string>((string _, in ReadContext __) => { });
            Assert.Throws<ArgumentException>(() => DeserializableMember.Create(type, name, setter, parser, MemberRequired.Yes, badReset));

            Assert.Throws<ArgumentException>(() => DeserializableMember.Create(type, "", setter, parser, MemberRequired.Yes, null));
        }

        [Fact]
        public void ReadContexts()
        {
            {
                var cc = Cesil.ReadContext.ConvertingColumn(Options.Default, 1, ColumnIdentifier.Create(1), null);
                var cr = Cesil.ReadContext.ConvertingRow(Options.Default, 1, null);
                var rc = Cesil.ReadContext.ReadingColumn(Options.Default, 1, ColumnIdentifier.Create(1), null);

                Assert.Same(Options.Default, cc.Options);
                Assert.True(cc.HasColumn);
                Assert.Equal(ColumnIdentifier.Create(1), cc.Column);

                Assert.Same(Options.Default, cr.Options);
                Assert.False(cr.HasColumn);
                Assert.Throws<InvalidOperationException>(() => cr.Column);

                Assert.Same(Options.Default, rc.Options);
                Assert.True(rc.HasColumn);
                Assert.Equal(ColumnIdentifier.Create(1), rc.Column);
            }

            // equality
            {
                var cc1 = Cesil.ReadContext.ConvertingColumn(Options.Default, 1, ColumnIdentifier.Create(1), null);
                var cc2 = Cesil.ReadContext.ConvertingColumn(Options.Default, 1, ColumnIdentifier.Create(1), "foo");
                var cc3 = Cesil.ReadContext.ConvertingColumn(Options.Default, 1, ColumnIdentifier.Create(2), null);
                var cc4 = Cesil.ReadContext.ConvertingColumn(Options.Default, 2, ColumnIdentifier.Create(1), null);
                var cc5 = Cesil.ReadContext.ConvertingColumn(Options.DynamicDefault, 1, ColumnIdentifier.Create(1), null);

                var cr1 = Cesil.ReadContext.ConvertingRow(Options.Default, 1, null);
                var cr2 = Cesil.ReadContext.ConvertingRow(Options.Default, 1, "foo");
                var cr3 = Cesil.ReadContext.ConvertingRow(Options.Default, 2, null);
                var cr4 = Cesil.ReadContext.ConvertingRow(Options.DynamicDefault, 1, null);

                var rc1 = Cesil.ReadContext.ReadingColumn(Options.Default, 1, ColumnIdentifier.Create(1), null);
                var rc2 = Cesil.ReadContext.ReadingColumn(Options.Default, 1, ColumnIdentifier.Create(1), "foo");
                var rc3 = Cesil.ReadContext.ReadingColumn(Options.Default, 1, ColumnIdentifier.Create(2), null);
                var rc4 = Cesil.ReadContext.ReadingColumn(Options.Default, 2, ColumnIdentifier.Create(1), null);
                var rc5 = Cesil.ReadContext.ReadingColumn(Options.DynamicDefault, 1, ColumnIdentifier.Create(1), null);

                var contexts = new[] { cc1, cc2, cc3, cc4, cc5, cr1, cr2, cr3, cr4, rc1, rc2, rc3, rc4, rc5 };

                var notContext = "";

                for (var i = 0; i < contexts.Length; i++)
                {
                    var ctx1 = contexts[i];
                    Assert.False(ctx1.Equals(notContext));
                    Assert.NotNull(ctx1.ToString());

                    for (var j = i; j < contexts.Length; j++)
                    {
                        var ctx2 = contexts[j];

                        var objEq = ctx1.Equals((object)ctx2);
                        var eq = ctx1 == ctx2;
                        var neq = ctx1 != ctx2;
                        var hashEq = ctx1.GetHashCode() == ctx2.GetHashCode();

                        if (i == j)
                        {
                            Assert.True(objEq);
                            Assert.True(eq);
                            Assert.False(neq);
                            Assert.True(hashEq);
                        }
                        else
                        {
                            Assert.False(objEq);
                            Assert.False(eq);
                            Assert.True(neq);
                        }
                    }
                }
            }
        }

        private class _ResultsErrors
        {
            public string Foo { get; set; }
        }

        private class _RowCreationFailure
        {
            public int Foo { get; set; }
        }

        [Fact]
        public void RowCreationFailure()
        {
            int failAfter = 0;
            int calls = 0;
            InstanceProviderDelegate<_RowCreationFailure> builder =
                (in ReadContext _, out _RowCreationFailure row) =>
                {
                    if (calls >= failAfter)
                    {
                        row = default;
                        return false;
                    }

                    calls++;

                    row = new _RowCreationFailure();
                    return true;
                };


            var typeDesc = ManualTypeDescriberBuilder.CreateBuilder();
            typeDesc.WithDeserializableProperty(typeof(_RowCreationFailure).GetProperty(nameof(_RowCreationFailure.Foo)));
            typeDesc.WithInstanceProvider((InstanceProvider)builder);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)typeDesc.ToManualTypeDescriber()).ToOptions();

            RunSyncReaderVariants<_RowCreationFailure>(
                opts,
                (config, makeReader) =>
                {
                    calls = 0;
                    failAfter = 3;

                    using (var reader = makeReader("Foo\r\n1\r\n2\r\n3\r\n4"))
                    using (var csv = config.CreateReader(reader))
                    {
                        Assert.True(csv.TryRead(out var r1));
                        Assert.Equal(1, r1.Foo);

                        Assert.True(csv.TryRead(out var r2));
                        Assert.Equal(2, r2.Foo);

                        Assert.True(csv.TryRead(out var r3));
                        Assert.Equal(3, r3.Foo);

                        Assert.Throws<InvalidOperationException>(() => csv.TryRead(out _));
                    }
                }
            );
        }

        private class _EnumeratorNoReset
        {
            public int A { get; set; }
            public int B { get; set; }
            public int C { get; set; }
        }


        [Fact]
        public void EnumeratorNoReset()
        {
            RunSyncReaderVariants<_EnumeratorNoReset>(
                Options.Default,
                (config, getReader) =>
                {
                    using (var reader = getReader("1,2,3"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var e = csv.EnumerateAll();
                        using (var i = e.GetEnumerator())
                        {
                            Assert.True(i.MoveNext());
                            var r = i.Current;
                            Assert.NotNull(r);
                            Assert.Equal(1, r.A);
                            Assert.Equal(2, r.B);
                            Assert.Equal(3, r.C);

                            Assert.False(i.MoveNext());

                            Assert.Throws<NotSupportedException>(() => i.Reset());
                        }
                    }
                }
            );
        }

        private class _WithComments
        {
            public string A { get; set; }
            public int Nope { get; set; }
        }

        [Fact]
        public void WithComments()
        {
            // \r\n
            {
                var opts = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

                // with headers
                RunSyncReaderVariants<_WithComments>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("A,Nope\r\n#comment\rwhatever\r\nhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, comment before header
                RunSyncReaderVariants<_WithComments>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\rwhatever\r\nA,Nope\r\nhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, no comment
                RunSyncReaderVariants<_WithComments>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("A,Nope"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res.ResultType);
                        }
                    }
                );

                // no headers
                RunSyncReaderVariants<_WithComments>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\rwhatever\r\nhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // two comments
                RunSyncReaderVariants<_WithComments>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\rwhatever\r\n#again!###foo###"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res2.ResultType);
                            Assert.Equal("again!###foo###", res2.Comment);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );
            }

            // \r
            {
                var opts = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithRowEnding(RowEnding.CarriageReturn).ToOptions();

                // with headers
                RunSyncReaderVariants<_WithComments>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("A,Nope\r#comment\nwhatever\rhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, comment before header
                RunSyncReaderVariants<_WithComments>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\nwhatever\rA,Nope\rhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, no comment
                RunSyncReaderVariants<_WithComments>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("A,Nope"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res.ResultType);
                        }
                    }
                );

                // no headers
                RunSyncReaderVariants<_WithComments>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\nwhatever\rhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // two comments
                RunSyncReaderVariants<_WithComments>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\nwhatever\r#again!###foo###"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res2.ResultType);
                            Assert.Equal("again!###foo###", res2.Comment);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );
            }

            // \n
            {
                var opts = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithRowEnding(RowEnding.LineFeed).ToOptions();

                // with headers
                RunSyncReaderVariants<_WithComments>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("A,Nope\n#comment\rwhatever\nhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, comment before header
                RunSyncReaderVariants<_WithComments>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\rwhatever\nA,Nope\nhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, no comment
                RunSyncReaderVariants<_WithComments>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("A,Nope"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res.ResultType);
                        }
                    }
                );

                // no headers
                RunSyncReaderVariants<_WithComments>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\rwhatever\nhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // two comments
                RunSyncReaderVariants<_WithComments>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\rwhatever\n#again!###foo###"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res2.ResultType);
                            Assert.Equal("again!###foo###", res2.Comment);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );
            }
        }

        private class _DelegateReset
        {
            public int Foo { get; set; }
        }

        [Fact]
        public void DelegateStaticReset()
        {
            var resetCalled = 0;
            StaticResetDelegate resetDel =
                (in ReadContext _) =>
                {
                    resetCalled++;
                };

            var name = nameof(_DelegateReset.Foo);
            var parser = Parser.GetDefault(typeof(int).GetTypeInfo());
            var setter = Setter.ForMethod(typeof(_DelegateReset).GetProperty(name).SetMethod);
            var reset = Reset.ForDelegate(resetDel);

            var describer = ManualTypeDescriberBuilder.CreateBuilder();
            describer.WithExplicitSetter(typeof(_DelegateReset).GetTypeInfo(), name, setter, parser, MemberRequired.No, reset);
            InstanceProviderDelegate<_DelegateReset> del = (in ReadContext _, out _DelegateReset i) => { i = new _DelegateReset(); return true; };
            describer.WithInstanceProvider((InstanceProvider)del);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).ToOptions();

            RunSyncReaderVariants<_DelegateReset>(
                opts,
                (config, getReader) =>
                {
                    resetCalled = 0;

                    using (var reader = getReader("1\r\n23\r\n456\r\n7\r\n"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var row = csv.ReadAll();

                        Assert.Collection(
                            row,
                            r => Assert.Equal(1, r.Foo),
                            r => Assert.Equal(23, r.Foo),
                            r => Assert.Equal(456, r.Foo),
                            r => Assert.Equal(7, r.Foo)
                        );
                    }

                    Assert.Equal(4, resetCalled);
                }
            );
        }

        [Fact]
        public void DelegateReset()
        {
            var resetCalled = 0;
            ResetDelegate<_DelegateReset> resetDel =
                (_DelegateReset row, in ReadContext _) =>
                {
                    resetCalled++;
                };

            var name = nameof(_DelegateReset.Foo);
            var parser = Parser.GetDefault(typeof(int).GetTypeInfo());
            var setter = Setter.ForMethod(typeof(_DelegateReset).GetProperty(name).SetMethod);
            var reset = Reset.ForDelegate(resetDel);

            var describer = ManualTypeDescriberBuilder.CreateBuilder();
            describer.WithExplicitSetter(typeof(_DelegateReset).GetTypeInfo(), name, setter, parser, MemberRequired.No, reset);
            InstanceProviderDelegate<_DelegateReset> del = (in ReadContext _, out _DelegateReset i) => { i = new _DelegateReset(); return true; };
            describer.WithInstanceProvider((InstanceProvider)del);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).ToOptions();

            RunSyncReaderVariants<_DelegateReset>(
                opts,
                (config, getReader) =>
                {
                    resetCalled = 0;

                    using (var reader = getReader("1\r\n23\r\n456\r\n7\r\n"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var row = csv.ReadAll();

                        Assert.Collection(
                            row,
                            r => Assert.Equal(1, r.Foo),
                            r => Assert.Equal(23, r.Foo),
                            r => Assert.Equal(456, r.Foo),
                            r => Assert.Equal(7, r.Foo)
                        );
                    }

                    Assert.Equal(4, resetCalled);
                }
            );
        }

        private class _DelegateSetter
        {
            public int Foo { get; set; }
        }

        [Fact]
        public void DelegateStaticSetter()
        {
            var setterCalled = 0;

            StaticSetterDelegate<int> parser =
                (int value, in ReadContext _) =>
                {
                    setterCalled++;
                };

            var describer = ManualTypeDescriberBuilder.CreateBuilder();
            describer.WithExplicitSetter(typeof(_DelegateSetter).GetTypeInfo(), "Foo", Setter.ForDelegate(parser));
            InstanceProviderDelegate<_DelegateSetter> del = (in ReadContext _, out _DelegateSetter i) => { i = new _DelegateSetter(); return true; };
            describer.WithInstanceProvider((InstanceProvider)del);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).ToOptions();

            RunSyncReaderVariants<_DelegateSetter>(
                opts,
                (config, getReader) =>
                {
                    setterCalled = 0;

                    using (var reader = getReader("1\r\n23\r\n456\r\n7\r\n"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var row = csv.ReadAll();

                        Assert.Collection(
                            row,
                            r => Assert.Equal(0, r.Foo),
                            r => Assert.Equal(0, r.Foo),
                            r => Assert.Equal(0, r.Foo),
                            r => Assert.Equal(0, r.Foo)
                        );
                    }

                    Assert.Equal(4, setterCalled);
                }
            );
        }

        [Fact]
        public void DelegateSetter()
        {
            var setterCalled = 0;

            SetterDelegate<_DelegateSetter, int> parser =
                (_DelegateSetter row, int value, in ReadContext _) =>
                {
                    setterCalled++;

                    row.Foo = value * 2;
                };

            var describer = ManualTypeDescriberBuilder.CreateBuilder();
            describer.WithExplicitSetter(typeof(_DelegateSetter).GetTypeInfo(), "Foo", Setter.ForDelegate(parser));
            InstanceProviderDelegate<_DelegateSetter> del = (in ReadContext _, out _DelegateSetter i) => { i = new _DelegateSetter(); return true; };
            describer.WithInstanceProvider((InstanceProvider)del);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).ToOptions();

            RunSyncReaderVariants<_DelegateSetter>(
                opts,
                (config, getReader) =>
                {
                    setterCalled = 0;

                    using (var reader = getReader("1\r\n23\r\n456\r\n7\r\n"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var row = csv.ReadAll();

                        Assert.Collection(
                            row,
                            r => Assert.Equal(1 * 2, r.Foo),
                            r => Assert.Equal(23 * 2, r.Foo),
                            r => Assert.Equal(456 * 2, r.Foo),
                            r => Assert.Equal(7 * 2, r.Foo)
                        );
                    }

                    Assert.Equal(4, setterCalled);
                }
            );
        }

        private class _ConstructorParser_Outer
        {
            public _ConstructorParser Foo { get; set; }
            public _ConstructorParser_Outer() { }
        }

        private class _ConstructorParser
        {
            public static int Cons1Called = 0;
            public static int Cons2Called = 0;

            public string Value { get; }

            public _ConstructorParser(ReadOnlySpan<char> a)
            {
                Cons1Called++;
                Value = new string(a);
            }

            public _ConstructorParser(ReadOnlySpan<char> a, in ReadContext ctx)
            {
                Cons2Called++;
                Value = new string(a) + ctx.Column.Index;
            }
        }

        [Fact]
        public void ConstructorParser()
        {
            var cons1 = typeof(_ConstructorParser).GetConstructor(new[] { typeof(ReadOnlySpan<char>) });
            var cons2 = typeof(_ConstructorParser).GetConstructor(new[] { typeof(ReadOnlySpan<char>), typeof(ReadContext).MakeByRefType() });

            // single param
            {
                var describer = ManualTypeDescriberBuilder.CreateBuilder();
                describer.WithDeserializableProperty(
                    typeof(_ConstructorParser_Outer).GetProperty(nameof(_ConstructorParser_Outer.Foo)),
                    nameof(_ConstructorParser_Outer.Foo),
                    Parser.ForConstructor(cons1)
                );

                InstanceProviderDelegate<_ConstructorParser_Outer> del = (in ReadContext _, out _ConstructorParser_Outer i) => { i = new _ConstructorParser_Outer(); return true; };
                describer.WithInstanceProvider((InstanceProvider)del);

                var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).ToOptions();

                RunSyncReaderVariants<_ConstructorParser_Outer>(
                    opts,
                    (config, getReader) =>
                    {
                        _ConstructorParser.Cons1Called = 0;

                        using (var reader = getReader("1\r\n23\r\n456\r\n7\r\n"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var row = csv.ReadAll();

                            Assert.Collection(
                                row,
                                r => Assert.Equal("1", r.Foo.Value),
                                r => Assert.Equal("23", r.Foo.Value),
                                r => Assert.Equal("456", r.Foo.Value),
                                r => Assert.Equal("7", r.Foo.Value)
                            );
                        }

                        Assert.Equal(4, _ConstructorParser.Cons1Called);
                    }
                );
            }

            // two params
            {
                var describer = ManualTypeDescriberBuilder.CreateBuilder();
                describer.WithDeserializableProperty(
                    typeof(_ConstructorParser_Outer).GetProperty(nameof(_ConstructorParser_Outer.Foo)),
                    nameof(_ConstructorParser_Outer.Foo),
                    Parser.ForConstructor(cons2)
                );
                InstanceProviderDelegate<_ConstructorParser_Outer> del = (in ReadContext _, out _ConstructorParser_Outer i) => { i = new _ConstructorParser_Outer(); return true; };
                describer.WithInstanceProvider((InstanceProvider)del);

                var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).ToOptions();

                RunSyncReaderVariants<_ConstructorParser_Outer>(
                    opts,
                    (config, getReader) =>
                    {
                        _ConstructorParser.Cons2Called = 0;

                        using (var reader = getReader("1\r\n23\r\n456\r\n7\r\n"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var row = csv.ReadAll();

                            Assert.Collection(
                                row,
                                r => Assert.Equal("10", r.Foo.Value),
                                r => Assert.Equal("230", r.Foo.Value),
                                r => Assert.Equal("4560", r.Foo.Value),
                                r => Assert.Equal("70", r.Foo.Value)
                            );
                        }

                        Assert.Equal(4, _ConstructorParser.Cons2Called);
                    }
                );
            }
        }

        private class _DelegateParser
        {
            public int Foo { get; set; }
        }

        [Fact]
        public void DelegateParser()
        {
            var parserCalled = 0;

            ParserDelegate<int> parser =
                (ReadOnlySpan<char> data, in ReadContext _, out int res) =>
                {
                    parserCalled++;

                    res = data.Length;
                    return true;
                };

            var describer = ManualTypeDescriberBuilder.CreateBuilder();
            describer.WithDeserializableProperty(
                typeof(_DelegateParser).GetProperty(nameof(_DelegateParser.Foo)),
                nameof(_DelegateParser.Foo),
                Parser.ForDelegate(parser)
            );
            InstanceProviderDelegate<_DelegateParser> del = (in ReadContext _, out _DelegateParser i) => { i = new _DelegateParser(); return true; };
            describer.WithInstanceProvider((InstanceProvider)del);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).ToOptions();

            RunSyncReaderVariants<_DelegateParser>(
                opts,
                (config, getReader) =>
                {
                    parserCalled = 0;

                    using (var reader = getReader("1\r\n23\r\n456\r\n7\r\n"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var row = csv.ReadAll();

                        Assert.Collection(
                            row,
                            r => Assert.Equal(1, r.Foo),
                            r => Assert.Equal(2, r.Foo),
                            r => Assert.Equal(3, r.Foo),
                            r => Assert.Equal(1, r.Foo)
                        );
                    }

                    Assert.Equal(4, parserCalled);
                }
            );
        }

        private class _StaticSetter
        {
            public static int Foo { get; set; }
        }

        [Fact]
        public void StaticSetter()
        {
            var describer = ManualTypeDescriberBuilder.CreateBuilder();
            describer.WithDeserializableProperty(typeof(_StaticSetter).GetProperty(nameof(_StaticSetter.Foo), BindingFlags.Static | BindingFlags.Public));
            InstanceProviderDelegate<_StaticSetter> del = (in ReadContext _, out _StaticSetter i) => { i = new _StaticSetter(); return true; };
            describer.WithInstanceProvider((InstanceProvider)del);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).ToOptions();

            RunSyncReaderVariants<_StaticSetter>(
                opts,
                (config, getReader) =>
                {
                    _StaticSetter.Foo = 123;

                    using (var reader = getReader("456"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var row = csv.ReadAll();

                        Assert.Collection(row, r => Assert.NotNull(r));
                    }

                    Assert.Equal(456, _StaticSetter.Foo);
                }
            );
        }

        private class _WithReset
        {
            public string A { get; set; }

            private int _B;
            public int B
            {
                get
                {
                    return _B;
                }
                set
                {
                    if (value > 5) return;

                    _B = value;
                }
            }

            public void ResetB()
            {
                _B = 2;
            }
        }

        private class _WithReset_Static
        {
            public static int Count;

            public string A { get; set; }

            public int B { get; set; }

            public static void ResetB()
            {
                Count++;
            }
        }

        private class _WithReset_StaticWithParam
        {
            public string A { get; set; }

            private int _B;
            public int B
            {
                get
                {
                    return _B;
                }
                set
                {
                    if (value > 5) return;

                    _B = value;
                }
            }

            public static void ResetB(_WithReset_StaticWithParam row)
            {
                row._B = 2;
            }
        }

        [Fact]
        public void WithReset()
        {
            // simple
            {
                const string CSV = "A,B\r\nfoo,1\r\nbar,6\r\n";

                RunSyncReaderVariants<_WithReset>(
                    Options.Default,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader(CSV))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("foo", a.A); Assert.Equal(1, a.B); },
                                a => { Assert.Equal("bar", a.A); Assert.Equal(2, a.B); }
                            );
                        }
                    }
                );
            }

            // static
            {
                const string CSV = "A,B\r\nfoo,1\r\nbar,6\r\n";

                RunSyncReaderVariants<_WithReset_Static>(
                    Options.Default,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader(CSV))
                        using (var csv = config.CreateReader(reader))
                        {
                            _WithReset_Static.Count = 0;

                            var rows = csv.ReadAll();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("foo", a.A); Assert.Equal(1, a.B); },
                                a => { Assert.Equal("bar", a.A); Assert.Equal(6, a.B); }
                            );

                            Assert.Equal(2, _WithReset_Static.Count);
                        }
                    }
                );
            }

            // static with param
            {
                const string CSV = "A,B\r\nfoo,1\r\nbar,6\r\n";

                RunSyncReaderVariants<_WithReset_StaticWithParam>(
                    Options.Default,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader(CSV))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("foo", a.A); Assert.Equal(1, a.B); },
                                a => { Assert.Equal("bar", a.A); Assert.Equal(2, a.B); }
                            );
                        }
                    }
                );
            }
        }

        [Fact]
        public void TransitionMatrixConstants()
        {
            var maxStateVal = Enum.GetValues(typeof(ReaderStateMachine.State)).Cast<ReaderStateMachine.State>().Select(b => (byte)b).Max();

            // making these consts is a win, but we want to make sure we don't break them
            Assert.Equal(maxStateVal + 1, ReaderStateMachine.RuleCacheStateCount);

            var characterTypeMax = Enum.GetValues(typeof(ReaderStateMachine.CharacterType)).Cast<byte>().Max();

            Assert.Equal(characterTypeMax + 1, ReaderStateMachine.RuleCacheCharacterCount);
            Assert.Equal((maxStateVal + 1) * (characterTypeMax + 1), ReaderStateMachine.RuleCacheConfigSize);

            var rowEndingsMax = Enum.GetValues(typeof(RowEnding)).Cast<byte>().Max();

            Assert.Equal(rowEndingsMax + 1, ReaderStateMachine.RuleCacheRowEndingCount);
            Assert.Equal((rowEndingsMax + 1) * 16, ReaderStateMachine.RuleCacheConfigCount);
        }

        [Fact]
        public void StateMasks()
        {
            foreach (ReaderStateMachine.State state in Enum.GetValues(typeof(ReaderStateMachine.State)))
            {
                var wasSpecial = false;

                var inComment = (((byte)state) & ReaderStateMachine.IN_COMMENT_MASK) == ReaderStateMachine.IN_COMMENT_MASK;
                if (inComment)
                {
                    Assert.True(
                        state == ReaderStateMachine.State.Comment_BeforeHeader ||
                        state == ReaderStateMachine.State.Comment_BeforeHeader_ExpectingEndOfComment ||
                        state == ReaderStateMachine.State.Comment_BeforeRecord ||
                        state == ReaderStateMachine.State.Comment_BeforeRecord_ExpectingEndOfComment
                    );
                    wasSpecial = true;
                }

                var inEscapedValue = ReaderStateMachine.IsInEscapedValue(state);
                if (inEscapedValue)
                {
                    Assert.True(
                        state == ReaderStateMachine.State.Header_InEscapedValue ||
                        state == ReaderStateMachine.State.Header_InEscapedValueWithPendingEscape ||
                        state == ReaderStateMachine.State.Record_InEscapedValue ||
                        state == ReaderStateMachine.State.Record_InEscapedValueWithPendingEscape
                    );
                    wasSpecial = true;
                }

                var canEndRecord = (((byte)state) & ReaderStateMachine.CAN_END_RECORD_MASK) == ReaderStateMachine.CAN_END_RECORD_MASK;
                if (canEndRecord)
                {
                    Assert.True(
                            state == ReaderStateMachine.State.Record_InEscapedValueWithPendingEscape ||
                            state == ReaderStateMachine.State.Record_Unescaped_NoValue ||
                            state == ReaderStateMachine.State.Record_Unescaped_WithValue
                    );
                    wasSpecial = true;
                }

                if (!wasSpecial)
                {
                    Assert.True(
                        state == ReaderStateMachine.State.NONE ||
                        state == ReaderStateMachine.State.Header_Start ||
                        state == ReaderStateMachine.State.Header_InEscapedValue_ExpectingEndOfValueOrRecord ||
                        state == ReaderStateMachine.State.Header_Unescaped_NoValue ||
                        state == ReaderStateMachine.State.Header_Unescaped_WithValue ||
                        state == ReaderStateMachine.State.Header_ExpectingEndOfRecord ||
                        state == ReaderStateMachine.State.Record_Start ||
                        state == ReaderStateMachine.State.Record_InEscapedValue_ExpectingEndOfValueOrRecord ||
                        state == ReaderStateMachine.State.Record_ExpectingEndOfRecord ||
                        state == ReaderStateMachine.State.Invalid ||
                        state == ReaderStateMachine.State.DataEnded
                    );
                }
            }
        }

        private class _TabSeparator
        {
            public string Foo { get; set; }
            public int Bar { get; set; }
        }

        [Fact]
        public void TabSeparator()
        {
            const string TSV = @"Foo	Bar
""hello""""world""	123
";
            var opts = Options.CreateBuilder(Options.Default).WithEscapedValueStartAndEnd('"').WithValueSeparator("\t").ToOptions();

            RunSyncReaderVariants<_TabSeparator>(
                opts,
                (config, getReader) =>
                {
                    using (var reader = getReader(TSV))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();

                        Assert.Collection(
                            rows,
                            a => { Assert.Equal("hello\"world", a.Foo); Assert.Equal(123, a.Bar); }
                        );
                    }
                }
            );
        }

        private class _DifferentEscapes
        {
            public string Foo { get; set; }
            public string Bar { get; set; }
        }

        [Fact]
        public void DifferentEscapes()
        {
            var opts = Options.CreateBuilder(Options.Default).WithEscapedValueStartAndEnd('"').WithEscapedValueEscapeCharacter('\\').ToOptions();

            // simple
            {
                RunSyncReaderVariants<_DifferentEscapes>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("Foo,Bar\r\nhello,world"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("hello", a.Foo); Assert.Equal("world", a.Bar); }
                            );
                        }
                    }
                );
            }

            // escaped
            {
                RunSyncReaderVariants<_DifferentEscapes>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("Foo,Bar\r\n\"hello\",\"world\""))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("hello", a.Foo); Assert.Equal("world", a.Bar); }
                            );
                        }
                    }
                );
            }

            // escaped with quotes
            {
                RunSyncReaderVariants<_DifferentEscapes>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("Foo,Bar\r\n\"he\\\"llo\",\"world\""))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("he\"llo", a.Foo); Assert.Equal("world", a.Bar); }
                            );
                        }
                    }
                );
            }

            // escaped with slash
            {
                RunSyncReaderVariants<_DifferentEscapes>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("Foo,Bar\r\n\"hello\",\"w\\\\orld\""))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("hello", a.Foo); Assert.Equal("w\\orld", a.Bar); }
                            );
                        }
                    }
                );
            }

            // escape char outside of quotes
            {
                RunSyncReaderVariants<_DifferentEscapes>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("Foo,Bar\r\n\\,\\ooo"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("\\", a.Foo); Assert.Equal("\\ooo", a.Bar); }
                            );
                        }
                    }
                );
            }
        }

        private class _BadEscape
        {
            public string A { get; set; }
            public string B { get; set; }
        }

        [Fact]
        public void BadEscape()
        {
            var opts = Options.Default;
            var CSV = @"h""ello"",world";

            RunSyncReaderVariants<_BadEscape>(
                opts,
                (config, getReader) =>
                {
                    using (var reader = getReader(CSV))
                    using (var csv = config.CreateReader(reader))
                    {
                        Assert.Throws<InvalidOperationException>(() => csv.ReadAll());
                    }
                }
            );
        }

        private class _TryReadWithReuse
        {
            public string Bar { get; set; }
        }

        [Fact]
        public void TryReadWithReuse()
        {
            const string CSV = "hello\r\nworld\r\nfoo\r\n";

            RunSyncReaderVariants<_TryReadWithReuse>(
                Options.Default,
                (config, getReader) =>
                {
                    _TryReadWithReuse pre = null;

                    using (var reader = getReader(CSV))
                    using (var csv = config.CreateReader(reader))
                    {
                        Assert.True(csv.TryReadWithReuse(ref pre));
                        Assert.Equal("hello", pre.Bar);

                        var oldPre = pre;
                        Assert.True(csv.TryReadWithReuse(ref pre));
                        Assert.Equal("world", pre.Bar);
                        Assert.Same(oldPre, pre);

                        Assert.True(csv.TryReadWithReuse(ref pre));
                        Assert.Equal("foo", pre.Bar);
                        Assert.Same(oldPre, pre);

                        Assert.False(csv.TryReadWithReuse(ref pre));
                        Assert.Same(oldPre, pre);
                    }
                }
            );
        }

        private class _ReadAll
        {
            public string Foo { get; set; }
            public int? Bar { get; set; }
            public DateTime Fizz { get; set; }
            public double? Buzz { get; set; }
        }

        [Fact]
        public void ReadAll()
        {
            var CSV =
$@"Foo,Fizz,Bar,Buzz
""hello{'\n'}world"",{new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local)},,123.45
,{new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local)},,
mkay,{new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local)},8675309,987654321.012345";

            var opts = Options.Default;

            var ix = 0;

            RunSyncReaderVariants<_ReadAll>(
                    opts,
                    (config, getReader) =>
                    {
                        ix++;

                        using (var reader = getReader(CSV))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();
                            Assert.Collection(
                                read,
                                r1 =>
                                {

                                    Assert.Equal("hello\nworld", r1.Foo);
                                    Assert.Equal(new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local), r1.Fizz);
                                    Assert.Equal((int?)null, r1.Bar);
                                    Assert.Equal(123.45, r1.Buzz);
                                },
                                r2 =>
                                {

                                    Assert.Equal("", r2.Foo);
                                    Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local), r2.Fizz);
                                    Assert.Equal((int?)null, r2.Bar);
                                    Assert.Equal((double?)null, r2.Buzz);
                                },
                                r3 =>
                                {

                                    Assert.Equal("mkay", r3.Foo);
                                    Assert.Equal(new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local), r3.Fizz);
                                    Assert.Equal((int?)8675309, r3.Bar);
                                    Assert.Equal((double?)987654321.012345, r3.Buzz);
                                }
                            );
                        }
                    }
                );
        }

        [Fact]
        public void ReadAll_PreAllocated()
        {
            var CSV =
$@"Foo,Fizz,Bar,Buzz
""hello{'\n'}world"",{new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local)},,123.45
,{new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local)},,
mkay,{new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local)},8675309,987654321.012345";

            var opts = Options.Default;

            RunSyncReaderVariants<_ReadAll>(
                    opts,
                    (config, getReader) =>
                    {
                        var pre = new List<_ReadAll>();
                        pre.Add(new _ReadAll { Bar = 1, Buzz = 2.2, Fizz = new DateTime(3, 3, 3), Foo = "4" });

                        using (var reader = getReader(CSV))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll(pre);
                            Assert.Collection(
                                read,
                                r1 =>
                                {
                                    Assert.Equal("4", r1.Foo);
                                    Assert.Equal(new DateTime(3, 3, 3), r1.Fizz);
                                    Assert.Equal(1, r1.Bar);
                                    Assert.Equal(2.2, r1.Buzz);
                                },
                                r2 =>
                                {

                                    Assert.Equal("hello\nworld", r2.Foo);
                                    Assert.Equal(new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local), r2.Fizz);
                                    Assert.Equal((int?)null, r2.Bar);
                                    Assert.Equal(123.45, r2.Buzz);
                                },
                                r3 =>
                                {

                                    Assert.Equal("", r3.Foo);
                                    Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local), r3.Fizz);
                                    Assert.Equal((int?)null, r3.Bar);
                                    Assert.Equal((double?)null, r3.Buzz);
                                },
                                r4 =>
                                {

                                    Assert.Equal("mkay", r4.Foo);
                                    Assert.Equal(new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local), r4.Fizz);
                                    Assert.Equal((int?)8675309, r4.Bar);
                                    Assert.Equal((double?)987654321.012345, r4.Buzz);
                                }
                            );
                        }
                    }
                );
        }

        [Fact]
        public void EnumerateAll()
        {
            var CSV =
$@"Foo,Fizz,Bar,Buzz
""hello{'\n'}world"",{new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local)},,123.45
,{new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local)},,
mkay,{new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local)},8675309,987654321.012345";

            var opts = Options.Default;

            RunSyncReaderVariants<_ReadAll>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader(CSV))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.EnumerateAll();

                            var rows = new List<_ReadAll>();
                            foreach (var r in read)
                            {
                                rows.Add(r);

                                // can't double enumerate
                                //   happens here because `foreach` implicitly disposes `read`
                                Assert.Throws<InvalidOperationException>(() => read.GetEnumerator());
                            }

                            Assert.Collection(
                                rows,
                                r1 =>
                                {

                                    Assert.Equal("hello\nworld", r1.Foo);
                                    Assert.Equal(new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local), r1.Fizz);
                                    Assert.Equal((int?)null, r1.Bar);
                                    Assert.Equal(123.45, r1.Buzz);
                                },
                                r2 =>
                                {

                                    Assert.Equal("", r2.Foo);
                                    Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local), r2.Fizz);
                                    Assert.Equal((int?)null, r2.Bar);
                                    Assert.Equal((double?)null, r2.Buzz);
                                },
                                r3 =>
                                {

                                    Assert.Equal("mkay", r3.Foo);
                                    Assert.Equal(new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local), r3.Fizz);
                                    Assert.Equal((int?)8675309, r3.Bar);
                                    Assert.Equal((double?)987654321.012345, r3.Buzz);
                                }
                            );
                        }
                    }
                );
        }

        private class _OneColumnOneRow
        {
            public string Foo { get; set; }
        }

        [Fact]
        public void OneColumnOneRow()
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

            // normal
            RunSyncReaderVariants<_OneColumnOneRow>(
                opts,
                (config, getReader) =>
                {
                    using (var str = getReader("hello"))
                    using (var reader = config.CreateReader(str))
                    {
                        Assert.True(reader.TryRead(out var record));
                        Assert.NotNull(record);
                        Assert.Equal("hello", record.Foo);

                        Assert.False(reader.TryRead(out _));
                    }
                }
            );

            // quoted
            RunSyncReaderVariants<_OneColumnOneRow>(
                opts,
                (config, getReader) =>
                {
                    using (var str = getReader("\"hello world\""))
                    using (var reader = config.CreateReader(str))
                    {
                        Assert.True(reader.TryRead(out var record));
                        Assert.NotNull(record);
                        Assert.Equal("hello world", record.Foo);

                        Assert.False(reader.TryRead(out _));
                    }
                }
            );

            // escaped
            RunSyncReaderVariants<_OneColumnOneRow>(
                opts,
                (config, getReader) =>
                {
                    using (var str = getReader("\"hello \"\" world\""))
                    using (var reader = config.CreateReader(str))
                    {
                        Assert.True(reader.TryRead(out var record));
                        Assert.NotNull(record);
                        Assert.Equal("hello \" world", record.Foo);

                        Assert.False(reader.TryRead(out _));
                    }
                }
            );
        }

        private class _TwoColumnOneRow
        {
            public string One { get; set; }
            public string Two { get; set; }
        }

        [Fact]
        public void TwoColumnOneRow()
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

            // normal
            RunSyncReaderVariants<_TwoColumnOneRow>(
                opts,
                (config, getReader) =>
                {
                    using (var str = getReader("hello,world"))
                    using (var reader = config.CreateReader(str))
                    {
                        Assert.True(reader.TryRead(out var t));

                        Assert.Equal("hello", t.One);
                        Assert.Equal("world", t.Two);

                        Assert.False(reader.TryRead(out _));
                    }
                }
            );

            // quoted
            RunSyncReaderVariants<_TwoColumnOneRow>(
                opts,
                (config, getReader) =>
                {
                    using (var str = getReader("\"hello,world\",\"fizz,buzz\""))
                    using (var reader = config.CreateReader(str))
                    {
                        Assert.True(reader.TryRead(out var t));

                        Assert.Equal("hello,world", t.One);
                        Assert.Equal("fizz,buzz", t.Two);

                        Assert.False(reader.TryRead(out _));
                    }
                }
            );

            // escaped
            RunSyncReaderVariants<_TwoColumnOneRow>(
                opts,
                (config, getReader) =>
                {
                    using (var str = getReader("\"hello\"\"world\",\"fizz\"\"buzz\""))
                    using (var reader = config.CreateReader(str))
                    {
                        Assert.True(reader.TryRead(out var t));

                        Assert.Equal("hello\"world", t.One);
                        Assert.Equal("fizz\"buzz", t.Two);

                        Assert.False(reader.TryRead(out _));
                    }
                }
            );
        }

        private class _TwoColumnTwoRow
        {
            public string Fizz { get; set; }
            public string Buzz { get; set; }
        }

        [Fact]
        public void TwoColumnTwoRow()
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

            // normal
            RunSyncReaderVariants<_TwoColumnTwoRow>(
                opts,
                (config, getReader) =>
                {
                    using (var str = getReader("hello,world\r\nfoo,bar"))
                    using (var reader = config.CreateReader(str))
                    {
                        Assert.True(reader.TryRead(out var t));
                        Assert.Equal("hello", t.Fizz);
                        Assert.Equal("world", t.Buzz);

                        Assert.True(reader.TryRead(out t));
                        Assert.Equal("foo", t.Fizz);
                        Assert.Equal("bar", t.Buzz);

                        Assert.False(reader.TryRead(out _));
                    }
                }
            );

            // quoted
            RunSyncReaderVariants<_TwoColumnTwoRow>(
                opts,
                (config, getReader) =>
                {
                    using (var str = getReader("\"hello,world\",whatever\r\n\"foo,bar\",whoever"))
                    using (var reader = config.CreateReader(str))
                    {
                        Assert.True(reader.TryRead(out var t));
                        Assert.Equal("hello,world", t.Fizz);
                        Assert.Equal("whatever", t.Buzz);

                        Assert.True(reader.TryRead(out t));
                        Assert.Equal("foo,bar", t.Fizz);
                        Assert.Equal("whoever", t.Buzz);

                        Assert.False(reader.TryRead(out _));
                    }
                }
            );

            // escaped
            RunSyncReaderVariants<_TwoColumnTwoRow>(
                opts,
                (config, getReader) =>
                {
                    using (var str = getReader("\"hello\"\"world\",whatever\r\n\"foo\"\"bar\",whoever"))
                    using (var reader = config.CreateReader(str))
                    {
                        Assert.True(reader.TryRead(out var t));
                        Assert.Equal("hello\"world", t.Fizz);
                        Assert.Equal("whatever", t.Buzz);

                        Assert.True(reader.TryRead(out t));
                        Assert.Equal("foo\"bar", t.Fizz);
                        Assert.Equal("whoever", t.Buzz);

                        Assert.False(reader.TryRead(out _));
                    }
                }
            );
        }

        private class _DetectLineEndings
        {
            public string Foo { get; set; }
            public string Bar { get; set; }
            public string Fizz { get; set; }
        }

        [Fact]
        public void DetectLineEndings()
        {
            var opts = Options.CreateBuilder(Options.Default).WithRowEnding(RowEnding.Detect).WithReadHeader(ReadHeader.Never).ToOptions();

            // normal
            {
                // \r\n
                RunSyncReaderVariants<_DetectLineEndings>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("a,bb,ccc\r\ndddd,eeeee,ffffff\r\n1,2,3\r\n"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a", t1.Foo);
                            Assert.Equal("bb", t1.Bar);
                            Assert.Equal("ccc", t1.Fizz);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("dddd", t2.Foo);
                            Assert.Equal("eeeee", t2.Bar);
                            Assert.Equal("ffffff", t2.Fizz);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal("1", t3.Foo);
                            Assert.Equal("2", t3.Bar);
                            Assert.Equal("3", t3.Fizz);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \r
                RunSyncReaderVariants<_DetectLineEndings>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("a,bb,ccc\rdddd,eeeee,ffffff\r1,2,3\r"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a", t1.Foo);
                            Assert.Equal("bb", t1.Bar);
                            Assert.Equal("ccc", t1.Fizz);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("dddd", t2.Foo);
                            Assert.Equal("eeeee", t2.Bar);
                            Assert.Equal("ffffff", t2.Fizz);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal("1", t3.Foo);
                            Assert.Equal("2", t3.Bar);
                            Assert.Equal("3", t3.Fizz);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \n
                RunSyncReaderVariants<_DetectLineEndings>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("a,bb,ccc\ndddd,eeeee,ffffff\n1,2,3\n"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a", t1.Foo);
                            Assert.Equal("bb", t1.Bar);
                            Assert.Equal("ccc", t1.Fizz);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("dddd", t2.Foo);
                            Assert.Equal("eeeee", t2.Bar);
                            Assert.Equal("ffffff", t2.Fizz);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal("1", t3.Foo);
                            Assert.Equal("2", t3.Bar);
                            Assert.Equal("3", t3.Fizz);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );
            }

            // quoted
            {
                // \r\n
                RunSyncReaderVariants<_DetectLineEndings>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",bb,ccc\r\ndddd,\"ee\neee\",ffffff\r\n1,2,\"3\r\n\"\r\n"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a\r", t1.Foo);
                            Assert.Equal("bb", t1.Bar);
                            Assert.Equal("ccc", t1.Fizz);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("dddd", t2.Foo);
                            Assert.Equal("ee\neee", t2.Bar);
                            Assert.Equal("ffffff", t2.Fizz);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal("1", t3.Foo);
                            Assert.Equal("2", t3.Bar);
                            Assert.Equal("3\r\n", t3.Fizz);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \r
                RunSyncReaderVariants<_DetectLineEndings>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",bb,ccc\rdddd,\"ee\neee\",ffffff\r1,2,\"3\r\n\"\r"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a\r", t1.Foo);
                            Assert.Equal("bb", t1.Bar);
                            Assert.Equal("ccc", t1.Fizz);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("dddd", t2.Foo);
                            Assert.Equal("ee\neee", t2.Bar);
                            Assert.Equal("ffffff", t2.Fizz);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal("1", t3.Foo);
                            Assert.Equal("2", t3.Bar);
                            Assert.Equal("3\r\n", t3.Fizz);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \n
                RunSyncReaderVariants<_DetectLineEndings>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",bb,ccc\ndddd,\"ee\neee\",ffffff\n1,2,\"3\r\n\"\n"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a\r", t1.Foo);
                            Assert.Equal("bb", t1.Bar);
                            Assert.Equal("ccc", t1.Fizz);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("dddd", t2.Foo);
                            Assert.Equal("ee\neee", t2.Bar);
                            Assert.Equal("ffffff", t2.Fizz);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal("1", t3.Foo);
                            Assert.Equal("2", t3.Bar);
                            Assert.Equal("3\r\n", t3.Fizz);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );
            }

            // escaped
            {
                // \r\n
                RunSyncReaderVariants<_DetectLineEndings>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",\"b\"\"b\",ccc\r\n\"\"\"dddd\",\"ee\neee\",ffffff\r\n1,\"\"\"2\"\"\",\"3\r\n\"\r\n"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a\r", t1.Foo);
                            Assert.Equal("b\"b", t1.Bar);
                            Assert.Equal("ccc", t1.Fizz);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("\"dddd", t2.Foo);
                            Assert.Equal("ee\neee", t2.Bar);
                            Assert.Equal("ffffff", t2.Fizz);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal("1", t3.Foo);
                            Assert.Equal("\"2\"", t3.Bar);
                            Assert.Equal("3\r\n", t3.Fizz);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \r
                RunSyncReaderVariants<_DetectLineEndings>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",\"b\"\"b\",ccc\r\"\"\"dddd\",\"ee\neee\",ffffff\r1,\"\"\"2\"\"\",\"3\r\n\"\r"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a\r", t1.Foo);
                            Assert.Equal("b\"b", t1.Bar);
                            Assert.Equal("ccc", t1.Fizz);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("\"dddd", t2.Foo);
                            Assert.Equal("ee\neee", t2.Bar);
                            Assert.Equal("ffffff", t2.Fizz);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal("1", t3.Foo);
                            Assert.Equal("\"2\"", t3.Bar);
                            Assert.Equal("3\r\n", t3.Fizz);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \n
                RunSyncReaderVariants<_DetectLineEndings>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",\"b\"\"b\",ccc\n\"\"\"dddd\",\"ee\neee\",ffffff\n1,\"\"\"2\"\"\",\"3\r\n\"\n"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a\r", t1.Foo);
                            Assert.Equal("b\"b", t1.Bar);
                            Assert.Equal("ccc", t1.Fizz);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("\"dddd", t2.Foo);
                            Assert.Equal("ee\neee", t2.Bar);
                            Assert.Equal("ffffff", t2.Fizz);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal("1", t3.Foo);
                            Assert.Equal("\"2\"", t3.Bar);
                            Assert.Equal("3\r\n", t3.Fizz);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );
            }
        }

        private class _DetectHeaders
        {
            public int Hello { get; set; }
            public double World { get; set; }
        }

        [Fact]
        public void DetectHeaders()
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Detect).WithRowEnding(RowEnding.Detect).ToOptions();

            // no headers
            RunSyncReaderVariants<_DetectHeaders>(
                opts,
                (config, getReader) =>
                {
                    using (var str = getReader("123,4.56"))
                    using (var reader = (Reader<_DetectHeaders>)config.CreateReader(str))
                    {
                        Assert.True(reader.TryRead(out var t));
                        Assert.Equal(123, t.Hello);
                        Assert.Equal(4.56, t.World);

                        Assert.Equal(ReadHeader.Never, reader.ReadHeaders.Value);

                        Assert.Collection(
                            reader.RowBuilder.Columns,
                            c => Assert.Equal("Hello", c),
                            c => Assert.Equal("World", c)
                        );

                        Assert.False(reader.TryRead(out _));
                    }
                }
            );

            // headers
            {
                // \r\n
                RunSyncReaderVariants<_DetectHeaders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("Hello,World\r\n123,4.56\r\n789,0.12\r\n"))
                        using (var reader = (Reader<_DetectHeaders>)config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t));
                            Assert.Equal(123, t.Hello);
                            Assert.Equal(4.56, t.World);

                            Assert.Equal(ReadHeader.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.RowBuilder.Columns,
                                c => Assert.Equal("Hello", c),
                                c => Assert.Equal("World", c)
                            );

                            Assert.True(reader.TryRead(out t));
                            Assert.Equal(789, t.Hello);
                            Assert.Equal(0.12, t.World);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \n
                RunSyncReaderVariants<_DetectHeaders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("Hello,World\n123,4.56\n789,0.12\n"))
                        using (var reader = (Reader<_DetectHeaders>)config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t));
                            Assert.Equal(123, t.Hello);
                            Assert.Equal(4.56, t.World);

                            Assert.Equal(ReadHeader.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.RowBuilder.Columns,
                                c => Assert.Equal("Hello", c),
                                c => Assert.Equal("World", c)
                            );

                            Assert.True(reader.TryRead(out t));
                            Assert.Equal(789, t.Hello);
                            Assert.Equal(0.12, t.World);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \r
                RunSyncReaderVariants<_DetectHeaders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("Hello,World\r123,4.56\r789,0.12\r"))
                        using (var reader = (Reader<_DetectHeaders>)config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t));
                            Assert.Equal(123, t.Hello);
                            Assert.Equal(4.56, t.World);

                            Assert.Equal(ReadHeader.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.RowBuilder.Columns,
                                c => Assert.Equal("Hello", c),
                                c => Assert.Equal("World", c)
                            );

                            Assert.True(reader.TryRead(out t));
                            Assert.Equal(789, t.Hello);
                            Assert.Equal(0.12, t.World);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );
            }

            // headers, different order
            {
                // \r\n
                RunSyncReaderVariants<_DetectHeaders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("World,Hello\r\n4.56,123\r\n0.12,789\r\n"))
                        using (var reader = (Reader<_DetectHeaders>)config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t));
                            Assert.Equal(123, t.Hello);
                            Assert.Equal(4.56, t.World);

                            Assert.Equal(ReadHeader.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.RowBuilder.Columns,
                                c => Assert.Equal("World", c),
                                c => Assert.Equal("Hello", c)
                            );

                            Assert.True(reader.TryRead(out t));
                            Assert.Equal(789, t.Hello);
                            Assert.Equal(0.12, t.World);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \n
                RunSyncReaderVariants<_DetectHeaders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("World,Hello\n4.56,123\n0.12,789\n"))
                        using (var reader = (Reader<_DetectHeaders>)config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t));
                            Assert.Equal(123, t.Hello);
                            Assert.Equal(4.56, t.World);

                            Assert.Equal(ReadHeader.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.RowBuilder.Columns,
                                c => Assert.Equal("World", c),
                                c => Assert.Equal("Hello", c)
                            );

                            Assert.True(reader.TryRead(out t));
                            Assert.Equal(789, t.Hello);
                            Assert.Equal(0.12, t.World);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \r
                RunSyncReaderVariants<_DetectHeaders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("World,Hello\r4.56,123\r0.12,789\r"))
                        using (var reader = (Reader<_DetectHeaders>)config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t));
                            Assert.Equal(123, t.Hello);
                            Assert.Equal(4.56, t.World);

                            Assert.Equal(ReadHeader.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.RowBuilder.Columns,
                                c => Assert.Equal("World", c),
                                c => Assert.Equal("Hello", c)
                            );

                            Assert.True(reader.TryRead(out t));
                            Assert.Equal(789, t.Hello);
                            Assert.Equal(0.12, t.World);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );
            }

            // headers, missing
            {
                // \r\n
                RunSyncReaderVariants<_DetectHeaders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("World,Foo\r\n4.56,123\r\n0.12,789\r\n"))
                        using (var reader = (Reader<_DetectHeaders>)config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t));
                            Assert.Equal(0, t.Hello);
                            Assert.Equal(4.56, t.World);

                            Assert.Equal(ReadHeader.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.RowBuilder.Columns,
                                c => Assert.Equal("World", c),
                                c => Assert.Equal("--UNKNOWN--", c)
                            );

                            Assert.True(reader.TryRead(out t));
                            Assert.Equal(0, t.Hello);
                            Assert.Equal(0.12, t.World);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \n
                RunSyncReaderVariants<_DetectHeaders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("World,Foo\n4.56,123\n0.12,789\n"))
                        using (var reader = (Reader<_DetectHeaders>)config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t));
                            Assert.Equal(0, t.Hello);
                            Assert.Equal(4.56, t.World);

                            Assert.Equal(ReadHeader.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.RowBuilder.Columns,
                                c => Assert.Equal("World", c),
                                c => Assert.Equal("--UNKNOWN--", c)
                            );

                            Assert.True(reader.TryRead(out t));
                            Assert.Equal(0, t.Hello);
                            Assert.Equal(0.12, t.World);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \r
                RunSyncReaderVariants<_DetectHeaders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("World,Foo\r4.56,123\r0.12,789\r"))
                        using (var reader = (Reader<_DetectHeaders>)config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t));
                            Assert.Equal(0, t.Hello);
                            Assert.Equal(4.56, t.World);

                            Assert.Equal(ReadHeader.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.RowBuilder.Columns,
                                c => Assert.Equal("World", c),
                                c => Assert.Equal("--UNKNOWN--", c)
                            );

                            Assert.True(reader.TryRead(out t));
                            Assert.Equal(0, t.Hello);
                            Assert.Equal(0.12, t.World);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );
            }
        }

        private class _IsRequiredMissing
        {
            public string A { get; set; }
            [DataMember(IsRequired = true)]
            public string B { get; set; }
        }

        private sealed class _IsRequiredMissing_Hold
        {
            public string A { get; private set; }
            public string B { get; private set; }
            public _IsRequiredMissing_Hold(string a, string b)
            {
                A = a;
                B = b;
            }
        }

        [Fact]
        public void IsRequiredNotInHeader()
        {
            // simple type
            {
                var opts = Options.Default;
                var CSV = "A,C\r\nhello,world";

                RunSyncReaderVariants<_IsRequiredMissing>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            Assert.Throws<SerializationException>(() => csv.ReadAll());
                        }
                    }
                );
            }

            // hold type
            {
                var t = typeof(_IsRequiredMissing_Hold).GetTypeInfo();
                var cons = t.GetConstructors().Single();
                var pA = cons.GetParameters().Single(a => a.Name == "a");
                var pB = cons.GetParameters().Single(b => b.Name == "b");

                var td = ManualTypeDescriber.CreateBuilder();
                td.WithInstanceProvider(InstanceProvider.ForConstructorWithParameters(cons));
                td.WithExplicitSetter(t, "A", Setter.ForConstructorParameter(pA), Parser.GetDefault(typeof(string).GetTypeInfo()), MemberRequired.Yes);
                td.WithExplicitSetter(t, "B", Setter.ForConstructorParameter(pB), Parser.GetDefault(typeof(string).GetTypeInfo()), MemberRequired.Yes);

                var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(td.ToManualTypeDescriber()).ToOptions();
                var CSV = "A,C\r\nhello,world";

                RunSyncReaderVariants<_IsRequiredMissing_Hold>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            Assert.Throws<SerializationException>(() => csv.ReadAll());
                        }
                    }
                );
            }
        }

        [Fact]
        public void IsRequiredNotInRow()
        {
            var opts = Options.Default;

            // beginning
            RunSyncReaderVariants<_IsRequiredMissing>(
                opts,
                (config, getReader) =>
                {
                    var CSV = "B,C\r\nhello,world\r\n,";

                    using (var str = getReader(CSV))
                    using (var csv = config.CreateReader(str))
                    {
                        Assert.Throws<SerializationException>(() => csv.ReadAll());
                    }
                }
            );

            // middle
            RunSyncReaderVariants<_IsRequiredMissing>(
                opts,
                (config, getReader) =>
                {
                    var CSV = "A,B,C\r\nhello,world,foo\r\n,,";

                    using (var str = getReader(CSV))
                    using (var csv = config.CreateReader(str))
                    {
                        Assert.Throws<SerializationException>(() => csv.ReadAll());
                    }
                }
            );

            // end
            RunSyncReaderVariants<_IsRequiredMissing>(
                opts,
                (config, getReader) =>
                {
                    var CSV = "A,B\r\nhello,world\r\n,";

                    using (var str = getReader(CSV))
                    using (var csv = config.CreateReader(str))
                    {
                        Assert.Throws<SerializationException>(() => csv.ReadAll());
                    }
                }
            );
        }

        [Fact]
        public void WeirdComments()
        {
            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).WithCommentCharacter('#').WithRowEnding(RowEnding.LineFeed).ToOptions();
                RunSyncReaderVariants<_Comment>(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "#this is a test comment!\r\nhello,world\nfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );

                RunSyncReaderVariants<_Comment>(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "hello,world\n#this is a test comment!\r\nfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );
            }

            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).WithCommentCharacter('#').WithRowEnding(RowEnding.CarriageReturn).ToOptions();
                RunSyncReaderVariants<_Comment>(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "#this is a test comment!\n\rhello,world\rfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );

                RunSyncReaderVariants<_Comment>(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "hello,world\r#this is a test comment!\n\rfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );
            }

            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).WithCommentCharacter('#').WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();
                RunSyncReaderVariants<_Comment>(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "#this is a test comment!\n\r\nhello,world\r\nfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );

                RunSyncReaderVariants<_Comment>(
                   opts,
                   (config, getReader) =>
                   {
                       var CSV = "#this is a test comment!\r\r\nhello,world\r\nfoo,bar";
                       using (var str = getReader(CSV))
                       using (var csv = config.CreateReader(str))
                       {
                           var rows = csv.ReadAll();
                           Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                       }
                   }
               );

                RunSyncReaderVariants<_Comment>(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "hello,world\r\n#this is a test comment!\n\r\nfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );

                RunSyncReaderVariants<_Comment>(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "hello,world\r\n#this is a test comment!\r\r\nfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );
            }
        }

        private class _Comment
        {
            [DataMember(Name = "hello")]
            public string Hello { get; set; }
            [DataMember(Name = "world")]
            public string World { get; set; }
        }

        [Fact]
        public void Comments()
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).WithCommentCharacter('#').ToOptions();

            // comment first line
            RunSyncReaderVariants<_Comment>(
                opts,
                (config, getReader) =>
                {
                    var CSV = "#this is a test comment!\r\nhello,world\r\nfoo,bar";
                    using (var str = getReader(CSV))
                    using (var csv = config.CreateReader(str))
                    {
                        var rows = csv.ReadAll();
                        Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                    }
                }
            );

            // comment after header
            RunSyncReaderVariants<_Comment>(
                opts,
                (config, getReader) =>
                {
                    var CSV = "hello,world\r\n#this is a test comment\r\nfoo,bar";
                    using (var str = getReader(CSV))
                    using (var csv = config.CreateReader(str))
                    {
                        var rows = csv.ReadAll();
                        Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                    }
                }
            );

            // comment between rows
            RunSyncReaderVariants<_Comment>(
                opts,
                (config, getReader) =>
                {
                    var CSV = "hello,world\r\nfoo,bar\r\n#comment!\r\nfizz,buzz";
                    using (var str = getReader(CSV))
                    using (var csv = config.CreateReader(str))
                    {
                        var rows = csv.ReadAll();
                        Assert.Collection(
                            rows,
                            a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); },
                            b => { Assert.Equal("fizz", b.Hello); Assert.Equal("buzz", b.World); }
                        );
                    }
                }
            );

            // comment at end
            RunSyncReaderVariants<_Comment>(
                opts,
                (config, getReader) =>
                {
                    var CSV = "hello,world\r\nfoo,bar\r\n#comment!";
                    using (var str = getReader(CSV))
                    using (var csv = config.CreateReader(str))
                    {
                        var rows = csv.ReadAll();
                        Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                    }
                }
            );
        }

        private class _Context
        {
            public string Foo { get; set; }
            public int Bar { get; set; }
        }

        private static List<string> _Context_ParseFoo_Records;
        public static bool _Context_ParseFoo(ReadOnlySpan<char> data, in ReadContext ctx, out string val)
        {
            _Context_ParseFoo_Records.Add($"{ctx.RowNumber},{ctx.Column.Name},{ctx.Column.Index},{new string(data)},{ctx.Context}");

            val = new string(data);
            return true;
        }

        private static List<string> _Context_ParseBar_Records;
        public static bool _Context_ParseBar(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            _Context_ParseBar_Records.Add($"{ctx.RowNumber},{ctx.Column.Name},{ctx.Column.Index},{new string(data)},{ctx.Context}");

            if (!int.TryParse(data, out val))
            {
                val = default;
                return false;
            }

            return true;
        }

        [Fact]
        public void Context()
        {
            var parseFoo = (Parser)typeof(ReaderTests).GetMethod(nameof(_Context_ParseFoo));
            var parseBar = (Parser)typeof(ReaderTests).GetMethod(nameof(_Context_ParseBar));

            var describer = ManualTypeDescriberBuilder.CreateBuilder(ManualTypeDescriberFallbackBehavior.UseFallback);
            describer.WithInstanceProvider((InstanceProvider)typeof(_Context).GetConstructor(Type.EmptyTypes));
            describer.WithDeserializableProperty(typeof(_Context).GetProperty(nameof(_Context.Foo)), nameof(_Context.Foo), parseFoo);
            describer.WithDeserializableProperty(typeof(_Context).GetProperty(nameof(_Context.Bar)), nameof(_Context.Bar), parseBar);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(describer.ToManualTypeDescriber()).ToOptions();

            // no headers
            {
                RunSyncReaderVariants<_Context>(
                    opts,
                    (config, getReader) =>
                    {
                        _Context_ParseFoo_Records = new List<string>();
                        _Context_ParseBar_Records = new List<string>();

                        using (var reader = getReader("hello,123\r\nfoo,456\r\n,\r\nnope,7"))
                        using (var csv = config.CreateReader(reader, "context!"))
                        {
                            var r = csv.ReadAll();

                            Assert.Equal(4, r.Count);
                        }

                        Assert.Collection(
                            _Context_ParseFoo_Records,
                            c => Assert.Equal("0,Foo,0,hello,context!", c),
                            c => Assert.Equal("1,Foo,0,foo,context!", c),
                            c => Assert.Equal("2,Foo,0,,context!", c),
                            c => Assert.Equal("3,Foo,0,nope,context!", c)
                        );

                        Assert.Collection(
                            _Context_ParseBar_Records,
                            c => Assert.Equal("0,Bar,1,123,context!", c),
                            c => Assert.Equal("1,Bar,1,456,context!", c),
                            c => Assert.Equal("3,Bar,1,7,context!", c)
                        );
                    }
                );
            }

            // with headers
            {
                RunSyncReaderVariants<_Context>(
                    opts,
                    (config, getReader) =>
                    {
                        _Context_ParseFoo_Records = new List<string>();
                        _Context_ParseBar_Records = new List<string>();

                        using (var reader = getReader("Bar,Foo\r\n123,hello\r\n456,foo\r\n8,\r\n7,nope"))
                        using (var csv = config.CreateReader(reader, 999))
                        {
                            var r = csv.ReadAll();

                            Assert.Equal(4, r.Count);
                        }

                        Assert.Collection(
                            _Context_ParseFoo_Records,
                            c => Assert.Equal("0,Foo,1,hello,999", c),
                            c => Assert.Equal("1,Foo,1,foo,999", c),
                            c => Assert.Equal("3,Foo,1,nope,999", c)
                        );

                        Assert.Collection(
                            _Context_ParseBar_Records,
                            c => Assert.Equal("0,Bar,0,123,999", c),
                            c => Assert.Equal("1,Bar,0,456,999", c),
                            c => Assert.Equal("2,Bar,0,8,999", c),
                            c => Assert.Equal("3,Bar,0,7,999", c)
                        );
                    }
                );
            }
        }

        [Fact]
        public async Task MultiCharacterSeparatorInHeadersAsync()
        {
            // always
            {
                var opts = Options.CreateBuilder(Options.Default).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Always).ToOptions();

                await RunAsyncReaderVariants<_MultiCharacterSeparatorInHeaders>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("B#|#\"Foo#|#Bar\"\r\n123#|#hello"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(
                                rows,
                                a =>
                                {
                                    Assert.Equal("hello", a.A);
                                    Assert.Equal(123, a.B);
                                }
                            );
                        }
                    }
                );
            }

            // detect
            {
                var opts = Options.CreateBuilder(Options.Default).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Detect).ToOptions();

                await RunAsyncReaderVariants<_MultiCharacterSeparatorInHeaders>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("B#|#\"Foo#|#Bar\"\r\n123#|#hello"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(
                                rows,
                                a =>
                                {
                                    Assert.Equal("hello", a.A);
                                    Assert.Equal(123, a.B);
                                }
                            );
                        }
                    }
                );
            }

            // detect rows endings
            {
                var opts = Options.CreateBuilder(Options.Default).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Detect).WithRowEnding(RowEnding.Detect).ToOptions();

                // \r\n
                {
                    await RunAsyncReaderVariants<_MultiCharacterSeparatorInHeaders>(
                        opts,
                        async (config, getReader) =>
                        {
                            await using (var reader = await getReader("B#|#\"Foo#|#Bar\"\r\n123#|#hello"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.A);
                                        Assert.Equal(123, a.B);
                                    }
                                );
                            }
                        }
                    );
                }

                // \r
                {
                    RunSyncReaderVariants<_MultiCharacterSeparatorInHeaders>(
                        opts,
                        (config, getReader) =>
                        {
                            using (var reader = getReader("B#|#\"Foo#|#Bar\"\r123#|#hello"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.A);
                                        Assert.Equal(123, a.B);
                                    }
                                );
                            }
                        }
                    );
                }

                // \n
                {
                    await RunAsyncReaderVariants<_MultiCharacterSeparatorInHeaders>(
                        opts,
                        async (config, getReader) =>
                        {
                            await using (var reader = await getReader("B#|#\"Foo#|#Bar\"\n123#|#hello"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.A);
                                        Assert.Equal(123, a.B);
                                    }
                                );
                            }
                        }
                    );
                }
            }
        }

        [Fact]
        public async Task MultiCharacterSeparatorsAsync()
        {
            // header variants
            {
                // no headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Never).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

                    await RunAsyncReaderVariants<_MultiCharacterSeparators>(
                        opts,
                        async (config, getReader) =>
                        {
                            await using (var reader = await getReader("hello#|#123\r\n\"world\"#|#456\r\n\"f#|#b\"#|#789"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.A);
                                        Assert.Equal(123, a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", b.A);
                                        Assert.Equal(456, b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", c.A);
                                        Assert.Equal(789, c.B);
                                    }
                                );
                            }
                        }
                    );
                }

                // always headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Always).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

                    await RunAsyncReaderVariants<_MultiCharacterSeparators>(
                        opts,
                        async (config, getReader) =>
                        {
                            await using (var reader = await getReader("A#|#B\r\nhello#|#123\r\n\"world\"#|#456\r\n\"f#|#b\"#|#789"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.A);
                                        Assert.Equal(123, a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", b.A);
                                        Assert.Equal(456, b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", c.A);
                                        Assert.Equal(789, c.B);
                                    }
                                );
                            }
                        }
                    );
                }

                // detect headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Detect).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

                    // not present
                    await RunAsyncReaderVariants<_MultiCharacterSeparators>(
                        opts,
                        async (config, getReader) =>
                        {
                            await using (var reader = await getReader("hello#|#123\r\n\"world\"#|#456\r\n\"f#|#b\"#|#789"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.A);
                                        Assert.Equal(123, a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", b.A);
                                        Assert.Equal(456, b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", c.A);
                                        Assert.Equal(789, c.B);
                                    }
                                );
                            }
                        }
                    );

                    // present
                    await RunAsyncReaderVariants<_MultiCharacterSeparators>(
                        opts,
                        async (config, getReader) =>
                        {
                            await using (var reader = await getReader("B#|#A\r\n123#|#hello\r\n456#|#\"world\"\r\n789#|#\"f#|#b\""))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.A);
                                        Assert.Equal(123, a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", b.A);
                                        Assert.Equal(456, b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", c.A);
                                        Assert.Equal(789, c.B);
                                    }
                                );
                            }
                        }
                    );
                }
            }

            // detect line endings
            {
                var opts = Options.CreateBuilder(Options.Default).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Detect).WithRowEnding(RowEnding.Detect).ToOptions();

                // \r\n
                {
                    // not present
                    await RunAsyncReaderVariants<_MultiCharacterSeparators>(
                        opts,
                        async (config, getReader) =>
                        {
                            await using (var reader = await getReader("hello#|#123\r\n\"world\"#|#456\r\n\"f#|#b\"#|#789"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.A);
                                        Assert.Equal(123, a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", b.A);
                                        Assert.Equal(456, b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", c.A);
                                        Assert.Equal(789, c.B);
                                    }
                                );
                            }
                        }
                    );

                    // present
                    await RunAsyncReaderVariants<_MultiCharacterSeparators>(
                        opts,
                        async (config, getReader) =>
                        {
                            await using (var reader = await getReader("B#|#A\r\n123#|#hello\r\n456#|#\"world\"\r\n789#|#\"f#|#b\""))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.A);
                                        Assert.Equal(123, a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", b.A);
                                        Assert.Equal(456, b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", c.A);
                                        Assert.Equal(789, c.B);
                                    }
                                );
                            }
                        }
                    );
                }

                // \r
                {
                    // not present
                    await RunAsyncReaderVariants<_MultiCharacterSeparators>(
                        opts,
                        async (config, getReader) =>
                        {
                            await using (var reader = await getReader("hello#|#123\r\"world\"#|#456\r\"f#|#b\"#|#789"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.A);
                                        Assert.Equal(123, a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", b.A);
                                        Assert.Equal(456, b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", c.A);
                                        Assert.Equal(789, c.B);
                                    }
                                );
                            }
                        }
                    );

                    // present
                    await RunAsyncReaderVariants<_MultiCharacterSeparators>(
                        opts,
                        async (config, getReader) =>
                        {
                            await using (var reader = await getReader("B#|#A\r123#|#hello\r456#|#\"world\"\r789#|#\"f#|#b\""))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.A);
                                        Assert.Equal(123, a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", b.A);
                                        Assert.Equal(456, b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", c.A);
                                        Assert.Equal(789, c.B);
                                    }
                                );
                            }
                        }
                    );
                }

                // \n
                {
                    // not present
                    await RunAsyncReaderVariants<_MultiCharacterSeparators>(
                        opts,
                        async (config, getReader) =>
                        {
                            await using (var reader = await getReader("hello#|#123\n\"world\"#|#456\n\"f#|#b\"#|#789"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.A);
                                        Assert.Equal(123, a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", b.A);
                                        Assert.Equal(456, b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", c.A);
                                        Assert.Equal(789, c.B);
                                    }
                                );
                            }
                        }
                    );

                    // present
                    await RunAsyncReaderVariants<_MultiCharacterSeparators>(
                        opts,
                        async (config, getReader) =>
                        {
                            await using (var reader = await getReader("B#|#A\n123#|#hello\n456#|#\"world\"\n789#|#\"f#|#b\""))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.A);
                                        Assert.Equal(123, a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", b.A);
                                        Assert.Equal(456, b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", c.A);
                                        Assert.Equal(789, c.B);
                                    }
                                );
                            }
                        }
                    );
                }
            }
        }

        [Fact]
        public async Task ValueTypeInstanceProvidersAsync()
        {
            var ip =
                InstanceProvider.ForDelegate(
                    (in ReadContext _, out _ValueTypeInstanceProviders val) =>
                    {
                        val = new _ValueTypeInstanceProviders { A = 4 };
                        return true;
                    }
                );
            var setter =
                Setter.ForDelegate(
                    (ref _ValueTypeInstanceProviders row, int value, in ReadContext _) =>
                    {
                        row.A *= value;
                    }
                );

            var tdb = ManualTypeDescriber.CreateBuilder(ManualTypeDescriberFallbackBehavior.UseFallback, TypeDescribers.Default);
            tdb.WithInstanceProvider(ip);
            tdb.WithExplicitSetter(typeof(_ValueTypeInstanceProviders).GetTypeInfo(), "A", setter);
            var td = tdb.ToManualTypeDescriber();

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(td).WithCommentCharacter('#').ToOptions();

            // always called
            {
                await RunAsyncReaderVariants<_ValueTypeInstanceProviders>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("A\r\n1\r\n2\r\n3\r\n4"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadAsync();
                            Assert.True(res1.HasValue);
                            var r1 = res1.Value;
                            Assert.Equal(4, r1.A);

                            var res2 = await csv.TryReadAsync();
                            Assert.True(res2.HasValue);
                            var r2 = res2.Value;
                            Assert.Equal(8, r2.A);

                            var res3 = await csv.TryReadAsync();
                            Assert.True(res3.HasValue);
                            var r3 = res3.Value;
                            Assert.Equal(12, r3.A);

                            var res4 = await csv.TryReadAsync();
                            Assert.True(res4.HasValue);
                            var r4 = res4.Value;
                            Assert.Equal(16, r4.A);

                            var res5 = await csv.TryReadAsync();
                            Assert.False(res5.HasValue);
                        }
                    }
                );
            }

            // always called, comments
            {
                await RunAsyncReaderVariants<_ValueTypeInstanceProviders>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("A\r\n1\r\n2\r\n#hello\r\n3\r\n4"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.True(res1.HasValue);
                            var r1 = res1.Value;
                            Assert.Equal(4, r1.A);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.True(res2.HasValue);
                            var r2 = res2.Value;
                            Assert.Equal(8, r2.A);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.True(res3.HasComment);
                            var com3 = res3.Comment;
                            Assert.Equal("hello", com3);

                            var res4 = await csv.TryReadWithCommentAsync();
                            Assert.True(res4.HasValue);
                            var r4 = res4.Value;
                            Assert.Equal(12, r4.A);

                            var res5 = await csv.TryReadWithCommentAsync();
                            Assert.True(res5.HasValue);
                            var r5 = res5.Value;
                            Assert.Equal(16, r5.A);

                            var res6 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res6.ResultType);
                        }
                    }
                );
            }

            // never called
            {
                await RunAsyncReaderVariants<_ValueTypeInstanceProviders>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("A\r\n1\r\n2\r\n3\r\n4"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var r = new _ValueTypeInstanceProviders { A = -2 };

                            var res1 = await csv.TryReadWithReuseAsync(ref r);
                            Assert.True(res1.HasValue);
                            r = res1.Value;
                            Assert.Equal(-2, r.A);

                            var res2 = await csv.TryReadWithReuseAsync(ref r);
                            Assert.True(res2.HasValue);
                            r = res2.Value;
                            Assert.Equal(-4, r.A);

                            var res3 = await csv.TryReadWithReuseAsync(ref r);
                            Assert.True(res3.HasValue);
                            r = res3.Value;
                            Assert.Equal(-12, r.A);

                            var res4 = await csv.TryReadWithReuseAsync(ref r);
                            Assert.True(res4.HasValue);
                            r = res4.Value;
                            Assert.Equal(-48, r.A);

                            var res5 = await csv.TryReadWithReuseAsync(ref r);
                            Assert.False(res5.HasValue);
                        }
                    }
                );
            }

            // never called, comments
            {
                await RunAsyncReaderVariants<_ValueTypeInstanceProviders>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("A\r\n1\r\n2\r\n#hello\r\n3\r\n4"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var r = new _ValueTypeInstanceProviders { A = -2 };

                            var res1 = await csv.TryReadWithCommentReuseAsync(ref r);
                            Assert.True(res1.HasValue);
                            r = res1.Value;
                            Assert.Equal(-2, r.A);

                            var res2 = await csv.TryReadWithCommentReuseAsync(ref r);
                            Assert.True(res2.HasValue);
                            r = res2.Value;
                            Assert.Equal(-4, r.A);

                            var res3 = await csv.TryReadWithCommentReuseAsync(ref r);
                            Assert.True(res3.HasComment);
                            var com3 = res3.Comment;
                            Assert.Equal("hello", com3);

                            var res4 = await csv.TryReadWithCommentReuseAsync(ref r);
                            Assert.True(res4.HasValue);
                            r = res4.Value;
                            Assert.Equal(-12, r.A);

                            var res5 = await csv.TryReadWithCommentReuseAsync(ref r);
                            Assert.True(res5.HasValue);
                            r = res5.Value;
                            Assert.Equal(-48, r.A);

                            var res6 = await csv.TryReadWithCommentReuseAsync(ref r);
                            Assert.Equal(ReadWithCommentResultType.NoValue, res6.ResultType);
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task InstanceSetterWithContextAsync()
        {
            var t = typeof(_InstanceSetterWithContext).GetTypeInfo();

            var mtd = t.GetMethod(nameof(_InstanceSetterWithContext.Setter), BindingFlags.Public | BindingFlags.Instance);
            var setter = Setter.ForMethod(mtd);

            var tdb = ManualTypeDescriber.CreateBuilder(ManualTypeDescriberFallbackBehavior.UseFallback, TypeDescribers.Default);
            tdb.WithExplicitSetter(t, nameof(_InstanceSetterWithContext.A), setter);

            var td = tdb.ToManualTypeDescriber();

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(td).ToOptions();

            await RunAsyncReaderVariants<_InstanceSetterWithContext>(
                opts,
                async (config, getReader) =>
                {
                    await using (var reader = await getReader("A\r\n1\r\n2\r\n3"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = await csv.ReadAllAsync();
                        Assert.Collection(
                            rows,
                            a => Assert.Equal(2, a.A),
                            b => Assert.Equal(4, b.A),
                            c => Assert.Equal(6, c.A)
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task WellKnownSingleColumnsAsync()
        {
            // bool
            {
                await RunAsyncReaderVariants<bool>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("true\r\nfalse\r\ntrue"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new[] { true, false, true }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // bool?
            {
                await RunAsyncReaderVariants<bool?>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("\r\nfalse\r\ntrue"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new[] { default(bool?), false, true }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // char
            {
                await RunAsyncReaderVariants<char>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("a\r\nb\r\nc"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new[] { 'a', 'b', 'c' }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // char?
            {
                await RunAsyncReaderVariants<char?>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("\r\nb\r\nc"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new[] { default(char?), 'b', 'c' }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // byte
            {
                await RunAsyncReaderVariants<byte>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("0\r\n128\r\n255"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new byte[] { 0, 128, 255 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // byte?
            {
                await RunAsyncReaderVariants<byte?>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("0\r\n\r\n255"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new byte?[] { 0, null, 255 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // sbyte
            {
                await RunAsyncReaderVariants<sbyte>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("0\r\n-127\r\n-2"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new sbyte[] { 0, -127, -2 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // sbyte?
            {
                await RunAsyncReaderVariants<sbyte?>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("\r\n-127\r\n-2"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new sbyte?[] { null, -127, -2 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // short
            {
                await RunAsyncReaderVariants<short>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("0\r\n-9876\r\n-16000"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new short[] { 0, -9876, -16000 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // short?
            {
                await RunAsyncReaderVariants<short?>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("0\r\n\r\n-16000"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new short?[] { 0, null, -16000 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // ushort
            {
                await RunAsyncReaderVariants<ushort>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("0\r\n12345\r\n32000"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new ushort[] { 0, 12345, 32000 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // ushort?
            {
                await RunAsyncReaderVariants<ushort?>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("\r\n12345\r\n32000"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new ushort?[] { null, 12345, 32000 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // int
            {
                await RunAsyncReaderVariants<int>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("0\r\n2000000\r\n-15"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new[] { 0, 2000000, -15 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // int?
            {
                await RunAsyncReaderVariants<int?>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("\r\n2000000\r\n-15"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new int?[] { null, 2000000, -15 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // uint
            {
                await RunAsyncReaderVariants<uint>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("0\r\n2000000\r\n4000000000"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new uint[] { 0, 2000000, 4_000_000_000 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // uint?
            {
                await RunAsyncReaderVariants<uint?>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("\r\n2000000\r\n4000000000"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new uint?[] { null, 2000000, 4_000_000_000 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // long
            {
                await RunAsyncReaderVariants<long>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader($"0\r\n{long.MinValue}\r\n{long.MaxValue}"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new long[] { 0, long.MinValue, long.MaxValue }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // long?
            {
                await RunAsyncReaderVariants<long?>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader($"{long.MinValue}\r\n\r\n{long.MaxValue}"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new long?[] { long.MinValue, null, long.MaxValue }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // ulong
            {
                await RunAsyncReaderVariants<ulong>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader($"0\r\n123\r\n{ulong.MaxValue}"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new ulong[] { 0, 123, ulong.MaxValue }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // ulong?
            {
                await RunAsyncReaderVariants<ulong?>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader($"0\r\n\r\n{ulong.MaxValue}"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new ulong?[] { 0, null, ulong.MaxValue }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // float
            {
                await RunAsyncReaderVariants<float>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader($"0.12\r\n123456789.0123\r\n-999999.88888"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new float[] { 0.12f, 123456789.0123f, -999999.88888f }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // float?
            {
                await RunAsyncReaderVariants<float?>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader($"0.12\r\n\r\n-999999.88888"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new float?[] { 0.12f, null, -999999.88888f }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // double
            {
                await RunAsyncReaderVariants<double>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader($"0.12\r\n123456789.0123\r\n-999999.88888"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new double[] { 0.12, 123456789.0123, -999999.88888 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // double?
            {
                await RunAsyncReaderVariants<double?>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader($"0.12\r\n\r\n-999999.88888"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new double?[] { 0.12, null, -999999.88888 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // decimal
            {
                await RunAsyncReaderVariants<decimal>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader($"0.12\r\n123456789.0123\r\n-999999.88888"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new decimal[] { 0.12m, 123456789.0123m, -999999.88888m }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // decimal?
            {
                await RunAsyncReaderVariants<decimal?>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader($"0.12\r\n\r\n-999999.88888"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new decimal?[] { 0.12m, null, -999999.88888m }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // string
            {
                await RunAsyncReaderVariants<string>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader($"hello\r\n\r\nworld"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new string[] { "hello", null, "world" }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // Version
            {
                await RunAsyncReaderVariants<Version>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader($"1.2\r\n\r\n1.2.3.4"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new[] { new Version(1, 2), null, new Version(1, 2, 3, 4) }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // Uri
            {
                await RunAsyncReaderVariants<Uri>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader($"http://example.com/\r\n\r\nhttps://stackoverflow.com/questions"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new[] { new Uri("http://example.com/"), null, new Uri("https://stackoverflow.com/questions") }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // enum
            {
                await RunAsyncReaderVariants<_WellKnownSingleColumns>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader($"Foo\r\nBar\r\nFoo"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new[] { _WellKnownSingleColumns.Foo, _WellKnownSingleColumns.Bar, _WellKnownSingleColumns.Foo }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // enum?
            {
                await RunAsyncReaderVariants<_WellKnownSingleColumns?>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader($"Foo\r\nBar\r\n\r\n"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new _WellKnownSingleColumns?[] { _WellKnownSingleColumns.Foo, _WellKnownSingleColumns.Bar, null }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // flags enum
            {
                await RunAsyncReaderVariants<_WellKnownSingleColumns_Flags>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader($"\"Foo, Bar\"\r\nBar\r\nFizz"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new[] { _WellKnownSingleColumns_Flags.Foo | _WellKnownSingleColumns_Flags.Bar, _WellKnownSingleColumns_Flags.Bar, _WellKnownSingleColumns_Flags.Fizz }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // flags enum?
            {
                await RunAsyncReaderVariants<_WellKnownSingleColumns_Flags?>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader($"\"Foo, Bar\"\r\n\r\nFizz"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new _WellKnownSingleColumns_Flags?[] { _WellKnownSingleColumns_Flags.Foo | _WellKnownSingleColumns_Flags.Bar, null, _WellKnownSingleColumns_Flags.Fizz }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // DateTime
            {
                await RunAsyncReaderVariants<DateTime>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        var ci = CultureInfo.InvariantCulture;

                        await using (var reader =
                            await getReader(
                                $"\"{DateTime.MaxValue.ToString(ci)}\"\r\n\"{new DateTime(2020, 04, 23, 0, 0, 0, DateTimeKind.Unspecified).ToString(ci)}\"\r\n\"{DateTime.MinValue.ToString(ci)}\""
                            )
                        )
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            var shouldMatch =
                                new[]
                                {
                                    DateTime.Parse(DateTime.MaxValue.ToString(ci)),
                                    DateTime.Parse(new DateTime(2020, 04, 23, 0, 0, 0, DateTimeKind.Unspecified).ToString(ci)),
                                    DateTime.Parse(DateTime.MinValue.ToString(ci)),
                                };
                            Assert.True(shouldMatch.SequenceEqual(rows));
                        }
                    }
                );
            }

            // DateTime?
            {
                await RunAsyncReaderVariants<DateTime?>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        var ci = CultureInfo.InvariantCulture;

                        await using (var reader =
                            await getReader(
                                $"\"{DateTime.MaxValue.ToString(ci)}\"\r\n\r\n\"{DateTime.MinValue.ToString(ci)}\""
                            )
                        )
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            var shouldMatch =
                                new DateTime?[]
                                {
                                    DateTime.Parse(DateTime.MaxValue.ToString(ci)),
                                    null,
                                    DateTime.Parse(DateTime.MinValue.ToString(ci)),
                                };
                            Assert.True(shouldMatch.SequenceEqual(rows));
                        }
                    }
                );
            }

            // DateTimeOffset
            {
                await RunAsyncReaderVariants<DateTimeOffset>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        var ci = CultureInfo.InvariantCulture;

                        await using (var reader =
                            await getReader(
                                $"\"{DateTimeOffset.MaxValue.ToString(ci)}\"\r\n\"{new DateTimeOffset(2020, 04, 23, 0, 0, 0, TimeSpan.Zero).ToString(ci)}\"\r\n\"{DateTimeOffset.MinValue.ToString(ci)}\""
                            )
                        )
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            var shouldMatch =
                                new[]
                                {
                                    DateTimeOffset.Parse(DateTimeOffset.MaxValue.ToString(ci)),
                                    DateTimeOffset.Parse(new DateTimeOffset(2020, 04, 23, 0, 0, 0, TimeSpan.Zero).ToString(ci)),
                                    DateTimeOffset.Parse(DateTimeOffset.MinValue.ToString(ci)),
                                };
                            Assert.True(shouldMatch.SequenceEqual(rows));
                        }
                    }
                );
            }

            // DateTimeOffset?
            {
                await RunAsyncReaderVariants<DateTimeOffset?>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        var ci = CultureInfo.InvariantCulture;

                        await using (var reader =
                            await getReader(
                                $"\"{DateTimeOffset.MaxValue.ToString(ci)}\"\r\n\r\n\"{DateTimeOffset.MinValue.ToString(ci)}\""
                            )
                        )
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            var shouldMatch =
                                new DateTimeOffset?[]
                                {
                                    DateTimeOffset.Parse(DateTimeOffset.MaxValue.ToString(ci)),
                                    null,
                                    DateTimeOffset.Parse(DateTimeOffset.MinValue.ToString(ci)),
                                };
                            Assert.True(shouldMatch.SequenceEqual(rows));
                        }
                    }
                );
            }

            // Guid
            {
                await RunAsyncReaderVariants<Guid>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader($"2E9348A1-C3D9-4A9C-95FF-D97591F91542\r\nECB04C56-3042-4234-B757-6AC6E53E10C2"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new[] { Guid.Parse("2E9348A1-C3D9-4A9C-95FF-D97591F91542"), Guid.Parse("ECB04C56-3042-4234-B757-6AC6E53E10C2") }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // Guid?
            {
                await RunAsyncReaderVariants<Guid?>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader($"2E9348A1-C3D9-4A9C-95FF-D97591F91542\r\n\r\n"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new Guid?[] { Guid.Parse("2E9348A1-C3D9-4A9C-95FF-D97591F91542"), null }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // TimeSpan
            {
                await RunAsyncReaderVariants<TimeSpan>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader($"\"{TimeSpan.MaxValue}\"\r\n\"{TimeSpan.FromMilliseconds(123456)}\"\r\n\"{TimeSpan.MaxValue}\""))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new[] { TimeSpan.MaxValue, TimeSpan.FromMilliseconds(123456), TimeSpan.MaxValue }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // TimeSpan?
            {
                await RunAsyncReaderVariants<TimeSpan?>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader($"\"{TimeSpan.MaxValue}\"\r\n\r\n\"{TimeSpan.MaxValue}\""))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new TimeSpan?[] { TimeSpan.MaxValue, null, TimeSpan.MaxValue }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // Index
            {
                await RunAsyncReaderVariants<Index>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader($"{^1}\r\n{(Index)2}\r\n{^3}"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new[] { ^1, (Index)2, ^3 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // Index?
            {
                await RunAsyncReaderVariants<Index?>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader($"{^1}\r\n\r\n{^3}"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new Index?[] { ^1, null, ^3 }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // Range
            {
                await RunAsyncReaderVariants<Range>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader($"{1..^1}\r\n{..^2}\r\n{^3..}"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new[] { 1..^1, ..^2, ^3.. }.SequenceEqual(rows));
                        }
                    }
                );
            }

            // Range?
            {
                await RunAsyncReaderVariants<Range?>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader($"{1..^1}\r\n\r\n{^3..}"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.True(new Range?[] { 1..^1, null, ^3.. }.SequenceEqual(rows));
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task ByRefSetterAsync()
        {
            var t = typeof(_ByRefSetter).GetTypeInfo();
            var byMethod = Setter.ForMethod(typeof(ReaderTests).GetMethod(nameof(_ByRefSetterStaticMethod), BindingFlags.Static | BindingFlags.NonPublic));
            var byKnownDelegate = Setter.ForDelegate((ref _ByRefSetter row, int b, in ReadContext ctx) => { row.B = b * 3; });

            _ByRefSetterDelegate otherDel = (ref _ByRefSetter row, int c, in ReadContext ctx) => { row.C = c * 4; };
            var byOtherDelegate = (Setter)otherDel;

            var m = ManualTypeDescriber.CreateBuilder(ManualTypeDescriberFallbackBehavior.UseFallback, TypeDescribers.Default);
            m.WithExplicitSetter(t, "A", byMethod);
            m.WithExplicitSetter(t, "B", byKnownDelegate);
            m.WithExplicitSetter(t, "C", byOtherDelegate);

            var td = m.ToManualTypeDescriber();

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(td).ToOptions();

            await RunAsyncReaderVariants<_ByRefSetter>(
                opts,
                async (config, getReader) =>
                {
                    await using (var reader = await getReader("A,B,C\r\n1,2,3\r\n4,5,6\r\n7,8,9"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = await csv.ReadAllAsync();

                        Assert.Collection(
                            rows,
                            a =>
                            {
                                Assert.Equal(1 * 2, a.A);
                                Assert.Equal(2 * 3, a.B);
                                Assert.Equal(3 * 4, a.C);
                            },
                            b =>
                            {
                                Assert.Equal(4 * 2, b.A);
                                Assert.Equal(5 * 3, b.B);
                                Assert.Equal(6 * 4, b.C);
                            },
                            c =>
                            {
                                Assert.Equal(7 * 2, c.A);
                                Assert.Equal(8 * 3, c.B);
                                Assert.Equal(9 * 4, c.C);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task ThrowOnExcessColumnsAsync()
        {
            var opts = Options.CreateBuilder(Options.Default).WithExtraColumnTreatment(ExtraColumnTreatment.ThrowException).ToOptions();

            // with headers
            {
                // fine, shouldn't throw
                await RunAsyncReaderVariants<_ThrowOnExcessColumns>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("A,B\r\nhello,world\r\n"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("hello", a.A); Assert.Equal("world", a.B); }
                            );
                        }
                    }
                );

                // should throw on second read
                await RunAsyncReaderVariants<_ThrowOnExcessColumns>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("A,B\r\nhello,world\r\nfizz,buzz,bazz"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res = await csv.TryReadAsync();
                            Assert.True(res.HasValue);
                            var row = res.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal("world", row.B);

                            await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.TryReadAsync());
                        }
                    }
                );
            }

            // no headers
            {
                var noHeadersOpts = Options.CreateBuilder(opts).WithReadHeader(ReadHeader.Never).ToOptions();

                // fine, shouldn't throw
                await RunAsyncReaderVariants<_ThrowOnExcessColumns>(
                    noHeadersOpts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("hello,world\r\n"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("hello", a.A); Assert.Equal("world", a.B); }
                            );
                        }
                    }
                );

                // should throw on second read
                await RunAsyncReaderVariants<_ThrowOnExcessColumns>(
                    noHeadersOpts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("hello,world\r\nfizz,buzz,bazz"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res = await csv.TryReadAsync();
                            Assert.True(res.HasValue);
                            var row = res.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal("world", row.B);

                            await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.TryReadAsync());
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task IgnoreExcessColumnsAsync()
        {
            // with headers
            await RunAsyncReaderVariants<_IgnoreExcessColumns>(
                Options.Default,
                async (config, getReader) =>
                {
                    await using (var reader = await getReader("A,B\r\nhello,world\r\nfizz,buzz,bazz\r\nfe,fi,fo,fum"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = await csv.ReadAllAsync();

                        Assert.Collection(
                            rows,
                            a => { Assert.Equal("hello", a.A); Assert.Equal("world", a.B); },
                            a => { Assert.Equal("fizz", a.A); Assert.Equal("buzz", a.B); },
                            a => { Assert.Equal("fe", a.A); Assert.Equal("fi", a.B); }
                        );
                    }
                }
            );

            // without headers
            var noHeadersOpts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();
            await RunAsyncReaderVariants<_IgnoreExcessColumns>(
                noHeadersOpts,
                async (config, getReader) =>
                {
                    await using (var reader = await getReader("hello,world\r\nfizz,buzz,bazz\r\nfe,fi,fo,fum"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = await csv.ReadAllAsync();

                        Assert.Collection(
                            rows,
                            a => { Assert.Equal("hello", a.A); Assert.Equal("world", a.B); },
                            a => { Assert.Equal("fizz", a.A); Assert.Equal("buzz", a.B); },
                            a => { Assert.Equal("fe", a.A); Assert.Equal("fi", a.B); }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task VariousResetsAsync()
        {
            var t = typeof(_VariousResets).GetTypeInfo();
            var cons = t.GetConstructors().Single();

            var a = t.GetPropertyNonNull(nameof(_VariousResets.A), BindingFlags.Public | BindingFlags.Instance);
            var aReset = t.GetMethodNonNull(nameof(_VariousResets.ResetA_Row_Context), BindingFlags.Public | BindingFlags.Static);
            var b = t.GetPropertyNonNull(nameof(_VariousResets.B), BindingFlags.Public | BindingFlags.Instance);
            var bReset = t.GetMethodNonNull(nameof(_VariousResets.ResetB_NoRow_Context), BindingFlags.Public | BindingFlags.Static);
            var c = t.GetPropertyNonNull(nameof(_VariousResets.C), BindingFlags.Public | BindingFlags.Instance);
            var cReset = t.GetMethodNonNull(nameof(_VariousResets.ResetC_Context), BindingFlags.Public | BindingFlags.Instance);
            var d = t.GetPropertyNonNull(nameof(_VariousResets.D), BindingFlags.Public | BindingFlags.Instance);
            var dReset = t.GetMethodNonNull(nameof(_VariousResets.ResetD_Row_ByRef), BindingFlags.Public | BindingFlags.Static);

            var m = ManualTypeDescriber.CreateBuilder();
            m.WithInstanceProvider(InstanceProvider.ForParameterlessConstructor(cons));
            m.WithExplicitSetter(t, "A", Setter.ForProperty(a), Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.No, Reset.ForMethod(aReset));
            m.WithExplicitSetter(t, "B", Setter.ForProperty(b), Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.No, Reset.ForMethod(bReset));
            m.WithExplicitSetter(t, "C", Setter.ForProperty(c), Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.No, Reset.ForMethod(cReset));

            var td = m.ToManualTypeDescriber();
            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(td).ToOptions();

            await RunAsyncReaderVariants<_VariousResets>(
                opts,
                async (config, getReader) =>
                {
                    _VariousResets._B = 123;

                    await using (var reader = await getReader("A,B,C\r\n4,5,6"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = await csv.ReadAllAsync();

                        Assert.Collection(
                            rows,
                            r =>
                            {
                                Assert.Equal(12, r.A);
                                Assert.Equal(9, r.B);
                                Assert.Equal(10, r.C);
                            }
                        );
                    }
                }
            );

            // now with D
            m.WithExplicitSetter(t, "D", Setter.ForProperty(d), Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.No, Reset.ForMethod(dReset));
            var td2 = m.ToManualTypeDescriber();
            var opts2 = Options.CreateBuilder(Options.Default).WithTypeDescriber(td2).ToOptions();

            await RunAsyncReaderVariants<_VariousResets>(
                opts2,
                async (config, getReader) =>
                {
                    _VariousResets._B = 8675;

                    await using (var reader = await getReader("A,B,C,D\r\n4,5,6,7"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var pre = new _VariousResets();
                        var oldPre = pre;
                        var res = await csv.TryReadWithReuseAsync(ref pre);

                        Assert.True(res.HasValue);
                        var val = res.Value;

                        Assert.NotSame(oldPre, val);
                        Assert.Equal(0, val.A);
                        Assert.Equal(9, val.B);
                        Assert.Equal(0, val.C);
                        Assert.Equal(13, val.D);
                    }
                }
            );
        }

        [Fact]
        public async Task VariousSettersAsync()
        {
            var t = typeof(_VariousSetters).GetTypeInfo();
            var cons = t.GetConstructors().Single();

            var m = ManualTypeDescriber.CreateBuilder();
            m.WithInstanceProvider(InstanceProvider.ForConstructorWithParameters(cons));
            m.WithExplicitSetter(t, "A", Setter.ForConstructorParameter(cons.GetParameters().Single()), Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes);
            m.WithExplicitSetter(t, "B", Setter.ForMethod(t.GetMethod(nameof(_VariousSetters.StaticSetter_Row_NoContext))));
            m.WithExplicitSetter(t, "C", Setter.ForMethod(t.GetMethod(nameof(_VariousSetters.StaticSetter_Row_Context))));
            m.WithExplicitSetter(t, "D", Setter.ForMethod(t.GetMethod(nameof(_VariousSetters.StaticSetter_NoRow_Context))));

            var td = m.ToManualTypeDescriber();
            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(td).ToOptions();

            await RunAsyncReaderVariants<_VariousSetters>(
                opts,
                async (config, getReader) =>
                {
                    _VariousSetters.D = 0;

                    await using (var reader = await getReader("A,B,C,D\r\n1,foo,2,3"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = await csv.ReadAllAsync();

                        Assert.Collection(
                            rows,
                            r =>
                            {
                                Assert.Equal(-1, r.A);
                                Assert.Equal("foo.foo", r.B);
                                Assert.Equal(3, r.C);
                                Assert.Equal(5, _VariousSetters.D);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task PoisedTryReadWithCommentReuseAsync()
        {
            var setter = Setter.ForDelegate((_PoisonedTryReadWithCommentReuse row, string val, in ReadContext _) => throw new Exception());

            var type = typeof(_PoisonedTryReadWithCommentReuse).GetTypeInfo();
            var cons = type.GetConstructor(Type.EmptyTypes);
            var provider = InstanceProvider.ForParameterlessConstructor(cons);

            var m = ManualTypeDescriberBuilder.CreateBuilder().WithInstanceProvider(provider).WithExplicitSetter(type, "Foo", setter).ToManualTypeDescriber();

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(m).ToOptions();

            await RunAsyncReaderVariants<_PoisonedTryReadWithCommentReuse>(
                opts,
                async (config, getReader) =>
                {
                    await using (var reader = await getReader("Foo\r\nbar"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        await Assert.ThrowsAnyAsync<Exception>(
                            async () =>
                            {
                                _PoisonedTryReadWithCommentReuse row = null;
                                await csv.TryReadWithCommentReuseAsync(ref row);
                            }
                        );

                        var poisonable = csv as PoisonableBase;

                        Assert.Equal(PoisonType.Exception, poisonable.Poison.Value);
                    }
                }
            );
        }

        [Fact]
        public async Task ChainedParsersAsync()
        {
            var p0 =
                Parser.ForDelegate(
                    (ReadOnlySpan<char> data, in ReadContext ctx, out int val) =>
                    {
                        var a = (_ChainedParsers_Context)ctx.Context;

                        if (a.Num != 1)
                        {
                            val = default;
                            return false;
                        }

                        val = int.Parse(data);
                        val *= 2;

                        return true;
                    }
                );

            var p1 =
                Parser.ForDelegate(
                    (ReadOnlySpan<char> data, in ReadContext ctx, out int val) =>
                    {
                        var a = (_ChainedParsers_Context)ctx.Context;

                        if (a.Num != 2)
                        {
                            val = default;
                            return false;
                        }

                        val = int.Parse(data);
                        val--;

                        return true;
                    }
                );

            var p2 =
                Parser.ForDelegate(
                    (ReadOnlySpan<char> data, in ReadContext ctx, out int val) =>
                    {
                        var a = (_ChainedParsers_Context)ctx.Context;

                        if (a.Num != 3)
                        {
                            val = default;
                            return false;
                        }

                        val = int.Parse(data);
                        val = -(val << 3);

                        return true;
                    }
                );

            var p = p0.Else(p1).Else(p2);

            var i = InstanceProvider.ForParameterlessConstructor(typeof(_ChainedParsers).GetConstructor(Type.EmptyTypes));

            var m = ManualTypeDescriber.CreateBuilder();
            m.WithInstanceProvider(i);
            m.WithExplicitSetter(
                typeof(_ChainedParsers).GetTypeInfo(),
                nameof(_ChainedParsers.Foo),
                Setter.ForMethod(typeof(_ChainedParsers).GetProperty(nameof(_ChainedParsers.Foo)).SetMethod),
                p
            );

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(m.ToManualTypeDescriber()).ToOptions();

            await RunAsyncReaderVariants<_ChainedParsers>(
                opts,
                async (config, getReader) =>
                {
                    var ctx = new _ChainedParsers_Context();

                    await using (var reader = await getReader("Foo\r\n1\r\n2\r\n3\r\n4"))
                    await using (var csv = config.CreateAsyncReader(reader, ctx))
                    {
                        ctx.Num = 1;
                        var res1 = await csv.TryReadAsync();
                        Assert.True(res1.HasValue);
                        var r1 = res1.Value;
                        Assert.Equal(2, r1.Foo);

                        ctx.Num = 2;
                        var res2 = await csv.TryReadAsync();
                        Assert.True(res2.HasValue);
                        var r2 = res2.Value;
                        Assert.Equal(1, r2.Foo);

                        ctx.Num = 3;
                        var res3 = await csv.TryReadAsync();
                        Assert.True(res3.HasValue);
                        var r3 = res3.Value;
                        Assert.Equal(-(3 << 3), r3.Foo);

                        ctx.Num = 4;
                        await AssertThrowsInnerAsync<SerializationException>(async () => await csv.TryReadAsync());
                    }
                }
            );

            static async ValueTask AssertThrowsInnerAsync<TException>(Func<ValueTask> func)
                where TException : Exception
            {
                try
                {
                    await func();
                }
                catch (Exception e)
                {
                    if (e is AggregateException)
                    {
                        Assert.IsType<TException>(e.InnerException);
                    }
                    else
                    {
                        Assert.IsType<TException>(e);
                    }
                }
            }
        }

        [Fact]
        public async Task ChainedInstanceProvidersAsync()
        {
            var num = 0;

            var i1 =
                InstanceProvider.ForDelegate(
                    (in ReadContext _, out _ChainedInstanceProviders res) =>
                    {
                        if (num == 1)
                        {
                            res = new _ChainedInstanceProviders(100);
                            return true;
                        }

                        res = null;
                        return false;
                    }
                );
            var i2 =
                InstanceProvider.ForDelegate(
                    (in ReadContext _, out _ChainedInstanceProviders res) =>
                    {
                        if (num == 2)
                        {
                            res = new _ChainedInstanceProviders(123);
                            return true;
                        }

                        res = null;
                        return false;
                    }
                );
            var i3 =
                InstanceProvider.ForDelegate(
                    (in ReadContext _, out _ChainedInstanceProviders res) =>
                    {
                        if (num == 3)
                        {
                            res = new _ChainedInstanceProviders(999);
                            return true;
                        }

                        res = null;
                        return false;
                    }
                );

            var i = i1.Else(i2).Else(i3);

            var m = ManualTypeDescriber.CreateBuilder();
            m.WithInstanceProvider(i);
            m.WithExplicitSetter(
                typeof(_ChainedInstanceProviders).GetTypeInfo(),
                nameof(_ChainedInstanceProviders.Foo),
                Setter.ForMethod(typeof(_ChainedInstanceProviders).GetProperty(nameof(_ChainedInstanceProviders.Foo)).SetMethod)
            );

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(m.ToManualTypeDescriber()).ToOptions();

            await RunAsyncReaderVariants<_ChainedInstanceProviders>(
                opts,
                async (config, getReader) =>
                {
                    await using (var reader = await getReader("Foo\r\nabc\r\n123\r\neasy\r\nhard"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        num = 1;
                        var r1Res = await csv.TryReadAsync();
                        Assert.True(r1Res.HasValue);
                        var r1 = r1Res.Value;
                        Assert.Equal(100, r1.Cons);
                        Assert.Equal("abc", r1.Foo);

                        num = 3;
                        var r2Res = await csv.TryReadAsync();
                        Assert.True(r2Res.HasValue);
                        var r2 = r2Res.Value;
                        Assert.Equal(999, r2.Cons);
                        Assert.Equal("123", r2.Foo);

                        num = 2;
                        var r3Res = await csv.TryReadAsync();
                        Assert.True(r3Res.HasValue);
                        var r3 = r3Res.Value;
                        Assert.Equal(123, r3.Cons);
                        Assert.Equal("easy", r3.Foo);

                        num = -1;
                        await AssertThrowsInnerAsync<InvalidOperationException>(async () => await csv.TryReadAsync());
                    }
                }
            );

            static async ValueTask AssertThrowsInnerAsync<TException>(Func<ValueTask> func)
                where TException : Exception
            {
                try
                {
                    await func();
                }
                catch (Exception e)
                {
                    if (e is AggregateException)
                    {
                        Assert.IsType<TException>(e.InnerException);
                    }
                    else
                    {
                        Assert.IsType<TException>(e);
                    }
                }
            }
        }

        [Fact]
        public async Task WhitespaceTrimmingAsync()
        {
            // in values
            {
                // leading
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWhitespaceTreatment(WhitespaceTreatments.TrimLeadingInValues).ToOptions();

                    await RunAsyncReaderVariants<_WhitespaceTrimming>(
                        opts,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("  Foo,  Bar\r\nhello,123\r\n   world,\t456\r\n\"\t \nfizz\",\"\t\t\t789\""))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.Foo);
                                        Assert.Equal(123, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", a.Foo);
                                        Assert.Equal(456, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("fizz", a.Foo);
                                        Assert.Equal(789, a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }

                // trailing
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWhitespaceTreatment(WhitespaceTreatments.TrimTrailingInValues).ToOptions();

                    await RunAsyncReaderVariants<_WhitespaceTrimming>(
                        opts,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("Foo   ,Bar   \r\nhello,123\r\nworld   ,456\t\r\n\"fizz\t \n\",\"789\t\t\t\""))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.Foo);
                                        Assert.Equal(123, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", a.Foo);
                                        Assert.Equal(456, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("fizz", a.Foo);
                                        Assert.Equal(789, a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }

                // both
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWhitespaceTreatment(WhitespaceTreatments.TrimInValues).ToOptions();

                    await RunAsyncReaderVariants<_WhitespaceTrimming>(
                        opts,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("   Foo   ,   Bar   \r\nhello,123\r\n\tworld   ,   456\t\r\n\"\tfizz\t \n\",\"\t789\t\t\t\""))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.Foo);
                                        Assert.Equal(123, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", a.Foo);
                                        Assert.Equal(456, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("fizz", a.Foo);
                                        Assert.Equal(789, a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }
            }

            // outside of values
            {
                // leading
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWhitespaceTreatment(WhitespaceTreatments.TrimBeforeValues).ToOptions();

                    await RunAsyncReaderVariants<_WhitespaceTrimming>(
                        opts,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("  Foo,  Bar\r\nhello,123\r\n   world,\t456\r\n\"\t \nfizz\", \t \"789\""))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.Foo);
                                        Assert.Equal(123, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", a.Foo);
                                        Assert.Equal(456, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("\t \nfizz", a.Foo);
                                        Assert.Equal(789, a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }

                // trailing
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWhitespaceTreatment(WhitespaceTreatments.TrimAfterValues).ToOptions();

                    await RunAsyncReaderVariants<_WhitespaceTrimming>(
                        opts,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("Foo  ,\"Bar\"  \r\nhello,123\r\nworld   ,456\t\r\n\"fizz\t \n\",\"789\" \t "))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.Foo);
                                        Assert.Equal(123, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", a.Foo);
                                        Assert.Equal(456, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("fizz\t \n", a.Foo);
                                        Assert.Equal(789, a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }

                // leading
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWhitespaceTreatment(WhitespaceTreatments.TrimBeforeValues).ToOptions();

                    await RunAsyncReaderVariants<_WhitespaceTrimming>(
                        opts,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("  Foo,  Bar\r\nhello,123\r\n   world,\t456\r\n\"\t \nfizz\", \t \"789\""))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.Foo);
                                        Assert.Equal(123, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", a.Foo);
                                        Assert.Equal(456, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("\t \nfizz", a.Foo);
                                        Assert.Equal(789, a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }

                // both
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWhitespaceTreatment(WhitespaceTreatments.TrimBeforeValues | WhitespaceTreatments.TrimAfterValues).ToOptions();

                    await RunAsyncReaderVariants<_WhitespaceTrimming>(
                        opts,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("  Foo  ,\t\"Bar\"  \r\nhello,123\r\n\t world   ,456\t\r\n  \"fizz\t \n\",  \"789\" \t "))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", a.Foo);
                                        Assert.Equal(123, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", a.Foo);
                                        Assert.Equal(456, a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("fizz\t \n", a.Foo);
                                        Assert.Equal(789, a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }
            }

            // inside and outside of values
            {
                var opts = Options.CreateBuilder(Options.Default).WithWhitespaceTreatment(WhitespaceTreatments.Trim).ToOptions();

                await RunAsyncReaderVariants<_WhitespaceTrimming>(
                    opts,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader("  \"  Foo  \"  ,\t\t\"\tBar\t\"  \r\nhello,123\r\n\t world   ,456\t\r\n  \"fizz\t \n\",  \"  789\r\n\" \t "))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(
                                rows,
                                a =>
                                {
                                    Assert.Equal("hello", a.Foo);
                                    Assert.Equal(123, a.Bar);
                                },
                                a =>
                                {
                                    Assert.Equal("world", a.Foo);
                                    Assert.Equal(456, a.Bar);
                                },
                                a =>
                                {
                                    Assert.Equal("fizz", a.Foo);
                                    Assert.Equal(789, a.Bar);
                                }
                            );
                        }
                    }
                );
            }

            // none
            {
                // no changes in values
                await RunAsyncReaderVariants<_WhitespaceTrimming>(
                    Options.Default,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader("Foo,\"Bar\"\r\nhello\t,123\r\n  world,456\r\n\"\r\nfizz\",\"789\""))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(
                                rows,
                                a =>
                                {
                                    Assert.Equal("hello\t", a.Foo);
                                    Assert.Equal(123, a.Bar);
                                },
                                a =>
                                {
                                    Assert.Equal("  world", a.Foo);
                                    Assert.Equal(456, a.Bar);
                                },
                                a =>
                                {
                                    Assert.Equal("\r\nfizz", a.Foo);
                                    Assert.Equal(789, a.Bar);
                                }
                            );
                        }
                    }
                );

                // bad headers
                {
                    // leading value
                    await RunAsyncReaderVariants<_WhitespaceTrimming>(
                        Options.Default,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("  Foo,\"Bar\"\r\nfoo,123"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();

                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        // Foo is smashed
                                        Assert.Null(a.Foo);
                                        // Bar is fine
                                        Assert.Equal(123, a.Bar);
                                    }
                                );
                            }
                        }
                    );

                    // trailing value
                    await RunAsyncReaderVariants<_WhitespaceTrimming>(
                        Options.Default,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("Foo\t,\"Bar\"\r\nfoo,123"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();

                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        // Foo is smashed
                                        Assert.Null(a.Foo);
                                        // Bar is fine
                                        Assert.Equal(123, a.Bar);
                                    }
                                );
                            }
                        }
                    );

                    // leading value, escaped
                    await RunAsyncReaderVariants<_WhitespaceTrimming>(
                        Options.Default,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("Foo,\"  Bar\"\r\nfoo,123"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();

                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        // Foo is fine
                                        Assert.Equal("foo", a.Foo);
                                        // Bar is smashed
                                        Assert.Equal(0, a.Bar);
                                    }
                                );
                            }
                        }
                    );

                    // leading value, escaped, exceptional
                    await RunAsyncReaderVariants<_WhitespaceTrimming>(
                        Options.Default,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("Foo,\t\"  Bar\"\r\nfoo,123"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                await AssertThrowsInnerAsync<InvalidOperationException>(async () => await csv.ReadAllAsync());
                            }
                        }
                    );

                    // trailing value, escaped
                    await RunAsyncReaderVariants<_WhitespaceTrimming>(
                        Options.Default,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("Foo,\"Bar\r\n\"\r\nfoo,123"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();

                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        // Foo is fine
                                        Assert.Equal("foo", a.Foo);
                                        // Bar is smashed
                                        Assert.Equal(0, a.Bar);
                                    }
                                );
                            }
                        }
                    );

                    // trailing value, escaped, exceptional
                    await RunAsyncReaderVariants<_WhitespaceTrimming>(
                        Options.Default,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("Foo,\"Bar\r\n\"\t\t\r\nfoo,123"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                await AssertThrowsInnerAsync<InvalidOperationException>(async () => await csv.ReadAllAsync());
                            }
                        }
                    );
                }
            }

            static async ValueTask AssertThrowsInnerAsync<TException>(Func<ValueTask> func)
                where TException : Exception
            {
                try
                {
                    await func();
                }
                catch (Exception e)
                {
                    if (e is AggregateException)
                    {
                        Assert.IsType<TException>(e.InnerException);
                    }
                    else
                    {
                        Assert.IsType<TException>(e);
                    }
                }
            }
        }

        [Fact]
        public async Task MissingHeadersAsync()
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions();

            await RunAsyncReaderVariants<_MissingHeaders>(
                opts,
                async (config, getReader) =>
                {
                    await using (var reader = await getReader("fizz"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var exc = await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.TryReadAsync());

                        Assert.Equal("First row of input was not a row of headers", exc.Message);
                    }
                }
            );
        }

        [Fact]
        public async Task ReadAllEmptyAsync()
        {
            await RunAsyncReaderVariants<_ReadAllEmpty>(
                Options.Default,
                async (config, getReader) =>
                {
                    await using (var reader = await getReader(""))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var res = await csv.ReadAllAsync();

                        Assert.Empty(res);
                    }
                }
            );
        }

        [Fact]
        public async Task ReadOneThenAllAsync()
        {
            await RunAsyncReaderVariants<_ReadOneThenAll>(
                Options.Default,
                async (config, getReader) =>
                {
                    await using (var reader = await getReader("Foo\r\nbar\r\nfizz\r\nbuzz"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = new List<_ReadOneThenAll>();

                        var res = await csv.TryReadAsync();
                        Assert.True(res.HasValue);
                        rows.Add(res.Value);

                        await csv.ReadAllAsync(rows);

                        Assert.Collection(
                            rows,
                            a => Assert.Equal("bar", a.Foo),
                            b => Assert.Equal("fizz", b.Foo),
                            c => Assert.Equal("buzz", c.Foo)
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task NullIntoAsync()
        {
            await RunAsyncReaderVariants<_NullInto>(
                Options.Default,
                async (config, getReader) =>
                {
                    await using (var reader = await getReader(""))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        await Assert.ThrowsAsync<ArgumentNullException>(async () => await csv.ReadAllAsync(default(List<_NullInto>)));
                    }
                },
                cancellable: false
            );
        }

        [Fact]
        public async Task UncommonAdvanceResultsAsync()
        {
            {
                var opts = Options.CreateBuilder(Options.Default).WithRowEnding(RowEnding.CarriageReturnLineFeed).WithEscapedValueStartAndEnd('\\').ToOptions();

                // escape char after \r
                await RunAsyncReaderVariants<_UncommonAdvanceResults>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("hello\r\\"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var exc = await UnwrapThrowsAsync<InvalidOperationException>(async () => await csv.TryReadAsync());

                            Assert.Equal("Encountered '\\' when expecting end of record", exc.Message);
                        }
                    }
                );
            }

            {
                var opts = Options.CreateBuilder(Options.Default).WithRowEnding(RowEnding.CarriageReturnLineFeed).WithEscapedValueStartAndEnd('\\').WithReadHeader(ReadHeader.Never).ToOptions();

                // kept reading after things were busted
                await RunAsyncReaderVariants<_UncommonAdvanceResults>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("hello\r\\ " + string.Join(" ", Enumerable.Repeat('c', 1000))))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            await Assert.ThrowsAnyAsync<Exception>(async () => await csv.TryReadAsync());

                            await UnwrapThrowsAsync<InvalidOperationException>(async () => await csv.TryReadAsync());
                        }
                    }
                );
            }

            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

                // kept reading after things were busted
                await RunAsyncReaderVariants<_UncommonAdvanceResults>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("hel\"lo"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var exc = await UnwrapThrowsAsync<InvalidOperationException>(async () => await csv.TryReadAsync());
                            Assert.Equal("Encountered '\"', starting an escaped value, when already in a value", exc.Message);
                        }
                    }
                );
            }

            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

                // kept reading after things were busted
                await RunAsyncReaderVariants<_UncommonAdvanceResults>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("\"hel\"lo\""))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var exc = await UnwrapThrowsAsync<InvalidOperationException>(async () => await csv.TryReadAsync());
                            Assert.Equal("Encountered 'l' in an escape sequence, which is invalid", exc.Message);
                        }
                    }
                );
            }

            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions();

                // kept reading after things were busted
                await RunAsyncReaderVariants<_UncommonAdvanceResults>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("\"A"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var exc = await UnwrapThrowsAsync<InvalidOperationException>(async () => await csv.TryReadAsync());
                            Assert.Equal("Data ended unexpectedly", exc.Message);
                        }
                    }
                );
            }


            async static ValueTask<T> UnwrapThrowsAsync<T>(Func<Task> get)
                where T : Exception
            {
                var res = await Assert.ThrowsAnyAsync<Exception>(get);

                if (res is AggregateException agg)
                {
                    var exc = res.InnerException as T;
                    Assert.NotNull(exc);

                    return exc;
                }

                var sync = res as T;
                Assert.NotNull(sync);

                return sync;
            }
        }

        [Fact]
        public async Task ResultErrorsAsync()
        {
            // without comments
            {
                await RunAsyncReaderVariants<_ResultsErrors>(
                    Options.Default,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader("hello"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var resValue = await csv.TryReadAsync();
                            Assert.True(resValue.HasValue);
                            Assert.Equal("hello", resValue.Value.Foo);
                            var resValueStr = resValue.ToString();
                            Assert.NotNull(resValueStr);
                            Assert.NotEqual(-1, resValueStr.IndexOf(resValue.Value.ToString()));

                            var resNone = await csv.TryReadAsync();
                            Assert.False(resNone.HasValue);
                            Assert.NotNull(resNone.ToString());
                            Assert.Throws<InvalidOperationException>(() => resNone.Value);
                        }
                    }
                );
            }

            // with comments
            {
                var withComments = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').ToOptions();

                await RunAsyncReaderVariants<_ResultsErrors>(
                    withComments,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader("hello\r\n#foo"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var resValue = await csv.TryReadWithCommentAsync();
                            Assert.True(resValue.HasValue);
                            Assert.False(resValue.HasComment);
                            Assert.Equal(ReadWithCommentResultType.HasValue, resValue.ResultType);
                            Assert.Equal("hello", resValue.Value.Foo);
                            var resValueStr = resValue.ToString();
                            Assert.NotNull(resValueStr);
                            Assert.NotEqual(-1, resValueStr.IndexOf(resValue.Value.ToString()));
                            Assert.Throws<InvalidOperationException>(() => resValue.Comment);

                            var resComment = await csv.TryReadWithCommentAsync();
                            Assert.False(resComment.HasValue);
                            Assert.True(resComment.HasComment);
                            Assert.Equal(ReadWithCommentResultType.HasComment, resComment.ResultType);
                            Assert.Equal("foo", resComment.Comment);
                            var resCommentStr = resComment.ToString();
                            Assert.NotNull(resCommentStr);
                            Assert.NotEqual(-1, resCommentStr.IndexOf("foo"));
                            Assert.Throws<InvalidOperationException>(() => resComment.Value);

                            var resNone = await csv.TryReadWithCommentAsync();
                            Assert.False(resNone.HasValue);
                            Assert.False(resNone.HasComment);
                            Assert.Equal(ReadWithCommentResultType.NoValue, resNone.ResultType);
                            Assert.NotNull(resNone.ToString());
                            Assert.Throws<InvalidOperationException>(() => resNone.Comment);
                            Assert.Throws<InvalidOperationException>(() => resNone.Value);
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task CommentEndingInCarriageReturnAsync()
        {
            var opt = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

            await RunAsyncReaderVariants<_CommentEndingInCarriageReturn>(
                opt,
                async (config, getReader) =>
                {
                    await using (var reader = await getReader("#\r"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var res = await csv.TryReadWithCommentAsync();
                        Assert.True(res.HasComment);
                        Assert.Equal("\r", res.Comment);

                        res = await csv.TryReadWithCommentAsync();
                        Assert.Equal(ReadWithCommentResultType.NoValue, res.ResultType);
                    }
                }
            );
        }

        private sealed class _PipeReaderAsync
        {
            public string Hello { get; set; }
            public string World { get; set; }
        }

        [Fact]
        public async Task PipeReaderAsync()
        {
            var pipe = new Pipe();

            var txt = Encoding.UTF32.GetBytes("Hello,World\r\nfoo,bar");

            await pipe.Writer.WriteAsync(txt.AsMemory());
            await pipe.Writer.FlushAsync();

            pipe.Writer.Complete();

            var config = Configuration.For<_PipeReaderAsync>();
            await using (var csv = config.CreateAsyncReader(pipe.Reader, Encoding.UTF32))
            {
                var rows = await csv.ReadAllAsync();

                Assert.Collection(
                    rows,
                    a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); }
                );
            }
        }

        [Fact]
        public async Task FailingParserAsync()
        {
            var m = ManualTypeDescriberBuilder.CreateBuilder(ManualTypeDescriberFallbackBehavior.UseFallback);

            m.WithInstanceProvider(InstanceProvider.ForDelegate((in ReadContext _, out _FailingParser val) => { val = new _FailingParser(); return true; }));

            var t = typeof(_FailingParser).GetTypeInfo();
            var s = Setter.ForMethod(t.GetProperty(nameof(_FailingParser.Foo)).SetMethod);
            var p = Parser.ForDelegate((ReadOnlySpan<char> data, in ReadContext ctx, out string result) => { result = ""; return false; });

            m.WithExplicitSetter(t, "Foo", s, p);

            var opt = Options.CreateBuilder(Options.Default).WithTypeDescriber(m.ToManualTypeDescriber()).ToOptions();

            await RunAsyncReaderVariants<_FailingParser>(
                opt,
                async (config, getReader) =>
                {
                    await using (var r = await getReader("hello"))
                    await using (var csv = config.CreateAsyncReader(r))
                    {
                        await Assert.ThrowsAsync<SerializationException>(async () => await csv.ReadAllAsync());
                    }
                }
            );
        }

#if DEBUG
        private sealed class _AsyncEnumerableAsync
        {
            public string Foo { get; set; }
        }

        [Fact]
        public async Task AsyncEnumerableAsync()
        {
            await RunAsyncReaderVariants<_AsyncEnumerableAsync>(
                Options.Default,
                async (config, getReader) =>
                {
                    var testConfig = config as AsyncCountingAndForcingConfig<_AsyncEnumerableAsync>;

                    await using (var reader = await getReader("foo\r\n123\r\nnope"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var e = csv.EnumerateAllAsync();
                        testConfig?.Set(e);

                        var ix = 0;
                        await foreach (var row in e)
                        {
                            switch (ix)
                            {
                                case 0:
                                    Assert.Equal("foo", row.Foo);
                                    break;
                                case 1:
                                    Assert.Equal("123", row.Foo);
                                    break;
                                case 2:
                                    Assert.Equal("nope", row.Foo);
                                    break;
                                default:
                                    Assert.NotNull("Shouldn't be possible");
                                    break;
                            }
                            ix++;
                        }

                        Assert.Equal(3, ix);
                    }
                }
            );

            await RunAsyncReaderVariants<_AsyncEnumerableAsync>(
                Options.Default,
                async (config, getReader) =>
                {
                    var testConfig = config as AsyncCountingAndForcingConfig<_AsyncEnumerableAsync>;

                    await using (var reader = await getReader("foo\r\n123\r\nnope"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var e = csv.EnumerateAllAsync();
                        var i = e.GetAsyncEnumerator();

                        testConfig?.Set(i);

                        var ix = 0;
                        while (await i.MoveNextAsync())
                        {
                            var row = i.Current;
                            switch (ix)
                            {
                                case 0:
                                    Assert.Equal("foo", row.Foo);
                                    break;
                                case 1:
                                    Assert.Equal("123", row.Foo);
                                    break;
                                case 2:
                                    Assert.Equal("nope", row.Foo);
                                    break;
                                default:
                                    Assert.NotNull("Shouldn't be possible");
                                    break;
                            }
                            ix++;
                        }

                        Assert.Equal(3, ix);
                    }
                }
            );
        }
#endif

        [Fact]
        public async Task RowCreationFailureAsync()
        {
            int failAfter = 0;
            int calls = 0;
            InstanceProviderDelegate<_RowCreationFailure> builder =
                (in ReadContext _, out _RowCreationFailure row) =>
                {
                    if (calls >= failAfter)
                    {
                        row = default;
                        return false;
                    }

                    calls++;

                    row = new _RowCreationFailure();
                    return true;
                };


            var typeDesc = ManualTypeDescriberBuilder.CreateBuilder();
            typeDesc.WithDeserializableProperty(typeof(_RowCreationFailure).GetProperty(nameof(_RowCreationFailure.Foo)));
            typeDesc.WithInstanceProvider((InstanceProvider)builder);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(typeDesc.ToManualTypeDescriber()).ToOptions();

            await RunAsyncReaderVariants<_RowCreationFailure>(
                opts,
                async (config, makeReader) =>
                {
                    calls = 0;
                    failAfter = 3;

                    await using (var reader = await makeReader("Foo\r\n1\r\n2\r\n3\r\n4"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var res1 = await csv.TryReadAsync();
                        Assert.True(res1.HasValue);
                        Assert.Equal(1, res1.Value.Foo);

                        var res2 = await csv.TryReadAsync();
                        Assert.True(res2.HasValue);
                        Assert.Equal(2, res2.Value.Foo);

                        var res3 = await csv.TryReadAsync();
                        Assert.True(res3.HasValue);
                        Assert.Equal(3, res3.Value.Foo);

                        await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.TryReadAsync());
                    }
                }
            );
        }


        [Fact]
        public async Task WithCommentsAsync()
        {
            // \r\n
            {
                var opts = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

                // with headers
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("A,Nope\r\n#comment\rwhatever\r\nhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, comment before header
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\rwhatever\r\nA,Nope\r\nhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, no comment
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("A,Nope"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res.ResultType);
                        }
                    }
                );

                // no headers
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\rwhatever\r\nhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // two comments
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\rwhatever\r\n#again!###foo###"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res2.ResultType);
                            Assert.Equal("again!###foo###", res2.Comment);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );
            }

            // \r
            {
                var opts = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithRowEnding(RowEnding.CarriageReturn).ToOptions();

                // with headers
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("A,Nope\r#comment\nwhatever\rhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, comment before header
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\nwhatever\rA,Nope\rhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, no comment
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("A,Nope"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res.ResultType);
                        }
                    }
                );

                // no headers
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\nwhatever\rhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // two comments
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\nwhatever\r#again!###foo###"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res2.ResultType);
                            Assert.Equal("again!###foo###", res2.Comment);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );
            }

            // \n
            {
                var opts = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithRowEnding(RowEnding.LineFeed).ToOptions();

                // with headers
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("A,Nope\n#comment\rwhatever\nhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, comment before header
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\rwhatever\nA,Nope\nhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, no comment
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("A,Nope"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res.ResultType);
                        }
                    }
                );

                // no headers
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\rwhatever\nhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // two comments
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\rwhatever\n#again!###foo###"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res2.ResultType);
                            Assert.Equal("again!###foo###", res2.Comment);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task WeirdCommentsAsync()
        {
            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).WithCommentCharacter('#').WithRowEnding(RowEnding.LineFeed).ToOptions();
                await RunAsyncReaderVariants<_Comment>(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "#this is a test comment!\r\nhello,world\nfoo,bar";
                        await using (var str = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );

                await RunAsyncReaderVariants<_Comment>(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "hello,world\n#this is a test comment!\r\nfoo,bar";
                        await using (var str = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );
            }

            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).WithCommentCharacter('#').WithRowEnding(RowEnding.CarriageReturn).ToOptions();
                await RunAsyncReaderVariants<_Comment>(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "#this is a test comment!\n\rhello,world\rfoo,bar";
                        await using (var str = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );

                await RunAsyncReaderVariants<_Comment>(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "hello,world\r#this is a test comment!\n\rfoo,bar";
                        await using (var str = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );
            }

            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).WithCommentCharacter('#').WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();
                await RunAsyncReaderVariants<_Comment>(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "#this is a test comment!\n\r\nhello,world\r\nfoo,bar";
                        await using (var str = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );

                await RunAsyncReaderVariants<_Comment>(
                   opts,
                   async (config, getReader) =>
                   {
                       var CSV = "#this is a test comment!\r\r\nhello,world\r\nfoo,bar";
                       await using (var str = await getReader(CSV))
                       await using (var csv = config.CreateAsyncReader(str))
                       {
                           var rows = await csv.ReadAllAsync();
                           Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                       }
                   }
               );

                await RunAsyncReaderVariants<_Comment>(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "hello,world\r\n#this is a test comment!\n\r\nfoo,bar";
                        await using (var str = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );

                await RunAsyncReaderVariants<_Comment>(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "hello,world\r\n#this is a test comment!\r\r\nfoo,bar";
                        await using (var str = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task DelegateStaticResetAsync()
        {
            var resetCalled = 0;
            StaticResetDelegate resetDel =
                (in ReadContext _) =>
                {
                    resetCalled++;
                };

            var name = nameof(_DelegateReset.Foo);
            var parser = Parser.GetDefault(typeof(int).GetTypeInfo());
            var setter = Setter.ForMethod(typeof(_DelegateReset).GetProperty(name).SetMethod);
            var reset = Reset.ForDelegate(resetDel);

            var describer = ManualTypeDescriberBuilder.CreateBuilder();
            describer.WithExplicitSetter(typeof(_DelegateReset).GetTypeInfo(), name, setter, parser, MemberRequired.No, reset);
            InstanceProviderDelegate<_DelegateReset> del = (in ReadContext _, out _DelegateReset i) => { i = new _DelegateReset(); return true; };
            describer.WithInstanceProvider((InstanceProvider)del);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).ToOptions();

            await RunAsyncReaderVariants<_DelegateReset>(
                opts,
                async (config, getReader) =>
                {
                    resetCalled = 0;

                    await using (var reader = await getReader("1\r\n23\r\n456\r\n7\r\n"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var row = await csv.ReadAllAsync();

                        Assert.Collection(
                            row,
                            r => Assert.Equal(1, r.Foo),
                            r => Assert.Equal(23, r.Foo),
                            r => Assert.Equal(456, r.Foo),
                            r => Assert.Equal(7, r.Foo)
                        );
                    }

                    Assert.Equal(4, resetCalled);
                }
            );
        }

        [Fact]
        public async Task DelegateResetAsync()
        {
            var resetCalled = 0;
            ResetDelegate<_DelegateReset> resetDel =
                (_DelegateReset row, in ReadContext _) =>
                {
                    resetCalled++;
                };

            var name = nameof(_DelegateReset.Foo);
            var parser = Parser.GetDefault(typeof(int).GetTypeInfo());
            var setter = Setter.ForMethod(typeof(_DelegateReset).GetProperty(name).SetMethod);
            var reset = Reset.ForDelegate(resetDel);

            var describer = ManualTypeDescriberBuilder.CreateBuilder();
            describer.WithExplicitSetter(typeof(_DelegateReset).GetTypeInfo(), name, setter, parser, MemberRequired.No, reset);
            InstanceProviderDelegate<_DelegateReset> del = (in ReadContext _, out _DelegateReset i) => { i = new _DelegateReset(); return true; };
            describer.WithInstanceProvider((InstanceProvider)del);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).ToOptions();

            await RunAsyncReaderVariants<_DelegateReset>(
                opts,
                async (config, getReader) =>
                {
                    resetCalled = 0;

                    await using (var reader = await getReader("1\r\n23\r\n456\r\n7\r\n"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var row = await csv.ReadAllAsync();

                        Assert.Collection(
                            row,
                            r => Assert.Equal(1, r.Foo),
                            r => Assert.Equal(23, r.Foo),
                            r => Assert.Equal(456, r.Foo),
                            r => Assert.Equal(7, r.Foo)
                        );
                    }

                    Assert.Equal(4, resetCalled);
                }
            );
        }

        [Fact]
        public async Task DelegateStaticSetterAsync()
        {
            var setterCalled = 0;

            StaticSetterDelegate<int> parser =
                (int value, in ReadContext _) =>
                {
                    setterCalled++;
                };

            var describer = ManualTypeDescriberBuilder.CreateBuilder();
            describer.WithExplicitSetter(typeof(_DelegateSetter).GetTypeInfo(), "Foo", Setter.ForDelegate(parser));
            InstanceProviderDelegate<_DelegateSetter> del = (in ReadContext _, out _DelegateSetter i) => { i = new _DelegateSetter(); return true; };
            describer.WithInstanceProvider((InstanceProvider)del);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).ToOptions();

            await RunAsyncReaderVariants<_DelegateSetter>(
                opts,
                async (config, getReader) =>
                {
                    setterCalled = 0;

                    await using (var reader = await getReader("1\r\n23\r\n456\r\n7\r\n"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var row = await csv.ReadAllAsync();

                        Assert.Collection(
                            row,
                            r => Assert.Equal(0, r.Foo),
                            r => Assert.Equal(0, r.Foo),
                            r => Assert.Equal(0, r.Foo),
                            r => Assert.Equal(0, r.Foo)
                        );
                    }

                    Assert.Equal(4, setterCalled);
                }
            );
        }

        [Fact]
        public async Task DelegateSetterAsync()
        {
            var setterCalled = 0;

            SetterDelegate<_DelegateSetter, int> parser =
                (_DelegateSetter row, int value, in ReadContext _) =>
                {
                    setterCalled++;

                    row.Foo = value * 2;
                };

            var describer = ManualTypeDescriberBuilder.CreateBuilder();
            describer.WithExplicitSetter(typeof(_DelegateSetter).GetTypeInfo(), "Foo", Setter.ForDelegate(parser));
            InstanceProviderDelegate<_DelegateSetter> del = (in ReadContext _, out _DelegateSetter i) => { i = new _DelegateSetter(); return true; };
            describer.WithInstanceProvider((InstanceProvider)del);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).ToOptions();

            await RunAsyncReaderVariants<_DelegateSetter>(
                opts,
                async (config, getReader) =>
                {
                    setterCalled = 0;

                    await using (var reader = await getReader("1\r\n23\r\n456\r\n7\r\n"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var row = await csv.ReadAllAsync();

                        Assert.Collection(
                            row,
                            r => Assert.Equal(1 * 2, r.Foo),
                            r => Assert.Equal(23 * 2, r.Foo),
                            r => Assert.Equal(456 * 2, r.Foo),
                            r => Assert.Equal(7 * 2, r.Foo)
                        );
                    }

                    Assert.Equal(4, setterCalled);
                }
            );
        }

        [Fact]
        public async Task ConstructorParserAsync()
        {
            var cons1 = typeof(_ConstructorParser).GetConstructor(new[] { typeof(ReadOnlySpan<char>) });
            var cons2 = typeof(_ConstructorParser).GetConstructor(new[] { typeof(ReadOnlySpan<char>), typeof(ReadContext).MakeByRefType() });

            // single param
            {
                var describer = ManualTypeDescriberBuilder.CreateBuilder();
                describer.WithDeserializableProperty(
                    typeof(_ConstructorParser_Outer).GetProperty(nameof(_ConstructorParser_Outer.Foo)),
                    nameof(_ConstructorParser_Outer.Foo),
                    Parser.ForConstructor(cons1)
                );
                InstanceProviderDelegate<_ConstructorParser_Outer> del = (in ReadContext _, out _ConstructorParser_Outer i) => { i = new _ConstructorParser_Outer(); return true; };
                describer.WithInstanceProvider((InstanceProvider)del);

                var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).ToOptions();

                await RunAsyncReaderVariants<_ConstructorParser_Outer>(
                    opts,
                    async (config, getReader) =>
                    {
                        _ConstructorParser.Cons1Called = 0;

                        await using (var reader = await getReader("1\r\n23\r\n456\r\n7\r\n"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var row = await csv.ReadAllAsync();

                            Assert.Collection(
                                row,
                                r => Assert.Equal("1", r.Foo.Value),
                                r => Assert.Equal("23", r.Foo.Value),
                                r => Assert.Equal("456", r.Foo.Value),
                                r => Assert.Equal("7", r.Foo.Value)
                            );
                        }

                        Assert.Equal(4, _ConstructorParser.Cons1Called);
                    }
                );
            }

            // two params
            {
                var describer = ManualTypeDescriberBuilder.CreateBuilder();
                describer.WithDeserializableProperty(
                    typeof(_ConstructorParser_Outer).GetProperty(nameof(_ConstructorParser_Outer.Foo)),
                    nameof(_ConstructorParser_Outer.Foo),
                    Parser.ForConstructor(cons2)
                );
                InstanceProviderDelegate<_ConstructorParser_Outer> del = (in ReadContext _, out _ConstructorParser_Outer i) => { i = new _ConstructorParser_Outer(); return true; };
                describer.WithInstanceProvider((InstanceProvider)del);

                var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).ToOptions();

                await RunAsyncReaderVariants<_ConstructorParser_Outer>(
                    opts,
                    async (config, getReader) =>
                    {
                        _ConstructorParser.Cons2Called = 0;

                        await using (var reader = await getReader("1\r\n23\r\n456\r\n7\r\n"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var row = await csv.ReadAllAsync();

                            Assert.Collection(
                                row,
                                r => Assert.Equal("10", r.Foo.Value),
                                r => Assert.Equal("230", r.Foo.Value),
                                r => Assert.Equal("4560", r.Foo.Value),
                                r => Assert.Equal("70", r.Foo.Value)
                            );
                        }

                        Assert.Equal(4, _ConstructorParser.Cons2Called);
                    }
                );
            }
        }

        [Fact]
        public async Task DelegateParserAsync()
        {
            var parserCalled = 0;

            ParserDelegate<int> parser =
                (ReadOnlySpan<char> data, in ReadContext _, out int res) =>
                {
                    parserCalled++;

                    res = data.Length;
                    return true;
                };

            var describer = ManualTypeDescriberBuilder.CreateBuilder();
            describer.WithDeserializableProperty(
                typeof(_DelegateParser).GetProperty(nameof(_DelegateParser.Foo)),
                nameof(_DelegateParser.Foo),
                Parser.ForDelegate(parser)
            );
            InstanceProviderDelegate<_DelegateParser> del = (in ReadContext _, out _DelegateParser i) => { i = new _DelegateParser(); return true; };
            describer.WithInstanceProvider((InstanceProvider)del);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).ToOptions();

            await RunAsyncReaderVariants<_DelegateParser>(
                opts,
                async (config, getReader) =>
                {
                    parserCalled = 0;

                    await using (var reader = await getReader("1\r\n23\r\n456\r\n7\r\n"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var row = await csv.ReadAllAsync();

                        Assert.Collection(
                            row,
                            r => Assert.Equal(1, r.Foo),
                            r => Assert.Equal(2, r.Foo),
                            r => Assert.Equal(3, r.Foo),
                            r => Assert.Equal(1, r.Foo)
                        );
                    }

                    Assert.Equal(4, parserCalled);
                }
            );
        }

        [Fact]
        public async Task WithResetAsync()
        {
            // simple
            {
                const string CSV = "A,B\r\nfoo,1\r\nbar,6\r\n";

                await RunAsyncReaderVariants<_WithReset>(
                    Options.Default,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader(CSV))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("foo", a.A); Assert.Equal(1, a.B); },
                                a => { Assert.Equal("bar", a.A); Assert.Equal(2, a.B); }
                            );
                        }
                    }
                );
            }

            // static
            {
                const string CSV = "A,B\r\nfoo,1\r\nbar,6\r\n";

                await RunAsyncReaderVariants<_WithReset_Static>(
                    Options.Default,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader(CSV))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            _WithReset_Static.Count = 0;

                            var rows = await csv.ReadAllAsync();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("foo", a.A); Assert.Equal(1, a.B); },
                                a => { Assert.Equal("bar", a.A); Assert.Equal(6, a.B); }
                            );

                            Assert.Equal(2, _WithReset_Static.Count);
                        }
                    }
                );
            }

            // static with param
            {
                const string CSV = "A,B\r\nfoo,1\r\nbar,6\r\n";

                await RunAsyncReaderVariants<_WithReset_StaticWithParam>(
                    Options.Default,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader(CSV))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("foo", a.A); Assert.Equal(1, a.B); },
                                a => { Assert.Equal("bar", a.A); Assert.Equal(2, a.B); }
                            );
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task BadEscapeAsync()
        {
            var opts = Options.Default;
            var CSV = @"h""ello"",world";

            await RunAsyncReaderVariants<_BadEscape>(
                opts,
                async (config, makeReader) =>
                {
                    await using (var reader = await makeReader(CSV))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        try
                        {
                            await csv.ReadAllAsync();
                        }
                        catch (Exception e)
                        {
                            switch (e)
                            {
                                case AggregateException ae:
                                    Assert.Collection(
                                        ae.InnerExceptions,
                                        (e) => Assert.True(e is InvalidOperationException)
                                    );
                                    break;
                                case InvalidOperationException ioe:
                                    break;
                                default:
                                    // intentionally fail
                                    Assert.Null(e);
                                    break;
                            }
                        }
                    }
                }
            );
        }

        [Fact]
        public async Task TryReadWithReuseAsync()
        {
            const string CSV = "hello\r\nworld\r\nfoo\r\n";

            await RunAsyncReaderVariants<_TryReadWithReuse>(
                Options.Default,
                async (config, getReader) =>
                {
                    _TryReadWithReuse pre = null;

                    await using (var reader = await getReader(CSV))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var ret1 = await csv.TryReadWithReuseAsync(ref pre);
                        Assert.True(ret1.HasValue);
                        Assert.Equal("hello", ret1.Value.Bar);

                        var ret2 = await csv.TryReadWithReuseAsync(ref pre);
                        Assert.True(ret2.HasValue);
                        Assert.Equal("world", ret2.Value.Bar);
                        Assert.Same(ret1.Value, ret2.Value);

                        var ret3 = await csv.TryReadWithReuseAsync(ref pre);
                        Assert.True(ret3.HasValue);
                        Assert.Equal("foo", ret3.Value.Bar);
                        Assert.Same(ret1.Value, ret2.Value);
                        Assert.Same(ret2.Value, ret3.Value);

                        var ret4 = await csv.TryReadWithReuseAsync(ref pre);
                        Assert.False(ret4.HasValue);
                    }
                }
            );
        }

        [Fact]
        public async Task ReadAllAsync()
        {
            var CSV =
$@"Foo,Fizz,Bar,Buzz
""hello{'\n'}world"",{new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local)},,123.45
,{new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local)},,
mkay,{new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local)},8675309,987654321.012345";

            var opts = Options.Default;

            await RunAsyncReaderVariants<_ReadAll>(
                opts,
                async (config, makeReader) =>
                {
                    await using (var reader = await makeReader(CSV))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var read = await csv.ReadAllAsync();
                        Assert.Collection(
                            read,
                            r1 =>
                            {

                                Assert.Equal("hello\nworld", r1.Foo);
                                Assert.Equal(new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local), r1.Fizz);
                                Assert.Equal((int?)null, r1.Bar);
                                Assert.Equal(123.45, r1.Buzz);
                            },
                            r2 =>
                            {

                                Assert.Equal("", r2.Foo);
                                Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local), r2.Fizz);
                                Assert.Equal((int?)null, r2.Bar);
                                Assert.Equal((double?)null, r2.Buzz);
                            },
                            r3 =>
                            {

                                Assert.Equal("mkay", r3.Foo);
                                Assert.Equal(new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local), r3.Fizz);
                                Assert.Equal((int?)8675309, r3.Bar);
                                Assert.Equal((double?)987654321.012345, r3.Buzz);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task ReadAllAsync_PreAllocated()
        {
            var CSV =
$@"Foo,Fizz,Bar,Buzz
""hello{'\n'}world"",{new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local)},,123.45
,{new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local)},,
mkay,{new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local)},8675309,987654321.012345";

            var opts = Options.Default;

            await RunAsyncReaderVariants<_ReadAll>(
                opts,
                async (config, makeReader) =>
                {
                    await using (var reader = await makeReader(CSV))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var pre = new List<_ReadAll>();
                        pre.Add(new _ReadAll { Bar = 1, Buzz = 2.2, Fizz = new DateTime(3, 3, 3), Foo = "4" });

                        var read = await csv.ReadAllAsync(pre);
                        Assert.Collection(
                            read,
                            r1 =>
                            {
                                Assert.Equal("4", r1.Foo);
                                Assert.Equal(new DateTime(3, 3, 3), r1.Fizz);
                                Assert.Equal(1, r1.Bar);
                                Assert.Equal(2.2, r1.Buzz);
                            },
                            r2 =>
                            {

                                Assert.Equal("hello\nworld", r2.Foo);
                                Assert.Equal(new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local), r2.Fizz);
                                Assert.Equal((int?)null, r2.Bar);
                                Assert.Equal(123.45, r2.Buzz);
                            },
                            r3 =>
                            {

                                Assert.Equal("", r3.Foo);
                                Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local), r3.Fizz);
                                Assert.Equal((int?)null, r3.Bar);
                                Assert.Equal((double?)null, r3.Buzz);
                            },
                            r4 =>
                            {

                                Assert.Equal("mkay", r4.Foo);
                                Assert.Equal(new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local), r4.Fizz);
                                Assert.Equal((int?)8675309, r4.Bar);
                                Assert.Equal((double?)987654321.012345, r4.Buzz);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task EnumerateAllAsync()
        {
            var CSV =
$@"Foo,Fizz,Bar,Buzz
""hello{'\n'}world"",{new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local)},,123.45
,{new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local)},,
mkay,{new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local)},8675309,987654321.012345";

            var opts = Options.Default;

            await RunAsyncReaderVariants<_ReadAll>(
                opts,
                async (config, makeReader) =>
                {
                    await using (var reader = await makeReader(CSV))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var enumerable = csv.EnumerateAllAsync();
                        var enumerator = enumerable.GetAsyncEnumerator();
                        try
                        {
                            Assert.True(await enumerator.MoveNextAsync());
                            {
                                var r1 = enumerator.Current;

                                Assert.Equal("hello\nworld", r1.Foo);
                                Assert.Equal(new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local), r1.Fizz);
                                Assert.Equal((int?)null, r1.Bar);
                                Assert.Equal(123.45, r1.Buzz);
                            }

                            Assert.True(await enumerator.MoveNextAsync());
                            {
                                var r2 = enumerator.Current;

                                Assert.Equal("", r2.Foo);
                                Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local), r2.Fizz);
                                Assert.Equal((int?)null, r2.Bar);
                                Assert.Equal((double?)null, r2.Buzz);
                            }

                            Assert.True(await enumerator.MoveNextAsync());
                            {
                                var r3 = enumerator.Current;

                                Assert.Equal("mkay", r3.Foo);
                                Assert.Equal(new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local), r3.Fizz);
                                Assert.Equal((int?)8675309, r3.Bar);
                                Assert.Equal((double?)987654321.012345, r3.Buzz);
                            }

                            Assert.False(await enumerator.MoveNextAsync());

                            // double enumeration fails
                            Assert.Throws<InvalidOperationException>(() => enumerable.GetAsyncEnumerator());
                        }
                        finally
                        {
                            await enumerator.DisposeAsync();
                        }
                    }
                }
            );

            await RunAsyncReaderVariants<_ReadAll>(
                opts,
                async (config, makeReader) =>
                {
                    await using (var reader = await makeReader(CSV))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = new List<_ReadAll>();

                        var enumerable = csv.EnumerateAllAsync();
                        await foreach (var row in enumerable)
                        {
                            rows.Add(row);

                            // double enumeration fails
                            //   check happens here because `await foreach` implicitly disposes `enumerable`
                            Assert.Throws<InvalidOperationException>(() => enumerable.GetAsyncEnumerator());
                        }

                        Assert.Collection(
                            rows,
                            r1 =>
                            {
                                Assert.Equal("hello\nworld", r1.Foo);
                                Assert.Equal(new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local), r1.Fizz);
                                Assert.Equal((int?)null, r1.Bar);
                                Assert.Equal(123.45, r1.Buzz);
                            },
                            r2 =>
                            {
                                Assert.Equal("", r2.Foo);
                                Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local), r2.Fizz);
                                Assert.Equal((int?)null, r2.Bar);
                                Assert.Equal((double?)null, r2.Buzz);
                            },
                            r3 =>
                            {
                                Assert.Equal("mkay", r3.Foo);
                                Assert.Equal(new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local), r3.Fizz);
                                Assert.Equal((int?)8675309, r3.Bar);
                                Assert.Equal((double?)987654321.012345, r3.Buzz);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task OneColumnOneRowAsync()
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

            // normal
            {
                await RunAsyncReaderVariants<_OneColumnOneRow>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var str = await getReader("hello"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();

                            Assert.True(t.HasValue);
                            Assert.NotNull(t.Value);
                            Assert.Equal("hello", t.Value.Foo);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }

                    }
                );
            }

            // quoted
            {
                await RunAsyncReaderVariants<_OneColumnOneRow>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var str = await getReader("\"hello world\""))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();

                            Assert.True(t.HasValue);
                            Assert.NotNull(t.Value);
                            Assert.Equal("hello world", t.Value.Foo);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );
            }

            // escaped
            {
                await RunAsyncReaderVariants<_OneColumnOneRow>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var str = await getReader("\"hello \"\" world\""))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();

                            Assert.True(t.HasValue);
                            Assert.NotNull(t.Value);
                            Assert.Equal("hello \" world", t.Value.Foo);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task TwoColumnOneRowAsync()
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

            // normal
            {
                await RunAsyncReaderVariants<_TwoColumnOneRow>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var str = await getReader("hello,world"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();

                            Assert.True(t.HasValue);
                            Assert.Equal("hello", t.Value.One);
                            Assert.Equal("world", t.Value.Two);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );
            }

            // quoted
            {
                await RunAsyncReaderVariants<_TwoColumnOneRow>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var str = await getReader("\"hello,world\",\"fizz,buzz\""))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();

                            Assert.True(t.HasValue);
                            Assert.Equal("hello,world", t.Value.One);
                            Assert.Equal("fizz,buzz", t.Value.Two);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );
            }

            // escaped
            {
                await RunAsyncReaderVariants<_TwoColumnOneRow>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var str = await getReader("\"hello\"\"world\",\"fizz\"\"buzz\""))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();

                            Assert.True(t.HasValue);
                            Assert.Equal("hello\"world", t.Value.One);
                            Assert.Equal("fizz\"buzz", t.Value.Two);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task TwoColumnTwoRowAsync()
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

            // normal
            {
                await RunAsyncReaderVariants<_TwoColumnTwoRow>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var str = await getReader("hello,world\r\nfoo,bar"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);

                            Assert.Equal("hello", t.Value.Fizz);
                            Assert.Equal("world", t.Value.Buzz);

                            t = await reader.TryReadAsync();
                            Assert.Equal("foo", t.Value.Fizz);
                            Assert.Equal("bar", t.Value.Buzz);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );
            }

            // quoted
            {
                await RunAsyncReaderVariants<_TwoColumnTwoRow>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var str = await getReader("\"hello,world\",whatever\r\n\"foo,bar\",whoever"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal("hello,world", t.Value.Fizz);
                            Assert.Equal("whatever", t.Value.Buzz);

                            t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal("foo,bar", t.Value.Fizz);
                            Assert.Equal("whoever", t.Value.Buzz);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );
            }

            // escaped
            {
                await RunAsyncReaderVariants<_TwoColumnTwoRow>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var str = await getReader("\"hello\"\"world\",whatever\r\n\"foo\"\"bar\",whoever"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal("hello\"world", t.Value.Fizz);
                            Assert.Equal("whatever", t.Value.Buzz);

                            t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal("foo\"bar", t.Value.Fizz);
                            Assert.Equal("whoever", t.Value.Buzz);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task DetectLineEndingsAsync()
        {
            var opts = Options.CreateBuilder(Options.Default).WithRowEnding(RowEnding.Detect).WithReadHeader(ReadHeader.Never).ToOptions();

            // normal
            {
                // \r\n
                {
                    await RunAsyncReaderVariants<_DetectLineEndings>(
                        opts,
                        async (config, getReader) =>
                        {
                            await using (var str = await getReader("a,bb,ccc\r\ndddd,eeeee,ffffff\r\n1,2,3\r\n"))
                            await using (var reader = config.CreateAsyncReader(str))
                            {
                                var t1 = await reader.TryReadAsync();
                                Assert.Equal("a", t1.Value.Foo);
                                Assert.Equal("bb", t1.Value.Bar);
                                Assert.Equal("ccc", t1.Value.Fizz);

                                var t2 = await reader.TryReadAsync();
                                Assert.Equal("dddd", t2.Value.Foo);
                                Assert.Equal("eeeee", t2.Value.Bar);
                                Assert.Equal("ffffff", t2.Value.Fizz);

                                var t3 = await reader.TryReadAsync();
                                Assert.Equal("1", t3.Value.Foo);
                                Assert.Equal("2", t3.Value.Bar);
                                Assert.Equal("3", t3.Value.Fizz);

                                var t4 = await reader.TryReadAsync();
                                Assert.False(t4.HasValue);
                            }
                        }
                    );
                }

                // \r
                {
                    await RunAsyncReaderVariants<_DetectLineEndings>(
                        opts,
                        async (config, getReader) =>
                        {
                            await using (var str = await getReader("a,bb,ccc\rdddd,eeeee,ffffff\r1,2,3\r"))
                            await using (var reader = config.CreateAsyncReader(str))
                            {
                                var t1 = await reader.TryReadAsync();
                                Assert.Equal("a", t1.Value.Foo);
                                Assert.Equal("bb", t1.Value.Bar);
                                Assert.Equal("ccc", t1.Value.Fizz);

                                var t2 = await reader.TryReadAsync();
                                Assert.Equal("dddd", t2.Value.Foo);
                                Assert.Equal("eeeee", t2.Value.Bar);
                                Assert.Equal("ffffff", t2.Value.Fizz);

                                var t3 = await reader.TryReadAsync();
                                Assert.Equal("1", t3.Value.Foo);
                                Assert.Equal("2", t3.Value.Bar);
                                Assert.Equal("3", t3.Value.Fizz);

                                var t4 = await reader.TryReadAsync();
                                Assert.False(t4.HasValue);
                            }
                        }
                    );
                }

                // \n
                {
                    await RunAsyncReaderVariants<_DetectLineEndings>(
                        opts,
                        async (config, getReader) =>
                        {
                            await using (var str = await getReader("a,bb,ccc\ndddd,eeeee,ffffff\n1,2,3\n"))
                            await using (var reader = config.CreateAsyncReader(str))
                            {
                                var t1 = await reader.TryReadAsync();
                                Assert.Equal("a", t1.Value.Foo);
                                Assert.Equal("bb", t1.Value.Bar);
                                Assert.Equal("ccc", t1.Value.Fizz);

                                var t2 = await reader.TryReadAsync();
                                Assert.Equal("dddd", t2.Value.Foo);
                                Assert.Equal("eeeee", t2.Value.Bar);
                                Assert.Equal("ffffff", t2.Value.Fizz);

                                var t3 = await reader.TryReadAsync();
                                Assert.Equal("1", t3.Value.Foo);
                                Assert.Equal("2", t3.Value.Bar);
                                Assert.Equal("3", t3.Value.Fizz);

                                var t4 = await reader.TryReadAsync();
                                Assert.False(t4.HasValue);
                            }
                        }
                    );
                }
            }

            // quoted
            {
                // \r\n
                {
                    await RunAsyncReaderVariants<_DetectLineEndings>(
                        opts,
                        async (config, getReader) =>
                        {
                            await using (var str = await getReader("\"a\r\",bb,ccc\r\ndddd,\"ee\neee\",ffffff\r\n1,2,\"3\r\n\"\r\n"))
                            await using (var reader = config.CreateAsyncReader(str))
                            {
                                var t1 = await reader.TryReadAsync();
                                Assert.Equal("a\r", t1.Value.Foo);
                                Assert.Equal("bb", t1.Value.Bar);
                                Assert.Equal("ccc", t1.Value.Fizz);

                                var t2 = await reader.TryReadAsync();
                                Assert.Equal("dddd", t2.Value.Foo);
                                Assert.Equal("ee\neee", t2.Value.Bar);
                                Assert.Equal("ffffff", t2.Value.Fizz);

                                var t3 = await reader.TryReadAsync();
                                Assert.Equal("1", t3.Value.Foo);
                                Assert.Equal("2", t3.Value.Bar);
                                Assert.Equal("3\r\n", t3.Value.Fizz);

                                var t4 = await reader.TryReadAsync();
                                Assert.False(t4.HasValue);
                            }
                        }
                    );
                }

                // \r
                {
                    await RunAsyncReaderVariants<_DetectLineEndings>(
                        opts,
                        async (config, getReader) =>
                        {
                            await using (var str = await getReader("\"a\r\",bb,ccc\rdddd,\"ee\neee\",ffffff\r1,2,\"3\r\n\"\r"))
                            await using (var reader = config.CreateAsyncReader(str))
                            {
                                var t1 = await reader.TryReadAsync();
                                Assert.Equal("a\r", t1.Value.Foo);
                                Assert.Equal("bb", t1.Value.Bar);
                                Assert.Equal("ccc", t1.Value.Fizz);

                                var t2 = await reader.TryReadAsync();
                                Assert.Equal("dddd", t2.Value.Foo);
                                Assert.Equal("ee\neee", t2.Value.Bar);
                                Assert.Equal("ffffff", t2.Value.Fizz);

                                var t3 = await reader.TryReadAsync();
                                Assert.Equal("1", t3.Value.Foo);
                                Assert.Equal("2", t3.Value.Bar);
                                Assert.Equal("3\r\n", t3.Value.Fizz);

                                var t4 = await reader.TryReadAsync();
                                Assert.False(t4.HasValue);
                            }
                        }
                    );
                }

                // \n
                {
                    await RunAsyncReaderVariants<_DetectLineEndings>(
                        opts,
                        async (config, getReader) =>
                        {
                            await using (var str = await getReader("\"a\r\",bb,ccc\ndddd,\"ee\neee\",ffffff\n1,2,\"3\r\n\"\n"))
                            await using (var reader = config.CreateAsyncReader(str))
                            {
                                var t1 = await reader.TryReadAsync();
                                Assert.Equal("a\r", t1.Value.Foo);
                                Assert.Equal("bb", t1.Value.Bar);
                                Assert.Equal("ccc", t1.Value.Fizz);

                                var t2 = await reader.TryReadAsync();
                                Assert.Equal("dddd", t2.Value.Foo);
                                Assert.Equal("ee\neee", t2.Value.Bar);
                                Assert.Equal("ffffff", t2.Value.Fizz);

                                var t3 = await reader.TryReadAsync();
                                Assert.Equal("1", t3.Value.Foo);
                                Assert.Equal("2", t3.Value.Bar);
                                Assert.Equal("3\r\n", t3.Value.Fizz);

                                var t4 = await reader.TryReadAsync();
                                Assert.False(t4.HasValue);
                            }
                        }
                    );
                }
            }

            // escaped
            {
                // \r\n
                {
                    await RunAsyncReaderVariants<_DetectLineEndings>(
                        opts,
                        async (config, getReader) =>
                        {
                            await using (var str = await getReader("\"a\r\",\"b\"\"b\",ccc\r\n\"\"\"dddd\",\"ee\neee\",ffffff\r\n1,\"\"\"2\"\"\",\"3\r\n\"\r\n"))
                            await using (var reader = config.CreateAsyncReader(str))
                            {
                                var t1 = await reader.TryReadAsync();
                                Assert.Equal("a\r", t1.Value.Foo);
                                Assert.Equal("b\"b", t1.Value.Bar);
                                Assert.Equal("ccc", t1.Value.Fizz);

                                var t2 = await reader.TryReadAsync();
                                Assert.Equal("\"dddd", t2.Value.Foo);
                                Assert.Equal("ee\neee", t2.Value.Bar);
                                Assert.Equal("ffffff", t2.Value.Fizz);

                                var t3 = await reader.TryReadAsync();
                                Assert.Equal("1", t3.Value.Foo);
                                Assert.Equal("\"2\"", t3.Value.Bar);
                                Assert.Equal("3\r\n", t3.Value.Fizz);

                                var t4 = await reader.TryReadAsync();
                                Assert.False(t4.HasValue);
                            }
                        }
                    );
                }

                // \r
                {
                    await RunAsyncReaderVariants<_DetectLineEndings>(
                         opts,
                         async (config, getReader) =>
                         {
                             await using (var str = await getReader("\"a\r\",\"b\"\"b\",ccc\r\"\"\"dddd\",\"ee\neee\",ffffff\r1,\"\"\"2\"\"\",\"3\r\n\"\r"))
                             await using (var reader = config.CreateAsyncReader(str))
                             {
                                 var t1 = await reader.TryReadAsync();
                                 Assert.Equal("a\r", t1.Value.Foo);
                                 Assert.Equal("b\"b", t1.Value.Bar);
                                 Assert.Equal("ccc", t1.Value.Fizz);

                                 var t2 = await reader.TryReadAsync();
                                 Assert.Equal("\"dddd", t2.Value.Foo);
                                 Assert.Equal("ee\neee", t2.Value.Bar);
                                 Assert.Equal("ffffff", t2.Value.Fizz);

                                 var t3 = await reader.TryReadAsync();
                                 Assert.Equal("1", t3.Value.Foo);
                                 Assert.Equal("\"2\"", t3.Value.Bar);
                                 Assert.Equal("3\r\n", t3.Value.Fizz);

                                 var t4 = await reader.TryReadAsync();
                                 Assert.False(t4.HasValue);
                             }
                         }
                    );
                }

                // \n
                {
                    await RunAsyncReaderVariants<_DetectLineEndings>(
                        opts,
                        async (config, getReader) =>
                        {
                            await using (var str = await getReader("\"a\r\",\"b\"\"b\",ccc\n\"\"\"dddd\",\"ee\neee\",ffffff\n1,\"\"\"2\"\"\",\"3\r\n\"\n"))
                            await using (var reader = config.CreateAsyncReader(str))
                            {
                                var t1 = await reader.TryReadAsync();
                                Assert.Equal("a\r", t1.Value.Foo);
                                Assert.Equal("b\"b", t1.Value.Bar);
                                Assert.Equal("ccc", t1.Value.Fizz);

                                var t2 = await reader.TryReadAsync();
                                Assert.Equal("\"dddd", t2.Value.Foo);
                                Assert.Equal("ee\neee", t2.Value.Bar);
                                Assert.Equal("ffffff", t2.Value.Fizz);

                                var t3 = await reader.TryReadAsync();
                                Assert.Equal("1", t3.Value.Foo);
                                Assert.Equal("\"2\"", t3.Value.Bar);
                                Assert.Equal("3\r\n", t3.Value.Fizz);

                                var t4 = await reader.TryReadAsync();
                                Assert.False(t4.HasValue);
                            }
                        }
                    );
                }
            }
        }



        [Fact]
        public async Task DetectHeadersAsync()
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Detect).WithRowEnding(RowEnding.Detect).ToOptions();

            // no headers
            await RunAsyncReaderVariants<_DetectHeaders>(
                opts,
                async (config, del) =>
                {
                    await using (var str = await del("123,4.56"))
                    await using (var reader = (AsyncReader<_DetectHeaders>)config.CreateAsyncReader(str))
                    {
                        var t = await reader.TryReadAsync();
                        Assert.True(t.HasValue);
                        Assert.Equal(123, t.Value.Hello);
                        Assert.Equal(4.56, t.Value.World);

                        Assert.Equal(ReadHeader.Never, reader.ReadHeaders.Value);

                        Assert.Collection(
                            reader.RowBuilder.Columns,
                            c => Assert.Equal("Hello", c),
                            c => Assert.Equal("World", c)
                        );

                        t = await reader.TryReadAsync();
                        Assert.False(t.HasValue);
                    }
                }
            );

            // headers
            {
                // \r\n
                await RunAsyncReaderVariants<_DetectHeaders>(
                    opts,
                    async (config, del) =>
                    {
                        await using (var str = await del("Hello,World\r\n123,4.56\r\n789,0.12\r\n"))
                        await using (var reader = (AsyncReader<_DetectHeaders>)config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(123, t.Value.Hello);
                            Assert.Equal(4.56, t.Value.World);

                            Assert.Equal(ReadHeader.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.RowBuilder.Columns,
                                c => Assert.Equal("Hello", c),
                                c => Assert.Equal("World", c)
                            );

                            t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(789, t.Value.Hello);
                            Assert.Equal(0.12, t.Value.World);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );

                // \n
                await RunAsyncReaderVariants<_DetectHeaders>(
                    opts,
                    async (config, del) =>
                    {
                        await using (var str = await del("Hello,World\n123,4.56\n789,0.12\n"))
                        await using (var reader = (AsyncReader<_DetectHeaders>)config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(123, t.Value.Hello);
                            Assert.Equal(4.56, t.Value.World);

                            Assert.Equal(ReadHeader.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.RowBuilder.Columns,
                                c => Assert.Equal("Hello", c),
                                c => Assert.Equal("World", c)
                            );

                            t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(789, t.Value.Hello);
                            Assert.Equal(0.12, t.Value.World);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );

                // \r
                await RunAsyncReaderVariants<_DetectHeaders>(
                    opts,
                    async (config, del) =>
                    {
                        await using (var str = await del("Hello,World\r123,4.56\r789,0.12\r"))
                        await using (var reader = (AsyncReader<_DetectHeaders>)config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(123, t.Value.Hello);
                            Assert.Equal(4.56, t.Value.World);

                            Assert.Equal(ReadHeader.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.RowBuilder.Columns,
                                c => Assert.Equal("Hello", c),
                                c => Assert.Equal("World", c)
                            );

                            t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(789, t.Value.Hello);
                            Assert.Equal(0.12, t.Value.World);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );
            }

            // headers, different order
            {
                // \r\n
                await RunAsyncReaderVariants<_DetectHeaders>(
                    opts,
                    async (config, del) =>
                    {
                        await using (var str = await del("World,Hello\r\n4.56,123\r\n0.12,789\r\n"))
                        await using (var reader = (AsyncReader<_DetectHeaders>)config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(123, t.Value.Hello);
                            Assert.Equal(4.56, t.Value.World);

                            Assert.Equal(ReadHeader.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.RowBuilder.Columns,
                                c => Assert.Equal("World", c),
                                c => Assert.Equal("Hello", c)
                            );

                            t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(789, t.Value.Hello);
                            Assert.Equal(0.12, t.Value.World);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );

                // \n
                await RunAsyncReaderVariants<_DetectHeaders>(
                    opts,
                    async (config, del) =>
                    {
                        await using (var str = await del("World,Hello\n4.56,123\n0.12,789\n"))
                        await using (var reader = (AsyncReader<_DetectHeaders>)config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(123, t.Value.Hello);
                            Assert.Equal(4.56, t.Value.World);

                            Assert.Equal(ReadHeader.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.RowBuilder.Columns,
                                c => Assert.Equal("World", c),
                                c => Assert.Equal("Hello", c)
                            );

                            t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(789, t.Value.Hello);
                            Assert.Equal(0.12, t.Value.World);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );

                // \r
                await RunAsyncReaderVariants<_DetectHeaders>(
                    opts,
                    async (config, del) =>
                    {
                        await using (var str = await del("World,Hello\r4.56,123\r0.12,789\r"))
                        await using (var reader = (AsyncReader<_DetectHeaders>)config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(123, t.Value.Hello);
                            Assert.Equal(4.56, t.Value.World);

                            Assert.Equal(ReadHeader.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.RowBuilder.Columns,
                                c => Assert.Equal("World", c),
                                c => Assert.Equal("Hello", c)
                            );

                            t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(789, t.Value.Hello);
                            Assert.Equal(0.12, t.Value.World);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );
            }

            // headers, missing
            {
                // \r\n
                await RunAsyncReaderVariants<_DetectHeaders>(
                    opts,
                    async (config, del) =>
                    {
                        await using (var str = await del("World,Foo\r\n4.56,123\r\n0.12,789\r\n"))
                        await using (var reader = (AsyncReader<_DetectHeaders>)config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(0, t.Value.Hello);
                            Assert.Equal(4.56, t.Value.World);

                            Assert.Equal(ReadHeader.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.RowBuilder.Columns,
                                c => Assert.Equal("World", c),
                                c => Assert.Equal("--UNKNOWN--", c)
                            );

                            t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(0, t.Value.Hello);
                            Assert.Equal(0.12, t.Value.World);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );

                // \n
                await RunAsyncReaderVariants<_DetectHeaders>(
                    opts,
                    async (config, del) =>
                    {
                        await using (var str = await del("World,Foo\n4.56,123\n0.12,789\n"))
                        await using (var reader = (AsyncReader<_DetectHeaders>)config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(0, t.Value.Hello);
                            Assert.Equal(4.56, t.Value.World);

                            Assert.Equal(ReadHeader.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.RowBuilder.Columns,
                                c => Assert.Equal("World", c),
                                c => Assert.Equal("--UNKNOWN--", c)
                            );

                            t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(0, t.Value.Hello);
                            Assert.Equal(0.12, t.Value.World);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );

                // \r
                await RunAsyncReaderVariants<_DetectHeaders>(
                    opts,
                    async (config, del) =>
                    {
                        await using (var str = await del("World,Foo\r4.56,123\r0.12,789\r"))
                        await using (var reader = (AsyncReader<_DetectHeaders>)config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(0, t.Value.Hello);
                            Assert.Equal(4.56, t.Value.World);

                            Assert.Equal(ReadHeader.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.RowBuilder.Columns,
                                c => Assert.Equal("World", c),
                                c => Assert.Equal("--UNKNOWN--", c)
                            );

                            t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(0, t.Value.Hello);
                            Assert.Equal(0.12, t.Value.World);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task IsRequiredNotInHeaderAsync()
        {
            // simple
            {
                var opts = Options.Default;
                var CSV = "A,C\r\nhello,world";

                await RunAsyncReaderVariants<_IsRequiredMissing>(
                    opts,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader(CSV))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            await Assert.ThrowsAsync<SerializationException>(async () => await csv.ReadAllAsync());
                        }
                    }
                );
            }

            // hold type
            {
                var t = typeof(_IsRequiredMissing_Hold).GetTypeInfo();
                var cons = t.GetConstructors().Single();
                var pA = cons.GetParameters().Single(a => a.Name == "a");
                var pB = cons.GetParameters().Single(b => b.Name == "b");

                var td = ManualTypeDescriber.CreateBuilder();
                td.WithInstanceProvider(InstanceProvider.ForConstructorWithParameters(cons));
                td.WithExplicitSetter(t, "A", Setter.ForConstructorParameter(pA), Parser.GetDefault(typeof(string).GetTypeInfo()), MemberRequired.Yes);
                td.WithExplicitSetter(t, "B", Setter.ForConstructorParameter(pB), Parser.GetDefault(typeof(string).GetTypeInfo()), MemberRequired.Yes);

                var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(td.ToManualTypeDescriber()).ToOptions();
                var CSV = "A,C\r\nhello,world";

                await RunAsyncReaderVariants<_IsRequiredMissing_Hold>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var str = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            await Assert.ThrowsAsync<SerializationException>(async () => await csv.ReadAllAsync());
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task IsRequiredNotInRowAsync()
        {
            var opts = Options.Default;

            // beginning
            {
                var CSV = "B,C\r\nhello,world\r\n,";

                await RunAsyncReaderVariants<_IsRequiredMissing>(
                    opts,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader(CSV))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            await Assert.ThrowsAsync<SerializationException>(async () => await csv.ReadAllAsync());
                        }
                    }
                );
            }

            // middle
            {
                var CSV = "A,B,C\r\nhello,world,foo\r\n,,";

                await RunAsyncReaderVariants<_IsRequiredMissing>(
                    opts,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader(CSV))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            await Assert.ThrowsAsync<SerializationException>(async () => await csv.ReadAllAsync());
                        }
                    }
                );
            }

            // end
            {
                var CSV = "A,B\r\nhello,world\r\n,";

                await RunAsyncReaderVariants<_IsRequiredMissing>(
                    opts,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader(CSV))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            await Assert.ThrowsAsync<SerializationException>(async () => await csv.ReadAllAsync());
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task DifferentEscapesAsync()
        {
            var opts = Options.CreateBuilder(Options.Default).WithEscapedValueStartAndEnd('"').WithEscapedValueEscapeCharacter('\\').ToOptions();

            // simple
            {
                await RunAsyncReaderVariants<_DifferentEscapes>(
                    opts,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader("Foo,Bar\r\nhello,world"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("hello", a.Foo); Assert.Equal("world", a.Bar); }
                            );
                        }
                    }
                );
            }

            // escaped
            {
                await RunAsyncReaderVariants<_DifferentEscapes>(
                    opts,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader("Foo,Bar\r\n\"hello\",\"world\""))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("hello", a.Foo); Assert.Equal("world", a.Bar); }
                            );
                        }
                    }
                );
            }

            // escaped with quotes
            {
                await RunAsyncReaderVariants<_DifferentEscapes>(
                    opts,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader("Foo,Bar\r\n\"he\\\"llo\",\"world\""))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("he\"llo", a.Foo); Assert.Equal("world", a.Bar); }
                            );
                        }
                    }
                );
            }

            // escaped with slash
            {
                await RunAsyncReaderVariants<_DifferentEscapes>(
                    opts,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader("Foo,Bar\r\n\"hello\",\"w\\\\orld\""))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("hello", a.Foo); Assert.Equal("w\\orld", a.Bar); }
                            );
                        }
                    }
                );
            }

            // escape char outside of quotes
            {
                await RunAsyncReaderVariants<_DifferentEscapes>(
                    opts,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader("Foo,Bar\r\n\\,\\ooo"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("\\", a.Foo); Assert.Equal("\\ooo", a.Bar); }
                            );
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task TabSeparatorAsync()
        {
            const string TSV = @"Foo	Bar
""hello""""world""	123
";
            var opts = Options.CreateBuilder(Options.Default).WithEscapedValueStartAndEnd('"').WithValueSeparator("\t").ToOptions();

            await RunAsyncReaderVariants<_TabSeparator>(
                opts,
                async (config, getReader) =>
                {
                    await using (var reader = await getReader(TSV))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = await csv.ReadAllAsync();

                        Assert.Collection(
                            rows,
                            a => { Assert.Equal("hello\"world", a.Foo); Assert.Equal(123, a.Bar); }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task CommentsAsync()
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).WithCommentCharacter('#').ToOptions();

            // comment first line
            {
                var CSV = "#this is a test comment!\r\nhello,world\r\nfoo,bar";
                await RunAsyncReaderVariants<_Comment>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );
            }

            // comment after header
            {
                var CSV = "hello,world\r\n#this is a test comment\r\nfoo,bar";
                await RunAsyncReaderVariants<_Comment>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );
            }

            // comment between rows
            {
                var CSV = "hello,world\r\nfoo,bar\r\n#comment!\r\nfizz,buzz";
                await RunAsyncReaderVariants<_Comment>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); },
                                b => { Assert.Equal("fizz", b.Hello); Assert.Equal("buzz", b.World); }
                            );
                        }
                    }
                );
            }

            // comment at end
            {
                var CSV = "hello,world\r\nfoo,bar\r\n#comment!";
                await RunAsyncReaderVariants<_Comment>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task ContextAsync()
        {
            var parseFoo = (Parser)typeof(ReaderTests).GetMethod(nameof(_Context_ParseFoo));
            var parseBar = (Parser)typeof(ReaderTests).GetMethod(nameof(_Context_ParseBar));

            var describer = ManualTypeDescriberBuilder.CreateBuilder(ManualTypeDescriberFallbackBehavior.UseFallback);
            describer.WithInstanceProvider((InstanceProvider)typeof(_Context).GetConstructor(Type.EmptyTypes));
            describer.WithDeserializableProperty(typeof(_Context).GetProperty(nameof(_Context.Foo)), nameof(_Context.Foo), parseFoo);
            describer.WithDeserializableProperty(typeof(_Context).GetProperty(nameof(_Context.Bar)), nameof(_Context.Bar), parseBar);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(describer.ToManualTypeDescriber()).ToOptions();

            // no headers
            {
                await RunAsyncReaderVariants<_Context>(
                    opts,
                    async (config, getReader) =>
                    {
                        _Context_ParseFoo_Records = new List<string>();
                        _Context_ParseBar_Records = new List<string>();

                        await using (var reader = await getReader("hello,123\r\nfoo,456\r\n,\r\nnope,7"))
                        await using (var csv = config.CreateAsyncReader(reader, -22))
                        {
                            var r = await csv.ReadAllAsync();

                            Assert.Equal(4, r.Count);
                        }

                        Assert.Collection(
                            _Context_ParseFoo_Records,
                            c => Assert.Equal("0,Foo,0,hello,-22", c),
                            c => Assert.Equal("1,Foo,0,foo,-22", c),
                            c => Assert.Equal("2,Foo,0,,-22", c),
                            c => Assert.Equal("3,Foo,0,nope,-22", c)
                        );

                        Assert.Collection(
                            _Context_ParseBar_Records,
                            c => Assert.Equal("0,Bar,1,123,-22", c),
                            c => Assert.Equal("1,Bar,1,456,-22", c),
                            c => Assert.Equal("3,Bar,1,7,-22", c)
                        );
                    }
                );
            }

            // with headers
            {
                await RunAsyncReaderVariants<_Context>(
                    opts,
                    async (config, getReader) =>
                    {
                        _Context_ParseFoo_Records = new List<string>();
                        _Context_ParseBar_Records = new List<string>();

                        await using (var reader = await getReader("Bar,Foo\r\n123,hello\r\n456,foo\r\n8,\r\n7,nope"))
                        await using (var csv = config.CreateAsyncReader(reader, "world"))
                        {
                            var r = await csv.ReadAllAsync();

                            Assert.Equal(4, r.Count);
                        }

                        Assert.Collection(
                            _Context_ParseFoo_Records,
                            c => Assert.Equal("0,Foo,1,hello,world", c),
                            c => Assert.Equal("1,Foo,1,foo,world", c),
                            c => Assert.Equal("3,Foo,1,nope,world", c)
                        );

                        Assert.Collection(
                            _Context_ParseBar_Records,
                            c => Assert.Equal("0,Bar,0,123,world", c),
                            c => Assert.Equal("1,Bar,0,456,world", c),
                            c => Assert.Equal("2,Bar,0,8,world", c),
                            c => Assert.Equal("3,Bar,0,7,world", c)
                        );
                    }
                );
            }
        }

        [Fact]
        public async Task StaticSetterAsync()
        {
            var describer = ManualTypeDescriberBuilder.CreateBuilder();
            describer.WithDeserializableProperty(typeof(_StaticSetter).GetProperty(nameof(_StaticSetter.Foo), BindingFlags.Static | BindingFlags.Public));
            InstanceProviderDelegate<_StaticSetter> del = (in ReadContext _, out _StaticSetter i) => { i = new _StaticSetter(); return true; };
            describer.WithInstanceProvider((InstanceProvider)del);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).ToOptions();

            await RunAsyncReaderVariants<_StaticSetter>(
                opts,
                async (config, getReader) =>
                {
                    _StaticSetter.Foo = 123;

                    await using (var reader = await getReader("456"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var row = await csv.ReadAllAsync();

                        Assert.Collection(row, r => Assert.NotNull(r));
                    }

                    Assert.Equal(456, _StaticSetter.Foo);
                }
            );
        }
    }
#pragma warning restore IDE1006
}