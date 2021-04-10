using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

using Cesil.SourceGenerator;

using static Cesil.Tests.SourceGeneratorTestHelper;
using System.Collections.Immutable;

namespace Cesil.Tests
{
    public class GenerateDeserializerConfigurationFixture : IAsyncLifetime
    {
        internal IMethodSymbol Method { get; private set; }
        internal ITypeSymbol Type { get; private set; }
        internal IParameterSymbol Parameter { get; private set; }
        internal IPropertySymbol Property { get; private set; }

        public async Task InitializeAsync()
        {
            var gen = new SerializerGenerator();
            var (comp, diags) = await RunSourceGeneratorAsync(
@"
namespace Foo 
{   
    class Foo
    {
        public Foo(int a) { }

        public string Bar { get; set; }
    }
}", gen);

            Assert.Empty(diags);

            Type = Utils.NonNull(comp.GetTypeByMetadataName("System.String"));
            Method = Type.GetMembers().OfType<IMethodSymbol>().First();
            Parameter = Type.GetMembers().OfType<IMethodSymbol>().First(m => m.MethodKind == MethodKind.Constructor).Parameters.First();
            Property = Type.GetMembers().OfType<IPropertySymbol>().First();
        }

        public Task DisposeAsync()
        => Task.CompletedTask;
    }

    public class GenerateDeserializerConfigurationTests : IClassFixture<GenerateDeserializerConfigurationFixture>
    {
        private readonly ITypeSymbol Type;
        private readonly IParameterSymbol Parameter;
        private readonly IMethodSymbol Method;
        private readonly IPropertySymbol Property;

        public GenerateDeserializerConfigurationTests(GenerateDeserializerConfigurationFixture fixture)
        {
            Type = fixture.Type;
            Parameter = fixture.Parameter;
            Method = fixture.Method;
            Property = fixture.Property;
        }

        [Fact]
        public async Task IsRecordAsync()
        {
            var comp = await GetCompilationAsync(
                @"
namespace Foo 
{   
    class NotRecordEmpty { }
    class NotRecord1
    {
        internal NotRecord1(int a) { }
    }
    
    record EmptyRecord { }
    record Record1(int A) { }
    record Record2(string B) : Record1(B.Length) { }
    record Record3(double A)
    {
        internal Record3(int a) : this((double)a * 2) { }
    }
}",
                nameof(IsRecordAsync),
                NullableContextOptions.Disable
            );

            Assert.Empty(comp.GetDiagnostics());

            var nre = comp.GetTypeByMetadataName("Foo.NotRecord1");
            Assert.NotNull(nre);
            var (nreS, nreC, _) = nre.IsRecord();
            Assert.False(nreS);

            var nr1 = comp.GetTypeByMetadataName("Foo.NotRecordEmpty");
            Assert.NotNull(nr1);

            var er = comp.GetTypeByMetadataName("Foo.EmptyRecord");
            Assert.NotNull(er);
            var (erS, erC, erP) = er.IsRecord();
            Assert.True(erS);
            Assert.Empty(erC.Parameters);
            Assert.Empty(erP);

            var (nr1S, nr1C, _) = nr1.IsRecord();
            Assert.False(nr1S);

            var r1 = comp.GetTypeByMetadataName("Foo.Record1");
            Assert.NotNull(r1);
            var (r1S, r1C, r1P) = r1.IsRecord();
            Assert.True(r1S);
            Assert.Collection(
                r1C.Parameters,
                p =>
                {
                    Assert.Equal(SpecialType.System_Int32, p.Type.SpecialType);
                }
            );
            Assert.Collection(
                r1P,
                p =>
                {
                    Assert.Equal("A", p.Name);
                }
            );

            var r2 = comp.GetTypeByMetadataName("Foo.Record2");
            Assert.NotNull(r2);
            var (r2S, r2C, r2P) = r2.IsRecord();
            Assert.True(r2S);
            Assert.Collection(
                r2C.Parameters,
                p =>
                {
                    Assert.Equal(SpecialType.System_String, p.Type.SpecialType);
                }
            );
            Assert.Collection(
                r2P,
                p1 =>
                {
                    Assert.Equal("A", p1.Name);
                },
                p2 =>
                {
                    Assert.Equal("B", p2.Name);
                }
            );

            var r3 = comp.GetTypeByMetadataName("Foo.Record3");
            Assert.NotNull(r3);
            var (r3S, r3C, r3P) = r3.IsRecord();
            Assert.True(r3S);
            Assert.Collection(
                r3C.Parameters,
                p =>
                {
                    Assert.Equal(SpecialType.System_Double, p.Type.SpecialType);
                }
            );
            Assert.Collection(
                r3P,
                p =>
                {
                    Assert.Equal("A", p.Name);
                }
            );
        }

        [Fact]
        public async Task SimpleAsync()
        {
            var gen = new DeserializerGenerator();
            var (_, diags) = await RunSourceGeneratorAsync(
@"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public int Bar { get; set; }
        [DeserializerMember(ParserType=typeof(ReadMe), ParserMethodName=nameof(ForString))]
        public string Fizz;
        [DeserializerMember(Name=""Hello"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForDateTime))]
        public void SomeMtd(DateTime val) { }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }

        public static bool ForString(ReadOnlySpan<char> data, in ReadContext ctx, out string val)
        {
            val = default;
            return false;
        }

        public static bool ForDateTime(ReadOnlySpan<char> data, in ReadContext ctx, out DateTime val)
        {
            val = default;
            return false;
        }
    }
}", gen);

            Assert.Empty(diags);

            var shouldBeWriteMe = Assert.Single(gen.ToGenerateFor);
            Assert.Equal("ReadMe", shouldBeWriteMe.Identifier.ValueText);

            var members = gen.Members.First().Value;

            // Bar
            {
                var bar = Assert.Single(members, x => x.Name == "Bar");
                Assert.False(bar.IsRequired);
                Assert.Equal("ForInt", (bar.Parser.Method.DeclaringSyntaxReferences.Single().GetSyntax() as MethodDeclarationSyntax).Identifier.ValueText);
                Assert.Equal("Bar", bar.Setter.Property.Name);
                Assert.Null(bar.Order);
                Assert.Null(bar.Reset);
            }

            // Fizz
            {
                var fizz = Assert.Single(members, x => x.Name == "Fizz");
                Assert.False(fizz.IsRequired);
                Assert.Equal("ForString", (fizz.Parser.Method.DeclaringSyntaxReferences.Single().GetSyntax() as MethodDeclarationSyntax).Identifier.ValueText);
                Assert.Equal("Fizz", fizz.Setter.Field.Name);
                Assert.Null(fizz.Order);
                Assert.Null(fizz.Reset);
            }

            // Hello
            {
                var hello = Assert.Single(members, x => x.Name == "Hello");
                Assert.False(hello.IsRequired);
                Assert.Equal("ForDateTime", (hello.Parser.Method.DeclaringSyntaxReferences.Single().GetSyntax() as MethodDeclarationSyntax).Identifier.ValueText);
                Assert.Equal("SomeMtd", hello.Setter.Method.Name);
                Assert.Null(hello.Order);
                Assert.Null(hello.Reset);
            }
        }

