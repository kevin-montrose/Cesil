using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Cesil.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Cesil.Tests
{
    public class GenerateDeserializerGenerationTests
    {
        [Fact]
        public async Task InitOnlyAsync()
        {
            // simple
            {
                var type = await RunSourceGeneratorAsync(
                    "Foo.InitOnly",
                    @"
using Cesil;

namespace Foo
{
    [GenerateDeserializer]
    public class InitOnly
    {
        public int Bar { get; init; }
    }
}");

                var rows = Read(type, "Bar\r\n1\r\n132");

                Assert.Collection(
                    rows,
                    r1 =>
                    {
                        Assert.Equal(1, (int)r1.Bar);
                    },
                    r2 =>
                    {
                        Assert.Equal(132, (int)r2.Bar);
                    }
                );
            }

            // multiple
            {
                var type = await RunSourceGeneratorAsync(
                    "Foo.InitOnly",
                    @"
using Cesil;

namespace Foo
{
    [GenerateDeserializer]
    public class InitOnly
    {
        public int Foo { get; init; }
        public int Bar { get; init; }
    }
}");

                // both set
                {
                    var rows = Read(type, "Foo,Bar\r\n1,2\r\n3,4");

                    Assert.Collection(
                        rows,
                        r1 =>
                        {
                            Assert.Equal(1, (int)r1.Foo);
                            Assert.Equal(2, (int)r1.Bar);
                        },
                        r2 =>
                        {
                            Assert.Equal(3, (int)r2.Foo);
                            Assert.Equal(4, (int)r2.Bar);
                        }
                    );
                }

                // only foo
                {
                    var rows = Read(type, "Foo\r\n1\r\n3");

                    Assert.Collection(
                        rows,
                        r1 =>
                        {
                            Assert.Equal(1, (int)r1.Foo);
                            Assert.Equal(0, (int)r1.Bar);
                        },
                        r2 =>
                        {
                            Assert.Equal(3, (int)r2.Foo);
                            Assert.Equal(0, (int)r2.Bar);
                        }
                    );
                }

                // only bar
                {
                    var rows = Read(type, "Bar\r\n1\r\n3");

                    Assert.Collection(
                        rows,
                        r1 =>
                        {
                            Assert.Equal(1, (int)r1.Bar);
                            Assert.Equal(0, (int)r1.Foo);
                        },
                        r2 =>
                        {
                            Assert.Equal(3, (int)r2.Bar);
                            Assert.Equal(0, (int)r2.Foo);
                        }
                    );
                }
            }

            // both init and regular
            {
                var type = await RunSourceGeneratorAsync(
                    "Foo.InitOnly",
                    @"
using Cesil;

namespace Foo
{
    [GenerateDeserializer]
    public class InitOnly
    {
        public int Foo { get; init; }
        public int Bar { get; set; }
    }
}");

                // both set
                {
                    var rows = Read(type, "Foo,Bar\r\n1,2\r\n3,4");

                    Assert.Collection(
                        rows,
                        r1 =>
                        {
                            Assert.Equal(1, (int)r1.Foo);
                            Assert.Equal(2, (int)r1.Bar);
                        },
                        r2 =>
                        {
                            Assert.Equal(3, (int)r2.Foo);
                            Assert.Equal(4, (int)r2.Bar);
                        }
                    );
                }

                // only foo
                {
                    var rows = Read(type, "Foo\r\n1\r\n3");

                    Assert.Collection(
                        rows,
                        r1 =>
                        {
                            Assert.Equal(1, (int)r1.Foo);
                            Assert.Equal(0, (int)r1.Bar);
                        },
                        r2 =>
                        {
                            Assert.Equal(3, (int)r2.Foo);
                            Assert.Equal(0, (int)r2.Bar);
                        }
                    );
                }

                // only bar
                {
                    var rows = Read(type, "Bar\r\n1\r\n3");

                    Assert.Collection(
                        rows,
                        r1 =>
                        {
                            Assert.Equal(1, (int)r1.Bar);
                            Assert.Equal(0, (int)r1.Foo);
                        },
                        r2 =>
                        {
                            Assert.Equal(3, (int)r2.Bar);
                            Assert.Equal(0, (int)r2.Foo);
                        }
                    );
                }
            }
        }

        [Fact]
        public async Task InitOnlyTypeDescriberAsync()
        {
            // public
            {
                var type = await RunSourceGeneratorAsync(
                    "Foo.InitOnlyTypeDescriberAsync",
                    @"
using Cesil;

namespace Foo
{
    [GenerateDeserializer]
    public class InitOnlyTypeDescriberAsync
    {
        public int Foo { get; init; }
    }
}"
                    );

                var expectedSetter = type.GetPropertyNonNull("Foo", BindingFlags.Public | BindingFlags.Instance).SetMethod;

                var members = TypeDescribers.AheadOfTime.EnumerateMembersToDeserialize(type.GetTypeInfo());
                Assert.Collection(
                    members,
                    foo =>
                    {
                        Assert.Equal("Foo", foo.Name);
                        Assert.False(foo.IsRequired);
                        Assert.True(foo.IsBackedByGeneratedMethod);
                        Assert.False(foo.Reset.HasValue);

                        var setter = foo.Setter;
                        Assert.True(setter.Method.HasValue);
                        var setterMtd = setter.Method.Value;
                        Assert.Equal(expectedSetter, setterMtd);
                    }
                );
            }

            // internal
            {
                var type = await RunSourceGeneratorAsync(
                    "Foo.InitOnlyTypeDescriberAsync",
                    @"
using Cesil;

namespace Foo
{
    [GenerateDeserializer]
    public class InitOnlyTypeDescriberAsync
    {
        [DeserializerMember]
        internal int Foo { get; init; }
    }
}"
                    );

                var expectedSetter = type.GetPropertyNonNull("Foo", BindingFlags.NonPublic | BindingFlags.Instance).SetMethod;

                var members = TypeDescribers.AheadOfTime.EnumerateMembersToDeserialize(type.GetTypeInfo());
                Assert.Collection(
                    members,
                    foo =>
                    {
                        Assert.Equal("Foo", foo.Name);
                        Assert.False(foo.IsRequired);
                        Assert.True(foo.IsBackedByGeneratedMethod);
                        Assert.False(foo.Reset.HasValue);

                        var setter = foo.Setter;
                        Assert.True(setter.Method.HasValue);
                        var setterMtd = setter.Method.Value;
                        Assert.Equal(expectedSetter, setterMtd);
                    }
                );
            }
        }

        [Fact]
        public async Task SimpleAsync()
        {
            var type = await RunSourceGeneratorAsync(
                "Foo.ReadMe",
                @"
using System;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    public class ReadMe
    {
        [DeserializerMember(ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public int Bar { get; set; }
        [DeserializerMember(ParserType=typeof(ReadMe), ParserMethodName=nameof(ForString))]
        public string Fizz = """";
        
        private DateTime _Hello;
        [DeserializerMember(Name=""Hello"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForDateTime))]
        public void SomeMtd(DateTime dt) 
        { 
            _Hello = dt;
        }

        public DateTime GetHello()
        => _Hello;

        public ReadMe() { }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        => int.TryParse(data, out val);

        public static bool ForString(ReadOnlySpan<char> data, in ReadContext ctx, out string val)
        {
            val = new string(data);
            return true;
        }

        public static bool ForDateTime(ReadOnlySpan<char> data, in ReadContext ctx, out DateTime val)
        => DateTime.TryParse(data, out val);
    }
}");

            var members = TypeDescribers.AheadOfTime.EnumerateMembersToDeserialize(type);
            Assert.Collection(
                members,
                bar =>
                {
                    Assert.True(bar.IsBackedByGeneratedMethod);
                    Assert.Equal("Bar", bar.Name);
                    Assert.False(bar.IsRequired);
                    Assert.Equal("__Column_0_Parser", bar.Parser.Method.Value.Name);
                    Assert.Equal("__Column_0_Setter", bar.Setter.Method.Value.Name);
                    Assert.False(bar.Reset.HasValue);
                },
                fizz =>
                {
                    Assert.True(fizz.IsBackedByGeneratedMethod);
                    Assert.Equal("Fizz", fizz.Name);
                    Assert.False(fizz.IsRequired);
                    Assert.Equal("__Column_1_Parser", fizz.Parser.Method.Value.Name);
                    Assert.Equal("__Column_1_Setter", fizz.Setter.Method.Value.Name);
                    Assert.False(fizz.Reset.HasValue);
                },
                hello =>
                {
                    Assert.True(hello.IsBackedByGeneratedMethod);
                    Assert.Equal("Hello", hello.Name);
                    Assert.False(hello.IsRequired);
                    Assert.Equal("__Column_2_Parser", hello.Parser.Method.Value.Name);
                    Assert.Equal("__Column_2_Setter", hello.Setter.Method.Value.Name);
                    Assert.False(hello.Reset.HasValue);
                }
            );

            var ip = TypeDescribers.AheadOfTime.GetInstanceProvider(type);
            Assert.Equal("__InstanceProvider", ip.Method.Value.Name);

            var rows = Read(type, "Bar,Fizz,Hello\r\n123,hello,12/18/2020 01:02:03\r\n456,world,01/02/0003 04:05:06");

            Assert.Collection(
                rows,
                r1 =>
                {
                    Assert.Equal(123, (int)r1.Bar);
                    Assert.Equal("hello", (string)r1.Fizz);
                    Assert.Equal(DateTime.Parse("12/18/2020 01:02:03", CultureInfo.InvariantCulture, DateTimeStyles.None), (DateTime)r1.GetHello());
                },
                r2 =>
                {
                    Assert.Equal(456, (int)r2.Bar);
                    Assert.Equal("world", (string)r2.Fizz);
                    Assert.Equal(DateTime.Parse("01/02/0003 04:05:06", CultureInfo.InvariantCulture, DateTimeStyles.None), (DateTime)r2.GetHello());
                }
            );

            var rowReordered = Read(type, "Hello,Fizz,Bar\r\n12/18/2020 01:02:03,hello,123\r\n01/02/0003 04:05:06,world,456");

            Assert.Collection(
                rowReordered,
                r1 =>
                {
                    Assert.Equal(123, (int)r1.Bar);
                    Assert.Equal("hello", (string)r1.Fizz);
                    Assert.Equal(DateTime.Parse("12/18/2020 01:02:03", CultureInfo.InvariantCulture, DateTimeStyles.None), (DateTime)r1.GetHello());
                },
                r2 =>
                {
                    Assert.Equal(456, (int)r2.Bar);
                    Assert.Equal("world", (string)r2.Fizz);
                    Assert.Equal(DateTime.Parse("01/02/0003 04:05:06", CultureInfo.InvariantCulture, DateTimeStyles.None), (DateTime)r2.GetHello());
                }
            );
        }

        [Fact]
        public async Task MethodInstanceProviderAsync()
        {
            var type = await RunSourceGeneratorAsync(
                "Foo.ReadMe",
                @"
using System;
using Cesil;

namespace Foo 
{   
    internal class ReadMeMaker
    {
        internal static bool TryGetReadMe(in ReadContext ctx, out ReadMe row)
        {
            row = new ReadMe(ctx.RowNumber);
            return true;
        }
    }

    [GenerateDeserializer(
        InstanceProviderType = typeof(ReadMeMaker), 
        InstanceProviderMethodName = nameof(ReadMeMaker.TryGetReadMe)
    )]
    public class ReadMe
    {
        [DeserializerMember(ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public int Bar { get; set; }
        [DeserializerMember(ParserType=typeof(ReadMe), ParserMethodName=nameof(ForString))]
        public string Fizz = """";
        
        private DateTime _Hello;
        [DeserializerMember(Name=""Hello"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForDateTime))]
        public void SomeMtd(DateTime dt) 
        { 
            _Hello = dt;
        }

        public DateTime GetHello()
        => _Hello;

        private readonly int RowNumber;

        public ReadMe(int rowNum)
        {
            RowNumber = rowNum;
        }

        public int GetRowNumber()
        => RowNumber;

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        => int.TryParse(data, out val);

        public static bool ForString(ReadOnlySpan<char> data, in ReadContext ctx, out string val)
        {
            val = new string(data);
            return true;
        }

        public static bool ForDateTime(ReadOnlySpan<char> data, in ReadContext ctx, out DateTime val)
        => DateTime.TryParse(data, out val);
    }
}");

            var members = TypeDescribers.AheadOfTime.EnumerateMembersToDeserialize(type);
            Assert.Collection(
                members,
                bar =>
                {
                    Assert.True(bar.IsBackedByGeneratedMethod);
                    Assert.Equal("Bar", bar.Name);
                    Assert.False(bar.IsRequired);
                    Assert.Equal("__Column_0_Parser", bar.Parser.Method.Value.Name);
                    Assert.Equal("__Column_0_Setter", bar.Setter.Method.Value.Name);
                    Assert.False(bar.Reset.HasValue);
                },
                fizz =>
                {
                    Assert.True(fizz.IsBackedByGeneratedMethod);
                    Assert.Equal("Fizz", fizz.Name);
                    Assert.False(fizz.IsRequired);
                    Assert.Equal("__Column_1_Parser", fizz.Parser.Method.Value.Name);
                    Assert.Equal("__Column_1_Setter", fizz.Setter.Method.Value.Name);
                    Assert.False(fizz.Reset.HasValue);
                },
                hello =>
                {
                    Assert.True(hello.IsBackedByGeneratedMethod);
                    Assert.Equal("Hello", hello.Name);
                    Assert.False(hello.IsRequired);
                    Assert.Equal("__Column_2_Parser", hello.Parser.Method.Value.Name);
                    Assert.Equal("__Column_2_Setter", hello.Setter.Method.Value.Name);
                    Assert.False(hello.Reset.HasValue);
                }
            );

            var ip = TypeDescribers.AheadOfTime.GetInstanceProvider(type);
            Assert.Equal("__InstanceProvider", ip.Method.Value.Name);

            var rows = Read(type, "Bar,Fizz,Hello\r\n123,hello,12/18/2020 01:02:03\r\n456,world,01/02/0003 04:05:06");

            Assert.Collection(
                rows,
                r1 =>
                {
                    Assert.Equal(123, (int)r1.Bar);
                    Assert.Equal("hello", (string)r1.Fizz);
                    Assert.Equal(DateTime.Parse("12/18/2020 01:02:03", CultureInfo.InvariantCulture, DateTimeStyles.None), (DateTime)r1.GetHello());
                    Assert.Equal(0, (int)r1.GetRowNumber());
                },
                r2 =>
                {
                    Assert.Equal(456, (int)r2.Bar);
                    Assert.Equal("world", (string)r2.Fizz);
                    Assert.Equal(DateTime.Parse("01/02/0003 04:05:06", CultureInfo.InvariantCulture, DateTimeStyles.None), (DateTime)r2.GetHello());
                    Assert.Equal(1, (int)r2.GetRowNumber());
                }
            );
        }

        [Fact]
        public async Task MissingRequiredColumnsAsync()
        {
            var type = await RunSourceGeneratorAsync(
                "Foo.ReadMe",
                @"
using System;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    public class ReadMe
    {
        [DeserializerMember(ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public int Bar { get; set; }
        [DeserializerMember(ParserType=typeof(ReadMe), ParserMethodName=nameof(ForString), MemberRequired = MemberRequired.Yes)]
        public string Fizz = """";
        
        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        => int.TryParse(data, out val);

        public static bool ForString(ReadOnlySpan<char> data, in ReadContext ctx, out string val)
        {
            val = new string(data);
            return true;
        }
    }
}");

            var members = TypeDescribers.AheadOfTime.EnumerateMembersToDeserialize(type);
            Assert.Collection(
                members,
                bar =>
                {
                    Assert.True(bar.IsBackedByGeneratedMethod);
                    Assert.Equal("Bar", bar.Name);
                    Assert.False(bar.IsRequired);
                    Assert.Equal("__Column_0_Parser", bar.Parser.Method.Value.Name);
                    Assert.Equal("__Column_0_Setter", bar.Setter.Method.Value.Name);
                    Assert.False(bar.Reset.HasValue);
                },
                fizz =>
                {
                    Assert.True(fizz.IsBackedByGeneratedMethod);
                    Assert.Equal("Fizz", fizz.Name);
                    Assert.True(fizz.IsRequired);
                    Assert.Equal("__Column_1_Parser", fizz.Parser.Method.Value.Name);
                    Assert.Equal("__Column_1_Setter", fizz.Setter.Method.Value.Name);
                    Assert.False(fizz.Reset.HasValue);
                }
            );

            var rows = Read(type, "Bar,Fizz\r\n123,hello\r\n456,world");

            Assert.Collection(
                rows,
                r1 =>
                {
                    Assert.Equal(123, (int)r1.Bar);
                    Assert.Equal("hello", (string)r1.Fizz);
                },
                r2 =>
                {
                    Assert.Equal(456, (int)r2.Bar);
                    Assert.Equal("world", (string)r2.Fizz);
                }
            );

            Assert.Throws<SerializationException>(() => Read(type, "Bar,Fizz\r\n123,hello\r\n456"));
        }

        [Fact]
        public async Task ConstructorInstanceProviderAsync()
        {
            // simple, just one column
            {
                var type = await RunSourceGeneratorAsync(
                    "Foo.ReadMe",
                    @"
using System;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    public class ReadMe
    {
        public int Foo;

        [DeserializerInstanceProvider]
        internal ReadMe(
            [DeserializerMember(Name = ""ConsParam"", ParserType = typeof(ReadMe), ParserMethodName=nameof(ForInt))]
            int foo
        )
        {
            Foo = foo;
        }
        
        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            if(int.TryParse(data, out val))
            {
                val *= 2; // different just to make sure we're using this method
                return true;
            }

            return false;
        }
    }
}");

                var ip = TypeDescribers.AheadOfTime.GetInstanceProvider(type);
                Assert.True(ip.Constructor.HasValue);
                Assert.True(ip.ConstructorTakesParameters);

                var members = TypeDescribers.AheadOfTime.EnumerateMembersToDeserialize(type);
                Assert.Collection(
                    members,
                    consParam =>
                    {
                        Assert.True(consParam.IsBackedByGeneratedMethod);
                        Assert.Equal("ConsParam", consParam.Name);
                        Assert.True(consParam.IsRequired);
                        Assert.Equal("__Column_0_Parser", consParam.Parser.Method.Value.Name);
                        Assert.True(consParam.Setter.ConstructorParameter.HasValue);
                        Assert.False(consParam.Reset.HasValue);
                    }
                );

                var rows = Read(type, "ConsParam\r\n123");

                Assert.Collection(
                    rows,
                    r1 =>
                    {
                        Assert.Equal(246, (int)r1.Foo);
                    }
                );
            }

            // multiple columns
            {
                var type = await RunSourceGeneratorAsync(
                    "Foo.ReadMe",
                    @"
using System;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    public class ReadMe
    {
        public int Foo;
        public double Bar;

        [DeserializerInstanceProvider]
        internal ReadMe(
            [DeserializerMember(Name = ""ConsParam1"", ParserType = typeof(ReadMe), ParserMethodName=nameof(ForInt))]
            int foo,
            [DeserializerMember(Name = ""ConsParam2"", ParserType = typeof(ReadMe), ParserMethodName=nameof(ForDouble))]
            double bar
        )
        {
            Foo = foo;
            Bar = bar;
        }
        
        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            if(int.TryParse(data, out val))
            {
                val *= 2; // different just to make sure we're using this method
                return true;
            }

            return false;
        }

        public static bool ForDouble(ReadOnlySpan<char> data, in ReadContext ctx, out double val)
        {
            if(double.TryParse(data, out val))
            {
                val *= -1; // different just to make sure we're using this method
                return true;
            }

            return false;
        }
    }
}");

                var ip = TypeDescribers.AheadOfTime.GetInstanceProvider(type);
                Assert.True(ip.Constructor.HasValue);
                Assert.True(ip.ConstructorTakesParameters);

                var members = TypeDescribers.AheadOfTime.EnumerateMembersToDeserialize(type);
                Assert.Collection(
                    members,
                    consParam1 =>
                    {
                        Assert.True(consParam1.IsBackedByGeneratedMethod);
                        Assert.Equal("ConsParam1", consParam1.Name);
                        Assert.True(consParam1.IsRequired);
                        Assert.Equal("__Column_0_Parser", consParam1.Parser.Method.Value.Name);
                        Assert.True(consParam1.Setter.ConstructorParameter.HasValue);
                        Assert.False(consParam1.Reset.HasValue);
                    },
                    consParam2 =>
                    {
                        Assert.True(consParam2.IsBackedByGeneratedMethod);
                        Assert.Equal("ConsParam2", consParam2.Name);
                        Assert.True(consParam2.IsRequired);
                        Assert.Equal("__Column_1_Parser", consParam2.Parser.Method.Value.Name);
                        Assert.True(consParam2.Setter.ConstructorParameter.HasValue);
                        Assert.False(consParam2.Reset.HasValue);
                    }
                );

                var rows1 = Read(type, "ConsParam1,ConsParam2\r\n123,1.234");

                Assert.Collection(
                    rows1,
                    r1 =>
                    {
                        Assert.Equal(246, (int)r1.Foo);
                        Assert.Equal(-double.Parse("1.234"), (double)r1.Bar);
                    }
                );

                var rows2 = Read(type, "ConsParam2,ConsParam1\r\n1.234,123\r\n35.678,456");

                Assert.Collection(
                    rows2,
                    r1 =>
                    {
                        Assert.Equal(246, (int)r1.Foo);
                        Assert.Equal(-double.Parse("1.234"), (double)r1.Bar);
                    },
                    r2 =>
                    {
                        Assert.Equal(456 * 2, (int)r2.Foo);
                        Assert.Equal(-double.Parse("35.678"), (double)r2.Bar);
                    }
                );
            }

            // mix of constructor params and not
            {
                var type = await RunSourceGeneratorAsync(
                    "Foo.ReadMe",
                    @"
using System;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    public class ReadMe
    {
        public int Foo;
        public double Bar;

        [DeserializerMember(Name = ""NonCons"", ParserType = typeof(ReadMe), ParserMethodName=nameof(ForString))]
        public string? Fizz { get; set; }

        [DeserializerInstanceProvider]
        internal ReadMe(
            [DeserializerMember(Name = ""ConsParam1"", ParserType = typeof(ReadMe), ParserMethodName=nameof(ForInt))]
            int foo,
            [DeserializerMember(Name = ""ConsParam2"", ParserType = typeof(ReadMe), ParserMethodName=nameof(ForDouble))]
            double bar
        )
        {
            Foo = foo;
            Bar = bar;
        }
        
        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            if(int.TryParse(data, out val))
            {
                val *= 2; // different just to make sure we're using this method
                return true;
            }

            return false;
        }

        public static bool ForDouble(ReadOnlySpan<char> data, in ReadContext ctx, out double val)
        {
            if(double.TryParse(data, out val))
            {
                val *= -1; // different just to make sure we're using this method
                return true;
            }

            return false;
        }

        public static bool ForString(ReadOnlySpan<char> data, in ReadContext ctx, out string val)
        {
            val = new string(data)+'c';
            return true;
        }
    }
}");

                var ip = TypeDescribers.AheadOfTime.GetInstanceProvider(type);
                Assert.True(ip.Constructor.HasValue);
                Assert.True(ip.ConstructorTakesParameters);

                var members = TypeDescribers.AheadOfTime.EnumerateMembersToDeserialize(type);
                Assert.Collection(
                    members,
                    nonCons =>
                    {
                        Assert.True(nonCons.IsBackedByGeneratedMethod);
                        Assert.Equal("NonCons", nonCons.Name);
                        Assert.False(nonCons.IsRequired);
                        Assert.Equal("__Column_0_Parser", nonCons.Parser.Method.Value.Name);
                        Assert.Equal("__Column_0_Setter", nonCons.Setter.Method.Value.Name);
                        Assert.False(nonCons.Reset.HasValue);
                    },
                    consParam1 =>
                    {
                        Assert.True(consParam1.IsBackedByGeneratedMethod);
                        Assert.Equal("ConsParam1", consParam1.Name);
                        Assert.True(consParam1.IsRequired);
                        Assert.Equal("__Column_1_Parser", consParam1.Parser.Method.Value.Name);
                        Assert.True(consParam1.Setter.ConstructorParameter.HasValue);
                        Assert.False(consParam1.Reset.HasValue);
                    },
                    consParam2 =>
                    {
                        Assert.True(consParam2.IsBackedByGeneratedMethod);
                        Assert.Equal("ConsParam2", consParam2.Name);
                        Assert.True(consParam2.IsRequired);
                        Assert.Equal("__Column_2_Parser", consParam2.Parser.Method.Value.Name);
                        Assert.True(consParam2.Setter.ConstructorParameter.HasValue);
                        Assert.False(consParam2.Reset.HasValue);
                    }
                );

                var rows1 = Read(type, "ConsParam1,ConsParam2,NonCons\r\n123,1.234,hello\r\n456,5.678");

                Assert.Collection(
                    rows1,
                    r1 =>
                    {
                        Assert.Equal(246, (int)r1.Foo);
                        Assert.Equal(-double.Parse("1.234"), (double)r1.Bar);
                        Assert.Equal("helloc", (string)r1.Fizz);
                    },
                    r2 =>
                    {
                        Assert.Equal(456 * 2, (int)r2.Foo);
                        Assert.Equal(-double.Parse("5.678"), (double)r2.Bar);
                        Assert.Null((string)r2.Fizz);
                    }
                );

                var rows2 = Read(type, "ConsParam2,NonCons,ConsParam1\r\n1.234,hello,123\r\n5.678,,456");

                Assert.Collection(
                    rows2,
                    r1 =>
                    {
                        Assert.Equal(246, (int)r1.Foo);
                        Assert.Equal(-double.Parse("1.234"), (double)r1.Bar);
                        Assert.Equal("helloc", (string)r1.Fizz);
                    },
                    r2 =>
                    {
                        Assert.Equal(456 * 2, (int)r2.Foo);
                        Assert.Equal(-double.Parse("5.678"), (double)r2.Bar);
                        Assert.Equal("c", (string)r2.Fizz);
                    }
                );
            }

            // with resets
            {
                var type = await RunSourceGeneratorAsync(
                    "Foo.ReadMe",
                    @"
using System;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    public class ReadMe
    {
        public bool FizzResetCalled = false;
        public static bool FooResetCalled = false;
        public static bool BarResetCalled = false;

        public int Foo;
        public double Bar;

        [DeserializerMember(Name = ""NonCons"", ParserType = typeof(ReadMe), ParserMethodName=nameof(ForString), ResetType = typeof(ReadMe), ResetMethodName=nameof(FizzReset))]
        public string? Fizz { get; set; }

        [DeserializerInstanceProvider]
        internal ReadMe(
            [DeserializerMember(Name = ""ConsParam1"", ParserType = typeof(ReadMe), ParserMethodName=nameof(ForInt), ResetType = typeof(ReadMe), ResetMethodName=nameof(FooReset))]
            int foo,
            [DeserializerMember(Name = ""ConsParam2"", ParserType = typeof(ReadMe), ParserMethodName=nameof(ForDouble), ResetType = typeof(ReadMe), ResetMethodName=nameof(BarReset))]
            double bar
        )
        {
            Foo = foo;
            Bar = bar;
        }
        
        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            if(int.TryParse(data, out val))
            {
                val *= 2; // different just to make sure we're using this method
                return true;
            }

            return false;
        }

        public static bool ForDouble(ReadOnlySpan<char> data, in ReadContext ctx, out double val)
        {
            if(double.TryParse(data, out val))
            {
                val *= -1; // different just to make sure we're using this method
                return true;
            }

            return false;
        }

        public static bool ForString(ReadOnlySpan<char> data, in ReadContext ctx, out string val)
        {
            val = new string(data)+'c';
            return true;
        }

        public void FizzReset(in ReadContext ctx)
        {
            this.FizzResetCalled = true;
        }

        public static void FooReset(in ReadContext ctx)
        {
            FooResetCalled = true;
        }

        public static void BarReset(in ReadContext ctx)
        {
            BarResetCalled = true;
        }
    }
}");

                var ip = TypeDescribers.AheadOfTime.GetInstanceProvider(type);
                Assert.True(ip.Constructor.HasValue);
                Assert.True(ip.ConstructorTakesParameters);

                var members = TypeDescribers.AheadOfTime.EnumerateMembersToDeserialize(type);
                Assert.Collection(
                    members,
                    nonCons =>
                    {
                        Assert.True(nonCons.IsBackedByGeneratedMethod);
                        Assert.Equal("NonCons", nonCons.Name);
                        Assert.False(nonCons.IsRequired);
                        Assert.Equal("__Column_0_Parser", nonCons.Parser.Method.Value.Name);
                        Assert.Equal("__Column_0_Setter", nonCons.Setter.Method.Value.Name);
                        Assert.Equal("__Column_0_Reset", nonCons.Reset.Value.Method.Value.Name);
                    },
                    consParam1 =>
                    {
                        Assert.True(consParam1.IsBackedByGeneratedMethod);
                        Assert.Equal("ConsParam1", consParam1.Name);
                        Assert.True(consParam1.IsRequired);
                        Assert.Equal("__Column_1_Parser", consParam1.Parser.Method.Value.Name);
                        Assert.True(consParam1.Setter.ConstructorParameter.HasValue);
                        Assert.Equal("__Column_1_Reset", consParam1.Reset.Value.Method.Value.Name);
                    },
                    consParam2 =>
                    {
                        Assert.True(consParam2.IsBackedByGeneratedMethod);
                        Assert.Equal("ConsParam2", consParam2.Name);
                        Assert.True(consParam2.IsRequired);
                        Assert.Equal("__Column_2_Parser", consParam2.Parser.Method.Value.Name);
                        Assert.True(consParam2.Setter.ConstructorParameter.HasValue);
                        Assert.Equal("__Column_2_Reset", consParam2.Reset.Value.Method.Value.Name);
                    }
                );

                var barResetCalledField = type.GetFieldNonNull("BarResetCalled", BindingFlagsConstants.PublicStatic);
                var fooResetCalledField = type.GetFieldNonNull("FooResetCalled", BindingFlagsConstants.PublicStatic);

                barResetCalledField.SetValue(null, false);
                fooResetCalledField.SetValue(null, false);
                var rows1 = Read(type, "ConsParam1,ConsParam2,NonCons\r\n123,1.234,hello\r\n456,5.678");

                Assert.Collection(
                    rows1,
                    r1 =>
                    {
                        Assert.Equal(246, (int)r1.Foo);
                        Assert.Equal(-double.Parse("1.234"), (double)r1.Bar);
                        Assert.Equal("helloc", (string)r1.Fizz);
                        Assert.True((bool)r1.FizzResetCalled);
                    },
                    r2 =>
                    {
                        Assert.Equal(456 * 2, (int)r2.Foo);
                        Assert.Equal(-double.Parse("5.678"), (double)r2.Bar);
                        Assert.Null((string)r2.Fizz);
                        Assert.False((bool)r2.FizzResetCalled);
                    }
                );

                Assert.True((bool)barResetCalledField.GetValue(null));
                Assert.True((bool)fooResetCalledField.GetValue(null));

                barResetCalledField.SetValue(null, false);
                fooResetCalledField.SetValue(null, false);
                var rows2 = Read(type, "ConsParam2,NonCons,ConsParam1\r\n1.234,hello,123\r\n5.678,,456");

                Assert.Collection(
                    rows2,
                    r1 =>
                    {
                        Assert.Equal(246, (int)r1.Foo);
                        Assert.Equal(-double.Parse("1.234"), (double)r1.Bar);
                        Assert.Equal("helloc", (string)r1.Fizz);
                        Assert.True((bool)r1.FizzResetCalled);
                    },
                    r2 =>
                    {
                        Assert.Equal(456 * 2, (int)r2.Foo);
                        Assert.Equal(-double.Parse("5.678"), (double)r2.Bar);
                        Assert.Equal("c", (string)r2.Fizz);
                        Assert.True((bool)r2.FizzResetCalled);
                    }
                );

                Assert.True((bool)barResetCalledField.GetValue(null));
                Assert.True((bool)fooResetCalledField.GetValue(null));
            }
        }

        [Fact]
        public async Task AllDefaultsAsync()
        {
            var type = await RunSourceGeneratorAsync(
                "Foo.Everything",
                @"
using System;
using Cesil;

namespace Foo 
{   
    [Flags]
    public enum WideRowFlagsEnum
    {
        Empty = 0,

        Hello = 1 << 0,
        World = 1 << 1
    }

    public enum WideRowEnum
    {
        None = 0,

        Foo = 1,
        Fizz = 2,
        Bar = 3
    }

    [GenerateDeserializer]
    public class Everything
    {
        [DeserializerMemberAttribute]
        public bool Bool { get; set; }
        [DeserializerMemberAttribute]
        public byte Byte { get; set; }
        [DeserializerMemberAttribute]
        public sbyte SByte { get; set; }
        [DeserializerMemberAttribute]
        public short Short { get; set; }
        [DeserializerMemberAttribute]
        public ushort UShort { get; set; }
        [DeserializerMemberAttribute]
        public int Int { get; set; }
        [DeserializerMemberAttribute]
        public uint UInt { get; set; }
        [DeserializerMemberAttribute]
        public long Long { get; set; }
        [DeserializerMemberAttribute]
        public ulong ULong { get; set; }
        [DeserializerMemberAttribute]
        public float Float { get; set; }
        [DeserializerMemberAttribute]
        public double Double { get; set; }
        [DeserializerMemberAttribute]
        public decimal Decimal { get; set; }
        [DeserializerMemberAttribute]
        public nint NInt { get; set; }
        [DeserializerMemberAttribute]
        public nuint NUInt { get; set; }

        [DeserializerMemberAttribute]
        public bool? NullableBool { get; set; }
        [DeserializerMemberAttribute]
        public byte? NullableByte { get; set; }
        [DeserializerMemberAttribute]
        public sbyte? NullableSByte { get; set; }
        [DeserializerMemberAttribute]
        public short? NullableShort { get; set; }
        [DeserializerMemberAttribute]
        public ushort? NullableUShort { get; set; }
        [DeserializerMemberAttribute]
        public int? NullableInt { get; set; }
        [DeserializerMemberAttribute]
        public uint? NullableUInt { get; set; }
        [DeserializerMemberAttribute]
        public long? NullableLong { get; set; }
        [DeserializerMemberAttribute]
        public ulong? NullableULong { get; set; }
        [DeserializerMemberAttribute]
        public float? NullableFloat { get; set; }
        [DeserializerMemberAttribute]
        public double? NullableDouble { get; set; }
        [DeserializerMemberAttribute]
        public decimal? NullableDecimal { get; set; }
        [DeserializerMemberAttribute]
        public nint? NullableNInt { get; set; }
        [DeserializerMemberAttribute]
        public nuint? NullableNUInt { get; set; }

        [DeserializerMemberAttribute]
        public string? String { get; set; }

        [DeserializerMemberAttribute]
        public char Char { get; set; }

        [DeserializerMemberAttribute]
        public char? NullableChar { get; set; }

        [DeserializerMemberAttribute]
        public Guid Guid { get; set; }
        [DeserializerMemberAttribute]
        public Guid? NullableGuid { get; set; }

        [DeserializerMemberAttribute]
        public DateTime DateTime { get; set; }
        [DeserializerMemberAttribute]
        public DateTimeOffset DateTimeOffset { get; set; }

        [DeserializerMemberAttribute]
        public DateTime? NullableDateTime { get; set; }
        [DeserializerMemberAttribute]
        public DateTimeOffset? NullableDateTimeOffset { get; set; }

        [DeserializerMemberAttribute]
        public Uri? Uri { get; set; }

        [DeserializerMemberAttribute]
        public TimeSpan TimeSpan { get; set; }

        [DeserializerMemberAttribute]
        public TimeSpan? NullableTimeSpan { get; set; }

        [DeserializerMemberAttribute]
        public WideRowEnum Enum { get; set; }
        [DeserializerMemberAttribute]
        public WideRowFlagsEnum FlagsEnum { get; set; }

        [DeserializerMemberAttribute]
        public WideRowEnum? NullableEnum { get; set; }
        [DeserializerMemberAttribute]
        public WideRowFlagsEnum? NullableFlagsEnum { get; set; }

        public Everything() { }
    }
}");
            var wideRowEnumType = type.Assembly.GetTypes().Single(t => t.Name == "WideRowEnum");
            var wideRowFlagsEnumType = type.Assembly.GetTypes().Single(t => t.Name == "WideRowFlagsEnum");

            var wideRowEnumValues = Enum.GetValues(wideRowEnumType);
            var wideRowFlagsEnumValues = Enum.GetValues(wideRowFlagsEnumType);

            var nullableWideRowEnumType = typeof(Nullable<>).MakeGenericType(wideRowEnumType);
            var nullableWideRowEnumHasValue = nullableWideRowEnumType.GetProperty("HasValue");
            var nullableWideRowEnumValue = nullableWideRowEnumType.GetProperty("Value");

            var nullableWideRowFlagsEnumType = typeof(Nullable<>).MakeGenericType(wideRowFlagsEnumType);
            var nullableWideRowFlagsEnumHasValue = nullableWideRowFlagsEnumType.GetProperty("HasValue");
            var nullableWideRowFlagsEnumValue = nullableWideRowFlagsEnumType.GetProperty("Value");

            var members = TypeDescribers.AheadOfTime.EnumerateMembersToDeserialize(type);
            var rows = Read(type, "Bool,Byte,SByte,Short,UShort,Int,UInt,Long,ULong,Float,Double,Decimal,NInt,NUInt,NullableBool,NullableByte,NullableSByte,NullableShort,NullableUShort,NullableInt,NullableUInt,NullableLong,NullableULong,NullableFloat,NullableDouble,NullableDecimal,NullableNInt,NullableNUInt,String,Char,NullableChar,Guid,NullableGuid,DateTime,DateTimeOffset,NullableDateTime,NullableDateTimeOffset,Uri,TimeSpan,NullableTimeSpan,Enum,FlagsEnum,NullableEnum,NullableFlagsEnum\r\nTrue,1,-1,-11,11,-111,111,-1111,1111,1.20000005,3.3999999999999999,4.5,-123,123,False,2,-2,-22,22,-222,222,-2222,2222,6.69999981,8.9000000000000004,0.1,-456,456,hello,a,b,6e3687af-99a8-4415-9cde-c0d90d182171,7e3687af-99a8-4415-9cde-c0d90d182171,2020-11-15 00:00:00Z,2020-11-15 00:00:00Z,2021-11-15 00:00:00Z,2021-11-15 00:00:00Z,https://example.com/example,01:02:03,04:05:06,None,Empty,Foo,Hello\r\nFalse,3,-3,-33,33,-333,333,-3333,3333,2.29999995,4.5,6.7,-789,789,,,,,,,,,,,,,,,,c,,8e3687af-99a8-4415-9cde-c0d90d182171,,2022-11-15 00:00:00Z,2022-11-15 00:00:00Z,,,,07:08:09,,Foo,Hello,,");

            Assert.Collection(
                rows,
                row1 =>
                {
                    Assert.True((bool)row1.Bool);
                    Assert.Equal((byte)1, (byte)row1.Byte);
                    Assert.Equal((sbyte)-1, (sbyte)row1.SByte);
                    Assert.Equal((short)-11, (short)row1.Short);
                    Assert.Equal((ushort)11, (ushort)row1.UShort);
                    Assert.Equal((int)-111, (int)row1.Int);
                    Assert.Equal((uint)111, (uint)row1.UInt);
                    Assert.Equal((long)-1111, (long)row1.Long);
                    Assert.Equal((ulong)1111, (ulong)row1.ULong);
                    Assert.Equal((float)1.2f, (float)row1.Float);
                    Assert.Equal((double)3.4, (double)row1.Double);
                    Assert.Equal((decimal)4.5m, (decimal)row1.Decimal);
                    Assert.Equal((nint)(-123), (nint)row1.NInt);
                    Assert.Equal((nuint)(123), (nuint)row1.NUInt);

                    Assert.Equal((bool?)false, (bool?)row1.NullableBool);
                    Assert.Equal((byte?)2, (byte?)row1.NullableByte);
                    Assert.Equal((sbyte?)-2, (sbyte?)row1.NullableSByte);
                    Assert.Equal((short?)-22, (short?)row1.NullableShort);
                    Assert.Equal((ushort?)22, (ushort?)row1.NullableUShort);
                    Assert.Equal((int?)-222, (int?)row1.NullableInt);
                    Assert.Equal((uint?)222, (uint?)row1.NullableUInt);
                    Assert.Equal((long?)-2222, (long?)row1.NullableLong);
                    Assert.Equal((ulong?)2222, (ulong?)row1.NullableULong);
                    Assert.Equal((float?)6.7f, (float?)row1.NullableFloat);
                    Assert.Equal((double?)8.9, (double?)row1.NullableDouble);
                    Assert.Equal((decimal?)0.1m, (decimal?)row1.NullableDecimal);
                    Assert.Equal((nint?)(-456), (nint?)row1.NullableNInt);
                    Assert.Equal((nuint?)456, (nuint?)row1.NullableNUInt);

                    Assert.Equal("hello", (string)row1.String);

                    Assert.Equal('a', (char)row1.Char);
                    Assert.Equal((char?)'b', (char?)row1.NullableChar);

                    var dt = ((DateTime)row1.DateTime).ToUniversalTime();
                    Assert.Equal(new DateTime(2020, 11, 15, 0, 0, 0, DateTimeKind.Utc), dt);
                    var dto = (DateTimeOffset)row1.DateTimeOffset;
                    Assert.Equal(new DateTimeOffset(2020, 11, 15, 0, 0, 0, TimeSpan.Zero), dto);

                    var ndt = ((DateTime?)row1.NullableDateTime)?.ToUniversalTime();
                    Assert.Equal((DateTime?)new DateTime(2021, 11, 15, 0, 0, 0, DateTimeKind.Utc), ndt);
                    var ndto = (DateTimeOffset?)row1.NullableDateTimeOffset;
                    Assert.Equal((DateTimeOffset?)new DateTimeOffset(2021, 11, 15, 0, 0, 0, TimeSpan.Zero), ndto);

                    Assert.Equal(new Uri("https://example.com/example"), (Uri)row1.Uri);

                    Assert.Equal(new TimeSpan(1, 2, 3), (TimeSpan)row1.TimeSpan);
                    Assert.Equal((TimeSpan?)new TimeSpan(4, 5, 6), (TimeSpan?)row1.NullableTimeSpan);

                    Assert.Equal(Guid.Parse("6E3687AF-99A8-4415-9CDE-C0D90D182171"), (Guid)row1.Guid);
                    Assert.Equal((Guid?)Guid.Parse("7E3687AF-99A8-4415-9CDE-C0D90D182171"), (Guid?)row1.NullableGuid);

                    Assert.Equal(wideRowEnumValues.GetValue(0), row1.Enum);
                    Assert.Equal(wideRowFlagsEnumValues.GetValue(0), row1.FlagsEnum);

                    var nullableEnum = row1.NullableEnum;
                    Assert.True((bool)nullableWideRowEnumHasValue.GetValue(nullableEnum));
                    var nullableEnumValue = nullableWideRowEnumValue.GetValue(nullableEnum);
                    Assert.Equal(wideRowEnumValues.GetValue(1), nullableEnumValue);

                    var nullableFlagsEnum = row1.NullableFlagsEnum;
                    Assert.True((bool)nullableWideRowFlagsEnumHasValue.GetValue(nullableFlagsEnum));
                    var nullableFlagsEnumValue = nullableWideRowFlagsEnumValue.GetValue(nullableFlagsEnum);
                    Assert.Equal(wideRowFlagsEnumValues.GetValue(1), nullableFlagsEnumValue);
                },
                row2 =>
                {
                    Assert.False((bool)row2.Bool);
                    Assert.Equal((byte)3, (byte)row2.Byte);
                    Assert.Equal((sbyte)-3, (sbyte)row2.SByte);
                    Assert.Equal((short)-33, (short)row2.Short);
                    Assert.Equal((ushort)33, (ushort)row2.UShort);
                    Assert.Equal((int)-333, (int)row2.Int);
                    Assert.Equal((uint)333, (uint)row2.UInt);
                    Assert.Equal((long)-3333, (long)row2.Long);
                    Assert.Equal((ulong)3333, (ulong)row2.ULong);
                    Assert.Equal((float)2.3f, (float)row2.Float);
                    Assert.Equal((double)4.5, (double)row2.Double);
                    Assert.Equal((decimal)6.7m, (decimal)row2.Decimal);
                    Assert.Equal((nint)(-789), (nint)row2.NInt);
                    Assert.Equal((nuint)789, (nuint)row2.NUInt);

                    Assert.Equal((bool?)null, (bool?)row2.NullableBool);
                    Assert.Equal((byte?)null, (byte?)row2.NullableByte);
                    Assert.Equal((sbyte?)null, (sbyte?)row2.NullableSByte);
                    Assert.Equal((short?)null, (short?)row2.NullableShort);
                    Assert.Equal((ushort?)null, (ushort?)row2.NullableUShort);
                    Assert.Equal((int?)null, (int?)row2.NullableInt);
                    Assert.Equal((uint?)null, (uint?)row2.NullableUInt);
                    Assert.Equal((long?)null, (long?)row2.NullableLong);
                    Assert.Equal((ulong?)null, (ulong?)row2.NullableULong);
                    Assert.Equal((float?)null, (float?)row2.NullableFloat);
                    Assert.Equal((double?)null, (double?)row2.NullableDouble);
                    Assert.Equal((decimal?)null, (decimal?)row2.NullableDecimal);
                    Assert.Equal((nint?)null, (nint?)row2.NullableNInt);
                    Assert.Equal((nuint?)null, (nuint?)row2.NullableNUInt);

                    Assert.Equal("", (string)row2.String);

                    Assert.Equal('c', (char)row2.Char);
                    Assert.Equal((char?)null, (char?)row2.NullableChar);

                    var dt = ((DateTime)row2.DateTime).ToUniversalTime();
                    Assert.Equal(new DateTime(2022, 11, 15, 0, 0, 0, DateTimeKind.Utc), dt);
                    var dto = (DateTimeOffset)row2.DateTimeOffset;
                    Assert.Equal(new DateTimeOffset(2022, 11, 15, 0, 0, 0, TimeSpan.Zero), dto);

                    var ndt = ((DateTime?)row2.NullableDateTime)?.ToUniversalTime();
                    Assert.Equal((DateTime?)null, ndt);
                    var ndto = (DateTimeOffset?)row2.NullableDateTimeOffset;
                    Assert.Equal((DateTimeOffset?)null, ndto);

                    Assert.True(Uri.TryCreate("", UriKind.RelativeOrAbsolute, out var shouldBeUri));

                    Assert.Equal(shouldBeUri, (Uri)row2.Uri);

                    Assert.Equal(new TimeSpan(7, 8, 9), (TimeSpan)row2.TimeSpan);
                    Assert.Equal((TimeSpan?)null, (TimeSpan?)row2.NullableTimeSpan);

                    Assert.Equal(Guid.Parse("8E3687AF-99A8-4415-9CDE-C0D90D182171"), (Guid)row2.Guid);
                    Assert.Equal((Guid?)null, (Guid?)row2.NullableGuid);

                    Assert.Equal(wideRowEnumValues.GetValue(1), row2.Enum);
                    Assert.Equal(wideRowFlagsEnumValues.GetValue(1), row2.FlagsEnum);

                    var nullableEnum = row2.NullableEnum;
                    Assert.Null(nullableEnum);

                    var nullableFlagsEnum = row2.NullableFlagsEnum;
                    Assert.Null(nullableFlagsEnum);
                }
            );
        }

        [Fact]
        public async Task ContextsAsync()
        {
            // non-constructor params, so "normal" reset behavior
            {
                var type = await RunSourceGeneratorAsync(
                        "Foo.ReadMe",
                        @"
using System;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    public class ReadMe
    {
        [DeserializerMember(ParserType = typeof(ReadMe), ParserMethodName=nameof(ParserForFizz), ResetType = typeof(ReadMe), ResetMethodName = nameof(ResetForFizz))]
        public string? Fizz { get; set; }
        [DeserializerMember(ParserType = typeof(ReadMe), ParserMethodName=nameof(ParserForBuzz), ResetType = typeof(ReadMe), ResetMethodName = nameof(ResetForBuzz))]
        public int Buzz { get; set; }
        [DeserializerMember(ParserType = typeof(ReadMe), ParserMethodName=nameof(ParserForFoo), ResetType = typeof(ReadMe), ResetMethodName = nameof(ResetForFoo))]
        public char Foo { get; set; }

        public static ReadContext? FizzParser;
        public ReadContext? FizzReset;

        public static ReadContext? BuzzParser;
        public ReadContext? BuzzReset;

        public static ReadContext? FooParser;
        public ReadContext? FooReset;

        internal static bool ParserForFizz(ReadOnlySpan<char> data, in ReadContext ctx, out string value)
        {
            FizzParser = ctx;
            value = new string(data);
            return true;
        }

        internal void ResetForFizz(in ReadContext ctx)
        {
            FizzReset = ctx;
        }

        internal static bool ParserForBuzz(ReadOnlySpan<char> data, in ReadContext ctx, out int value)
        {
            BuzzParser = ctx;
            
            return int.TryParse(data, out value);
        }

        internal void ResetForBuzz(in ReadContext ctx)
        {
            BuzzReset = ctx;
        }

        internal static bool ParserForFoo(ReadOnlySpan<char> data, in ReadContext ctx, out char value)
        {
            FooParser = ctx;
            
            value = data[0];
            return data.Length == 1;
        }

        internal void ResetForFoo(in ReadContext ctx)
        {
            FooReset = ctx;
        }
    }
}");
                var fizzParserField = type.GetFieldNonNull("FizzParser", BindingFlags.Public | BindingFlags.Static);
                var buzzParserField = type.GetFieldNonNull("BuzzParser", BindingFlags.Public | BindingFlags.Static);
                var fooParserField = type.GetFieldNonNull("FooParser", BindingFlags.Public | BindingFlags.Static);

                // in order
                {
                    var getRow = GetReadOne(type, "Fizz,Buzz,Foo\r\nhello,123,a\r\nworld,456,b");

                    // first row
                    var r1 = getRow();
                    Assert.NotNull(r1);

                    Assert.Equal("hello", (string)r1.Fizz);
                    var fizzParser1 = ((ReadContext?)fizzParserField.GetValue(null)).Value;
                    Assert.Equal(0, fizzParser1.Column.Index);
                    Assert.Equal("Fizz", fizzParser1.Column.Name);
                    Assert.Equal(0, fizzParser1.RowNumber);
                    var fizzReset1 = ((ReadContext?)r1.FizzReset).Value;
                    Assert.Equal(fizzParser1, fizzReset1);

                    Assert.Equal(123, (int)r1.Buzz);
                    var buzzParser1 = ((ReadContext?)buzzParserField.GetValue(null)).Value;
                    Assert.Equal(1, buzzParser1.Column.Index);
                    Assert.Equal("Buzz", buzzParser1.Column.Name);
                    Assert.Equal(0, buzzParser1.RowNumber);
                    var buzzReset1 = ((ReadContext?)r1.BuzzReset).Value;
                    Assert.Equal(buzzParser1, buzzReset1);

                    Assert.Equal('a', (char)r1.Foo);
                    var fooParser1 = ((ReadContext?)fooParserField.GetValue(null)).Value;
                    Assert.Equal(2, fooParser1.Column.Index);
                    Assert.Equal("Foo", fooParser1.Column.Name);
                    Assert.Equal(0, fooParser1.RowNumber);
                    var fooReset1 = ((ReadContext?)r1.FooReset).Value;
                    Assert.Equal(fooParser1, fooReset1);

                    // second row
                    var r2 = getRow();
                    Assert.NotNull(r2);

                    Assert.Equal("world", (string)r2.Fizz);
                    var fizzParser2 = ((ReadContext?)fizzParserField.GetValue(null)).Value;
                    Assert.Equal(0, fizzParser2.Column.Index);
                    Assert.Equal("Fizz", fizzParser2.Column.Name);
                    Assert.Equal(1, fizzParser2.RowNumber);
                    var fizzReset2 = ((ReadContext?)r2.FizzReset).Value;
                    Assert.Equal(fizzParser2, fizzReset2);

                    Assert.Equal(456, (int)r2.Buzz);
                    var buzzParser2 = ((ReadContext?)buzzParserField.GetValue(null)).Value;
                    Assert.Equal(1, buzzParser2.Column.Index);
                    Assert.Equal("Buzz", buzzParser2.Column.Name);
                    Assert.Equal(1, buzzParser2.RowNumber);
                    var buzzReset2 = ((ReadContext?)r2.BuzzReset).Value;
                    Assert.Equal(buzzParser2, buzzReset2);

                    Assert.Equal('b', (char)r2.Foo);
                    var fooParser2 = ((ReadContext?)fooParserField.GetValue(null)).Value;
                    Assert.Equal(2, fooParser2.Column.Index);
                    Assert.Equal("Foo", fooParser2.Column.Name);
                    Assert.Equal(1, fooParser2.RowNumber);
                    var fooReset2 = ((ReadContext?)r2.FooReset).Value;
                    Assert.Equal(fooParser2, fooReset2);

                    // done
                    Assert.Null(getRow());
                }

                fizzParserField.SetValue(null, default(ReadContext?));
                buzzParserField.SetValue(null, default(ReadContext?));
                fooParserField.SetValue(null, default(ReadContext?));

                // out of order
                {
                    var getRow = GetReadOne(type, "Fizz,Foo,Buzz\r\nhello,a,123\r\nworld,b,456");

                    // first row
                    var r1 = getRow();
                    Assert.NotNull(r1);

                    Assert.Equal("hello", (string)r1.Fizz);
                    var fizzParser1 = ((ReadContext?)fizzParserField.GetValue(null)).Value;
                    Assert.Equal(0, fizzParser1.Column.Index);
                    Assert.Equal("Fizz", fizzParser1.Column.Name);
                    Assert.Equal(0, fizzParser1.RowNumber);
                    var fizzReset1 = ((ReadContext?)r1.FizzReset).Value;
                    Assert.Equal(fizzParser1, fizzReset1);

                    Assert.Equal('a', (char)r1.Foo);
                    var fooParser1 = ((ReadContext?)fooParserField.GetValue(null)).Value;
                    Assert.Equal(1, fooParser1.Column.Index);
                    Assert.Equal("Foo", fooParser1.Column.Name);
                    Assert.Equal(0, fooParser1.RowNumber);
                    var fooReset1 = ((ReadContext?)r1.FooReset).Value;
                    Assert.Equal(fooParser1, fooReset1);

                    Assert.Equal(123, (int)r1.Buzz);
                    var buzzParser1 = ((ReadContext?)buzzParserField.GetValue(null)).Value;
                    Assert.Equal(2, buzzParser1.Column.Index);
                    Assert.Equal("Buzz", buzzParser1.Column.Name);
                    Assert.Equal(0, buzzParser1.RowNumber);
                    var buzzReset1 = ((ReadContext?)r1.BuzzReset).Value;
                    Assert.Equal(buzzParser1, buzzReset1);

                    // second row
                    var r2 = getRow();
                    Assert.NotNull(r2);

                    Assert.Equal("world", (string)r2.Fizz);
                    var fizzParser2 = ((ReadContext?)fizzParserField.GetValue(null)).Value;
                    Assert.Equal(0, fizzParser2.Column.Index);
                    Assert.Equal("Fizz", fizzParser2.Column.Name);
                    Assert.Equal(1, fizzParser2.RowNumber);
                    var fizzReset2 = ((ReadContext?)r2.FizzReset).Value;
                    Assert.Equal(fizzParser2, fizzReset2);

                    Assert.Equal('b', (char)r2.Foo);
                    var fooParser2 = ((ReadContext?)fooParserField.GetValue(null)).Value;
                    Assert.Equal(1, fooParser2.Column.Index);
                    Assert.Equal("Foo", fooParser2.Column.Name);
                    Assert.Equal(1, fooParser2.RowNumber);
                    var fooReset2 = ((ReadContext?)r2.FooReset).Value;
                    Assert.Equal(fooParser2, fooReset2);

                    Assert.Equal(456, (int)r2.Buzz);
                    var buzzParser2 = ((ReadContext?)buzzParserField.GetValue(null)).Value;
                    Assert.Equal(2, buzzParser2.Column.Index);
                    Assert.Equal("Buzz", buzzParser2.Column.Name);
                    Assert.Equal(1, buzzParser2.RowNumber);
                    var buzzReset2 = ((ReadContext?)r2.BuzzReset).Value;
                    Assert.Equal(buzzParser2, buzzReset2);

                    // done
                    Assert.Null(getRow());
                }
            }

            // with constructor params
            {
                var type = await RunSourceGeneratorAsync(
                        "Foo.ReadMe",
                        @"
using System;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    public class ReadMe
    {
        public string? Fizz;
        public int Buzz;
        public char Foo;

        [DeserializerInstanceProvider]
        public ReadMe(
            [DeserializerMember(Name = ""Fizz"", ParserType = typeof(ReadMe), ParserMethodName=nameof(ParserForFizz), ResetType = typeof(ReadMe), ResetMethodName = nameof(ResetForFizz))]
            string fizz,
            [DeserializerMember(Name = ""Buzz"", ParserType = typeof(ReadMe), ParserMethodName=nameof(ParserForBuzz), ResetType = typeof(ReadMe), ResetMethodName = nameof(ResetForBuzz))]
            int buzz,
            [DeserializerMember(Name = ""Foo"", ParserType = typeof(ReadMe), ParserMethodName=nameof(ParserForFoo), ResetType = typeof(ReadMe), ResetMethodName = nameof(ResetForFoo))]
            char foo
        )
        {
            Fizz = fizz;
            Buzz = buzz;
            Foo = foo;
        }

        public static ReadContext? FizzParser;
        public static ReadContext? FizzReset;

        public static ReadContext? BuzzParser;
        public static ReadContext? BuzzReset;

        public static ReadContext? FooParser;
        public static ReadContext? FooReset;

        internal static bool ParserForFizz(ReadOnlySpan<char> data, in ReadContext ctx, out string value)
        {
            FizzParser = ctx;
            value = new string(data);
            return true;
        }

        internal static void ResetForFizz(in ReadContext ctx)
        {
            FizzReset = ctx;
        }

        internal static bool ParserForBuzz(ReadOnlySpan<char> data, in ReadContext ctx, out int value)
        {
            BuzzParser = ctx;
            
            return int.TryParse(data, out value);
        }

        internal static void ResetForBuzz(in ReadContext ctx)
        {
            BuzzReset = ctx;
        }

        internal static bool ParserForFoo(ReadOnlySpan<char> data, in ReadContext ctx, out char value)
        {
            FooParser = ctx;
            
            value = data[0];
            return data.Length == 1;
        }

        internal static void ResetForFoo(in ReadContext ctx)
        {
            FooReset = ctx;
        }
    }
}");
                var fizzParserField = type.GetFieldNonNull("FizzParser", BindingFlags.Public | BindingFlags.Static);
                var fizzResetField = type.GetFieldNonNull("FizzReset", BindingFlags.Public | BindingFlags.Static);
                var buzzParserField = type.GetFieldNonNull("BuzzParser", BindingFlags.Public | BindingFlags.Static);
                var buzzResetField = type.GetFieldNonNull("BuzzReset", BindingFlags.Public | BindingFlags.Static);
                var fooParserField = type.GetFieldNonNull("FooParser", BindingFlags.Public | BindingFlags.Static);
                var fooResetField = type.GetFieldNonNull("FooReset", BindingFlags.Public | BindingFlags.Static);

                // in order
                {
                    var getRow = GetReadOne(type, "Fizz,Buzz,Foo\r\nhello,123,a\r\nworld,456,b");

                    // first row
                    var r1 = getRow();
                    Assert.NotNull(r1);

                    Assert.Equal("hello", (string)r1.Fizz);
                    var fizzParser1 = ((ReadContext?)fizzParserField.GetValue(null)).Value;
                    Assert.Equal(0, fizzParser1.Column.Index);
                    Assert.Equal("Fizz", fizzParser1.Column.Name);
                    Assert.Equal(0, fizzParser1.RowNumber);
                    var fizzReset1 = ((ReadContext?)fizzResetField.GetValue(null)).Value;
                    Assert.Equal(fizzParser1, fizzReset1);

                    Assert.Equal(123, (int)r1.Buzz);
                    var buzzParser1 = ((ReadContext?)buzzParserField.GetValue(null)).Value;
                    Assert.Equal(1, buzzParser1.Column.Index);
                    Assert.Equal("Buzz", buzzParser1.Column.Name);
                    Assert.Equal(0, buzzParser1.RowNumber);
                    var buzzReset1 = ((ReadContext?)buzzResetField.GetValue(null)).Value;
                    Assert.Equal(buzzParser1, buzzReset1);

                    Assert.Equal('a', (char)r1.Foo);
                    var fooParser1 = ((ReadContext?)fooParserField.GetValue(null)).Value;
                    Assert.Equal(2, fooParser1.Column.Index);
                    Assert.Equal("Foo", fooParser1.Column.Name);
                    Assert.Equal(0, fooParser1.RowNumber);
                    var fooReset1 = ((ReadContext?)fooResetField.GetValue(null)).Value;
                    Assert.Equal(fooParser1, fooReset1);

                    // second row
                    var r2 = getRow();
                    Assert.NotNull(r2);

                    Assert.Equal("world", (string)r2.Fizz);
                    var fizzParser2 = ((ReadContext?)fizzParserField.GetValue(null)).Value;
                    Assert.Equal(0, fizzParser2.Column.Index);
                    Assert.Equal("Fizz", fizzParser2.Column.Name);
                    Assert.Equal(1, fizzParser2.RowNumber);
                    var fizzReset2 = ((ReadContext?)fizzResetField.GetValue(null)).Value;
                    Assert.Equal(fizzParser2, fizzReset2);

                    Assert.Equal(456, (int)r2.Buzz);
                    var buzzParser2 = ((ReadContext?)buzzParserField.GetValue(null)).Value;
                    Assert.Equal(1, buzzParser2.Column.Index);
                    Assert.Equal("Buzz", buzzParser2.Column.Name);
                    Assert.Equal(1, buzzParser2.RowNumber);
                    var buzzReset2 = ((ReadContext?)buzzResetField.GetValue(null)).Value;
                    Assert.Equal(buzzParser2, buzzReset2);

                    Assert.Equal('b', (char)r2.Foo);
                    var fooParser2 = ((ReadContext?)fooParserField.GetValue(null)).Value;
                    Assert.Equal(2, fooParser2.Column.Index);
                    Assert.Equal("Foo", fooParser2.Column.Name);
                    Assert.Equal(1, fooParser2.RowNumber);
                    var fooReset2 = ((ReadContext?)fooResetField.GetValue(null)).Value;
                    Assert.Equal(fooParser2, fooReset2);

                    // done
                    Assert.Null(getRow());
                }

                fizzParserField.SetValue(null, default(ReadContext?));
                fizzResetField.SetValue(null, default(ReadContext?));
                buzzParserField.SetValue(null, default(ReadContext?));
                buzzResetField.SetValue(null, default(ReadContext?));
                fooParserField.SetValue(null, default(ReadContext?));
                fooResetField.SetValue(null, default(ReadContext?));

                // out of order
                {
                    var getRow = GetReadOne(type, "Fizz,Foo,Buzz\r\nhello,a,123\r\nworld,b,456");

                    // first row
                    var r1 = getRow();
                    Assert.NotNull(r1);

                    Assert.Equal("hello", (string)r1.Fizz);
                    var fizzParser1 = ((ReadContext?)fizzParserField.GetValue(null)).Value;
                    Assert.Equal(0, fizzParser1.Column.Index);
                    Assert.Equal("Fizz", fizzParser1.Column.Name);
                    Assert.Equal(0, fizzParser1.RowNumber);
                    var fizzReset1 = ((ReadContext?)fizzResetField.GetValue(null)).Value;
                    Assert.Equal(fizzParser1, fizzReset1);

                    Assert.Equal('a', (char)r1.Foo);
                    var fooParser1 = ((ReadContext?)fooParserField.GetValue(null)).Value;
                    Assert.Equal(1, fooParser1.Column.Index);
                    Assert.Equal("Foo", fooParser1.Column.Name);
                    Assert.Equal(0, fooParser1.RowNumber);
                    var fooReset1 = ((ReadContext?)fooResetField.GetValue(null)).Value;
                    Assert.Equal(fooParser1, fooReset1);

                    Assert.Equal(123, (int)r1.Buzz);
                    var buzzParser1 = ((ReadContext?)buzzParserField.GetValue(null)).Value;
                    Assert.Equal(2, buzzParser1.Column.Index);
                    Assert.Equal("Buzz", buzzParser1.Column.Name);
                    Assert.Equal(0, buzzParser1.RowNumber);
                    var buzzReset1 = ((ReadContext?)buzzResetField.GetValue(null)).Value;
                    Assert.Equal(buzzParser1, buzzReset1);

                    // second row
                    var r2 = getRow();
                    Assert.NotNull(r2);

                    Assert.Equal("world", (string)r2.Fizz);
                    var fizzParser2 = ((ReadContext?)fizzParserField.GetValue(null)).Value;
                    Assert.Equal(0, fizzParser2.Column.Index);
                    Assert.Equal("Fizz", fizzParser2.Column.Name);
                    Assert.Equal(1, fizzParser2.RowNumber);
                    var fizzReset2 = ((ReadContext?)fizzResetField.GetValue(null)).Value;
                    Assert.Equal(fizzParser2, fizzReset2);

                    Assert.Equal('b', (char)r2.Foo);
                    var fooParser2 = ((ReadContext?)fooParserField.GetValue(null)).Value;
                    Assert.Equal(1, fooParser2.Column.Index);
                    Assert.Equal("Foo", fooParser2.Column.Name);
                    Assert.Equal(1, fooParser2.RowNumber);
                    var fooReset2 = ((ReadContext?)fooResetField.GetValue(null)).Value;
                    Assert.Equal(fooParser2, fooReset2);

                    Assert.Equal(456, (int)r2.Buzz);
                    var buzzParser2 = ((ReadContext?)buzzParserField.GetValue(null)).Value;
                    Assert.Equal(2, buzzParser2.Column.Index);
                    Assert.Equal("Buzz", buzzParser2.Column.Name);
                    Assert.Equal(1, buzzParser2.RowNumber);
                    var buzzReset2 = ((ReadContext?)buzzResetField.GetValue(null)).Value;
                    Assert.Equal(buzzParser2, buzzReset2);

                    // done
                    Assert.Null(getRow());
                }
            }

            // mix of constructor parameters and explicit setters
            {
                var type = await RunSourceGeneratorAsync(
                        "Foo.ReadMe",
                        @"
using System;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    public class ReadMe
    {
        public string? Fizz;
        [DeserializerMember(ParserType = typeof(ReadMe), ParserMethodName=nameof(ParserForBuzz), ResetType = typeof(ReadMe), ResetMethodName = nameof(ResetForBuzz))]
        public int Buzz { get; set; }
        public char Foo;

        [DeserializerInstanceProvider]
        public ReadMe(
            [DeserializerMember(Name = ""Fizz"", ParserType = typeof(ReadMe), ParserMethodName=nameof(ParserForFizz), ResetType = typeof(ReadMe), ResetMethodName = nameof(ResetForFizz))]
            string fizz,
            [DeserializerMember(Name = ""Foo"", ParserType = typeof(ReadMe), ParserMethodName=nameof(ParserForFoo), ResetType = typeof(ReadMe), ResetMethodName = nameof(ResetForFoo))]
            char foo
        )
        {
            Fizz = fizz;
            Foo = foo;
        }

        public static ReadContext? FizzParser;
        public static ReadContext? FizzReset;

        public static ReadContext? BuzzParser;
        public ReadContext? BuzzReset;

        public static ReadContext? FooParser;
        public static ReadContext? FooReset;

        internal static bool ParserForFizz(ReadOnlySpan<char> data, in ReadContext ctx, out string value)
        {
            FizzParser = ctx;
            value = new string(data);
            return true;
        }

        internal static void ResetForFizz(in ReadContext ctx)
        {
            FizzReset = ctx;
        }

        internal static bool ParserForBuzz(ReadOnlySpan<char> data, in ReadContext ctx, out int value)
        {
            BuzzParser = ctx;
            
            return int.TryParse(data, out value);
        }

        internal void ResetForBuzz(in ReadContext ctx)
        {
            BuzzReset = ctx;
        }

        internal static bool ParserForFoo(ReadOnlySpan<char> data, in ReadContext ctx, out char value)
        {
            FooParser = ctx;
            
            value = data[0];
            return data.Length == 1;
        }

        internal static void ResetForFoo(in ReadContext ctx)
        {
            FooReset = ctx;
        }
    }
}");

                var fizzParserField = type.GetFieldNonNull("FizzParser", BindingFlags.Public | BindingFlags.Static);
                var fizzResetField = type.GetFieldNonNull("FizzReset", BindingFlags.Public | BindingFlags.Static);
                var buzzParserField = type.GetFieldNonNull("BuzzParser", BindingFlags.Public | BindingFlags.Static);
                var fooParserField = type.GetFieldNonNull("FooParser", BindingFlags.Public | BindingFlags.Static);
                var fooResetField = type.GetFieldNonNull("FooReset", BindingFlags.Public | BindingFlags.Static);

                // in order
                {
                    var getRow = GetReadOne(type, "Fizz,Buzz,Foo\r\nhello,123,a\r\nworld,456,b");

                    // first row
                    var r1 = getRow();
                    Assert.NotNull(r1);

                    Assert.Equal("hello", (string)r1.Fizz);
                    var fizzParser1 = ((ReadContext?)fizzParserField.GetValue(null)).Value;
                    Assert.Equal(0, fizzParser1.Column.Index);
                    Assert.Equal("Fizz", fizzParser1.Column.Name);
                    Assert.Equal(0, fizzParser1.RowNumber);
                    var fizzReset1 = ((ReadContext?)fizzResetField.GetValue(null)).Value;
                    Assert.Equal(fizzParser1, fizzReset1);

                    Assert.Equal(123, (int)r1.Buzz);
                    var buzzParser1 = ((ReadContext?)buzzParserField.GetValue(null)).Value;
                    Assert.Equal(1, buzzParser1.Column.Index);
                    Assert.Equal("Buzz", buzzParser1.Column.Name);
                    Assert.Equal(0, buzzParser1.RowNumber);
                    var buzzReset1 = ((ReadContext?)r1.BuzzReset).Value;
                    Assert.Equal(buzzParser1, buzzReset1);

                    Assert.Equal('a', (char)r1.Foo);
                    var fooParser1 = ((ReadContext?)fooParserField.GetValue(null)).Value;
                    Assert.Equal(2, fooParser1.Column.Index);
                    Assert.Equal("Foo", fooParser1.Column.Name);
                    Assert.Equal(0, fooParser1.RowNumber);
                    var fooReset1 = ((ReadContext?)fooResetField.GetValue(null)).Value;
                    Assert.Equal(fooParser1, fooReset1);

                    // second row
                    var r2 = getRow();
                    Assert.NotNull(r2);

                    Assert.Equal("world", (string)r2.Fizz);
                    var fizzParser2 = ((ReadContext?)fizzParserField.GetValue(null)).Value;
                    Assert.Equal(0, fizzParser2.Column.Index);
                    Assert.Equal("Fizz", fizzParser2.Column.Name);
                    Assert.Equal(1, fizzParser2.RowNumber);
                    var fizzReset2 = ((ReadContext?)fizzResetField.GetValue(null)).Value;
                    Assert.Equal(fizzParser2, fizzReset2);

                    Assert.Equal(456, (int)r2.Buzz);
                    var buzzParser2 = ((ReadContext?)buzzParserField.GetValue(null)).Value;
                    Assert.Equal(1, buzzParser2.Column.Index);
                    Assert.Equal("Buzz", buzzParser2.Column.Name);
                    Assert.Equal(1, buzzParser2.RowNumber);
                    var buzzReset2 = ((ReadContext?)r2.BuzzReset).Value;
                    Assert.Equal(buzzParser2, buzzReset2);

                    Assert.Equal('b', (char)r2.Foo);
                    var fooParser2 = ((ReadContext?)fooParserField.GetValue(null)).Value;
                    Assert.Equal(2, fooParser2.Column.Index);
                    Assert.Equal("Foo", fooParser2.Column.Name);
                    Assert.Equal(1, fooParser2.RowNumber);
                    var fooReset2 = ((ReadContext?)fooResetField.GetValue(null)).Value;
                    Assert.Equal(fooParser2, fooReset2);

                    // done
                    Assert.Null(getRow());
                }

                fizzParserField.SetValue(null, default(ReadContext?));
                fizzResetField.SetValue(null, default(ReadContext?));
                buzzParserField.SetValue(null, default(ReadContext?));
                fooParserField.SetValue(null, default(ReadContext?));
                fooResetField.SetValue(null, default(ReadContext?));

                // out of order
                {
                    var getRow = GetReadOne(type, "Fizz,Foo,Buzz\r\nhello,a,123\r\nworld,b,456");

                    // first row
                    var r1 = getRow();
                    Assert.NotNull(r1);

                    Assert.Equal("hello", (string)r1.Fizz);
                    var fizzParser1 = ((ReadContext?)fizzParserField.GetValue(null)).Value;
                    Assert.Equal(0, fizzParser1.Column.Index);
                    Assert.Equal("Fizz", fizzParser1.Column.Name);
                    Assert.Equal(0, fizzParser1.RowNumber);
                    var fizzReset1 = ((ReadContext?)fizzResetField.GetValue(null)).Value;
                    Assert.Equal(fizzParser1, fizzReset1);

                    Assert.Equal('a', (char)r1.Foo);
                    var fooParser1 = ((ReadContext?)fooParserField.GetValue(null)).Value;
                    Assert.Equal(1, fooParser1.Column.Index);
                    Assert.Equal("Foo", fooParser1.Column.Name);
                    Assert.Equal(0, fooParser1.RowNumber);
                    var fooReset1 = ((ReadContext?)fooResetField.GetValue(null)).Value;
                    Assert.Equal(fooParser1, fooReset1);

                    Assert.Equal(123, (int)r1.Buzz);
                    var buzzParser1 = ((ReadContext?)buzzParserField.GetValue(null)).Value;
                    Assert.Equal(2, buzzParser1.Column.Index);
                    Assert.Equal("Buzz", buzzParser1.Column.Name);
                    Assert.Equal(0, buzzParser1.RowNumber);
                    var buzzReset1 = ((ReadContext?)r1.BuzzReset).Value;
                    Assert.Equal(buzzParser1, buzzReset1);

                    // second row
                    var r2 = getRow();
                    Assert.NotNull(r2);

                    Assert.Equal("world", (string)r2.Fizz);
                    var fizzParser2 = ((ReadContext?)fizzParserField.GetValue(null)).Value;
                    Assert.Equal(0, fizzParser2.Column.Index);
                    Assert.Equal("Fizz", fizzParser2.Column.Name);
                    Assert.Equal(1, fizzParser2.RowNumber);
                    var fizzReset2 = ((ReadContext?)fizzResetField.GetValue(null)).Value;
                    Assert.Equal(fizzParser2, fizzReset2);

                    Assert.Equal('b', (char)r2.Foo);
                    var fooParser2 = ((ReadContext?)fooParserField.GetValue(null)).Value;
                    Assert.Equal(1, fooParser2.Column.Index);
                    Assert.Equal("Foo", fooParser2.Column.Name);
                    Assert.Equal(1, fooParser2.RowNumber);
                    var fooReset2 = ((ReadContext?)fooResetField.GetValue(null)).Value;
                    Assert.Equal(fooParser2, fooReset2);

                    Assert.Equal(456, (int)r2.Buzz);
                    var buzzParser2 = ((ReadContext?)buzzParserField.GetValue(null)).Value;
                    Assert.Equal(2, buzzParser2.Column.Index);
                    Assert.Equal("Buzz", buzzParser2.Column.Name);
                    Assert.Equal(1, buzzParser2.RowNumber);
                    var buzzReset2 = ((ReadContext?)r2.BuzzReset).Value;
                    Assert.Equal(buzzParser2, buzzReset2);

                    // done
                    Assert.Null(getRow());
                }
            }
        }

        [Fact]
        public async Task ExplicitOrderAsync()
        {
            var type = await RunSourceGeneratorAsync(
                        "Foo.ReadMe",
                        @"
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [GenerateDeserializer]
    public class ReadMe
    {
        [DataMember(Order = 2)]
        public int A { get; set; }
        [DataMember(Order = 1)]
        public string? B { get; set; }
        [DataMember(Order = 3)]
        public double C { get; set; }
    }
}");

            var rows = Read(type, "hello,456,1.23\r\nworld,789,4.56");

            Assert.Collection(
                rows,
                r1 =>
                {
                    Assert.Equal(456, (int)r1.A);
                    Assert.Equal("hello", (string)r1.B);
                    Assert.Equal(1.23, (double)r1.C);
                },
                r2 =>
                {
                    Assert.Equal(789, (int)r2.A);
                    Assert.Equal("world", (string)r2.B);
                    Assert.Equal(4.56, (double)r2.C);
                }
            );
        }

        [Fact]
        public async Task ValueTypeAsync()
        {
            // boring
            {
                var type = await RunSourceGeneratorAsync(
                           "Foo.ReadMe",
                           @"
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    public struct ReadMe
    {
        public int A { get; set; }
        public string? B { get; set; }
        public double C { get; set; }
    }
}");

                var rows = Read(type, "456,hello,1.23\r\n789,world,4.56");

                Assert.Collection(
                    rows,
                    r1 =>
                    {
                        Assert.Equal(456, (int)r1.A);
                        Assert.Equal("hello", (string)r1.B);
                        Assert.Equal(1.23, (double)r1.C);
                    },
                    r2 =>
                    {
                        Assert.Equal(789, (int)r2.A);
                        Assert.Equal("world", (string)r2.B);
                        Assert.Equal(4.56, (double)r2.C);
                    }
                );
            }

            // constructor
            {
                var type = await RunSourceGeneratorAsync(
                           "Foo.ReadMe",
                           @"
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    public struct ReadMe
    {
        public int A;
        public string? B;
        public double C;

        [DeserializerInstanceProvider]
        public ReadMe(
            [DeserializerMember]
            int a, 
            [DeserializerMember]
            string? b, 
            [DeserializerMember]
            double c
        )
        {
            A = a;
            B = b;
            C = c;
        }
    }
}");

                var rows = Read(type, "456,hello,1.23\r\n789,world,4.56");

                Assert.Collection(
                    rows,
                    r1 =>
                    {
                        Assert.Equal(456, (int)r1.A);
                        Assert.Equal("hello", (string)r1.B);
                        Assert.Equal(1.23, (double)r1.C);
                    },
                    r2 =>
                    {
                        Assert.Equal(789, (int)r2.A);
                        Assert.Equal("world", (string)r2.B);
                        Assert.Equal(4.56, (double)r2.C);
                    }
                );
            }
        }

        [Fact]
        public async Task DefaultIncludedMembersAsync()
        {
            // public properties included by default, but can be ignored
            {
                var type =
                    await RunSourceGeneratorAsync(
                        "Foo.DefaultIncludedMembers",
                        @"
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [GenerateDeserializer]
    public class DefaultIncludedMembers
    {
        public string? A { get; set; }

        [IgnoreDataMember]
        public string? B { get; set; }

        public string? C { get; set; }
    }
}"
                    );

                var rows = Read(type, "A,B,C\r\nhello,fizz,foo\r\nworld,buzz,bar");

                Assert.Collection(
                    rows,
                    r1 =>
                    {
                        Assert.Equal("hello", r1.A);
                        Assert.Null((string)r1.B);
                        Assert.Equal("foo", r1.C);
                    },
                    r2 =>
                    {
                        Assert.Equal("world", r2.A);
                        Assert.Null((string)r2.B);
                        Assert.Equal("bar", r2.C);
                    }
                );
            }

            // internal, private, and static properties ignored by default
            {
                var type =
                    await RunSourceGeneratorAsync(
                        "Foo.IgnoreMembersAsync",
                        @"
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    public class IgnoreMembersAsync
    {
        public string? A { get; set; }

        internal string? B { get; set; }

        private string? C { get; set; }

        public static string? D { get; set; }

        public string? E { get; set; }
    }
}"
                    );

                var bProp = type.GetPropertyNonNull("B", BindingFlags.NonPublic | BindingFlags.Instance);
                var cProp = type.GetPropertyNonNull("C", BindingFlags.NonPublic | BindingFlags.Instance);
                var dProp = type.GetPropertyNonNull("D", BindingFlags.Public | BindingFlags.Static);

                var rows = Read(type, "A,B,C,D,E\r\n1,2,3,4,5\r\n6,7,8,9,0");

                Assert.Collection(
                    rows,
                    r1 =>
                    {
                        Assert.Equal("1", r1.A);
                        Assert.Null(bProp.GetValue(r1));
                        Assert.Null(cProp.GetValue(r1));
                        Assert.Null(dProp.GetValue(null));
                        Assert.Equal("5", r1.E);
                    },
                    r2 =>
                    {
                        Assert.Equal("6", r2.A);
                        Assert.Null(bProp.GetValue(r2));
                        Assert.Null(cProp.GetValue(r2));
                        Assert.Null(dProp.GetValue(null));
                        Assert.Equal("0", r2.E);
                    }
                );
            }

            // internal, private, and static properties ignored by default
            {
                var type =
                    await RunSourceGeneratorAsync(
                        "Foo.IgnoreMembersAsync",
                        @"
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    public class IgnoreMembersAsync
    {
        public string? A { get; set; }

        public string? B => ""foo"";
    }
}"
                    );

                var rows = Read(type, "A,B\r\n1,2\r\n6,7");

                Assert.Collection(
                    rows,
                    r1 =>
                    {
                        Assert.Equal("1", r1.A);
                        Assert.Equal("foo", r1.B);
                    },
                    r2 =>
                    {
                        Assert.Equal("6", r2.A);
                        Assert.Equal("foo", r2.B);
                    }
                );
            }
        }

        [Fact]
        public async Task FailingParserAsync()
        {
            var type =
                    await RunSourceGeneratorAsync(
                        "Foo.FailingParserAsync",
                        @"
using Cesil;
using System;

namespace Foo 
{   
    [GenerateDeserializer]
    public class FailingParserAsync
    {
        [DeserializerMember(ParserType=typeof(FailingParserAsync), ParserMethodName=nameof(Fail))]
        public string? A { get; set; }

        internal static bool Fail(ReadOnlySpan<char> data, in ReadContext ctx, out string? val)
        {
            val = """";
            return false;
        }
    }
}"
                    );

            var exc = Assert.Throws<SerializationException>(() => Read(type, @"A\r\nfoo"));

            Assert.Equal("Failed to parse \"A\\r\\nfoo\" for column index=ColumnIdentifier with Index=0, Name=A using Parser backed by method Boolean __Column_0_Parser(System.ReadOnlySpan`1[System.Char], Cesil.ReadContext ByRef, System.String ByRef) creating System.String (AllowNull)", exc.Message);
        }

        [Fact]
        public async Task RecordsAsync()
        {
            // simple
            {
                var type =
                        await RunSourceGeneratorAsync(
                            "Foo.Records1",
                            @"
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    public record Records1(int A, string B);
}"
                        );

                var rows = Read(type, "A,B\r\n1,foo\r\n2,bar");

                Assert.Collection(
                    rows,
                    r1 =>
                    {
                        Assert.Equal(1, (int)r1.A);
                        Assert.Equal("foo", (string)r1.B);
                    },
                    r2 =>
                    {
                        Assert.Equal(2, (int)r2.A);
                        Assert.Equal("bar", (string)r2.B);
                    }
                );
            }

            // additional property
            {
                var type =
                        await RunSourceGeneratorAsync(
                            "Foo.Records2",
                            @"
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    public record Records2(int A)
    {
        public string? B { get; set; }
    }
}"
                        );

                var rows = Read(type, "A,B\r\n1,foo\r\n2,bar");

                Assert.Collection(
                    rows,
                    r1 =>
                    {
                        Assert.Equal(1, (int)r1.A);
                        Assert.Equal("foo", (string)r1.B);
                    },
                    r2 =>
                    {
                        Assert.Equal(2, (int)r2.A);
                        Assert.Equal("bar", (string)r2.B);
                    }
                );
            }

            // inheritance
            {
                var type =
                        await RunSourceGeneratorAsync(
                            "Foo.Records3",
                            @"
using Cesil;

namespace Foo 
{   
    public record Records1(int A, string B);

    [GenerateDeserializer]
    public record Records3(int C) : Records1(C * 2, C.ToString()+""!"") { }
}
            "
                        );
                
                var rows = Read(type, "A,B,C\r\n2,1!,1\r\n4,2!,2");

                Assert.Collection(
                    rows,
                    r1 =>
                    {
                        Assert.Equal(2, (int)r1.A);
                        Assert.Equal("1!", (string)r1.B);
                        Assert.Equal(1, (int)r1.C);
                    },
                    r2 =>
                    {
                        Assert.Equal(4, (int)r2.A);
                        Assert.Equal("2!", (string)r2.B);
                        Assert.Equal(2, (int)r2.C);
                    }
                );
            }

            // customized parameters
            {
                var type =
                        await RunSourceGeneratorAsync(
                            "Foo.Records4",
                            @"
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    public record Records4([DeserializerMember(Name=""Foo"")]int C) { }
}
            "
                        );

                var rows = Read(type, "Foo\r\n1\r\n2\r\n3");

                Assert.Collection(
                    rows,
                    r1 =>
                    {
                        Assert.Equal(1, (int)r1.C);
                    },
                    r2 =>
                    {
                        Assert.Equal(2, (int)r2.C);
                    },
                    r3 =>
                    {
                        Assert.Equal(3, (int)r3.C);
                    }
                );
            }
        }

        [Fact]
        public async Task SettersAsync()
        {
            // static taking value
            {
                var type =
                    await RunSourceGeneratorAsync(
                        "Foo.Bar",
                        @"
                        using Cesil;

                        namespace Foo 
                        {   
                            [GenerateDeserializer]
                            public class Bar
                            {
                                public static int A { get; set; }

                                [DeserializerMember(Name = ""Blah"")]
                                public static void SetA(int a)
                                {
                                    A = a;
                                }
                            }
                        }
        "
                    );

                var aProp = type.GetPropertyNonNull("A", BindingFlags.Public | BindingFlags.Static);

                aProp.SetValue(null, 0);

                var rows = Read(type, "Blah\r\n1234");
                Assert.Single(rows);

                var res = (int)aProp.GetValue(null);
                Assert.Equal(1234, res);
            }

            // static taking in context and value
            {
                var type =
                    await RunSourceGeneratorAsync(
                        "Foo.Bar",
                        @"
                        using Cesil;

                        namespace Foo 
                        {   
                            [GenerateDeserializer]
                            public class Bar
                            {
                                public static int A { get; set; }

                                [DeserializerMember(Name = ""Blah"")]
                                public static void SetA(int a, in ReadContext ctx)
                                {
                                    A = a;
                                }
                            }
                        }
        "
                    );

                var aProp = type.GetPropertyNonNull("A", BindingFlags.Public | BindingFlags.Static);

                aProp.SetValue(null, 0);

                var rows = Read(type, "Blah\r\n1234");
                Assert.Single(rows);

                var res = (int)aProp.GetValue(null);
                Assert.Equal(1234, res);
            }

            // static taking row, and value
            {
                var type =
                    await RunSourceGeneratorAsync(
                        "Foo.Bar",
                        @"
                        using Cesil;

                        namespace Foo 
                        {   
                            [GenerateDeserializer]
                            public class Bar
                            {
                                public static int A { get; set; }

                                [DeserializerMember(Name = ""Blah"")]
                                public static void SetA(Bar row, int a)
                                {
                                    A = a;
                                }
                            }
                        }
        "
                    );

                var aProp = type.GetPropertyNonNull("A", BindingFlags.Public | BindingFlags.Static);

                aProp.SetValue(null, 0);

                var rows = Read(type, "Blah\r\n1234");
                Assert.Single(rows);

                var res = (int)aProp.GetValue(null);
                Assert.Equal(1234, res);
            }

            // static taking row by ref, and value
            {
                var type =
                    await RunSourceGeneratorAsync(
                        "Foo.Bar",
                        @"
                        using Cesil;

                        namespace Foo 
                        {   
                            [GenerateDeserializer]
                            public class Bar
                            {
                                public static int A { get; set; }

                                [DeserializerMember(Name = ""Blah"")]
                                public static void SetA(ref Bar row, int a)
                                {
                                    A = a;
                                }
                            }
                        }
        "
                    );

                var aProp = type.GetPropertyNonNull("A", BindingFlags.Public | BindingFlags.Static);

                aProp.SetValue(null, 0);

                var rows = Read(type, "Blah\r\n1234");
                Assert.Single(rows);

                var res = (int)aProp.GetValue(null);
                Assert.Equal(1234, res);
            }

            // static taking row, value, and in ReadContext
            {
                var type =
                    await RunSourceGeneratorAsync(
                        "Foo.Bar",
                        @"
                        using Cesil;

                        namespace Foo 
                        {   
                            [GenerateDeserializer]
                            public class Bar
                            {
                                public static int A { get; set; }

                                [DeserializerMember(Name = ""Blah"")]
                                public static void SetA(Bar row, int a, in ReadContext ctx)
                                {
                                    A = a;
                                }
                            }
                        }
        "
                    );

                var aProp = type.GetPropertyNonNull("A", BindingFlags.Public | BindingFlags.Static);

                aProp.SetValue(null, 0);

                var rows = Read(type, "Blah\r\n1234");
                Assert.Single(rows);

                var res = (int)aProp.GetValue(null);
                Assert.Equal(1234, res);
            }

            // instance taking value and in ReadContext
            {
                var type =
                    await RunSourceGeneratorAsync(
                        "Foo.Bar",
                        @"
                        using Cesil;

                        namespace Foo 
                        {   
                            [GenerateDeserializer]
                            public class Bar
                            {
                                public int A { get; set; }

                                [DeserializerMember(Name = ""Blah"")]
                                public void SetA(int a, in ReadContext ctx)
                                {
                                    A = a;
                                }
                            }
                        }
        "
                    );

                var rows = Read(type, "Blah\r\n1234");

                Assert.Collection(
                    rows,
                    r1 =>
                    {
                        Assert.Equal(1234, (int)r1.A);
                    }
                );
            }

            // static field
            {
                var type =
                    await RunSourceGeneratorAsync(
                        "Foo.Bar",
                        @"
                        using Cesil;

                        namespace Foo 
                        {   
                            [GenerateDeserializer]
                            public class Bar
                            {
                                [DeserializerMember(Name = ""Blah"")]
                                public static int A;
                            }
                        }
        "
                    );

                var aField = type.GetFieldNonNull("A", BindingFlags.Static | BindingFlags.Public);

                aField.SetValue(null, 0);

                var rows = Read(type, "Blah\r\n1234");
                Assert.Single(rows);

                Assert.Equal(1234, (int)aField.GetValue(null));
            }

            // static property
            {
                var type =
                    await RunSourceGeneratorAsync(
                        "Foo.Bar",
                        @"
                        using Cesil;

                        namespace Foo 
                        {   
                            [GenerateDeserializer]
                            public class Bar
                            {
                                [DeserializerMember(Name = ""Blah"")]
                                public static int A { get; set; }
                            }
                        }
        "
                    );

                var aField = type.GetPropertyNonNull("A", BindingFlags.Static | BindingFlags.Public);

                aField.SetValue(null, 0);

                var rows = Read(type, "Blah\r\n1234");
                Assert.Single(rows);

                Assert.Equal(1234, (int)aField.GetValue(null));
            }
        }

        [Fact]
        public async Task ResetsAsync()
        {
            // static, no parameters
            {
                var type =
                    await RunSourceGeneratorAsync(
                        "Foo.Bar",
                        @"
                        using Cesil;

                        namespace Foo 
                        {   
                            [GenerateDeserializer]
                            public class Bar
                            {
                                public static int ResetCalled;

                                [DeserializerMember(ResetType = typeof(Bar), ResetMethodName = nameof(Reset))]
                                public int A { get; set; }


                                public static void Reset()
                                {
                                    ResetCalled = 1;
                                }
                            }
                        }
        "
                    );

                var called = type.GetFieldNonNull("ResetCalled", BindingFlags.Public | BindingFlags.Static);

                called.SetValue(null, 0);

                var rows = Read(type, "A\r\n1234");

                Assert.Equal(1, (int)called.GetValue(null));

                Assert.Collection(
                    rows,
                    r1 =>
                    {
                        Assert.Equal(1234, (int)r1.A);
                    }
                );
            }

            // static, takes row
            {
                var type =
                    await RunSourceGeneratorAsync(
                        "Foo.Bar",
                        @"
                        using Cesil;

                        namespace Foo 
                        {   
                            [GenerateDeserializer]
                            public class Bar
                            {
                                public static int ResetCalled;

                                [DeserializerMember(ResetType = typeof(Bar), ResetMethodName = nameof(Reset))]
                                public int A { get; set; }


                                public static void Reset(Bar row)
                                {
                                    ResetCalled = 1;
                                }
                            }
                        }
        "
                    );

                var called = type.GetFieldNonNull("ResetCalled", BindingFlags.Public | BindingFlags.Static);

                called.SetValue(null, 0);

                var rows = Read(type, "A\r\n1234");

                Assert.Equal(1, (int)called.GetValue(null));

                Assert.Collection(
                    rows,
                    r1 =>
                    {
                        Assert.Equal(1234, (int)r1.A);
                    }
                );
            }

            // static, takes row and in context
            {
                var type =
                    await RunSourceGeneratorAsync(
                        "Foo.Bar",
                        @"
                        using Cesil;

                        namespace Foo 
                        {   
                            [GenerateDeserializer]
                            public class Bar
                            {
                                public static int ResetCalled;

                                [DeserializerMember(ResetType = typeof(Bar), ResetMethodName = nameof(Reset))]
                                public int A { get; set; }


                                public static void Reset(Bar row, in ReadContext ctx)
                                {
                                    ResetCalled = 1;
                                }
                            }
                        }
        "
                    );

                var called = type.GetFieldNonNull("ResetCalled", BindingFlags.Public | BindingFlags.Static);

                called.SetValue(null, 0);

                var rows = Read(type, "A\r\n1234");

                Assert.Equal(1, (int)called.GetValue(null));

                Assert.Collection(
                    rows,
                    r1 =>
                    {
                        Assert.Equal(1234, (int)r1.A);
                    }
                );
            }

            // instance, no parameters, so good
            {
                var type =
                    await RunSourceGeneratorAsync(
                        "Foo.Bar",
                        @"
                        using Cesil;

                        namespace Foo 
                        {   
                            [GenerateDeserializer]
                            public class Bar
                            {
                                public int ResetCalled;

                                [DeserializerMember(ResetType = typeof(Bar), ResetMethodName = nameof(Reset))]
                                public int A { get; set; }


                                public void Reset()
                                {
                                    ResetCalled = 1;
                                }
                            }
                        }
        "
                    );

                var rows = Read(type, "A\r\n1234");

                Assert.Collection(
                    rows,
                    r1 =>
                    {
                        Assert.Equal(1234, (int)r1.A);

                        Assert.Equal(1, (int)r1.ResetCalled);
                    }
                );
            }
        }

        private static Func<dynamic> GetReadOne(System.Reflection.TypeInfo rowType, string csv)
        {
            var readImpl = GetReadOneImplOfT.MakeGenericMethod(rowType);

            try
            {
                var ret = readImpl.Invoke(null, new object[] { csv });

                return (Func<dynamic>)ret;
            }
            catch (TargetInvocationException e)
            {
                var wrapped = ExceptionDispatchInfo.Capture(e.InnerException);
                wrapped.Throw();

                throw new Exception("Shouldn't be possible");
            }
        }

        private static readonly MethodInfo GetReadOneImplOfT = typeof(GenerateDeserializerGenerationTests).GetMethod(nameof(GetReadOneImpl), BindingFlags.NonPublic | BindingFlags.Static);
        private static Func<dynamic> GetReadOneImpl<T>(string csv)
        {
            var config = GetConfiguration<T>();

            var rows = Enumerate();
            var e = rows.GetEnumerator();
            var done = false;

            return
                () =>
                {
                    if (done)
                    {
                        return null;
                    }

                    if (e.MoveNext())
                    {
                        return e.Current;
                    }

                    done = true;
                    return null;
                };

            IEnumerable<dynamic> Enumerate()
            {
                using (var str = new StringReader(csv))
                {
                    using (var reader = config.CreateReader(str))
                    {
                        foreach (var row in reader.EnumerateAll())
                        {
                            yield return (dynamic)row;
                        }
                    }
                }
            }
        }

        private static dynamic[] Read(System.Reflection.TypeInfo rowType, string csv)
        {
            var readImpl = ReadImplOfT.MakeGenericMethod(rowType);

            try
            {
                var ret = readImpl.Invoke(null, new object[] { csv });

                return (dynamic[])ret;
            }
            catch (TargetInvocationException e)
            {
                var wrapped = ExceptionDispatchInfo.Capture(e.InnerException);
                wrapped.Throw();

                throw new Exception("Shouldn't be possible");
            }
        }

        private static readonly MethodInfo ReadImplOfT = typeof(GenerateDeserializerGenerationTests).GetMethod(nameof(ReadImpl), BindingFlags.NonPublic | BindingFlags.Static);
        private static dynamic[] ReadImpl<T>(string csv)
        {
            var config = GetConfiguration<T>();

            using (var str = new StringReader(csv))
            {
                using (var reader = config.CreateReader(str))
                {
                    return reader.ReadAll().Select(r => (dynamic)r).ToArray();
                }
            }
        }

        private static IBoundConfiguration<T> GetConfiguration<T>()
        {
            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(TypeDescribers.AheadOfTime).ToOptions();
            var config = Configuration.For<T>(opts);

            // make sure we're actually using the ahead of time builder
            var configType = config.GetType().GetTypeInfo();
            var rowBuilderField = configType.GetFieldNonNull("RowBuilder", BindingFlagsConstants.InternalInstance);
            var val = rowBuilderField.GetValue(config);
            Assert.NotNull(val);

            var valType = val.GetType().GetTypeInfo();
            var valuePop = valType.GetPropertyNonNull("Value", BindingFlagsConstants.InternalInstance);

            var rowConstructor = valuePop.GetValueNonNull(val);
            Assert.NotNull(rowConstructor);

            var rowConstructorName = rowConstructor.GetType().GetTypeInfo().ToString();
            Assert.StartsWith("Cesil.AheadOfTimeRowConstructor`1", rowConstructorName);

            return config;
        }

        private static async Task<System.Reflection.TypeInfo> RunSourceGeneratorAsync(
            string typeName,
            string testFile,
            NullableContextOptions nullableContext = NullableContextOptions.Enable,
            [CallerMemberName] string caller = null
        )
        {
            var serializer = new DeserializerGenerator();

            var (producedCompilation, diagnostics) = await SourceGeneratorTestHelper.RunSourceGeneratorAsync(testFile, serializer, nullableContext, caller);

            Assert.Empty(diagnostics);

            var outputFile = Path.GetTempFileName();

            var res = producedCompilation.Emit(outputFile);

            Assert.Empty(res.Diagnostics);
            Assert.True(res.Success);

            var asm = Assembly.LoadFile(outputFile);
            var ret = Assert.Single(asm.GetTypes().Where(t => t.FullName == typeName));

            return ret.GetTypeInfo();
        }
    }
}