using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Cesil.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cesil.Tests
{
    internal static class SourceGeneratorTestHelper
    {
        internal static AttributedMembers GetAttributedMembers(this Compilation compilation)
        {
            var generator = new AttriberMembersGenerator();
            var parseOptions = (CSharpParseOptions)compilation.SyntaxTrees.ElementAt(0).Options;

            var generators = ImmutableArray.Create(generator);

            GeneratorDriver driver = CSharpGeneratorDriver.Create(generators, parseOptions: parseOptions);

            driver.RunGeneratorsAndUpdateCompilation(compilation, out var producedCompilation, out var diagnostics);

            return generator.Members;
        }

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
            NullableContextOptions nullableContext,
            bool addCesilReferences = true,
            IEnumerable<string> doNotAddReferences = null
        )
        {
            var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator);
            var systemAssemblies = trustedAssemblies.Where(p => Path.GetFileName(p).StartsWith("System.")).ToList();

            var references = systemAssemblies.Select(s => MetadataReference.CreateFromFile(s)).ToList();

            var projectName = $"Cesil.Tests.{nameof(SourceGeneratorTestHelper)}";
            var projectId = ProjectId.CreateNewId(projectName);

            var compilationOptions =
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    allowUnsafe: true,
                    nullableContextOptions: nullableContext
                );

            var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp9);

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

            var x = references.Where(r => r.FilePath.Contains("Serialization")).ToList();

            foreach (var reference in references)
            {
                var path = reference.Display ?? "";
                var ix = path.LastIndexOf('\\');
                if(ix != -1)
                {
                    path = path.Substring(ix + 1);
                }

                var iy = path.LastIndexOf('.');
                if(iy != -1)
                {
                    path = path.Substring(0, iy);
                }

                if (doNotAddReferences?.Contains(path) ?? false)
                {
                    continue;
                }

                solution = solution.AddMetadataReference(projectId, reference);
            }

            var csFile = $"{caller}.cs";
            var docId = DocumentId.CreateNewId(projectId, csFile);

            var project = solution.GetProject(projectId);

            project = project.AddDocument(csFile, testFile).Project;

            // find the Cesil folder to include code from
            if (addCesilReferences)
            {
                var cesilRef = GetCesilReference();
                project = project.AddMetadataReference(cesilRef);
            }

            var netstandardRef = GetNetStandard20Reference();
            project = project.AddMetadataReference(netstandardRef);

            return project.GetCompilationAsync();
        }

        internal static MetadataReference GetCesilReference()
        {
            var optsType = typeof(Options);
            var cesilLoc = optsType.Assembly.Location;

            return MetadataReference.CreateFromFile(cesilLoc);
        }

        internal static MetadataReference GetNetStandard20Reference()
        {
            var asm = Assembly.Load("netstandard, Version=2.0.0.0").Location;

            return MetadataReference.CreateFromFile(asm);
        }
    }
}