        [Fact]
        public async Task NameRequiredForMethodASync()
        {
            var gen = new DeserializerGenerator();
            var (_, diags) = await RunSourceGeneratorAsync(
@"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public void SetBar(int val) { }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

            var diag = Assert.Single(diags);
            AssertDiagnostic(Diagnostics.DeserializableMemberMustHaveNameSetForMethod, diag);
        }

        [Fact]
        public async Task BadParsersAsync()
        {
            // parser method specified, but does not exist
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(ParserType = typeof(ReadMe), ParserMethodName=""Foo""]
        public int Bar { get; set; }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.CouldNotFindMethod, diag);
            }

            // parser does not exist
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember]
        public ReadMe Bar { get; set; }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.NoBuiltInParser, diag);
            }

            // returns not boolean
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        public static int ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return -1;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.MethodMustReturnBool, diag);
            }

            // not static
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        public bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.MethodNotStatic, diag);
            }

            // not accessible
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        private static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.MethodNotPublicOrInternal, diag);
            }

            // wrong number of params
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        public static bool ForInt(ReadOnlySpan<char> data, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadParserParameters, diag);
            }

            // not ReadOnlySpan<char>
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        public static bool ForInt(string data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadParserParameters, diag);
            }

            // not _in_ ReadContext
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        public static bool ForInt(ReadOnlySpan<char> data, ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadParserParameters, diag);
            }

            // not in _ReadContext_
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        public static bool ForInt(ReadOnlySpan<char> data, in WriteContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadParserParameters, diag);
            }

            // not _out_ int
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadParserParameters, diag);
            }

            // not out _int_
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out string val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadParserParameters, diag);
            }
        }

        [Fact]
        public async Task BadSettersAsync()
        {
            // static setter method does not return void
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public static int Bar(int val) => val;

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.MethodMustReturnVoid, diag);
            }

            // static setter method cannot be generic
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public static void Bar<T>(int val) { }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.MethodCannotBeGeneric, diag);
            }

            // static setter method takes no parameters
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public static void Bar() { }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadSetterParameters_TooFew, diag);
            }

            // static setter method takes one parameter, but by ref
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public static void Bar(ref int val) { }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadSetterParameters_StaticOne, diag);
            }

            // static setter method takes two parameters, both by ref
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public static void Bar(ref int val, in ReadContext ctx) { }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadSetterParameters_StaticTwo, diag);
            }

            // static setter method takes two parameters, first is wrong type (not row)
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public static void Bar(ref string row, ref ReadContext ctx) { }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadSetterParameters_StaticTwo, diag);
            }

            // static setter method takes two parameters, first is wrong ref type (not ref)
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public static void Bar(in ReadMe row, ref ReadContext ctx) { }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadSetterParameters_StaticTwo, diag);
            }

            // static setter method takes two parameters, value isn't passed by value
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public static void Bar(ref ReadMe row, ref int val) { }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadSetterParameters_StaticTwo, diag);
            }

            // static setter method takes three parameters, row type is passed not by ref or value
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public static void Bar(in ReadMe row, int val, in ReadContext ctx) { }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadSetterParameters_StaticThree, diag);
            }

            // static setter method takes three parameters, wrong row type
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public static void Bar(string row, int val, in ReadContext ctx) { }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadSetterParameters_StaticThree, diag);
            }

            // static setter method takes three parameters, value not passed by value
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public static void Bar(ReadMe row, ref int val, in ReadContext ctx) { }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadSetterParameters_StaticThree, diag);
            }

            // static setter method takes three parameters, wrong context
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public static void Bar(ReadMe row, int val, in WriteContext ctx) { }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadSetterParameters_StaticThree, diag);
            }

            // static setter method takes too many parameters
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public static void Bar(ReadMe row, int val, in ReadContext ctx, string other) { }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadSetterParameters_TooMany, diag);
            }

            // instance setter method takes no parameters
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public void Bar() { }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadSetterParameters_TooFew, diag);
            }

            // instance setter method takes one parameter, but not by value
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public void Bar(in int foo) { }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadSetterParameters_InstanceOne, diag);
            }

            // instance setter method takes two parameters, but first isn't by value
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public void Bar(in int foo, in ReadContext ctx) { }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadSetterParameters_InstanceTwo, diag);
            }

            // instance setter method takes two parameters, but the wrong context
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public void Bar(int foo, in WriteContext ctx) { }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadSetterParameters_InstanceTwo, diag);
            }

            // instance setter method takes too many parameters
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public void Bar(int foo, in ReadContext ctx, string other) { }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadSetterParameters_TooMany, diag);
            }

            // property has no setter
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public int Bar => 1;

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.NoSetterOnDeserializableProperty, diag);
            }

            // property takes parameters
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public int this[int ix]
        {
            get => 0;
            set;
        }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.DeserializablePropertyCannotHaveParameters, diag);
            }

            // setters backed by instance properties that are init, but the instance provider isn't a constructor
            //
            // only need to test instance properties, because static properties cannot be init
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer(
        InstanceProviderType = typeof(ReadMe),
        InstanceProviderMethodName = nameof(ReadMe.TryGetInstance)
    )]
    class ReadMe
    {
        public int Foo { get; init; }

        public static bool TryGetInstance(in ReadContext ctx, out ReadMe res)
        {
            res = new ReadMe();
            return true;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadSetter_CannotHaveInitSettersWithNonConstructorInstanceProviders, diag);
            }

            // constructor params on records
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    record ReadMe([DeserializerMember(Name=""Hello"", Name=""Bar"")]int a)
    {
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.NameSpecifiedMultipleTimes, diag);
            }
        }

        [Fact]
        public async Task BadResetsAsync()
        {
            // reset, default, static, takes wrong type
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        public int Bar { get; set; }

        public static void ResetBar(string no) { }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadResetParameters_StaticOne, diag);
            }

            // reset, first arg is by ref
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt), ResetType=typeof(ReadMe), ResetMethodName = nameof(Reset))]
        public int Bar { get; set; }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }

        public static void Reset(ref ReadMe row) { }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadResetParameters_StaticOne, diag);
            }

            // reset doesn't exist
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt), ResetType=typeof(ReadMe), ResetMethodName = ""foo"")]
        public int Bar { get; set; }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.CouldNotFindMethod, diag);
            }

            // reset not accessible
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt), ResetType=typeof(ReadMe), ResetMethodName = nameof(ResetBar))]
        public int Bar { get; set; }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }

        private void ResetBar() { }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.MethodNotPublicOrInternal, diag);
            }

            // reset doesn't return void
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt), ResetType=typeof(ReadMe), ResetMethodName = nameof(ResetBar))]
        public int Bar { get; set; }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }

        public static int ResetBar()
        {
            return default;
        }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.MethodMustReturnVoid, diag);
            }

            // reset is generic
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt), ResetType=typeof(ReadMe), ResetMethodName = nameof(ResetBar))]
        public int Bar { get; set; }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }

        public static void ResetBar<T>() { }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.MethodCannotBeGeneric, diag);
            }

            // static reset takes one parameter, takes an in but not a ReadContext
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt), ResetType=typeof(ReadMe), ResetMethodName = nameof(ResetBar))]
        public int Bar { get; set; }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }

        public static void ResetBar(in string val) { }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadResetParameters_StaticOne, diag);
            }

            // static reset takes one parameter, is by value but not the row type
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt), ResetType=typeof(ReadMe), ResetMethodName = nameof(ResetBar))]
        public int Bar { get; set; }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }

        public static void ResetBar(string val) { }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadResetParameters_StaticOne, diag);
            }

            // static reset takes two parameters, first isn't by ref or value
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt), ResetType=typeof(ReadMe), ResetMethodName = nameof(ResetBar))]
        public int Bar { get; set; }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }

        public static void ResetBar(in string row, in ReadContext ctx) { }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadResetParameters_StaticTwo, diag);
            }

            // static reset takes two parameters, first isn't row type
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt), ResetType=typeof(ReadMe), ResetMethodName = nameof(ResetBar))]
        public int Bar { get; set; }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }

        public static void ResetBar(string row, in WriteContext ctx) { }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadResetParameters_StaticTwo, diag);
            }

            // static reset takes two parameters, second is wrong context
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt), ResetType=typeof(ReadMe), ResetMethodName = nameof(ResetBar))]
        public int Bar { get; set; }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }

        public static void ResetBar(ReadMe row, in WriteContext ctx) { }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadResetParameters_StaticTwo, diag);
            }

            // static reset takes too many parameters
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt), ResetType=typeof(ReadMe), ResetMethodName = nameof(ResetBar))]
        public int Bar { get; set; }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }

        public static void ResetBar(ReadMe row, in WriteContext ctx, string foo) { }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadResetParameters_TooMany, diag);
            }

            // instance reset takes takes one parameter, but not a ReadContext
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt), ResetType=typeof(ReadMe), ResetMethodName = nameof(ResetBar))]
        public int Bar { get; set; }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }

        public void ResetBar(string foo) { }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadResetParameters_InstanceOne, diag);
            }

            // instance reset takes takes too many parameters
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt), ResetType=typeof(ReadMe), ResetMethodName = nameof(ResetBar))]
        public int Bar { get; set; }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }

        public void ResetBar(in ReadContext ctx, string foo) { }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadResetParameters_TooMany, diag);
            }

            // instance reset on wrong row type
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(Name = ""Foo"", ParserType=typeof(ReadMe), ParserMethodName=nameof(ForInt), ResetType=typeof(Other), ResetMethodName = nameof(Other.ResetBar))]
        public int Bar { get; set; }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = default;
            return false;
        }

        
    }

    class Other
    {
        public void ResetBar(in ReadContext ctx) { }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadReset_NotOnRow, diag);
            }

            // instance reset constructor parameter
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        public int Bar;

        [DeserializerInstanceProvider]
        public ReadMe(
            [DeserializerMember(Name = ""Foo"", ResetType=typeof(ReadMe), ResetMethodName = nameof(ResetBar), MemberRequired = MemberRequired.Yes)]
            int bar
        )
        {
            Bar = bar;
        }

        public void ResetBar(in ReadContext ctx) { }
    }
}", gen);

                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadReset_MustBeStaticForParameters, diag);
            }
        }

        [Fact]
        public async Task DataMemberOrderUnspecifiedAsync()
        {
            // implicit
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [GenerateDeserializer]
    class DataMemberOrderUnspecifieds
    {
        [DeserializerMember(
            ParserType = typeof(DataMemberOrderUnspecifieds),
            ParserMethodName = nameof(Parser)
        )]
        [DataMember]
        public int Bar;

        internal static bool Parser(ReadOnlySpan<char> val, in ReadContext cxt, out int val)
        {
            val = 0;
            return false;
        }
    }
}", gen);

                Assert.Empty(diags);

                var member = gen.Members.Single().Value.Single();

                Assert.Null(member.Order);
            }

            // explicit -1
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [GenerateDeserializer]
    class DataMemberOrderUnspecifieds
    {
        [DeserializerMember(
            ParserType = typeof(DataMemberOrderUnspecifieds),
            ParserMethodName = nameof(Parser)
        )]
        [DataMember(Order = -1)]
        public int Bar;

        internal static bool Parser(ReadOnlySpan<char> val, in ReadContext cxt, out int val)
        {
            val = 0;
            return false;
        }
    }
}", gen);

                Assert.Empty(diags);

                var member = gen.Members.Single().Value.Single();

                Assert.Null(member.Order);
            }
        }

        [Fact]
        public async Task BadOrderAsync()
        {
            // multiple declarations
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [GenerateDeserializer]
    class BadOrders
    {
        [DeserializerMember(
            ParserType = typeof(BadOrders),
            ParserMethodName = nameof(ForInt),

            Order = 2
        )]
        [DataMember(Order = 2)]
        public int Bar { get; set; }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = 0;
            return false;
        }
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        AssertDiagnostic(Diagnostics.OrderSpecifiedMultipleTimes, d);
                    }
                );
            }
        }

        [Fact]
        public async Task BadIsRequiredAsync()
        {
            // not Yes or No
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [GenerateDeserializer]
    class BadIsRequired
    {
        [DeserializerMember(
            ParserType = typeof(BadIsRequired),
            ParserMethodName = nameof(ForInt),

            MemberRequired = (MemberRequired)3
        )]
        public int Bar { get; set; }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = 0;
            return false;
        }
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        AssertDiagnostic(Diagnostics.UnexpectedConstantValue, d);
                    }
                );
            }

            // multiple declarations
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [GenerateDeserializer]
    class BadIsRequired
    {
        [DeserializerMember(
            ParserType = typeof(BadIsRequired),
            ParserMethodName = nameof(ForInt),

            MemberRequired = MemberRequired.No
        )]
        [DataMember(IsRequired = false)]
        public int Bar { get; set; }

        public static bool ForInt(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            val = 0;
            return false;
        }
    }
}", gen);

                Assert.Collection(
                    diags,
                    d =>
                    {
                        AssertDiagnostic(Diagnostics.IsRequiredSpecifiedMultipleTimes, d);
                    }
                );
            }
        }

        [Fact]
        public async Task DefaultParsersAsync()
        {
            var gen = new DeserializerGenerator();
            var (_, diags) = await RunSourceGeneratorAsync(
@"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember]
        public int Bar { get; set; }
        [DeserializerMember]
        public string Fizz;
        [DeserializerMember(Name=""Hello"")]
        public void SomeMtd(DateTime val) { }
    }
}", gen);

            Assert.Empty(diags);

            Assert.Collection(
                gen.NeededDefaultParsers,
                forBar =>
                {
                    Assert.True(forBar.IsDefault);
                    Assert.Equal("System.Int32", forBar.ForDefaultType);
                },
                forFizz =>
                {
                    Assert.True(forFizz.IsDefault);
                    Assert.Equal("System.String?", forFizz.ForDefaultType);
                },
                forHello =>
                {
                    Assert.True(forHello.IsDefault);
                    Assert.Equal("System.DateTime", forHello.ForDefaultType);
                }
            );
        }

        [Fact]
        public async Task BadInstanceProviders()
        {
            // method not found
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer(
        InstanceProviderType = typeof(ReadMe),
        InstanceProviderMethodName = ""Blargh""
    )]
    class ReadMe
    {
        [DeserializerMember]
        public int Bar { get; set; }
        [DeserializerMember]
        public string Fizz;
        [DeserializerMember(Name=""Hello"")]
        public void SomeMtd(DateTime val) { }
    }
}", gen);

                var d = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.CouldNotFindMethod, d);
            }

            //  not static
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer(
        InstanceProviderType = typeof(ReadMe),
        InstanceProviderMethodName = nameof(ReadMe.InstanceProvider)
    )]
    class ReadMe
    {
        [DeserializerMember]
        public int Bar { get; set; }
        [DeserializerMember]
        public string Fizz;
        [DeserializerMember(Name=""Hello"")]
        public void SomeMtd(DateTime val) { }

        internal bool InstanceProvider(in ReadContext ctx, out ReadMe row)
        {
            row = null;
            return false;
        }
    }
}", gen);

                var d = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.MethodNotStatic, d);
            }

            //  not accessible
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer(
        InstanceProviderType = typeof(ReadMe),
        InstanceProviderMethodName = nameof(ReadMe.InstanceProvider)
    )]
    class ReadMe
    {
        [DeserializerMember]
        public int Bar { get; set; }
        [DeserializerMember]
        public string Fizz;
        [DeserializerMember(Name=""Hello"")]
        public void SomeMtd(DateTime val) { }

        private static bool InstanceProvider(in ReadContext ctx, out ReadMe row)
        {
            row = null;
            return false;
        }
    }
}", gen);

                var d = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.MethodNotPublicOrInternal, d);
            }

            //  doesn't return bool
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer(
        InstanceProviderType = typeof(ReadMe),
        InstanceProviderMethodName = nameof(ReadMe.InstanceProvider)
    )]
    class ReadMe
    {
        [DeserializerMember]
        public int Bar { get; set; }
        [DeserializerMember]
        public string Fizz;
        [DeserializerMember(Name=""Hello"")]
        public void SomeMtd(DateTime val) { }

        internal static void InstanceProvider(in ReadContext ctx, out ReadMe row)
        {
            row = null;
        }
    }
}", gen);

                var d = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.MethodMustReturnBool, d);
            }

            //  wrong number of parameters
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer(
        InstanceProviderType = typeof(ReadMe),
        InstanceProviderMethodName = nameof(ReadMe.InstanceProvider)
    )]
    class ReadMe
    {
        [DeserializerMember]
        public int Bar { get; set; }
        [DeserializerMember]
        public string Fizz;
        [DeserializerMember(Name=""Hello"")]
        public void SomeMtd(DateTime val) { }

        internal static bool InstanceProvider(in ReadContext ctx, out ReadMe row, string foo)
        {
            row = null;
            return false;
        }
    }
}", gen);

                var d = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadInstanceProviderParameters, d);
            }

            //  wrong context
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer(
        InstanceProviderType = typeof(ReadMe),
        InstanceProviderMethodName = nameof(ReadMe.InstanceProvider)
    )]
    class ReadMe
    {
        [DeserializerMember]
        public int Bar { get; set; }
        [DeserializerMember]
        public string Fizz;
        [DeserializerMember(Name=""Hello"")]
        public void SomeMtd(DateTime val) { }

        internal static bool InstanceProvider(in WriteContext ctx, out ReadMe row)
        {
            row = null;
            return false;
        }
    }
}", gen);

                var d = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadInstanceProviderParameters, d);
            }

            //  context passed incorrectly
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer(
        InstanceProviderType = typeof(ReadMe),
        InstanceProviderMethodName = nameof(ReadMe.InstanceProvider)
    )]
    class ReadMe
    {
        [DeserializerMember]
        public int Bar { get; set; }
        [DeserializerMember]
        public string Fizz;
        [DeserializerMember(Name=""Hello"")]
        public void SomeMtd(DateTime val) { }

        internal static bool InstanceProvider(ReadContext ctx, out ReadMe row)
        {
            row = null;
            return false;
        }
    }
}", gen);

                var d = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadInstanceProviderParameters, d);
            }

            //  not out row
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer(
        InstanceProviderType = typeof(ReadMe),
        InstanceProviderMethodName = nameof(ReadMe.InstanceProvider)
    )]
    class ReadMe
    {
        [DeserializerMember]
        public int Bar { get; set; }
        [DeserializerMember]
        public string Fizz;
        [DeserializerMember(Name=""Hello"")]
        public void SomeMtd(DateTime val) { }

        internal static bool InstanceProvider(in ReadContext ctx, ReadMe row)
        {
            row = null;
            return false;
        }
    }
}", gen);

                var d = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadInstanceProviderParameters, d);
            }

            //  out, but wrong type
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer(
        InstanceProviderType = typeof(ReadMe),
        InstanceProviderMethodName = nameof(ReadMe.InstanceProvider)
    )]
    class ReadMe
    {
        [DeserializerMember]
        public int Bar { get; set; }
        [DeserializerMember]
        public string Fizz;
        [DeserializerMember(Name=""Hello"")]
        public void SomeMtd(DateTime val) { }

        internal static bool InstanceProvider(in ReadContext ctx, out string row)
        {
            row = null;
            return false;
        }
    }
}", gen);

                var d = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.BadInstanceProviderParameters, d);
            }

            //  default constructor not accessible
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember]
        public int Bar { get; set; }
        [DeserializerMember]
        public string Fizz;
        [DeserializerMember(Name=""Hello"")]
        public void SomeMtd(DateTime val) { }

        private ReadMe() { }
    }
}", gen);

                var d = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.MethodNotPublicOrInternal, d);
            }

            //  no default constructor
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember]
        public int Bar { get; set; }
        [DeserializerMember]
        public string Fizz;
        [DeserializerMember(Name=""Hello"")]
        public void SomeMtd(DateTime val) { }

        internal ReadMe(int blah) { }
    }
}", gen);

                var d = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.NoInstanceProvider, d);
            }

            //  bad instance provider attribute
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer(
        InstanceProviderType = typeof(ReadMe),
        InstanceProviderType = typeof(ReadMe),

        InstanceProviderMethodName = nameof(ReadMe.IP)
    )]
    class ReadMe
    {
        public int Foo { get; set; }

        public static bool IP(out ReadMe val)
        {
            val = new ReadMe();
            return true;
        }
    }
}", gen);

                var d = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.InstanceProviderTypeSpecifiedMultipleTimes, d);
            }
        }

        [Fact]
        public async Task ConstructorInstanceProvidersAsync()
        {
            // non-default
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(ParserType = typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        [DeserializerInstanceProvider]
        internal ReadMe([DeserializerMember(ParserType = typeof(ReadMe), ParserMethodName=nameof(ForDouble))]double foo) { }

        internal static bool ForInt(ReadOnlySpan<char> data, in ReadContext _, out int val)
        {
            val = default;
            return false;
        }

        internal static bool ForDouble(ReadOnlySpan<char> data, in ReadContext _, out double val)
        {
            val = default;
            return false;
        }
    }
}", gen);

                var ip = Assert.Single(gen.InstanceProviders);
                Assert.True(ip.Value.IsConstructor);
                Assert.Collection(
                    ip.Value.Method.Parameters,
                    p =>
                    {
                        Assert.Equal("foo", p.Name);
                    }
                );

                var members = gen.Members[ip.Key];
                {
                    var bar = Assert.Single(members, x => x.Name == "Bar");
                    Assert.False(bar.IsRequired);
                    Assert.Equal("ForInt", (bar.Parser.Method.DeclaringSyntaxReferences.Single().GetSyntax() as MethodDeclarationSyntax).Identifier.ValueText);
                    Assert.Equal("Bar", bar.Setter.Property.Name);
                    Assert.Null(bar.Order);
                    Assert.Null(bar.Reset);
                }
                {
                    var foo = Assert.Single(members, x => x.Name == "foo");
                    Assert.True(foo.IsRequired);
                    Assert.Equal("ForDouble", (foo.Parser.Method.DeclaringSyntaxReferences.Single().GetSyntax() as MethodDeclarationSyntax).Identifier.ValueText);
                    Assert.Equal("foo", foo.Setter.Parameter.Name);
                    Assert.Null(foo.Order);
                    Assert.Null(foo.Reset);
                }
            }
        }

        [Fact]
        public async Task BadConstructorInstanceProvidersAsync()
        {
            // both provided
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer(InstanceProviderType = typeof(ReadMe), InstanceProviderMethodName = nameof(ReadMe.InstanceProvider)]
    class ReadMe
    {
        [DeserializerMember(ParserType = typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        [DeserializerInstanceProvider]
        internal ReadMe() { }

        internal static bool ForInt(ReadOnlySpan<char> data, in ReadContext _, out int val)
        {
            val = default;
            return false;
        }

        internal static bool InstanceProvider(in ReadContext _, out ReadMe row)
        {
            row = new ReadMe();
            return true;
        }
    }
}", gen);
                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.InstanceProviderConstructorAndMethodProvided, diag);
            }

            // parameters not annotated
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(ParserType = typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        [DeserializerInstanceProvider]
        internal ReadMe(double foo) { }

        internal static bool ForInt(ReadOnlySpan<char> data, in ReadContext _, out int val)
        {
            val = default;
            return false;
        }

        internal static bool InstanceProvider(in ReadContext _, out ReadMe row)
        {
            row = new ReadMe();
            return true;
        }
    }
}", gen);
                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.AllConstructorParametersMustBeMembers, diag);
            }

            // parameter annotated on method, not constructor
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(ParserType = typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        internal void Foo([DeserializerMember(ParserType = typeof(ReadMe), ParserMethodName=nameof(ForInt))]int foo) { }

        internal static bool ForInt(ReadOnlySpan<char> data, in ReadContext _, out int val)
        {
            val = default;
            return false;
        }

        internal static bool InstanceProvider(in ReadContext _, out ReadMe row)
        {
            row = new ReadMe();
            return true;
        }
    }
}", gen);
                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.DeserializableMemberOnNonConstructorParameter, diag);
            }

            // constructor parameter annotated, but constructor not annotated
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(ParserType = typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        internal ReadMe() { }

        internal ReadMe([DeserializerMember(ParserType = typeof(ReadMe), ParserMethodName=nameof(ForInt))]int foo) { }

        internal static bool ForInt(ReadOnlySpan<char> data, in ReadContext _, out int val)
        {
            val = default;
            return false;
        }

        internal static bool InstanceProvider(in ReadContext _, out ReadMe row)
        {
            row = new ReadMe();
            return true;
        }
    }
}", gen);
                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.ConstructorHasMembersButIsntInstanceProvider, diag);
            }

            // not all constructor parameters annotated
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(ParserType = typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        internal ReadMe() { }

        [DeserializerInstanceProvider]
        internal ReadMe([DeserializerMember(ParserType = typeof(ReadMe), ParserMethodName=nameof(ForInt))]int foo, int bar) { }

        internal static bool ForInt(ReadOnlySpan<char> data, in ReadContext _, out int val)
        {
            val = default;
            return false;
        }

        internal static bool InstanceProvider(in ReadContext _, out ReadMe row)
        {
            row = new ReadMe();
            return true;
        }
    }
}", gen);
                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.AllConstructorParametersMustBeMembers, diag);
            }

            // parameters annotated with wrong attributes
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [AttributeUsage(AttributeTargets.All)]
    class BadAttribute: Attribute
    {
    }

    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(ParserType = typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        internal ReadMe() { }

        [DeserializerInstanceProvider]
        internal ReadMe([BadAttribute]int foo) { }

        internal static bool ForInt(ReadOnlySpan<char> data, in ReadContext _, out int val)
        {
            val = default;
            return false;
        }

        internal static bool InstanceProvider(in ReadContext _, out ReadMe row)
        {
            row = new ReadMe();
            return true;
        }
    }
}", gen);
                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.AllConstructorParametersMustBeMembers, diag);
            }

            // parameters annotated with bad attribute syntax
            {
                var gen = new DeserializerGenerator();
                var (_, diags) = await RunSourceGeneratorAsync(
    @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        [DeserializerMember(ParserType = typeof(ReadMe), ParserMethodName=nameof(ForInt))]
        public int Bar { get; set; }

        internal ReadMe() { }

        [DeserializerInstanceProvider]
        internal ReadMe([?]int foo) { }

        internal static bool ForInt(ReadOnlySpan<char> data, in ReadContext _, out int val)
        {
            val = default;
            return false;
        }

        internal static bool InstanceProvider(in ReadContext _, out ReadMe row)
        {
            row = new ReadMe();
            return true;
        }
    }
}", gen);
                var diag = Assert.Single(diags);
                AssertDiagnostic(Diagnostics.AllConstructorParametersMustBeMembers, diag);
            }
        }

        [Fact]
        public async Task InheritedMembersAsync()
        {
            var gen = new DeserializerGenerator();
            var (_, diags) = await RunSourceGeneratorAsync(
@"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    abstrct class ReadMeBase
    {
        public int Bar { get; set; }
    }

    [GenerateDeserializer]
    class ReadMe : ReadMeBase
    {
        public string Fizz { get; set; }
    }
}", gen);

            Assert.Empty(diags);

            var toSerialize = Assert.Single(gen.Members);
            Assert.Equal("Foo.ReadMe", toSerialize.Key.ToFullyQualifiedName());
            Assert.Collection(
                toSerialize.Value,
                bar =>
                {
                    Assert.Equal("Bar", bar.Name);

                    var prop = bar.Setter.Property;
                    Assert.NotNull(prop);
                    Assert.Equal("Bar", prop.Name);
                },
                fizz =>
                {
                    Assert.Equal("Fizz", fizz.Name);

                    var prop = fizz.Setter.Property;
                    Assert.NotNull(prop);
                    Assert.Equal("Fizz", fizz.Name);
                }
            );
        }

        [Fact]
        public async Task DefaultResetAsync()
        {
            var gen = new DeserializerGenerator();
            var (_, diags) = await RunSourceGeneratorAsync(
@"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    class ReadMe
    {
        public string Fizz { get; set; }

        public void ResetFizz() { }
    }
}", gen);

            Assert.Empty(diags);

            var toDeserialize = Assert.Single(gen.Members);
            Assert.Equal("Foo.ReadMe", toDeserialize.Key.ToFullyQualifiedName());
            Assert.Collection(
                toDeserialize.Value,
                fizz =>
                {
                    Assert.Equal("Fizz", fizz.Name);

                    var prop = fizz.Setter.Property;
                    Assert.NotNull(prop);
                    Assert.Equal("Fizz", fizz.Name);

                    var reset = fizz.Reset?.Method;
                    Assert.NotNull(reset);
                    Assert.Equal("ResetFizz", reset.Name);
                }
            );
        }

        [Fact]
        public async Task RecordsAsync()
        {
            var gen = new DeserializerGenerator();
            var (_, diags) = await RunSourceGeneratorAsync(
@"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateDeserializer]
    record ReadMe(int A, string B)
    {
        public double C { get; init; }
    }
}", gen);

            Assert.Empty(diags);

            var toDeserialize = Assert.Single(gen.Members);
            Assert.Equal("Foo.ReadMe", toDeserialize.Key.ToFullyQualifiedName());
            Assert.Collection(
                toDeserialize.Value,
                a =>
                {
                    Assert.Equal("A", a.Name);

                    var param = a.Setter.Parameter;
                    Assert.NotNull(param);
                    Assert.Equal("A", param.Name);
                },
                b =>
                {
                    Assert.Equal("B", b.Name);

                    var param = b.Setter.Parameter;
                    Assert.NotNull(param);
                    Assert.Equal("B", param.Name);
                },
                c =>
                {
                    Assert.Equal("C", c.Name);

                    var prop = c.Setter.Property;
                    Assert.NotNull(prop);
                    Assert.Equal("C", prop.Name);
                }
            );
        }

        [Fact]
        public async Task MultipleNamesAsync()
        {
            var gen = new DeserializerGenerator();
            var (_, diags) = await RunSourceGeneratorAsync(
@"
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{
    [GenerateDeserializer]
    public class Bar 
    {
        [DeserializerMember(Name = ""Hello"")]
        [DataMember(Name = ""World"")]
        public string A { get; set; }
    }
}", gen);

            var d = Assert.Single(diags);
            AssertDiagnostic(Diagnostics.NameSpecifiedMultipleTimes, d);
        }

        [Fact]
        public async Task IsRequiredAsync()
        {
            // method member
            {
                // required
                {
                    var gen = new DeserializerGenerator();
                    var (_, diags) = 
                        await RunSourceGeneratorAsync(@"
                            using Cesil;

                            namespace Foo 
                            {
                                [GenerateDeserializer]
                                public class Bar 
                                {
                                    [DeserializerMember(Name = ""Hello"", MemberRequired = MemberRequired.Yes)]
                                    public void SetA(string a) => """";
                                }
                            }", 
                            gen
                        );

                    Assert.Empty(diags);

                    var foo = Assert.Single(gen.Members);
                    Assert.Collection(
                        foo.Value,
                        a =>
                        {
                            Assert.Equal("Hello", a.Name);
                            Assert.True(a.IsRequired);
                        }
                    );
                }

                // not required
                {
                    var gen = new DeserializerGenerator();
                    var (_, diags) =
                        await RunSourceGeneratorAsync(@"
                            using Cesil;

                            namespace Foo 
                            {
                                [GenerateDeserializer]
                                public class Bar 
                                {
                                    [DeserializerMember(Name = ""Hello"", MemberRequired = MemberRequired.No)]
                                    public void SetA(string a) => """";
                                }
                            }",
                            gen
                        );

                    Assert.Empty(diags);

                    var foo = Assert.Single(gen.Members);
                    Assert.Collection(
                        foo.Value,
                        a =>
                        {
                            Assert.Equal("Hello", a.Name);
                            Assert.False(a.IsRequired);
                        }
                    );
                }

                // default
                {
                    var gen = new DeserializerGenerator();
                    var (_, diags) =
                        await RunSourceGeneratorAsync(@"
                            using Cesil;

                            namespace Foo 
                            {
                                [GenerateDeserializer]
                                public class Bar 
                                {
                                    [DeserializerMember(Name = ""Hello"")]
                                    public void SetA(string a) => """";
                                }
                            }",
                            gen
                        );

                    Assert.Empty(diags);

                    var foo = Assert.Single(gen.Members);
                    Assert.Collection(
                        foo.Value,
                        a =>
                        {
                            Assert.Equal("Hello", a.Name);
                            Assert.False(a.IsRequired);
                        }
                    );
                }
            }

            // property member
            {
                // required
                {
                    var gen = new DeserializerGenerator();
                    var (_, diags) =
                        await RunSourceGeneratorAsync(@"
                            using Cesil;

                            namespace Foo 
                            {
                                [GenerateDeserializer]
                                public class Bar 
                                {
                                    [DeserializerMember(Name = ""Hello"", MemberRequired = MemberRequired.Yes)]
                                    public string A { get; set; }
                                }
                            }",
                            gen
                        );

                    Assert.Empty(diags);

                    var foo = Assert.Single(gen.Members);
                    Assert.Collection(
                        foo.Value,
                        a =>
                        {
                            Assert.Equal("Hello", a.Name);
                            Assert.True(a.IsRequired);
                        }
                    );
                }

                // not required
                {
                    var gen = new DeserializerGenerator();
                    var (_, diags) =
                        await RunSourceGeneratorAsync(@"
                            using Cesil;

                            namespace Foo 
                            {
                                [GenerateDeserializer]
                                public class Bar 
                                {
                                    [DeserializerMember(Name = ""Hello"", MemberRequired = MemberRequired.No)]
                                    public string A { get; set; }
                                }
                            }",
                            gen
                        );

                    Assert.Empty(diags);

                    var foo = Assert.Single(gen.Members);
                    Assert.Collection(
                        foo.Value,
                        a =>
                        {
                            Assert.Equal("Hello", a.Name);
                            Assert.False(a.IsRequired);
                        }
                    );
                }

                // default
                {
                    var gen = new DeserializerGenerator();
                    var (_, diags) =
                        await RunSourceGeneratorAsync(@"
                            using Cesil;

                            namespace Foo 
                            {
                                [GenerateDeserializer]
                                public class Bar 
                                {
                                    [DeserializerMember(Name = ""Hello"")]
                                    public string A { get; set; }
                                }
                            }",
                            gen
                        );

                    Assert.Empty(diags);

                    var foo = Assert.Single(gen.Members);
                    Assert.Collection(
                        foo.Value,
                        a =>
                        {
                            Assert.Equal("Hello", a.Name);
                            Assert.False(a.IsRequired);
                        }
                    );
                }
            }

            // constructor parameter member
            {
                // required
                {
                    var gen = new DeserializerGenerator();
                    var (_, diags) =
                        await RunSourceGeneratorAsync(@"
                            using Cesil;

                            namespace Foo 
                            {
                                [GenerateDeserializer]
                                public class Bar 
                                {
                                    [DeserializerInstanceProvider]
                                    public Bar([DeserializerMember(Name = ""Hello"", MemberRequired = MemberRequired.Yes)]string a) { }
                                }
                            }",
                            gen
                        );

                    Assert.Empty(diags);

                    var foo = Assert.Single(gen.Members);
                    Assert.Collection(
                        foo.Value,
                        a =>
                        {
                            Assert.Equal("Hello", a.Name);
                            Assert.True(a.IsRequired);
                        }
                    );
                }

                // not required
                {
                    var gen = new DeserializerGenerator();
                    var (_, diags) =
                        await RunSourceGeneratorAsync(@"
                            using Cesil;

                            namespace Foo 
                            {
                                [GenerateDeserializer]
                                public class Bar 
                                {
                                    [DeserializerInstanceProvider]
                                    public Bar([DeserializerMember(Name = ""Hello"", MemberRequired = MemberRequired.No)]string a) { }
                                }
                            }",
                            gen
                        );

                    var diag = Assert.Single(diags);
                    AssertDiagnostic(Diagnostics.ParametersMustBeRequired, diag);
                }

                // default
                {
                    var gen = new DeserializerGenerator();
                    var (_, diags) =
                        await RunSourceGeneratorAsync(@"
                            using Cesil;

                            namespace Foo 
                            {
                                [GenerateDeserializer]
                                public class Bar 
                                {
                                    [DeserializerInstanceProvider]
                                    public Bar([DeserializerMember(Name = ""Hello"")]string a) { }
                                }
                            }",
                            gen
                        );

                    Assert.Empty(diags);

                    var foo = Assert.Single(gen.Members);
                    Assert.Collection(
                        foo.Value,
                        a =>
                        {
                            Assert.Equal("Hello", a.Name);
                            Assert.True(a.IsRequired);
                        }
                    );
                }
            }

            // IsRequired via DataMember
            {
                var gen = new DeserializerGenerator();
                var (_, diags) =
                    await RunSourceGeneratorAsync(@"
                            using Cesil;
                            using System.Runtime.Serialization;

                            namespace Foo 
                            {
                                [GenerateDeserializer]
                                public class Bar 
                                {
                                    [DataMember(IsRequired = true)]
                                    public string Foo { get; set; }
                                }
                            }",
                        gen
                    );

                Assert.Empty(diags);

                var foo = Assert.Single(gen.Members);
                Assert.Collection(
                    foo.Value,
                    a =>
                    {
                        Assert.Equal("Foo", a.Name);
                        Assert.True(a.IsRequired);
                    }
                );
            }
        }

        [Fact]
        public async Task InferDefaultResetAsync()
        {
            // non-method has name collision
            {
                var comp =
                    await GetCompilationAsync(@"
                        using Cesil;

                        namespace Test
                        {
                            public class Foo
                            {
                                public string Bar { get; set; }

                                public bool ResetBar { get; set; }
                            }
                        }",
                        nameof(InferDefaultResetAsync),
                        NullableContextOptions.Disable
                    );

                var attrMembers = comp.GetAttributedMembers();

                var t = comp.GetTypeByMetadataName("Test.Foo");

                var builtIns = BuiltInTypes.Create(comp);
                Assert.True(FrameworkTypes.TryCreate(comp, builtIns, out var framework));
                Assert.True(CesilTypes.TryCreate(comp, out var ourTypes));
                var dts = new DeserializerTypes(builtIns, framework, ourTypes);

                var diags = ImmutableArray<Diagnostic>.Empty;
                var res = SourceGenerator.DeserializableMember.InferDefaultReset(attrMembers, dts, t, "Bar", ImmutableArray<AttributeSyntax>.Empty, null, ref diags);
                Assert.Null(res);
                Assert.Empty(diags);
            }

            // method returns a value
            {
                var comp =
                    await GetCompilationAsync(@"
                        using Cesil;

                        namespace Test
                        {
                            public class Foo
                            {
                                public string Bar { get; set; }

                                public string ResetBar() => "";
                            }
                        }",
                        nameof(InferDefaultResetAsync),
                        NullableContextOptions.Disable
                    );

                var attrMembers = comp.GetAttributedMembers();

                var t = comp.GetTypeByMetadataName("Test.Foo");

                var builtIns = BuiltInTypes.Create(comp);
                Assert.True(FrameworkTypes.TryCreate(comp, builtIns, out var framework));
                Assert.True(CesilTypes.TryCreate(comp, out var ourTypes));
                var dts = new DeserializerTypes(builtIns, framework, ourTypes);

                var diags = ImmutableArray<Diagnostic>.Empty;
                var res = SourceGenerator.DeserializableMember.InferDefaultReset(attrMembers, dts, t, "Bar", ImmutableArray<AttributeSyntax>.Empty, null, ref diags);
                Assert.Null(res);
                Assert.Empty(diags);
            }

            // static, no parameters, so good
            {
                var comp =
                    await GetCompilationAsync(@"
                        using Cesil;

                        namespace Test
                        {
                            public class Foo
                            {
                                public string Bar { get; set; }

                                public static void ResetBar() { }
                            }
                        }",
                        nameof(InferDefaultResetAsync),
                        NullableContextOptions.Disable
                    );

                var attrMembers = comp.GetAttributedMembers();

                var t = comp.GetTypeByMetadataName("Test.Foo");

                var builtIns = BuiltInTypes.Create(comp);
                Assert.True(FrameworkTypes.TryCreate(comp, builtIns, out var framework));
                Assert.True(CesilTypes.TryCreate(comp, out var ourTypes));
                var dts = new DeserializerTypes(builtIns, framework, ourTypes);

                var diags = ImmutableArray<Diagnostic>.Empty;
                var res = SourceGenerator.DeserializableMember.InferDefaultReset(attrMembers, dts, t, "Bar", ImmutableArray<AttributeSyntax>.Empty, null, ref diags);
                Assert.Equal("ResetBar", res.Method.Name);
            }

            // static, takes a context, so good
            {
                var comp =
                    await GetCompilationAsync(@"
                        using Cesil;

                        namespace Test
                        {
                            public class Foo
                            {
                                public string Bar { get; set; }

                                public static void ResetBar(in ReadContext ctx) { }
                            }
                        }",
                        nameof(InferDefaultResetAsync),
                        NullableContextOptions.Disable
                    );

                var attrMembers = comp.GetAttributedMembers();

                var t = comp.GetTypeByMetadataName("Test.Foo");

                var builtIns = BuiltInTypes.Create(comp);
                Assert.True(FrameworkTypes.TryCreate(comp, builtIns, out var framework));
                Assert.True(CesilTypes.TryCreate(comp, out var ourTypes));
                var dts = new DeserializerTypes(builtIns, framework, ourTypes);

                var diags = ImmutableArray<Diagnostic>.Empty;
                var res = SourceGenerator.DeserializableMember.InferDefaultReset(attrMembers, dts, t, "Bar", ImmutableArray<AttributeSyntax>.Empty, null, ref diags);
                Assert.Equal("ResetBar", res.Method.Name);
            }

            // static, too many parameters, no good
            {
                var comp =
                    await GetCompilationAsync(@"
                        using Cesil;

                        namespace Test
                        {
                            public class Foo
                            {
                                public string Bar { get; set; }

                                public static void ResetBar(Foo bar, in ReadContext ctx) { }
                            }
                        }",
                        nameof(InferDefaultResetAsync),
                        NullableContextOptions.Disable
                    );

                var attrMembers = comp.GetAttributedMembers();

                var t = comp.GetTypeByMetadataName("Test.Foo");

                var builtIns = BuiltInTypes.Create(comp);
                Assert.True(FrameworkTypes.TryCreate(comp, builtIns, out var framework));
                Assert.True(CesilTypes.TryCreate(comp, out var ourTypes));
                var dts = new DeserializerTypes(builtIns, framework, ourTypes);

                var diags = ImmutableArray<Diagnostic>.Empty;
                var res = SourceGenerator.DeserializableMember.InferDefaultReset(attrMembers, dts, t, "Bar", ImmutableArray<AttributeSyntax>.Empty, null, ref diags);
                Assert.Null(res);
            }

            // instance, takes nothing, so good
            {
                var comp =
                    await GetCompilationAsync(@"
                        using Cesil;

                        namespace Test
                        {
                            public class Foo
                            {
                                public string Bar { get; set; }

                                public void ResetBar() { }
                            }
                        }",
                        nameof(InferDefaultResetAsync),
                        NullableContextOptions.Disable
                    );

                var attrMembers = comp.GetAttributedMembers();

                var t = comp.GetTypeByMetadataName("Test.Foo");

                var builtIns = BuiltInTypes.Create(comp);
                Assert.True(FrameworkTypes.TryCreate(comp, builtIns, out var framework));
                Assert.True(CesilTypes.TryCreate(comp, out var ourTypes));
                var dts = new DeserializerTypes(builtIns, framework, ourTypes);

                var diags = ImmutableArray<Diagnostic>.Empty;
                var res = SourceGenerator.DeserializableMember.InferDefaultReset(attrMembers, dts, t, "Bar", ImmutableArray<AttributeSyntax>.Empty, null, ref diags);
                Assert.Equal("ResetBar", res.Method.Name);
            }

            // instace, takes parameter, so no good
            {
                var comp =
                    await GetCompilationAsync(@"
                        using Cesil;

                        namespace Test
                        {
                            public class Foo
                            {
                                public string Bar { get; set; }

                                public void ResetBar(string foo) { }
                            }
                        }",
                        nameof(InferDefaultResetAsync),
                        NullableContextOptions.Disable
                    );

                var attrMembers = comp.GetAttributedMembers();

                var t = comp.GetTypeByMetadataName("Test.Foo");

                var builtIns = BuiltInTypes.Create(comp);
                Assert.True(FrameworkTypes.TryCreate(comp, builtIns, out var framework));
                Assert.True(CesilTypes.TryCreate(comp, out var ourTypes));
                var dts = new DeserializerTypes(builtIns, framework, ourTypes);

                var diags = ImmutableArray<Diagnostic>.Empty;
                var res = SourceGenerator.DeserializableMember.InferDefaultReset(attrMembers, dts, t, "Bar", ImmutableArray<AttributeSyntax>.Empty, null, ref diags);
                Assert.Null(res);
            }
        }

        private void AssertDiagnostic(Func<Location, string, string[], Diagnostic> shouldBe, Diagnostic actuallyIs)
        {
            var id = shouldBe(null, "foo", new[] { "fizz", "buzz" }).Id;
            Assert.Equal(id, actuallyIs.Id);
        }

        private void AssertDiagnostic(Func<Location, ITypeSymbol, IParameterSymbol, Diagnostic> shouldBe, Diagnostic actuallyIs)
        {
            var id = shouldBe(null, Type, Parameter).Id;
            Assert.Equal(id, actuallyIs.Id);
        }

        private void AssertDiagnostic(Func<Location, ITypeSymbol, IParameterSymbol, IMethodSymbol, Diagnostic> shouldBe, Diagnostic actuallyIs)
        {
            var id = shouldBe(null, Type, Parameter, Method).Id;
            Assert.Equal(id, actuallyIs.Id);
        }

        private void AssertDiagnostic(Func<Location, ITypeSymbol, IPropertySymbol, Diagnostic> shouldBe, Diagnostic actuallyIs)
        {
            var id = shouldBe(null, Type, Property).Id;
            Assert.Equal(id, actuallyIs.Id);
        }

        private static void AssertDiagnostic(Func<Location, Diagnostic> shouldBe, Diagnostic actuallyIs)
        {
            var id = shouldBe(null).Id;
            Assert.Equal(id, actuallyIs.Id);
        }

        private void AssertDiagnostic(Func<Location, IMethodSymbol, Diagnostic> shouldBe, Diagnostic actuallyIs)
        {
            var id = shouldBe(null, Method).Id;
            Assert.Equal(id, actuallyIs.Id);
        }

        private void AssertDiagnostic(Func<Location, ITypeSymbol, Diagnostic> shouldBe, Diagnostic actuallyIs)
        {
            var id = shouldBe(null, Type).Id;
            Assert.Equal(id, actuallyIs.Id);
        }

        private void AssertDiagnostic(Func<Location, ITypeSymbol, IMethodSymbol, Diagnostic> shouldBe, Diagnostic actuallyIs)
        {
            var id = shouldBe(null, Type, Method).Id;
            Assert.Equal(id, actuallyIs.Id);
        }

        private void AssertDiagnostic(Func<Location, string, string, Diagnostic> shouldBe, Diagnostic actuallyIs)
        {
            var id = shouldBe(null, "a", "b").Id;
            Assert.Equal(id, actuallyIs.Id);
        }

        private void AssertDiagnostic(Func<Location, IMethodSymbol, ITypeSymbol, Diagnostic> shouldBe, Diagnostic actuallyIs)
        {
            var id = shouldBe(null, Method, Type).Id;
            Assert.Equal(id, actuallyIs.Id);
        }
    }
}
