using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cesil.SourceGenerator.Tests
{
    internal static class TestHelper
    {
        internal static string GetFlaggedSource(Diagnostic diag)
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

        internal static async Task<(Compilation Compilation, ImmutableArray<Diagnostic> Diagnostic)> RunSourceGeneratorAsync(
            string testFile,
            ISourceGenerator generator,
            NullableContextOptions nullableContext = NullableContextOptions.Enable,
            [CallerMemberName] string caller = null
        )
        {
            var compilation = await GetCompilationAsync(testFile, caller, nullableContext);

            var parseOptions = (CSharpParseOptions)compilation.SyntaxTrees.ElementAt(0).Options;

            var generators = ImmutableArray.Create(generator);

            GeneratorDriver driver = CSharpGeneratorDriver.Create(generators, parseOptions: parseOptions);

            driver.RunGeneratorsAndUpdateCompilation(compilation, out var producedCompilation, out var diagnostics);

            return (producedCompilation, diagnostics);
        }

        internal static Task<Compilation> GetCompilationAsync(
            string testFile,
            string caller,
            NullableContextOptions nullableContext
        )
        {
            var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator);
            var systemAssemblies = trustedAssemblies.Where(p => Path.GetFileName(p).StartsWith("System.")).ToList();

            var references = systemAssemblies.Select(s => MetadataReference.CreateFromFile(s)).ToList();

            var projectName = $"Cesil.SourceGenerator.Tests.{nameof(TestHelper)}";
            var projectId = ProjectId.CreateNewId(projectName);

            var compilationOptions =
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    allowUnsafe: true,
                    nullableContextOptions: nullableContext
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


            var cesilRef = GetCesilReference();
            project = project.AddMetadataReference(cesilRef);

            var netstandardRef = GetNetStandard20Reference();
            project = project.AddMetadataReference(netstandardRef);

            return project.GetCompilationAsync();
        }

        private static MetadataReference GetCesilReference()
        {
            var optsType = typeof(Options);
            var cesilLoc = optsType.Assembly.Location;

            return MetadataReference.CreateFromFile(cesilLoc);
        }

        private static MetadataReference GetNetStandard20Reference()
        {
            var asm = Assembly.Load("netstandard, Version=2.0.0.0").Location;

            return MetadataReference.CreateFromFile(asm);
        }
    }
}
