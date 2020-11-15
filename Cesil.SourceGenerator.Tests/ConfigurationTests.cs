using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Cesil.SourceGenerator.Tests
{
    public class ConfigurationTests
    {
        [Fact]
        public async Task SimpleAsync()
        {
            var gen = new SerializerGenerator();
            var (_, diags) = await RunSourceGeneratorAsync(
@"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class WriteMe
    {
        [Cesil.GenerateSerializableMemberAttribute(FormatterType=typeof(WriteMe), FormatterMethodName=nameof(ForInt))]
        public int Bar { get; set; }
        [Cesil.GenerateSerializableMemberAttribute(FormatterType=typeof(WriteMe), FormatterMethodName=nameof(ForString))]
        public string Fizz;
        [Cesil.GenerateSerializableMemberAttribute(Name=""Hello"", FormatterType=typeof(WriteMe), FormatterMethodName=nameof(ForDateTime))]
        public DateTime SomeMtd() => DateTime.Now;

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;

        public static bool ForString(string val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;

        public static bool ForDateTime(DateTime val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;
    }
}", gen);

            Assert.Empty(diags);

            var shouldBeWriteMe = Assert.Single(gen.ToGenerateFor);
            Assert.Equal("WriteMe", shouldBeWriteMe.Identifier.ValueText);

            var members = gen.Members.First().Value;

            // Bar
            {
                var bar = Assert.Single(members, x => x.Name == "Bar");
                Assert.True(bar.EmitDefaultValue);
                Assert.Equal("ForInt", (bar.Formatter.Method.DeclaringSyntaxReferences.Single().GetSyntax() as MethodDeclarationSyntax).Identifier.ValueText);
                Assert.Equal("Bar", bar.Getter.Property.Name);
                Assert.Null(bar.Order);
                Assert.Null(bar.ShouldSerialize);
            }

            // Fizz
            {
                var fizz = Assert.Single(members, x => x.Name == "Fizz");
                Assert.True(fizz.EmitDefaultValue);
                Assert.Equal("ForString", (fizz.Formatter.Method.DeclaringSyntaxReferences.Single().GetSyntax() as MethodDeclarationSyntax).Identifier.ValueText);
                Assert.Equal("Fizz", fizz.Getter.Field.Name);
                Assert.Null(fizz.Order);
                Assert.Null(fizz.ShouldSerialize);
            }

            // Hello
            {
                var hello = Assert.Single(members, x => x.Name == "Hello");
                Assert.True(hello.EmitDefaultValue);
                Assert.Equal("ForDateTime", (hello.Formatter.Method.DeclaringSyntaxReferences.Single().GetSyntax() as MethodDeclarationSyntax).Identifier.ValueText);
                Assert.Equal("SomeMtd", hello.Getter.Method.Name);
                Assert.Null(hello.Order);
                Assert.Null(hello.ShouldSerialize);
            }
        }

        [Fact]
        public async Task BadFormattersAsync()
        {
            // not paired (missing method)
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadFormatters
    {
        [Cesil.GenerateSerializableMemberAttribute(FormatterType=typeof(BadFormatters))]
        public int Bar { get; set; }

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.FormatterBothMustBeSet.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(FormatterType=typeof(BadFormatters))]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // not paired (missing type)
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadFormatters
    {
        [Cesil.GenerateSerializableMemberAttribute(FormatterMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.FormatterBothMustBeSet.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(FormatterMethodName=nameof(ForInt))]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // missing method
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadFormatters
    {
        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=""SomethingElse"")]
        public int Bar { get; set; }

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.CouldNotFindMethod.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=\"SomethingElse\")]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // ambiguous method
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadFormatters
    {
        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;

        public static bool ForInt()
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.MultipleMethodsFound.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // generic method
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadFormatters
    {
        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        public static bool ForInt<T>(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.MethodCannotBeGeneric.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // private method
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadFormatters
    {
        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        private static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.MethodNotPublicOrInternal.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // internal method
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadFormatters
    {
        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        public bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.MethodNotStatic.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // non-bool return method
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadFormatters
    {
        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        public static string ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => "";
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.MethodMustReturnBool.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // void return method
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadFormatters
    {
        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        public static void ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer){ }
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.MethodMustReturnBool.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // wrong number of parameters
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadFormatters
    {
        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        public static bool ForInt() 
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadFormatterParameters.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // first param not int
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadFormatters
    {
        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        public static bool ForInt(string val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadFormatterParameters.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // first param by ref
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadFormatters
    {
        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        public static bool ForInt(ref int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadFormatterParameters.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // second param not in
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadFormatters
    {
        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        public static bool ForInt(int val, WriteContext ctx, IBufferWriter<char> buffer)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadFormatterParameters.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // second param not WriteContext
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadFormatters
    {
        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        public static bool ForInt(int val, in string ctx, IBufferWriter<char> buffer)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadFormatterParameters.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // third param by ref
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadFormatters
    {
        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        public static bool ForInt(int val, in WriteContext ctx, ref IBufferWriter<char> buffer)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadFormatterParameters.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // third param not IBufferWriter<char>
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadFormatters
    {
        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<int> buffer)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadFormatterParameters.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // third param not IBufferWriter<anything>
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadFormatters
    {
        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        public static bool ForInt(int val, in WriteContext ctx, string buffer)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadFormatterParameters.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(FormatterType = typeof(BadFormatters), FormatterMethodName=nameof(ForInt))]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }
        }

        [Fact]
        public async Task BadShouldSerializeAsync()
        {
            // not paired (missing method)
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadShouldSerializes
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType=typeof(BadShouldSerializes),
            FormatterMethodName = nameof(ForInt),

            ShouldSerializeType = typeof(BadShouldSerializes)
        )]
        public int Bar { get; set; }

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;

        public static bool ShouldSerializeBar()
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.ShouldSerializeBothMustBeSet.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType=typeof(BadShouldSerializes),\r\n            FormatterMethodName = nameof(ForInt),\r\n\r\n            ShouldSerializeType = typeof(BadShouldSerializes)\r\n        )]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // not paired (missing type)
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadShouldSerializes
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType=typeof(BadShouldSerializes),
            FormatterMethodName = nameof(ForInt),

            ShouldSerializeMethodName = nameof(ShouldSerializeBar)
        )]
        public int Bar { get; set; }

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;

        public static bool ShouldSerializeBar()
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.ShouldSerializeBothMustBeSet.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType=typeof(BadShouldSerializes),\r\n            FormatterMethodName = nameof(ForInt),\r\n\r\n            ShouldSerializeMethodName = nameof(ShouldSerializeBar)\r\n        )]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // static, too many parameters
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadShouldSerializes
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType=typeof(BadShouldSerializes),
            FormatterMethodName = nameof(ForInt),

            ShouldSerializeType = typeof(BadShouldSerializes),
            ShouldSerializeMethodName = nameof(ShouldSerializeBar)
        )]
        public int Bar { get; set; }

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;

        public static bool ShouldSerializeBar(int a, int b, int c, int d)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadShouldSerializeParameters_TooMany.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType=typeof(BadShouldSerializes),\r\n            FormatterMethodName = nameof(ForInt),\r\n\r\n            ShouldSerializeType = typeof(BadShouldSerializes),\r\n            ShouldSerializeMethodName = nameof(ShouldSerializeBar)\r\n        )]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // static, one parameter by ref
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadShouldSerializes
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType=typeof(BadShouldSerializes),
            FormatterMethodName = nameof(ForInt),

            ShouldSerializeType = typeof(BadShouldSerializes),
            ShouldSerializeMethodName = nameof(ShouldSerializeBar)
        )]
        public int Bar { get; set; }

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;

        public static bool ShouldSerializeBar(ref BadShouldSerializes a)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadShouldSerializeParameters_StaticOne.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType=typeof(BadShouldSerializes),\r\n            FormatterMethodName = nameof(ForInt),\r\n\r\n            ShouldSerializeType = typeof(BadShouldSerializes),\r\n            ShouldSerializeMethodName = nameof(ShouldSerializeBar)\r\n        )]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // static, one parameter wrong type
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadShouldSerializes
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType=typeof(BadShouldSerializes),
            FormatterMethodName = nameof(ForInt),

            ShouldSerializeType = typeof(BadShouldSerializes),
            ShouldSerializeMethodName = nameof(ShouldSerializeBar)
        )]
        public int Bar { get; set; }

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;

        public static bool ShouldSerializeBar(int a)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadShouldSerializeParameters_StaticOne.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType=typeof(BadShouldSerializes),\r\n            FormatterMethodName = nameof(ForInt),\r\n\r\n            ShouldSerializeType = typeof(BadShouldSerializes),\r\n            ShouldSerializeMethodName = nameof(ShouldSerializeBar)\r\n        )]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // static, two parameters first by ref
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadShouldSerializes
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType=typeof(BadShouldSerializes),
            FormatterMethodName = nameof(ForInt),

            ShouldSerializeType = typeof(BadShouldSerializes),
            ShouldSerializeMethodName = nameof(ShouldSerializeBar)
        )]
        public int Bar { get; set; }

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;

        public static bool ShouldSerializeBar(ref BadShouldSerializes a, in WriteContext ctx)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadShouldSerializeParameters_StaticTwo.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType=typeof(BadShouldSerializes),\r\n            FormatterMethodName = nameof(ForInt),\r\n\r\n            ShouldSerializeType = typeof(BadShouldSerializes),\r\n            ShouldSerializeMethodName = nameof(ShouldSerializeBar)\r\n        )]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // static, two parameters first wrong type
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadShouldSerializes
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType=typeof(BadShouldSerializes),
            FormatterMethodName = nameof(ForInt),

            ShouldSerializeType = typeof(BadShouldSerializes),
            ShouldSerializeMethodName = nameof(ShouldSerializeBar)
        )]
        public int Bar { get; set; }

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;

        public static bool ShouldSerializeBar(int a, in WriteContext ctx)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadShouldSerializeParameters_StaticTwo.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType=typeof(BadShouldSerializes),\r\n            FormatterMethodName = nameof(ForInt),\r\n\r\n            ShouldSerializeType = typeof(BadShouldSerializes),\r\n            ShouldSerializeMethodName = nameof(ShouldSerializeBar)\r\n        )]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // static, two parameters second not by in
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadShouldSerializes
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType=typeof(BadShouldSerializes),
            FormatterMethodName = nameof(ForInt),

            ShouldSerializeType = typeof(BadShouldSerializes),
            ShouldSerializeMethodName = nameof(ShouldSerializeBar)
        )]
        public int Bar { get; set; }

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;

        public static bool ShouldSerializeBar(BadShouldSerializes a, WriteContext ctx)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadShouldSerializeParameters_StaticTwo.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType=typeof(BadShouldSerializes),\r\n            FormatterMethodName = nameof(ForInt),\r\n\r\n            ShouldSerializeType = typeof(BadShouldSerializes),\r\n            ShouldSerializeMethodName = nameof(ShouldSerializeBar)\r\n        )]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // static, two parameters second wrong type
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadShouldSerializes
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType=typeof(BadShouldSerializes),
            FormatterMethodName = nameof(ForInt),

            ShouldSerializeType = typeof(BadShouldSerializes),
            ShouldSerializeMethodName = nameof(ShouldSerializeBar)
        )]
        public int Bar { get; set; }

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;

        public static bool ShouldSerializeBar(BadShouldSerializes a, in object ctx)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadShouldSerializeParameters_StaticTwo.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType=typeof(BadShouldSerializes),\r\n            FormatterMethodName = nameof(ForInt),\r\n\r\n            ShouldSerializeType = typeof(BadShouldSerializes),\r\n            ShouldSerializeMethodName = nameof(ShouldSerializeBar)\r\n        )]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // instance, too many parameters
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadShouldSerializes
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType=typeof(BadShouldSerializes),
            FormatterMethodName = nameof(ForInt),

            ShouldSerializeType = typeof(BadShouldSerializes),
            ShouldSerializeMethodName = nameof(ShouldSerializeBar)
        )]
        public int Bar { get; set; }

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;

        public bool ShouldSerializeBar(int a, int b)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadShouldSerializeParameters_TooMany.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType=typeof(BadShouldSerializes),\r\n            FormatterMethodName = nameof(ForInt),\r\n\r\n            ShouldSerializeType = typeof(BadShouldSerializes),\r\n            ShouldSerializeMethodName = nameof(ShouldSerializeBar)\r\n        )]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // instance, first parameter not in
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadShouldSerializes
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType=typeof(BadShouldSerializes),
            FormatterMethodName = nameof(ForInt),

            ShouldSerializeType = typeof(BadShouldSerializes),
            ShouldSerializeMethodName = nameof(ShouldSerializeBar)
        )]
        public int Bar { get; set; }

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;

        public bool ShouldSerializeBar(WriteContext ctx)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadShouldSerializeParameters_InstanceOne.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType=typeof(BadShouldSerializes),\r\n            FormatterMethodName = nameof(ForInt),\r\n\r\n            ShouldSerializeType = typeof(BadShouldSerializes),\r\n            ShouldSerializeMethodName = nameof(ShouldSerializeBar)\r\n        )]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // instance, first parameter wrong type
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadShouldSerializes
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType=typeof(BadShouldSerializes),
            FormatterMethodName = nameof(ForInt),

            ShouldSerializeType = typeof(BadShouldSerializes),
            ShouldSerializeMethodName = nameof(ShouldSerializeBar)
        )]
        public int Bar { get; set; }

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;

        public bool ShouldSerializeBar(in object ctx)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadShouldSerializeParameters_InstanceOne.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType=typeof(BadShouldSerializes),\r\n            FormatterMethodName = nameof(ForInt),\r\n\r\n            ShouldSerializeType = typeof(BadShouldSerializes),\r\n            ShouldSerializeMethodName = nameof(ShouldSerializeBar)\r\n        )]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }
        }

        [Fact]
        public async Task BadEmitDefaultValueAsync()
        {
            // multiple declarations
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadEmitDefaultValues
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType=typeof(BadEmitDefaultValues),
            FormatterMethodName = nameof(ForInt),

            ShouldSerializeType = typeof(BadEmitDefaultValues),
            ShouldSerializeMethodName = nameof(ShouldSerializeBar),

            EmitDefaultValue = false
        )]
        [DataMember(EmitDefaultValue = false)]
        public int Bar { get; set; }

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;

        public static bool ShouldSerializeBar(BadEmitDefaultValues row, in WriteContext ctx)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.EmitDefaultValueSpecifiedMultipleTimes.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType=typeof(BadEmitDefaultValues),\r\n            FormatterMethodName = nameof(ForInt),\r\n\r\n            ShouldSerializeType = typeof(BadEmitDefaultValues),\r\n            ShouldSerializeMethodName = nameof(ShouldSerializeBar),\r\n\r\n            EmitDefaultValue = false\r\n        )]\r\n        [DataMember(EmitDefaultValue = false)]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }
        }

        [Fact]
        public async Task BadOrderAsync()
        {
            // multiple declarations
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadOrders
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType=typeof(BadOrders),
            FormatterMethodName = nameof(ForInt),

            ShouldSerializeType = typeof(BadOrders),
            ShouldSerializeMethodName = nameof(ShouldSerializeBar),

            Order = 2
        )]
        [DataMember(Order = 2)]
        public int Bar { get; set; }

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;

        public static bool ShouldSerializeBar(BadOrders row, in WriteContext ctx)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.OrderSpecifiedMultipleTimes.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType=typeof(BadOrders),\r\n            FormatterMethodName = nameof(ForInt),\r\n\r\n            ShouldSerializeType = typeof(BadOrders),\r\n            ShouldSerializeMethodName = nameof(ShouldSerializeBar),\r\n\r\n            Order = 2\r\n        )]\r\n        [DataMember(Order = 2)]\r\n        public int Bar { get; set; }\r\n", GetFlaggedSource(d));
                    }
                );
            }
        }

        [Fact]
        public async Task MissingMethodNameAsync()
        {
            var gen = new SerializerGenerator();
            var (_, diags) = await RunSourceGeneratorAsync(
@"
using System;
using System.Buffers;
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class MissingMethodNames
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType = typeof(MissingMethodNames),
            FormatterMethodName = nameof(ForInt),
        )]
        public int Bar() => 1;

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;

        public static bool ShouldSerializeBar(BadOrders row, in WriteContext ctx)
        => false;
    }
}", gen);

            Assert.Collection(
                diags,
                d =>
                {
                    Assert.Equal(Diagnostics.SerializableMemberMustHaveNameSetForMethod.Id, d.Id);
                    Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType = typeof(MissingMethodNames),\r\n            FormatterMethodName = nameof(ForInt),\r\n        )]\r\n        public int Bar() => 1;\r\n", GetFlaggedSource(d));
                }
            );
        }

        [Fact]
        public async Task MethodCannotReturnVoidAsync()
        {
            var gen = new SerializerGenerator();
            var (_, diags) = await RunSourceGeneratorAsync(
@"
using System;
using System.Buffers;
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class MethodCannotReturnVoids
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType = typeof(MethodCannotReturnVoids),
            FormatterMethodName = nameof(ForInt),
            Name = ""Bar""
        )]
        public void Bar() { }

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;

        public static bool ShouldSerializeBar(MethodCannotReturnVoids row, in WriteContext ctx)
        => false;
    }
}", gen);

            Assert.Collection(
                diags,
                d =>
                {
                    Assert.Equal(Diagnostics.MethodMustReturnNonVoid.Id, d.Id);
                    Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType = typeof(MethodCannotReturnVoids),\r\n            FormatterMethodName = nameof(ForInt),\r\n            Name = \"Bar\"\r\n        )]\r\n        public void Bar() { }\r\n", GetFlaggedSource(d));
                }
            );
        }

        [Fact]
        public async Task PropertyMustHaveGetterAsync()
        {
            var gen = new SerializerGenerator();
            var (_, diags) = await RunSourceGeneratorAsync(
@"
using System;
using System.Buffers;
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class PropertyMustHaveGetters
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType = typeof(PropertyMustHaveGetters),
            FormatterMethodName = nameof(ForInt)
        )]
        public int Bar { set; }

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;

        public static bool ShouldSerializeBar(PropertyMustHaveGetters row, in WriteContext ctx)
        => false;
    }
}", gen);

            Assert.Collection(
                diags,
                d =>
                {
                    Assert.Equal(Diagnostics.NoGetterOnSerializableProperty.Id, d.Id);
                    Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType = typeof(PropertyMustHaveGetters),\r\n            FormatterMethodName = nameof(ForInt)\r\n        )]\r\n        public int Bar { set; }\r\n", GetFlaggedSource(d));
                }
            );
        }

        [Fact]
        public async Task PropertyCannotHaveParametersAsync()
        {
            var gen = new SerializerGenerator();
            var (_, diags) = await RunSourceGeneratorAsync(
@"
using System;
using System.Buffers;
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class PropertyCannotHaveParameters
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType = typeof(PropertyCannotHaveParameters),
            FormatterMethodName = nameof(ForInt)
        )]
        public int this[int ix] { get => 2; }

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;

        public static bool ShouldSerializeBar(PropertyCannotHaveParameters row, in WriteContext ctx)
        => false;
    }
}", gen);

            Assert.Collection(
                diags,
                d =>
                {
                    Assert.Equal(Diagnostics.SerializablePropertyCannotHaveParameters.Id, d.Id);
                    Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType = typeof(PropertyCannotHaveParameters),\r\n            FormatterMethodName = nameof(ForInt)\r\n        )]\r\n        public int this[int ix] { get => 2; }\r\n", GetFlaggedSource(d));
                }
            );
        }

        [Fact]
        public async Task DataMemberOrderUnspecifiedAsync()
        {
            // implicit
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class DataMemberOrderUnspecifieds
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType = typeof(DataMemberOrderUnspecifieds),
            FormatterMethodName = nameof(Formatter)
        )]
        [DataMember]
        public int Bar;

        internal static bool Formatter(int val, in WriteContext cxt, IBufferWriter<char> buffer)
        => false;
    }
}", gen);

                Assert.Empty(diags);

                var member = gen.Members.Single().Value.Single();

                Assert.Null(member.Order);
            }

            // explicitly -1
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class DataMemberOrderUnspecifieds
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType = typeof(DataMemberOrderUnspecifieds),
            FormatterMethodName = nameof(Formatter)
        )]
        [DataMember(Order = -1)]
        public int Bar;

        internal static bool Formatter(int val, in WriteContext cxt, IBufferWriter<char> buffer)
        => false;
    }
}", gen);

                Assert.Empty(diags);

                var member = gen.Members.Single().Value.Single();

                Assert.Null(member.Order);
            }
        }

        [Fact]
        public async Task BadGetterMethodParametersAsync()
        {
            // static, too many parameters
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadGetterMethodParameters
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType = typeof(BadGetterMethodParameters),
            FormatterMethodName = nameof(ForInt),
            Name = ""Bar""
        )]
        public static int Bar(int a, int b, int c) => 1;

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadGetterParameters_TooMany.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType = typeof(BadGetterMethodParameters),\r\n            FormatterMethodName = nameof(ForInt),\r\n            Name = \"Bar\"\r\n        )]\r\n        public static int Bar(int a, int b, int c) => 1;\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // static, one parameter, WriteContext not in
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadGetterMethodParameters
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType = typeof(BadGetterMethodParameters),
            FormatterMethodName = nameof(ForInt),
            Name = ""Bar""
        )]
        public static int Bar(WriteContext ctx) => 1;

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadGetterParameters_StaticOne.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType = typeof(BadGetterMethodParameters),\r\n            FormatterMethodName = nameof(ForInt),\r\n            Name = \"Bar\"\r\n        )]\r\n        public static int Bar(WriteContext ctx) => 1;\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // static, one paramter, wrong type
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadGetterMethodParameters
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType = typeof(BadGetterMethodParameters),
            FormatterMethodName = nameof(ForInt),
            Name = ""Bar""
        )]
        public static int Bar(string row) => 1;

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadGetterParameters_StaticOne.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType = typeof(BadGetterMethodParameters),\r\n            FormatterMethodName = nameof(ForInt),\r\n            Name = \"Bar\"\r\n        )]\r\n        public static int Bar(string row) => 1;\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // static, one paramter, right type but iun
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadGetterMethodParameters
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType = typeof(BadGetterMethodParameters),
            FormatterMethodName = nameof(ForInt),
            Name = ""Bar""
        )]
        public static int Bar(in BadGetterMethodParameters row) => 1;

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadGetterParameters_StaticOne.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType = typeof(BadGetterMethodParameters),\r\n            FormatterMethodName = nameof(ForInt),\r\n            Name = \"Bar\"\r\n        )]\r\n        public static int Bar(in BadGetterMethodParameters row) => 1;\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // static, two paramters, wrong type
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadGetterMethodParameters
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType = typeof(BadGetterMethodParameters),
            FormatterMethodName = nameof(ForInt),
            Name = ""Bar""
        )]
        public static int Bar(string row, in WriteContext ctx) => 1;

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadGetterParameters_StaticTwo.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType = typeof(BadGetterMethodParameters),\r\n            FormatterMethodName = nameof(ForInt),\r\n            Name = \"Bar\"\r\n        )]\r\n        public static int Bar(string row, in WriteContext ctx) => 1;\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // static, two paramters, right type but in
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadGetterMethodParameters
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType = typeof(BadGetterMethodParameters),
            FormatterMethodName = nameof(ForInt),
            Name = ""Bar""
        )]
        public static int Bar(in BadGetterMethodParameters row, in WriteContext ctx) => 1;

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadGetterParameters_StaticTwo.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType = typeof(BadGetterMethodParameters),\r\n            FormatterMethodName = nameof(ForInt),\r\n            Name = \"Bar\"\r\n        )]\r\n        public static int Bar(in BadGetterMethodParameters row, in WriteContext ctx) => 1;\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // static, two paramters, write context not in
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadGetterMethodParameters
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType = typeof(BadGetterMethodParameters),
            FormatterMethodName = nameof(ForInt),
            Name = ""Bar""
        )]
        public static int Bar(BadGetterMethodParameters row, WriteContext ctx) => 1;

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadGetterParameters_StaticTwo.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType = typeof(BadGetterMethodParameters),\r\n            FormatterMethodName = nameof(ForInt),\r\n            Name = \"Bar\"\r\n        )]\r\n        public static int Bar(BadGetterMethodParameters row, WriteContext ctx) => 1;\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // static, two paramters, not WriteContext
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadGetterMethodParameters
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType = typeof(BadGetterMethodParameters),
            FormatterMethodName = nameof(ForInt),
            Name = ""Bar""
        )]
        public static int Bar(BadGetterMethodParameters row, in string ctx) => 1;

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadGetterParameters_StaticTwo.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType = typeof(BadGetterMethodParameters),\r\n            FormatterMethodName = nameof(ForInt),\r\n            Name = \"Bar\"\r\n        )]\r\n        public static int Bar(BadGetterMethodParameters row, in string ctx) => 1;\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // instance, too many parameters
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadGetterMethodParameters
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType = typeof(BadGetterMethodParameters),
            FormatterMethodName = nameof(ForInt),
            Name = ""Bar""
        )]
        public int Bar(int a, int b, int c) => 1;

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadGetterParameters_TooMany.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType = typeof(BadGetterMethodParameters),\r\n            FormatterMethodName = nameof(ForInt),\r\n            Name = \"Bar\"\r\n        )]\r\n        public int Bar(int a, int b, int c) => 1;\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // instance, one parameter, wrong type
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadGetterMethodParameters
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType = typeof(BadGetterMethodParameters),
            FormatterMethodName = nameof(ForInt),
            Name = ""Bar""
        )]
        public int Bar(int a) => 1;

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadGetterParameters_InstanceOne.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType = typeof(BadGetterMethodParameters),\r\n            FormatterMethodName = nameof(ForInt),\r\n            Name = \"Bar\"\r\n        )]\r\n        public int Bar(int a) => 1;\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // instance, one parameter, WriteContext not in
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadGetterMethodParameters
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType = typeof(BadGetterMethodParameters),
            FormatterMethodName = nameof(ForInt),
            Name = ""Bar""
        )]
        public int Bar(WriteContext ctx) => 1;

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadGetterParameters_InstanceOne.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType = typeof(BadGetterMethodParameters),\r\n            FormatterMethodName = nameof(ForInt),\r\n            Name = \"Bar\"\r\n        )]\r\n        public int Bar(WriteContext ctx) => 1;\r\n", GetFlaggedSource(d));
                    }
                );
            }

            // instance, one parameter, in wrong type
            {
                var gen = new SerializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class BadGetterMethodParameters
    {
        [Cesil.GenerateSerializableMemberAttribute(
            FormatterType = typeof(BadGetterMethodParameters),
            FormatterMethodName = nameof(ForInt),
            Name = ""Bar""
        )]
        public int Bar(in string ctx) => 1;

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        Assert.Equal(Diagnostics.BadGetterParameters_InstanceOne.Id, d.Id);
                        Assert.Equal("        [Cesil.GenerateSerializableMemberAttribute(\r\n            FormatterType = typeof(BadGetterMethodParameters),\r\n            FormatterMethodName = nameof(ForInt),\r\n            Name = \"Bar\"\r\n        )]\r\n        public int Bar(in string ctx) => 1;\r\n", GetFlaggedSource(d));
                    }
                );
            }
        }
        
        private static string GetFlaggedSource(Diagnostic diag)
        {
            var tree = diag.Location.SourceTree;
            if (tree == null)
            {
                throw new Exception("Couldn't find source for diagnostic");
            }

            var root = tree.GetRoot();
            var node = root.FindNode(diag.Location.SourceSpan);
            var sourceFlagged = node.ToFullString();

            return sourceFlagged;
        }

        private static async Task<(Compilation Compilation, ImmutableArray<Diagnostic> Diagnostic)> RunSourceGeneratorAsync(
            string testFile,
            ISourceGenerator generator,
            [CallerMemberName] string caller = null
        )
        {
            var compilation = await GetCompilationAsync(testFile, caller);

            var generators = ImmutableArray.Create(generator);

            GeneratorDriver driver = new CSharpGeneratorDriver(parseOptions: new CSharpParseOptions(LanguageVersion.CSharp8), generators, new DefaultAnalyzerConfigOptionsProvider(), ImmutableArray<AdditionalText>.Empty);

            driver.RunFullGeneration(compilation, out var producedCompilation, out var diagnostics);

            return (producedCompilation, diagnostics);
        }

        private static Task<Compilation> GetCompilationAsync(
            string testFile,
            string caller
        )
        {
            var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator);
            var systemAssemblies = trustedAssemblies.Where(p => Path.GetFileName(p).StartsWith("System.")).ToList();

            var references = systemAssemblies.Select(s => MetadataReference.CreateFromFile(s)).ToList();

            var projectName = $"Cesil.SourceGenerator.Tests.{nameof(ConfigurationTests)}";
            var projectId = ProjectId.CreateNewId(projectName);

            var compilationOptions =
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    allowUnsafe: true,
                    nullableContextOptions: NullableContextOptions.Enable
                );

            var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp8);

            var projectInfo =
                ProjectInfo.Create(
                    projectId,
                    VersionStamp.Create(),
                    projectName,
                    projectName,
                    LanguageNames.CSharp,
                    compilationOptions: compilationOptions,
                    parseOptions: parseOptions
                );

            var workspace = new AdhocWorkspace();

            var solution =
                workspace
                    .CurrentSolution
                    .AddProject(projectInfo);

            foreach (var reference in references)
            {
                solution = solution.AddMetadataReference(projectId, reference);
            }

            var csFile = $"{caller}.cs";
            var docId = DocumentId.CreateNewId(projectId, csFile);

            var project = solution.GetProject(projectId);

            project = project.AddDocument(csFile, testFile).Project;

            // find the Cesil folder to include code from
            string cesilRootDir;
            {
                cesilRootDir = Environment.CurrentDirectory;
                while (cesilRootDir != null)
                {
                    if (Directory.GetDirectories(cesilRootDir).Any(c => Path.GetFileName(c) == "Cesil"))
                    {
                        cesilRootDir = Path.Combine(cesilRootDir, "Cesil");
                        break;
                    }

                    cesilRootDir = Path.GetDirectoryName(cesilRootDir);
                }

                if (cesilRootDir == null)
                {
                    throw new Exception("Couldn't find Cesil root directory, are tests not being run from within the solution?");
                }
            }

            var files =
                new[]
                {
                    new [] { "Interface", "Attributes", "SerializeAttributes.cs" },
                    new [] { "Interface", "Attributes", "GeneratedSourceVersionAttribute.cs" },
                    new [] { "Context", "WriteContext.cs" },
                };

            foreach (var fileParts in files)
            {
                var toAddFilePath = cesilRootDir;
                foreach (var part in fileParts)
                {
                    toAddFilePath = Path.Combine(toAddFilePath, part);
                }

                var fileText = File.ReadAllText(toAddFilePath);
                project = project.AddDocument(Path.GetFileName(toAddFilePath), fileText).Project;
            }

            return project.GetCompilationAsync();
        }
    }
}
