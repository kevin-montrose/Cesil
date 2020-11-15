using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Xunit;

namespace Cesil.SourceGenerator.Tests
{
    public class GenerationTests
    {
        [Fact]
        public async Task SimpleAsync()
        {
            var type = await RunSourceGeneratorAsync(
                "Foo.WriteMe",
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
        public string Fizz = """";
        [Cesil.GenerateSerializableMemberAttribute(Name=""Hello"", FormatterType=typeof(WriteMe), FormatterMethodName=nameof(ForDateTime))]
        public DateTime SomeMtd() => new DateTime(2020, 11, 15, 0, 0, 0);

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;

        public static bool ForString(string val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;

        public static bool ForDateTime(DateTime val, in WriteContext ctx, IBufferWriter<char> buffer)
        => false;
    }
}");

            var members = TypeDescribers.AheadOfTime.EnumerateMembersToSerialize(type);
            Assert.Collection(
                members,
                bar =>
                {
                    Assert.True(bar.IsBackedByGeneratedMethod);
                    Assert.Equal("Bar", bar.Name);
                    Assert.True(bar.EmitDefaultValue);
                    Assert.Equal("ForInt", bar.Formatter.Method.Value.Name);
                    Assert.Equal("get_Bar", bar.Getter.Method.Value.Name);
                    Assert.False(bar.ShouldSerialize.HasValue);
                },
                fizz =>
                {
                    Assert.True(fizz.IsBackedByGeneratedMethod);
                    Assert.Equal("Fizz", fizz.Name);
                    Assert.True(fizz.EmitDefaultValue);
                    Assert.Equal("ForString", fizz.Formatter.Method.Value.Name);
                    Assert.Equal("Fizz", fizz.Getter.Field.Value.Name);
                    Assert.False(fizz.ShouldSerialize.HasValue);
                },
                hello =>
                {
                    Assert.True(hello.IsBackedByGeneratedMethod);
                    Assert.Equal("Hello", hello.Name);
                    Assert.True(hello.EmitDefaultValue);
                    Assert.Equal("ForDateTime", hello.Formatter.Method.Value.Name);
                    Assert.Equal("SomeMtd", hello.Getter.Method.Value.Name);
                    Assert.False(hello.ShouldSerialize.HasValue);
                }
            );

        }

        private static async Task<System.Reflection.TypeInfo> RunSourceGeneratorAsync(
            string typeName,
            string testFile,
            [CallerMemberName] string caller = null
        )
        {
            // todo: add DeserializerGenerator once we have it
            var serializer = new SerializerGenerator();
            var generators = ImmutableArray.Create<ISourceGenerator>(serializer);

            var compilation = await GetCompilationAsync(testFile, caller);

            GeneratorDriver driver = new CSharpGeneratorDriver(parseOptions: new CSharpParseOptions(LanguageVersion.CSharp8), generators, new DefaultAnalyzerConfigOptionsProvider(), ImmutableArray<AdditionalText>.Empty);

            driver.RunFullGeneration(compilation, out var producedCompilation, out var diagnostics);

            Assert.Empty(diagnostics);

            var outputFile = Path.GetTempFileName();

            var res = producedCompilation.Emit(outputFile);

            Assert.True(res.Success);

            var asm = Assembly.LoadFile(outputFile);
            var ret = Assert.Single(asm.GetTypes().Where(t => t.FullName == typeName));

            return ret.GetTypeInfo();
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

            var cesil = CompileCesilToAssembly();
            project = project.AddMetadataReference(cesil);

            project = project.AddDocument(csFile, testFile).Project;

            return project.GetCompilationAsync();
        }

        private static MetadataReference CompileCesilToAssembly()
        {
            var optsType = typeof(Options);
            var cesilLoc = optsType.Assembly.Location;

            return MetadataReference.CreateFromFile(cesilLoc);
        }
    }
}
