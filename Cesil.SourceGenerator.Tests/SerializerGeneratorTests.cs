using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
namespace Foo 
{   
    [Cesil.GenerateSerializableAttribute]
    class WriteMe
    {
        [Cesil.GenerateSerializableMemberAttribute]
        public int Bar { get; set; }
        [Cesil.GenerateSerializableMemberAttribute]
        public string Fizz;
        [Cesil.GenerateSerializableMemberAttribute(Name=""Hello"")]
        public string SomeMtd() => ""World"";
    }
}", gen);

            Assert.Empty(diags);
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

            var toAddFilePath = Path.Combine(cesilRootDir, "Interface");
            toAddFilePath = Path.Combine(toAddFilePath, "Attributes");
            toAddFilePath = Path.Combine(toAddFilePath, "SerializeAttributes.cs");

            var fileText = File.ReadAllText(toAddFilePath);
            project = project.AddDocument(Path.GetFileName(toAddFilePath), fileText).Project;

            return project.GetCompilationAsync();
        }
    }
}
