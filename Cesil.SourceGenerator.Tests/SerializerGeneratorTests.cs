using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Cesil.SourceGenerator.Tests
{
    public class SerializerGeneratorTests
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
        public DateTime SomeMtd() => ""World"";

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

        private sealed class DefaultAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
        {
            private sealed class DefaultAnalyzerConfigOptions : AnalyzerConfigOptions
            {
                public override bool TryGetValue(string key, [NotNullWhen(true)] out string value)
                {
                    value = null;
                    return false;
                }
            }

            internal DefaultAnalyzerConfigOptionsProvider() { }

            public override AnalyzerConfigOptions GlobalOptions { get; }

            public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
            => new DefaultAnalyzerConfigOptions();

            public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
            => new DefaultAnalyzerConfigOptions();
        }

        private static Task<Compilation> GetCompilationAsync(
            string testFile,
            string caller
        )
        {
            var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator);
            var systemAssemblies = trustedAssemblies.Where(p => Path.GetFileName(p).StartsWith("System.")).ToList();

            var references = systemAssemblies.Select(s => MetadataReference.CreateFromFile(s)).ToList();

            var projectName = $"Cesil.SourceGenerator.Tests.{nameof(SerializerGeneratorTests)}";
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
