using System;
using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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
            var (comp, diags) = await RunSourceGeneratorAsync(
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
