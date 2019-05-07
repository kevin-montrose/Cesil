using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

using static Cesil.Tests.Helpers;

namespace Cesil.Tests
{
    public class DynamicReaderTests
    {
        [Fact]
        public void Multi()
        {
            var optsHeader = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

            // with headers
            RunSyncDynamicReaderVariants(
                optsHeader,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("A,B\r\nfoo,bar\r\n1,3.3\r\n2019-01-01,d"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var read = csv.ReadAll();

                        Assert.Collection(
                            read,
                            row1 =>
                            {
                                string a1 = row1.A;
                                string a2 = row1["A"];
                                string a3 = row1[0];

                                Assert.Equal("foo", a1);
                                Assert.Equal("foo", a2);
                                Assert.Equal("foo", a3);

                                string b1 = row1.B;
                                string b2 = row1["B"];
                                string b3 = row1[1];

                                Assert.Equal("bar", b1);
                                Assert.Equal("bar", b2);
                                Assert.Equal("bar", b3);
                            },
                            row2 =>
                            {
                                int a1 = row2.A;
                                int a2 = row2["A"];
                                int a3 = row2[0];

                                Assert.Equal(1, a1);
                                Assert.Equal(1, a2);
                                Assert.Equal(1, a3);

                                double b1 = row2.B;
                                double b2 = row2["B"];
                                double b3 = row2[1];

                                Assert.Equal(3.3, b1);
                                Assert.Equal(3.3, b2);
                                Assert.Equal(3.3, b3);
                            },
                            row3 =>
                            {
                                DateTime a1 = row3.A;
                                DateTime a2 = row3["A"];
                                DateTime a3 = row3[0];

                                Assert.Equal(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), a1);
                                Assert.Equal(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), a2);
                                Assert.Equal(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), a3);

                                char b1 = row3.B;
                                char b2 = row3["B"];
                                char b3 = row3[1];

                                Assert.Equal('d', b1);
                                Assert.Equal('d', b2);
                                Assert.Equal('d', b3);
                            }
                        );
                    }
                }
            );

            var optsNoHeader = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

            // no headers
            RunSyncDynamicReaderVariants(
                optsNoHeader,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("foo,bar\r\n1,3.3\r\n2019-01-01,d"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var read = csv.ReadAll();

                        Assert.Collection(
                            read,
                            row1 =>
                            {
                                string a1 = row1[0];

                                Assert.Equal("foo", a1);

                                string b1 = row1[1];

                                Assert.Equal("bar", b1);
                            },
                            row2 =>
                            {
                                int a1 = row2[0];

                                Assert.Equal(1, a1);

                                double b1 = row2[1];

                                Assert.Equal(3.3, b1);
                            },
                            row3 =>
                            {
                                DateTime a1 = row3[0];

                                Assert.Equal(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), a1);

                                char b1 = row3[1];

                                Assert.Equal('d', b1);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public void Simple()
        {
            var optsHeader = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

            // with headers
            RunSyncDynamicReaderVariants(
                optsHeader,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("A,B\r\nfoo,bar"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var read = csv.ReadAll();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                string aIx = row[0];
                                string aName = row["A"];
                                string aMem = row.A;

                                Assert.Equal("foo", aIx);
                                Assert.Equal("foo", aName);
                                Assert.Equal("foo", aMem);

                                string bIx = row[1];
                                string bName = row["B"];
                                string bMem = row.B;

                                Assert.Equal("bar", bIx);
                                Assert.Equal("bar", bName);
                                Assert.Equal("bar", bMem);

                                // untyped enumerable
                                {
                                    System.Collections.IEnumerable e = row;
                                    Assert.Collection(
                                        e.Cast<dynamic>().Select(o => (string)o),
                                        a => Assert.Equal("foo", a),
                                        b => Assert.Equal("bar", b)
                                    );
                                }

                                // typed enumerable
                                {
                                    IEnumerable<string> e = row;
                                    Assert.Collection(
                                        e,
                                        a => Assert.Equal("foo", a),
                                        b => Assert.Equal("bar", b)
                                    );
                                }
                            }
                        );
                    }
                }
            );

            var optsNoHeader = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

            // no headers
            RunSyncDynamicReaderVariants(
                optsNoHeader,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("foo,bar"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var read = csv.ReadAll();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                string aIx = row[0];

                                Assert.Equal("foo", aIx);

                                string bIx = row[1];

                                Assert.Equal("bar", bIx);

                                // untyped enumerable
                                {
                                    System.Collections.IEnumerable e = row;
                                    Assert.Collection(
                                        e.Cast<dynamic>().Select(o => (string)o),
                                        a => Assert.Equal("foo", a),
                                        b => Assert.Equal("bar", b)
                                    );
                                }

                                // typed enumerable
                                {
                                    IEnumerable<string> e = row;
                                    Assert.Collection(
                                        e,
                                        a => Assert.Equal("foo", a),
                                        b => Assert.Equal("bar", b)
                                    );
                                }
                            }
                        );
                    }
                }
            );
        }

        class _Conversions
        {
            public int I { get; set; }

            public _Conversions(ReadOnlySpan<char> foo)
            {
                I = int.Parse(foo);
            }
        }

        [Fact]
        public void Conversions()
        {
            var optsHeaders = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

            // with headers
            RunSyncDynamicReaderVariants(
                optsHeaders,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("A,B\r\n1,57DEC02E-BDD6-4AF1-90F5-037596E08500"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var read = csv.ReadAll();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                var a = row[0];
                                string aStr = a;
                                int aInt = a;
                                float aFloat = a;
                                _Conversions aC = a;

                                Assert.Equal("1", aStr);
                                Assert.Equal(1, aInt);
                                Assert.Equal(1f, aFloat);
                                Assert.Equal(1, aC.I);

                                var b = row.B;
                                string bStr = b;
                                Guid bGuid = b;

                                Assert.Equal("57DEC02E-BDD6-4AF1-90F5-037596E08500", bStr);
                                Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), bGuid);
                            }
                        );
                    }
                }
            );

            var optsNoHeaders = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

            // with no headers
            RunSyncDynamicReaderVariants(
                optsNoHeaders,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("1,57DEC02E-BDD6-4AF1-90F5-037596E08500"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var read = csv.ReadAll();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                var a = row[0];
                                string aStr = a;
                                int aInt = a;
                                float aFloat = a;
                                _Conversions aC = a;

                                Assert.Equal("1", aStr);
                                Assert.Equal(1, aInt);
                                Assert.Equal(1f, aFloat);
                                Assert.Equal(1, aC.I);

                                var b = row[1];
                                string bStr = b;
                                Guid bGuid = b;

                                Assert.Equal("57DEC02E-BDD6-4AF1-90F5-037596E08500", bStr);
                                Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), bGuid);
                            }
                        );
                    }
                }
            );
        }

        enum _Tuple
        {
            Red,
            Green
        }

        [Fact]
        public void Tuple()
        {
            var optWithHeaders = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

            // headers
            {
                // one
                RunSyncDynamicReaderVariants(
                    optWithHeaders,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("A\r\n1\r\nfoo"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                },
                                row2 =>
                                {
                                    Tuple<string> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                }
                            );
                        }
                    }
                );

                // two
                RunSyncDynamicReaderVariants(
                    optWithHeaders,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("A,B\r\n1,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,-123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item2);
                                },
                                row2 =>
                                {
                                    Tuple<string, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(-123, typed.Item2);
                                }
                            );
                        }
                    }
                );

                // 17
                {
                    var row1Val =
                        new object[]
                        {
                        1,
                        Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"),
                        true,
                        false,
                        long.MaxValue,
                        0.123f,
                        (sbyte)-123,
                        (byte)128,
                        TimeSpan.FromMilliseconds(1234567890),

                        2,
                        Guid.Parse("77DEF02E-BDD6-4AF1-90F5-037596E08599"),
                        "blue",
                        _Tuple.Green,
                        ulong.MaxValue,
                        -999999.99m,
                        (short)-12300,
                        (sbyte)-2
                        };
                    var row1 = string.Join(",", row1Val);

                    RunSyncDynamicReaderVariants(
                        optWithHeaders,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader($"A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q\r\n{row1}"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var read = csv.ReadAll();

                                Assert.Collection(
                                    read,
                                    row1 =>
                                    {
                                        Tuple<
                                            int,
                                            Guid,
                                            bool,
                                            bool,
                                            long,
                                            float,
                                            sbyte,
                                            Tuple<
                                                byte,
                                                TimeSpan,

                                                int,
                                                Guid,
                                                string,
                                                _Tuple,
                                                ulong,
                                                Tuple<
                                                    decimal,
                                                    short,
                                                    sbyte
                                                >
                                            >
                                        > typed = row1;
                                        Assert.Equal(row1Val[0], typed.Item1);
                                        Assert.Equal(row1Val[1], typed.Item2);
                                        Assert.Equal(row1Val[2], typed.Item3);
                                        Assert.Equal(row1Val[3], typed.Item4);
                                        Assert.Equal(row1Val[4], typed.Item5);
                                        Assert.Equal(row1Val[5], typed.Item6);
                                        Assert.Equal(row1Val[6], typed.Item7);
                                        Assert.Equal(row1Val[7], typed.Rest.Item1);
                                        Assert.Equal(row1Val[8], typed.Rest.Item2);
                                        Assert.Equal(row1Val[9], typed.Rest.Item3);
                                        Assert.Equal(row1Val[10], typed.Rest.Item4);
                                        Assert.Equal(row1Val[11], typed.Rest.Item5);
                                        Assert.Equal(row1Val[12], typed.Rest.Item6);
                                        Assert.Equal(row1Val[13], typed.Rest.Item7);
                                        Assert.Equal(row1Val[14], typed.Rest.Rest.Item1);
                                        Assert.Equal(row1Val[15], typed.Rest.Rest.Item2);
                                        Assert.Equal(row1Val[16], typed.Rest.Rest.Item3);
                                    }
                                );
                            }
                        }
                    );
                }
            }

            var optNoHeaders = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

            // no headers
            {
                // one
                RunSyncDynamicReaderVariants(
                    optNoHeaders,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("1\r\nfoo"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                },
                                row2 =>
                                {
                                    Tuple<string> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                }
                            );
                        }
                    }
                );

                // two
                RunSyncDynamicReaderVariants(
                    optNoHeaders,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("1,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,-123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item2);
                                },
                                row2 =>
                                {
                                    Tuple<string, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(-123, typed.Item2);
                                }
                            );
                        }
                    }
                );

                // 17
                {
                    var row1Val =
                        new object[]
                        {
                        1,
                        Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"),
                        true,
                        false,
                        long.MaxValue,
                        0.123f,
                        (sbyte)-123,
                        (byte)128,
                        TimeSpan.FromMilliseconds(1234567890),

                        2,
                        Guid.Parse("77DEF02E-BDD6-4AF1-90F5-037596E08599"),
                        "blue",
                        _Tuple.Green,
                        ulong.MaxValue,
                        -999999.99m,
                        (short)-12300,
                        (sbyte)-2
                        };
                    var row1 = string.Join(",", row1Val);

                    RunSyncDynamicReaderVariants(
                        optNoHeaders,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader(row1))
                            using (var csv = config.CreateReader(reader))
                            {
                                var read = csv.ReadAll();

                                Assert.Collection(
                                    read,
                                    row1 =>
                                    {
                                        Tuple<
                                            int,
                                            Guid,
                                            bool,
                                            bool,
                                            long,
                                            float,
                                            sbyte,
                                            Tuple<
                                                byte,
                                                TimeSpan,

                                                int,
                                                Guid,
                                                string,
                                                _Tuple,
                                                ulong,
                                                Tuple<
                                                    decimal,
                                                    short,
                                                    sbyte
                                                >
                                            >
                                        > typed = row1;
                                        Assert.Equal(row1Val[0], typed.Item1);
                                        Assert.Equal(row1Val[1], typed.Item2);
                                        Assert.Equal(row1Val[2], typed.Item3);
                                        Assert.Equal(row1Val[3], typed.Item4);
                                        Assert.Equal(row1Val[4], typed.Item5);
                                        Assert.Equal(row1Val[5], typed.Item6);
                                        Assert.Equal(row1Val[6], typed.Item7);
                                        Assert.Equal(row1Val[7], typed.Rest.Item1);
                                        Assert.Equal(row1Val[8], typed.Rest.Item2);
                                        Assert.Equal(row1Val[9], typed.Rest.Item3);
                                        Assert.Equal(row1Val[10], typed.Rest.Item4);
                                        Assert.Equal(row1Val[11], typed.Rest.Item5);
                                        Assert.Equal(row1Val[12], typed.Rest.Item6);
                                        Assert.Equal(row1Val[13], typed.Rest.Item7);
                                        Assert.Equal(row1Val[14], typed.Rest.Rest.Item1);
                                        Assert.Equal(row1Val[15], typed.Rest.Rest.Item2);
                                        Assert.Equal(row1Val[16], typed.Rest.Rest.Item3);
                                    }
                                );
                            }
                        }
                    );
                }
            }
        }

        [Fact]
        public void ValueTuple()
        {
            // headers
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

                // one
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("A\r\n1\r\nfoo"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                },
                                row2 =>
                                {
                                    ValueTuple<string> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                }
                            );
                        }
                    }
                );

                // two
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("A,B\r\n1,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,-123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item2);
                                },
                                row2 =>
                                {
                                    ValueTuple<string, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(-123, typed.Item2);
                                }
                            );
                        }
                    }
                );

                // 17
                {
                    var row1Val =
                        new object[]
                        {
                            1,
                            Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"),
                            true,
                            false,
                            long.MaxValue,
                            0.123f,
                            (sbyte)-123,
                            (byte)128,
                            TimeSpan.FromMilliseconds(1234567890),

                            2,
                            Guid.Parse("77DEF02E-BDD6-4AF1-90F5-037596E08599"),
                            "blue",
                            _Tuple.Green,
                            ulong.MaxValue,
                            -999999.99m,
                            (short)-12300,
                            (sbyte)-2
                        };
                    var row1 = string.Join(",", row1Val);

                    RunSyncDynamicReaderVariants(
                        opts,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader($"A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q\r\n{row1}"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var read = csv.ReadAll();

                                Assert.Collection(
                                    read,
                                    row1 =>
                                    {
                                        ValueTuple<
                                            int,
                                            Guid,
                                            bool,
                                            bool,
                                            long,
                                            float,
                                            sbyte,
                                            ValueTuple<
                                                byte,
                                                TimeSpan,
                                                int,
                                                Guid,
                                                string,
                                                _Tuple,
                                                ulong,
                                                ValueTuple<
                                                    decimal,
                                                    short,
                                                    sbyte
                                                >
                                            >
                                        > typed = row1;
                                        Assert.Equal(row1Val[0], typed.Item1);
                                        Assert.Equal(row1Val[1], typed.Item2);
                                        Assert.Equal(row1Val[2], typed.Item3);
                                        Assert.Equal(row1Val[3], typed.Item4);
                                        Assert.Equal(row1Val[4], typed.Item5);
                                        Assert.Equal(row1Val[5], typed.Item6);
                                        Assert.Equal(row1Val[6], typed.Item7);
                                        Assert.Equal(row1Val[7], typed.Rest.Item1);
                                        Assert.Equal(row1Val[8], typed.Rest.Item2);
                                        Assert.Equal(row1Val[9], typed.Rest.Item3);
                                        Assert.Equal(row1Val[10], typed.Rest.Item4);
                                        Assert.Equal(row1Val[11], typed.Rest.Item5);
                                        Assert.Equal(row1Val[12], typed.Rest.Item6);
                                        Assert.Equal(row1Val[13], typed.Rest.Item7);
                                        Assert.Equal(row1Val[14], typed.Rest.Rest.Item1);
                                        Assert.Equal(row1Val[15], typed.Rest.Rest.Item2);
                                        Assert.Equal(row1Val[16], typed.Rest.Rest.Item3);
                                    }
                                );
                            }
                        }
                    );
                }
            }

            // no headers
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

                // one
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("1\r\nfoo"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                },
                                row2 =>
                                {
                                    ValueTuple<string> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                }
                            );
                        }
                    }
                );

                // two
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("1,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,-123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item2);
                                },
                                row2 =>
                                {
                                    ValueTuple<string, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(-123, typed.Item2);
                                }
                            );
                        }
                    }
                );

                // 17
                {
                    var row1Val =
                        new object[]
                        {
                            1,
                            Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"),
                            true,
                            false,
                            long.MaxValue,
                            0.123f,
                            (sbyte)-123,
                            (byte)128,
                            TimeSpan.FromMilliseconds(1234567890),

                            2,
                            Guid.Parse("77DEF02E-BDD6-4AF1-90F5-037596E08599"),
                            "blue",
                            _Tuple.Green,
                            ulong.MaxValue,
                            -999999.99m,
                            (short)-12300,
                            (sbyte)-2
                        };
                    var row1 = string.Join(",", row1Val);

                    RunSyncDynamicReaderVariants(
                        opts,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader(row1))
                            using (var csv = config.CreateReader(reader))
                            {
                                var read = csv.ReadAll();

                                Assert.Collection(
                                    read,
                                    row1 =>
                                    {
                                        ValueTuple<
                                            int,
                                            Guid,
                                            bool,
                                            bool,
                                            long,
                                            float,
                                            sbyte,
                                            ValueTuple<
                                                byte,
                                                TimeSpan,
                                                int,
                                                Guid,
                                                string,
                                                _Tuple,
                                                ulong,
                                                ValueTuple<
                                                    decimal,
                                                    short,
                                                    sbyte
                                                >
                                            >
                                        > typed = row1;
                                        Assert.Equal(row1Val[0], typed.Item1);
                                        Assert.Equal(row1Val[1], typed.Item2);
                                        Assert.Equal(row1Val[2], typed.Item3);
                                        Assert.Equal(row1Val[3], typed.Item4);
                                        Assert.Equal(row1Val[4], typed.Item5);
                                        Assert.Equal(row1Val[5], typed.Item6);
                                        Assert.Equal(row1Val[6], typed.Item7);
                                        Assert.Equal(row1Val[7], typed.Rest.Item1);
                                        Assert.Equal(row1Val[8], typed.Rest.Item2);
                                        Assert.Equal(row1Val[9], typed.Rest.Item3);
                                        Assert.Equal(row1Val[10], typed.Rest.Item4);
                                        Assert.Equal(row1Val[11], typed.Rest.Item5);
                                        Assert.Equal(row1Val[12], typed.Rest.Item6);
                                        Assert.Equal(row1Val[13], typed.Rest.Item7);
                                        Assert.Equal(row1Val[14], typed.Rest.Rest.Item1);
                                        Assert.Equal(row1Val[15], typed.Rest.Rest.Item2);
                                        Assert.Equal(row1Val[16], typed.Rest.Rest.Item3);
                                    }
                                );
                            }
                        }
                    );
                }
            }
        }

        class _POCO_Constructor
        {
            public int Prop1 { get; }
            public string Prop2 { get; }
            public DateTime Prop3 { get; }

            internal _POCO_Constructor(int p1, string p2, DateTime p3)
            {
                Prop1 = p1;
                Prop2 = p2;
                Prop3 = p3;
            }
        }

        [Fact]
        public void POCO_Constructor()
        {
            // headers
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var str = makeReader("A,B,C\r\n1,foo,2019-01-03"))
                        using (var csv = config.CreateReader(str))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    var lo = (_POCO_Constructor)row;

                                    Assert.Equal(1, lo.Prop1);
                                    Assert.Equal("foo", lo.Prop2);
                                    Assert.Equal(new DateTime(2019, 01, 03), lo.Prop3);
                                }
                            );
                        }
                    }
                );
            }

            // no headers
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var str = makeReader("1,foo,2019-01-03"))
                        using (var csv = config.CreateReader(str))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    var lo = (_POCO_Constructor)row;

                                    Assert.Equal(1, lo.Prop1);
                                    Assert.Equal("foo", lo.Prop2);
                                    Assert.Equal(new DateTime(2019, 01, 03), lo.Prop3);
                                }
                            );
                        }
                    }
                );
            }
        }

        class _POCO_Properties
        {
            public int A { get; set; }
            public string B { get; set; }
            internal DateTime C { get; set; }

            public _POCO_Properties() { }
        }

        [Fact]
        public void POCO_Properties()
        {
            // headers
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var str = makeReader("A,B,C\r\n1,foo,2019-01-03"))
                        using (var csv = config.CreateReader(str))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    var lo = (_POCO_Properties)row;

                                    Assert.Equal(1, lo.A);
                                    Assert.Equal("foo", lo.B);
                                    Assert.Equal(new DateTime(2019, 01, 03), lo.C);
                                }
                            );
                        }
                    }
                );
            }

            // no headers
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var str = makeReader("1,foo,2019-01-03"))
                        using (var csv = config.CreateReader(str))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    var lo = (_POCO_Properties)row;

                                    Assert.Equal(1, lo.A);
                                    Assert.Equal("foo", lo.B);
                                    Assert.Equal(new DateTime(2019, 01, 03), lo.C);
                                }
                            );
                        }
                    }
                );
            }
        }

        [Fact]
        public void DynamicRowDisposalOptions()
        {
            // dispose with reader
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        List<dynamic> read;

                        using (var str = makeReader("1,2,3"))
                        using (var csv = config.CreateReader(str))
                        {
                            read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    int a = row[0];
                                    int b = row[1];
                                    int c = row[2];

                                    Assert.Equal(1, a);
                                    Assert.Equal(2, b);
                                    Assert.Equal(3, c);
                                }
                            );
                        }

                        // explodes now that reader is disposed
                        Assert.Collection(
                            read,
                            row =>
                            {
                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    }
                );
            }

            // explicit disposal
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).Build();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        List<dynamic> read;

                        using (var str = makeReader("1,2,3"))
                        using (var csv = config.CreateReader(str))
                        {
                            read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    int a = row[0];
                                    int b = row[1];
                                    int c = row[2];

                                    Assert.Equal(1, a);
                                    Assert.Equal(2, b);
                                    Assert.Equal(3, c);
                                }
                            );
                        }

                        // still good after reader
                        Assert.Collection(
                            read,
                            row =>
                            {
                                int a = row[0];
                                int b = row[1];
                                int c = row[2];

                                Assert.Equal(1, a);
                                Assert.Equal(2, b);
                                Assert.Equal(3, c);
                            }
                        );

                        foreach (var r in read)
                        {
                            r.Dispose();
                        }

                        // explodes now that row are disposed
                        Assert.Collection(
                            read,
                            row =>
                            {
                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    }
                );
            }
        }

        [Fact]
        public void ReusingRows()
        {
            // both auto
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config1, makeReader1) =>
                    {
                        RunSyncDynamicReaderVariants(
                            opts,
                            (config2, makeReader2) =>
                            {
                                using (var r1 = makeReader1("1,2,3\r\n4,5,6"))
                                using (var r2 = makeReader2("7,8\r\n9,10"))
                                using (var csv1 = config1.CreateReader(r1))
                                using (var csv2 = config2.CreateReader(r2))
                                {
                                    dynamic row = null;
                                    Assert.True(csv1.TryReadWithReuse(ref row));
                                    Assert.Equal(1, (int)row[0]);
                                    Assert.Equal(2, (int)row[1]);
                                    Assert.Equal(3, (int)row[2]);

                                    Assert.True(csv2.TryReadWithReuse(ref row));
                                    Assert.Equal(7, (int)row[0]);
                                    Assert.Equal(8, (int)row[1]);

                                    Assert.True(csv1.TryReadWithReuse(ref row));
                                    Assert.Equal(4, (int)row[0]);
                                    Assert.Equal(5, (int)row[1]);
                                    Assert.Equal(6, (int)row[2]);

                                    Assert.True(csv2.TryReadWithReuse(ref row));
                                    Assert.Equal(9, (int)row[0]);
                                    Assert.Equal(10, (int)row[1]);

                                    Assert.False(csv1.TryReadWithReuse(ref row));
                                    Assert.False(csv2.TryReadWithReuse(ref row));
                                }
                            }
                        );
                    },
                    expectedRuns: 3
                );
            }

            // auto then explicitly
            {
                var opts1 = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();
                var opts2 = opts1.NewBuilder().WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).Build();
                RunSyncDynamicReaderVariants(
                    opts1,
                    (config1, makeReader1) =>
                    {
                        RunSyncDynamicReaderVariants(
                            opts2,
                            (config2, makeReader2) =>
                            {
                                dynamic row = null;

                                using (var r1 = makeReader1("1,2,3\r\n4,5,6"))
                                using (var r2 = makeReader2("7,8\r\n9,10"))
                                using (var csv1 = config1.CreateReader(r1))
                                using (var csv2 = config2.CreateReader(r2))
                                {
                                    Assert.True(csv1.TryReadWithReuse(ref row));
                                    Assert.Equal(1, (int)row[0]);
                                    Assert.Equal(2, (int)row[1]);
                                    Assert.Equal(3, (int)row[2]);

                                    Assert.True(csv2.TryReadWithReuse(ref row));
                                    Assert.Equal(7, (int)row[0]);
                                    Assert.Equal(8, (int)row[1]);

                                    Assert.True(csv1.TryReadWithReuse(ref row));
                                    Assert.Equal(4, (int)row[0]);
                                    Assert.Equal(5, (int)row[1]);
                                    Assert.Equal(6, (int)row[2]);

                                    Assert.True(csv2.TryReadWithReuse(ref row));
                                    Assert.Equal(9, (int)row[0]);
                                    Assert.Equal(10, (int)row[1]);
                                }

                                Assert.Equal(9, (int)row[0]);
                                Assert.Equal(10, (int)row[1]);

                                row.Dispose();

                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    },
                    expectedRuns: 3
                );
            }

            // explicitly then auto
            {
                var opts1 = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).Build();
                var opts2 = opts1.NewBuilder().WithDynamicRowDisposal(DynamicRowDisposal.OnReaderDispose).Build();
                RunSyncDynamicReaderVariants(
                    opts1,
                    (config1, makeReader1) =>
                    {
                        RunSyncDynamicReaderVariants(
                            opts2,
                            (config2, makeReader2) =>
                            {
                                dynamic row = null;

                                using (var r1 = makeReader1("1,2,3\r\n4,5,6"))
                                using (var r2 = makeReader2("7,8\r\n9,10"))
                                using (var csv1 = config1.CreateReader(r1))
                                using (var csv2 = config2.CreateReader(r2))
                                {
                                    Assert.True(csv1.TryReadWithReuse(ref row));
                                    Assert.Equal(1, (int)row[0]);
                                    Assert.Equal(2, (int)row[1]);
                                    Assert.Equal(3, (int)row[2]);

                                    Assert.True(csv2.TryReadWithReuse(ref row));
                                    Assert.Equal(7, (int)row[0]);
                                    Assert.Equal(8, (int)row[1]);

                                    Assert.True(csv2.TryReadWithReuse(ref row));
                                    Assert.Equal(9, (int)row[0]);
                                    Assert.Equal(10, (int)row[1]);


                                    Assert.True(csv1.TryReadWithReuse(ref row));
                                    Assert.Equal(4, (int)row[0]);
                                    Assert.Equal(5, (int)row[1]);
                                    Assert.Equal(6, (int)row[2]);
                                }

                                Assert.Equal(4, (int)row[0]);
                                Assert.Equal(5, (int)row[1]);
                                Assert.Equal(6, (int)row[2]);

                                row.Dispose();

                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    },
                    expectedRuns: 3
                );
            }

            // both explicitly
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).Build();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config1, makeReader1) =>
                    {
                        RunSyncDynamicReaderVariants(
                            opts,
                            (config2, makeReader2) =>
                            {
                                dynamic row = null;

                                using (var r1 = makeReader1("1,2,3\r\n4,5,6"))
                                using (var r2 = makeReader2("7,8\r\n9,10"))
                                using (var csv1 = config1.CreateReader(r1))
                                using (var csv2 = config2.CreateReader(r2))
                                {
                                    Assert.True(csv1.TryReadWithReuse(ref row));
                                    Assert.Equal(1, (int)row[0]);
                                    Assert.Equal(2, (int)row[1]);
                                    Assert.Equal(3, (int)row[2]);

                                    Assert.True(csv2.TryReadWithReuse(ref row));
                                    Assert.Equal(7, (int)row[0]);
                                    Assert.Equal(8, (int)row[1]);

                                    Assert.True(csv1.TryReadWithReuse(ref row));
                                    Assert.Equal(4, (int)row[0]);
                                    Assert.Equal(5, (int)row[1]);
                                    Assert.Equal(6, (int)row[2]);

                                    Assert.True(csv2.TryReadWithReuse(ref row));
                                    Assert.Equal(9, (int)row[0]);
                                    Assert.Equal(10, (int)row[1]);
                                }

                                Assert.Equal(9, (int)row[0]);
                                Assert.Equal(10, (int)row[1]);

                                row.Dispose();

                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    },
                    expectedRuns: 3
                );
            }
        }

        // async tests

        [Fact]
        public async Task MultiAsync()
        {
            var optsHeader = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

            // with headers
            await RunAsyncDynamicReaderVariants(
                optsHeader,
                async (config, makeReader) =>
                {
                    using (var reader = makeReader("A,B\r\nfoo,bar\r\n1,3.3\r\n2019-01-01,d"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var read = await csv.ReadAllAsync();

                        Assert.Collection(
                            read,
                            row1 =>
                            {
                                string a1 = row1.A;
                                string a2 = row1["A"];
                                string a3 = row1[0];

                                Assert.Equal("foo", a1);
                                Assert.Equal("foo", a2);
                                Assert.Equal("foo", a3);

                                string b1 = row1.B;
                                string b2 = row1["B"];
                                string b3 = row1[1];

                                Assert.Equal("bar", b1);
                                Assert.Equal("bar", b2);
                                Assert.Equal("bar", b3);
                            },
                            row2 =>
                            {
                                int a1 = row2.A;
                                int a2 = row2["A"];
                                int a3 = row2[0];

                                Assert.Equal(1, a1);
                                Assert.Equal(1, a2);
                                Assert.Equal(1, a3);

                                double b1 = row2.B;
                                double b2 = row2["B"];
                                double b3 = row2[1];

                                Assert.Equal(3.3, b1);
                                Assert.Equal(3.3, b2);
                                Assert.Equal(3.3, b3);
                            },
                            row3 =>
                            {
                                DateTime a1 = row3.A;
                                DateTime a2 = row3["A"];
                                DateTime a3 = row3[0];

                                Assert.Equal(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), a1);
                                Assert.Equal(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), a2);
                                Assert.Equal(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), a3);

                                char b1 = row3.B;
                                char b2 = row3["B"];
                                char b3 = row3[1];

                                Assert.Equal('d', b1);
                                Assert.Equal('d', b2);
                                Assert.Equal('d', b3);
                            }
                        );
                    }
                }
            );

            var optsNoHeader = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

            // no headers
            await RunAsyncDynamicReaderVariants(
                optsNoHeader,
                async (config, makeReader) =>
                {
                    using (var reader = makeReader("foo,bar\r\n1,3.3\r\n2019-01-01,d"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var read = await csv.ReadAllAsync();

                        Assert.Collection(
                            read,
                            row1 =>
                            {
                                string a1 = row1[0];

                                Assert.Equal("foo", a1);

                                string b1 = row1[1];

                                Assert.Equal("bar", b1);
                            },
                            row2 =>
                            {
                                int a1 = row2[0];

                                Assert.Equal(1, a1);

                                double b1 = row2[1];

                                Assert.Equal(3.3, b1);
                            },
                            row3 =>
                            {
                                DateTime a1 = row3[0];

                                Assert.Equal(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), a1);

                                char b1 = row3[1];

                                Assert.Equal('d', b1);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task SimpleAsync()
        {
            var optsHeader = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

            // with headers
            await RunAsyncDynamicReaderVariants(
                optsHeader,
                async (config, makeReader) =>
                {
                    using (var reader = makeReader("A,B\r\nfoo,bar"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var read = await csv.ReadAllAsync();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                string aIx = row[0];
                                string aName = row["A"];
                                string aMem = row.A;

                                Assert.Equal("foo", aIx);
                                Assert.Equal("foo", aName);
                                Assert.Equal("foo", aMem);

                                string bIx = row[1];
                                string bName = row["B"];
                                string bMem = row.B;

                                Assert.Equal("bar", bIx);
                                Assert.Equal("bar", bName);
                                Assert.Equal("bar", bMem);

                                // untyped enumerable
                                {
                                    System.Collections.IEnumerable e = row;
                                    Assert.Collection(
                                        e.Cast<dynamic>().Select(o => (string)o),
                                        a => Assert.Equal("foo", a),
                                        b => Assert.Equal("bar", b)
                                    );
                                }

                                // typed enumerable
                                {
                                    IEnumerable<string> e = row;
                                    Assert.Collection(
                                        e,
                                        a => Assert.Equal("foo", a),
                                        b => Assert.Equal("bar", b)
                                    );
                                }
                            }
                        );
                    }
                }
            );

            var optsNoHeader = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

            // no headers
            await RunAsyncDynamicReaderVariants(
                optsNoHeader,
                async (config, makeReader) =>
                {
                    using (var reader = makeReader("foo,bar"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var read = await csv.ReadAllAsync();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                string aIx = row[0];

                                Assert.Equal("foo", aIx);

                                string bIx = row[1];

                                Assert.Equal("bar", bIx);

                                // untyped enumerable
                                {
                                    System.Collections.IEnumerable e = row;
                                    Assert.Collection(
                                        e.Cast<dynamic>().Select(o => (string)o),
                                        a => Assert.Equal("foo", a),
                                        b => Assert.Equal("bar", b)
                                    );
                                }

                                // typed enumerable
                                {
                                    IEnumerable<string> e = row;
                                    Assert.Collection(
                                        e,
                                        a => Assert.Equal("foo", a),
                                        b => Assert.Equal("bar", b)
                                    );
                                }
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task ConversionsAsync()
        {
            var optsHeaders = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

            // with headers
            await RunAsyncDynamicReaderVariants(
                optsHeaders,
                async (config, makeReader) =>
                {
                    using (var reader = makeReader("A,B\r\n1,57DEC02E-BDD6-4AF1-90F5-037596E08500"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var read = await csv.ReadAllAsync();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                var a = row[0];
                                string aStr = a;
                                int aInt = a;
                                float aFloat = a;
                                _Conversions aC = a;

                                Assert.Equal("1", aStr);
                                Assert.Equal(1, aInt);
                                Assert.Equal(1f, aFloat);
                                Assert.Equal(1, aC.I);

                                var b = row.B;
                                string bStr = b;
                                Guid bGuid = b;

                                Assert.Equal("57DEC02E-BDD6-4AF1-90F5-037596E08500", bStr);
                                Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), bGuid);
                            }
                        );
                    }
                }
            );

            var optsNoHeaders = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

            // with no headers
            await RunAsyncDynamicReaderVariants(
                optsNoHeaders,
                async (config, makeReader) =>
                {
                    using (var reader = makeReader("1,57DEC02E-BDD6-4AF1-90F5-037596E08500"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var read = await csv.ReadAllAsync();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                var a = row[0];
                                string aStr = a;
                                int aInt = a;
                                float aFloat = a;
                                _Conversions aC = a;

                                Assert.Equal("1", aStr);
                                Assert.Equal(1, aInt);
                                Assert.Equal(1f, aFloat);
                                Assert.Equal(1, aC.I);

                                var b = row[1];
                                string bStr = b;
                                Guid bGuid = b;

                                Assert.Equal("57DEC02E-BDD6-4AF1-90F5-037596E08500", bStr);
                                Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), bGuid);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task TupleAsync()
        {
            var optWithHeaders = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

            // headers
            {
                // one
                await RunAsyncDynamicReaderVariants(
                    optWithHeaders,
                    async (config, makeReader) =>
                    {
                        using (var reader = makeReader("A\r\n1\r\nfoo"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                },
                                row2 =>
                                {
                                    Tuple<string> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                }
                            );
                        }
                    }
                );

                // two
                await RunAsyncDynamicReaderVariants(
                    optWithHeaders,
                    async (config, makeReader) =>
                    {
                        using (var reader = makeReader("A,B\r\n1,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,-123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item2);
                                },
                                row2 =>
                                {
                                    Tuple<string, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(-123, typed.Item2);
                                }
                            );
                        }
                    }
                );

                // 17
                {
                    var row1Val =
                        new object[]
                        {
                        1,
                        Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"),
                        true,
                        false,
                        long.MaxValue,
                        0.123f,
                        (sbyte)-123,
                        (byte)128,
                        TimeSpan.FromMilliseconds(1234567890),

                        2,
                        Guid.Parse("77DEF02E-BDD6-4AF1-90F5-037596E08599"),
                        "blue",
                        _Tuple.Green,
                        ulong.MaxValue,
                        -999999.99m,
                        (short)-12300,
                        (sbyte)-2
                        };
                    var row1 = string.Join(",", row1Val);

                    await RunAsyncDynamicReaderVariants(
                        optWithHeaders,
                        async (config, makeReader) =>
                        {
                            using (var reader = makeReader($"A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q\r\n{row1}"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var read = await csv.ReadAllAsync();

                                Assert.Collection(
                                    read,
                                    row1 =>
                                    {
                                        Tuple<
                                            int,
                                            Guid,
                                            bool,
                                            bool,
                                            long,
                                            float,
                                            sbyte,
                                            Tuple<
                                                byte,
                                                TimeSpan,

                                                int,
                                                Guid,
                                                string,
                                                _Tuple,
                                                ulong,
                                                Tuple<
                                                    decimal,
                                                    short,
                                                    sbyte
                                                >
                                            >
                                        > typed = row1;
                                        Assert.Equal(row1Val[0], typed.Item1);
                                        Assert.Equal(row1Val[1], typed.Item2);
                                        Assert.Equal(row1Val[2], typed.Item3);
                                        Assert.Equal(row1Val[3], typed.Item4);
                                        Assert.Equal(row1Val[4], typed.Item5);
                                        Assert.Equal(row1Val[5], typed.Item6);
                                        Assert.Equal(row1Val[6], typed.Item7);
                                        Assert.Equal(row1Val[7], typed.Rest.Item1);
                                        Assert.Equal(row1Val[8], typed.Rest.Item2);
                                        Assert.Equal(row1Val[9], typed.Rest.Item3);
                                        Assert.Equal(row1Val[10], typed.Rest.Item4);
                                        Assert.Equal(row1Val[11], typed.Rest.Item5);
                                        Assert.Equal(row1Val[12], typed.Rest.Item6);
                                        Assert.Equal(row1Val[13], typed.Rest.Item7);
                                        Assert.Equal(row1Val[14], typed.Rest.Rest.Item1);
                                        Assert.Equal(row1Val[15], typed.Rest.Rest.Item2);
                                        Assert.Equal(row1Val[16], typed.Rest.Rest.Item3);
                                    }
                                );
                            }
                        }
                    );
                }
            }

            var optNoHeaders = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

            // no headers
            {
                // one
                await RunAsyncDynamicReaderVariants(
                    optNoHeaders,
                    async (config, makeReader) =>
                    {
                        using (var reader = makeReader("1\r\nfoo"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                },
                                row2 =>
                                {
                                    Tuple<string> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                }
                            );
                        }
                    }
                );

                // two
                await RunAsyncDynamicReaderVariants(
                    optNoHeaders,
                    async (config, makeReader) =>
                    {
                        using (var reader = makeReader("1,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,-123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item2);
                                },
                                row2 =>
                                {
                                    Tuple<string, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(-123, typed.Item2);
                                }
                            );
                        }
                    }
                );

                // 17
                {
                    var row1Val =
                        new object[]
                        {
                        1,
                        Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"),
                        true,
                        false,
                        long.MaxValue,
                        0.123f,
                        (sbyte)-123,
                        (byte)128,
                        TimeSpan.FromMilliseconds(1234567890),

                        2,
                        Guid.Parse("77DEF02E-BDD6-4AF1-90F5-037596E08599"),
                        "blue",
                        _Tuple.Green,
                        ulong.MaxValue,
                        -999999.99m,
                        (short)-12300,
                        (sbyte)-2
                        };
                    var row1 = string.Join(",", row1Val);

                    await RunAsyncDynamicReaderVariants(
                        optNoHeaders,
                        async (config, makeReader) =>
                        {
                            using (var reader = makeReader(row1))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var read = await csv.ReadAllAsync();

                                Assert.Collection(
                                    read,
                                    row1 =>
                                    {
                                        Tuple<
                                            int,
                                            Guid,
                                            bool,
                                            bool,
                                            long,
                                            float,
                                            sbyte,
                                            Tuple<
                                                byte,
                                                TimeSpan,

                                                int,
                                                Guid,
                                                string,
                                                _Tuple,
                                                ulong,
                                                Tuple<
                                                    decimal,
                                                    short,
                                                    sbyte
                                                >
                                            >
                                        > typed = row1;
                                        Assert.Equal(row1Val[0], typed.Item1);
                                        Assert.Equal(row1Val[1], typed.Item2);
                                        Assert.Equal(row1Val[2], typed.Item3);
                                        Assert.Equal(row1Val[3], typed.Item4);
                                        Assert.Equal(row1Val[4], typed.Item5);
                                        Assert.Equal(row1Val[5], typed.Item6);
                                        Assert.Equal(row1Val[6], typed.Item7);
                                        Assert.Equal(row1Val[7], typed.Rest.Item1);
                                        Assert.Equal(row1Val[8], typed.Rest.Item2);
                                        Assert.Equal(row1Val[9], typed.Rest.Item3);
                                        Assert.Equal(row1Val[10], typed.Rest.Item4);
                                        Assert.Equal(row1Val[11], typed.Rest.Item5);
                                        Assert.Equal(row1Val[12], typed.Rest.Item6);
                                        Assert.Equal(row1Val[13], typed.Rest.Item7);
                                        Assert.Equal(row1Val[14], typed.Rest.Rest.Item1);
                                        Assert.Equal(row1Val[15], typed.Rest.Rest.Item2);
                                        Assert.Equal(row1Val[16], typed.Rest.Rest.Item3);
                                    }
                                );
                            }
                        }
                    );
                }
            }
        }

        [Fact]
        public async Task ValueTupleAsync()
        {
            // headers
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

                // one
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        using (var reader = makeReader("A\r\n1\r\nfoo"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                },
                                row2 =>
                                {
                                    ValueTuple<string> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                }
                            );
                        }
                    }
                );

                // two
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        using (var reader = makeReader("A,B\r\n1,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,-123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item2);
                                },
                                row2 =>
                                {
                                    ValueTuple<string, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(-123, typed.Item2);
                                }
                            );
                        }
                    }
                );

                // 17
                {
                    var row1Val =
                        new object[]
                        {
                            1,
                            Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"),
                            true,
                            false,
                            long.MaxValue,
                            0.123f,
                            (sbyte)-123,
                            (byte)128,
                            TimeSpan.FromMilliseconds(1234567890),

                            2,
                            Guid.Parse("77DEF02E-BDD6-4AF1-90F5-037596E08599"),
                            "blue",
                            _Tuple.Green,
                            ulong.MaxValue,
                            -999999.99m,
                            (short)-12300,
                            (sbyte)-2
                        };
                    var row1 = string.Join(",", row1Val);

                    await RunAsyncDynamicReaderVariants(
                        opts,
                        async (config, makeReader) =>
                        {
                            using (var reader = makeReader($"A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q\r\n{row1}"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var read = await csv.ReadAllAsync();

                                Assert.Collection(
                                    read,
                                    row1 =>
                                    {
                                        ValueTuple<
                                            int,
                                            Guid,
                                            bool,
                                            bool,
                                            long,
                                            float,
                                            sbyte,
                                            ValueTuple<
                                                byte,
                                                TimeSpan,
                                                int,
                                                Guid,
                                                string,
                                                _Tuple,
                                                ulong,
                                                ValueTuple<
                                                    decimal,
                                                    short,
                                                    sbyte
                                                >
                                            >
                                        > typed = row1;
                                        Assert.Equal(row1Val[0], typed.Item1);
                                        Assert.Equal(row1Val[1], typed.Item2);
                                        Assert.Equal(row1Val[2], typed.Item3);
                                        Assert.Equal(row1Val[3], typed.Item4);
                                        Assert.Equal(row1Val[4], typed.Item5);
                                        Assert.Equal(row1Val[5], typed.Item6);
                                        Assert.Equal(row1Val[6], typed.Item7);
                                        Assert.Equal(row1Val[7], typed.Rest.Item1);
                                        Assert.Equal(row1Val[8], typed.Rest.Item2);
                                        Assert.Equal(row1Val[9], typed.Rest.Item3);
                                        Assert.Equal(row1Val[10], typed.Rest.Item4);
                                        Assert.Equal(row1Val[11], typed.Rest.Item5);
                                        Assert.Equal(row1Val[12], typed.Rest.Item6);
                                        Assert.Equal(row1Val[13], typed.Rest.Item7);
                                        Assert.Equal(row1Val[14], typed.Rest.Rest.Item1);
                                        Assert.Equal(row1Val[15], typed.Rest.Rest.Item2);
                                        Assert.Equal(row1Val[16], typed.Rest.Rest.Item3);
                                    }
                                );
                            }
                        }
                    );
                }
            }

            // no headers
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

                // one
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        using (var reader = makeReader("1\r\nfoo"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                },
                                row2 =>
                                {
                                    ValueTuple<string> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                }
                            );
                        }
                    }
                );

                // two
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        using (var reader = makeReader("1,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,-123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item2);
                                },
                                row2 =>
                                {
                                    ValueTuple<string, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(-123, typed.Item2);
                                }
                            );
                        }
                    }
                );

                // 17
                {
                    var row1Val =
                        new object[]
                        {
                            1,
                            Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"),
                            true,
                            false,
                            long.MaxValue,
                            0.123f,
                            (sbyte)-123,
                            (byte)128,
                            TimeSpan.FromMilliseconds(1234567890),

                            2,
                            Guid.Parse("77DEF02E-BDD6-4AF1-90F5-037596E08599"),
                            "blue",
                            _Tuple.Green,
                            ulong.MaxValue,
                            -999999.99m,
                            (short)-12300,
                            (sbyte)-2
                        };
                    var row1 = string.Join(",", row1Val);

                    await RunAsyncDynamicReaderVariants(
                        opts,
                        async (config, makeReader) =>
                        {
                            using (var reader = makeReader(row1))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var read = await csv.ReadAllAsync();

                                Assert.Collection(
                                    read,
                                    row1 =>
                                    {
                                        ValueTuple<
                                            int,
                                            Guid,
                                            bool,
                                            bool,
                                            long,
                                            float,
                                            sbyte,
                                            ValueTuple<
                                                byte,
                                                TimeSpan,
                                                int,
                                                Guid,
                                                string,
                                                _Tuple,
                                                ulong,
                                                ValueTuple<
                                                    decimal,
                                                    short,
                                                    sbyte
                                                >
                                            >
                                        > typed = row1;
                                        Assert.Equal(row1Val[0], typed.Item1);
                                        Assert.Equal(row1Val[1], typed.Item2);
                                        Assert.Equal(row1Val[2], typed.Item3);
                                        Assert.Equal(row1Val[3], typed.Item4);
                                        Assert.Equal(row1Val[4], typed.Item5);
                                        Assert.Equal(row1Val[5], typed.Item6);
                                        Assert.Equal(row1Val[6], typed.Item7);
                                        Assert.Equal(row1Val[7], typed.Rest.Item1);
                                        Assert.Equal(row1Val[8], typed.Rest.Item2);
                                        Assert.Equal(row1Val[9], typed.Rest.Item3);
                                        Assert.Equal(row1Val[10], typed.Rest.Item4);
                                        Assert.Equal(row1Val[11], typed.Rest.Item5);
                                        Assert.Equal(row1Val[12], typed.Rest.Item6);
                                        Assert.Equal(row1Val[13], typed.Rest.Item7);
                                        Assert.Equal(row1Val[14], typed.Rest.Rest.Item1);
                                        Assert.Equal(row1Val[15], typed.Rest.Rest.Item2);
                                        Assert.Equal(row1Val[16], typed.Rest.Rest.Item3);
                                    }
                                );
                            }
                        }
                    );
                }
            }
        }

        [Fact]
        public async Task POCO_ConstructorAsync()
        {
            // headers
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        using (var str = makeReader("A,B,C\r\n1,foo,2019-01-03"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    var lo = (_POCO_Constructor)row;

                                    Assert.Equal(1, lo.Prop1);
                                    Assert.Equal("foo", lo.Prop2);
                                    Assert.Equal(new DateTime(2019, 01, 03), lo.Prop3);
                                }
                            );
                        }
                    }
                );
            }

            // no headers
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        using (var str = makeReader("1,foo,2019-01-03"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    var lo = (_POCO_Constructor)row;

                                    Assert.Equal(1, lo.Prop1);
                                    Assert.Equal("foo", lo.Prop2);
                                    Assert.Equal(new DateTime(2019, 01, 03), lo.Prop3);
                                }
                            );
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task POCO_PropertiesAsync()
        {
            // headers
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        using (var str = makeReader("A,B,C\r\n1,foo,2019-01-03"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    var lo = (_POCO_Properties)row;

                                    Assert.Equal(1, lo.A);
                                    Assert.Equal("foo", lo.B);
                                    Assert.Equal(new DateTime(2019, 01, 03), lo.C);
                                }
                            );
                        }
                    }
                );
            }

            // no headers
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        using (var str = makeReader("1,foo,2019-01-03"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    var lo = (_POCO_Properties)row;

                                    Assert.Equal(1, lo.A);
                                    Assert.Equal("foo", lo.B);
                                    Assert.Equal(new DateTime(2019, 01, 03), lo.C);
                                }
                            );
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task DynamicRowDisposalOptionsAsync()
        {
            // dispose with reader
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        List<dynamic> read;

                        using (var str = makeReader("1,2,3"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    int a = row[0];
                                    int b = row[1];
                                    int c = row[2];

                                    Assert.Equal(1, a);
                                    Assert.Equal(2, b);
                                    Assert.Equal(3, c);
                                }
                            );
                        }

                        // explodes now that reader is disposed
                        Assert.Collection(
                            read,
                            row =>
                            {
                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    }
                );
            }

            // explicit disposal
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).Build();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        List<dynamic> read;

                        using (var str = makeReader("1,2,3"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    int a = row[0];
                                    int b = row[1];
                                    int c = row[2];

                                    Assert.Equal(1, a);
                                    Assert.Equal(2, b);
                                    Assert.Equal(3, c);
                                }
                            );
                        }

                        // still good after reader
                        Assert.Collection(
                            read,
                            row =>
                            {
                                int a = row[0];
                                int b = row[1];
                                int c = row[2];

                                Assert.Equal(1, a);
                                Assert.Equal(2, b);
                                Assert.Equal(3, c);
                            }
                        );

                        foreach (var r in read)
                        {
                            r.Dispose();
                        }

                        // explodes now that row are disposed
                        Assert.Collection(
                            read,
                            row =>
                            {
                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    }
                );
            }
        }

        [Fact]
        public async Task ReusingRowsAsync()
        {
            // both auto
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config1, makeReader1) =>
                    {
                        await RunAsyncDynamicReaderVariants(
                            opts,
                            async (config2, makeReader2) =>
                            {
                                using (var r1 = makeReader1("1,2,3\r\n4,5,6"))
                                using (var r2 = makeReader2("7,8\r\n9,10"))
                                await using (var csv1 = config1.CreateAsyncReader(r1))
                                await using (var csv2 = config2.CreateAsyncReader(r2))
                                {
                                    dynamic row = null;
                                    var res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(1, (int)row[0]);
                                    Assert.Equal(2, (int)row[1]);
                                    Assert.Equal(3, (int)row[2]);

                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(7, (int)row[0]);
                                    Assert.Equal(8, (int)row[1]);

                                    res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(4, (int)row[0]);
                                    Assert.Equal(5, (int)row[1]);
                                    Assert.Equal(6, (int)row[2]);

                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(9, (int)row[0]);
                                    Assert.Equal(10, (int)row[1]);

                                    res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.False(res.HasValue);
                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.False(res.HasValue);
                                }
                            }
                        );
                    },
                    expectedRuns: 3
                );
            }

            // auto then explicitly
            {
                var opts1 = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();
                var opts2 = opts1.NewBuilder().WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).Build();
                await RunAsyncDynamicReaderVariants(
                    opts1,
                    async (config1, makeReader1) =>
                    {
                        await RunAsyncDynamicReaderVariants(
                            opts2,
                            async (config2, makeReader2) =>
                            {
                                dynamic row = null;

                                using (var r1 = makeReader1("1,2,3\r\n4,5,6"))
                                using (var r2 = makeReader2("7,8\r\n9,10"))
                                await using (var csv1 = config1.CreateAsyncReader(r1))
                                await using (var csv2 = config2.CreateAsyncReader(r2))
                                {
                                    var res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(1, (int)row[0]);
                                    Assert.Equal(2, (int)row[1]);
                                    Assert.Equal(3, (int)row[2]);

                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(7, (int)row[0]);
                                    Assert.Equal(8, (int)row[1]);

                                    res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(4, (int)row[0]);
                                    Assert.Equal(5, (int)row[1]);
                                    Assert.Equal(6, (int)row[2]);

                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(9, (int)row[0]);
                                    Assert.Equal(10, (int)row[1]);
                                }

                                Assert.Equal(9, (int)row[0]);
                                Assert.Equal(10, (int)row[1]);

                                row.Dispose();

                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    },
                    expectedRuns: 3
                );
            }

            // explicitly then auto
            {
                var opts1 = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).Build();
                var opts2 = opts1.NewBuilder().WithDynamicRowDisposal(DynamicRowDisposal.OnReaderDispose).Build();
                await RunAsyncDynamicReaderVariants(
                    opts1,
                    async (config1, makeReader1) =>
                    {
                        await RunAsyncDynamicReaderVariants(
                            opts2,
                            async (config2, makeReader2) =>
                            {
                                dynamic row = null;

                                using (var r1 = makeReader1("1,2,3\r\n4,5,6"))
                                using (var r2 = makeReader2("7,8\r\n9,10"))
                                await using (var csv1 = config1.CreateAsyncReader(r1))
                                await using (var csv2 = config2.CreateAsyncReader(r2))
                                {
                                    var res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(1, (int)row[0]);
                                    Assert.Equal(2, (int)row[1]);
                                    Assert.Equal(3, (int)row[2]);

                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(7, (int)row[0]);
                                    Assert.Equal(8, (int)row[1]);

                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(9, (int)row[0]);
                                    Assert.Equal(10, (int)row[1]);


                                    res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(4, (int)row[0]);
                                    Assert.Equal(5, (int)row[1]);
                                    Assert.Equal(6, (int)row[2]);
                                }

                                Assert.Equal(4, (int)row[0]);
                                Assert.Equal(5, (int)row[1]);
                                Assert.Equal(6, (int)row[2]);

                                row.Dispose();

                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    },
                    expectedRuns: 3
                );
            }

            // both explicitly
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).Build();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config1, makeReader1) =>
                    {
                        await RunAsyncDynamicReaderVariants(
                            opts,
                            async (config2, makeReader2) =>
                            {
                                dynamic row = null;

                                using (var r1 = makeReader1("1,2,3\r\n4,5,6"))
                                using (var r2 = makeReader2("7,8\r\n9,10"))
                                await using (var csv1 = config1.CreateAsyncReader(r1))
                                await using (var csv2 = config2.CreateAsyncReader(r2))
                                {
                                    var res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(1, (int)row[0]);
                                    Assert.Equal(2, (int)row[1]);
                                    Assert.Equal(3, (int)row[2]);

                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(7, (int)row[0]);
                                    Assert.Equal(8, (int)row[1]);

                                    res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(4, (int)row[0]);
                                    Assert.Equal(5, (int)row[1]);
                                    Assert.Equal(6, (int)row[2]);

                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(9, (int)row[0]);
                                    Assert.Equal(10, (int)row[1]);
                                }

                                Assert.Equal(9, (int)row[0]);
                                Assert.Equal(10, (int)row[1]);

                                row.Dispose();

                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    },
                    expectedRuns: 3
                );
            }
        }
    }
}