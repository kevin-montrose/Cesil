using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

using static Cesil.SourceGenerator.Tests.TestHelper;

namespace Cesil.SourceGenerator.Tests
{
    public class DeserializerConfigurationFixture : IAsyncLifetime
    {
        internal IMethodSymbol Method { get; private set; }
        internal ITypeSymbol Type { get; private set; }
        internal IParameterSymbol Parameter { get; private set; }

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
    }
}", gen);

            Assert.Empty(diags);

            Type = Utils.NonNull(comp.GetTypeByMetadataName("System.String"));
            Method = Type.GetMembers().OfType<IMethodSymbol>().First();
            Parameter = Type.GetMembers().OfType<IMethodSymbol>().First(m => m.MethodKind == MethodKind.Constructor).Parameters.First();
        }

        public Task DisposeAsync()
        => Task.CompletedTask;
    }

    public class DeserializerConfigurationTests : IClassFixture<DeserializerConfigurationFixture>
    {
        private readonly ITypeSymbol Type;
        private readonly IParameterSymbol Parameter;
        private readonly IMethodSymbol Method;

        public DeserializerConfigurationTests(DeserializerConfigurationFixture fixture)
        {
            Type = fixture.Type;
            Parameter = fixture.Parameter;
            Method = fixture.Method;
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
        }

        [Fact]
        public async Task BadResetsAsync()
        {
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
        }

        [Fact]
        public async Task ConstructorInstanceProvidersAsync()
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
        }

        private void AssertDiagnostic(Func<Location, ITypeSymbol, IParameterSymbol, IMethodSymbol, Diagnostic> shouldBe, Diagnostic actuallyIs)
        {
            var id = shouldBe(null, Type, Parameter, Method).Id;
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
