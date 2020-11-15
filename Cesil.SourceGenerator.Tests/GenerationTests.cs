using System;
using System.Collections.Generic;
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
    public class WriteMe
    {
        [Cesil.GenerateSerializableMemberAttribute(FormatterType=typeof(WriteMe), FormatterMethodName=nameof(ForInt))]
        public int Bar { get; set; }
        [Cesil.GenerateSerializableMemberAttribute(FormatterType=typeof(WriteMe), FormatterMethodName=nameof(ForString))]
        public string Fizz = """";
        [Cesil.GenerateSerializableMemberAttribute(Name=""Hello"", FormatterType=typeof(WriteMe), FormatterMethodName=nameof(ForDateTime))]
        public DateTime SomeMtd() => new DateTime(2020, 11, 15, 0, 0, 0);

        public WriteMe() { }

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        {
            var span = buffer.GetSpan(100);
            if(!val.TryFormat(span, out var written))
            {
                return false;
            }

            buffer.Advance(written);
            return true;
        }

        public static bool ForString(string val, in WriteContext ctx, IBufferWriter<char> buffer)
        {
            var span = buffer.GetSpan(val.Length);
            val.AsSpan().CopyTo(span);

            buffer.Advance(val.Length);
            return true;
        }

        public static bool ForDateTime(DateTime val, in WriteContext ctx, IBufferWriter<char> buffer)
        {
            var span = buffer.GetSpan(4);
            if(!val.Year.TryFormat(span, out var written))
            {
                return false;
            }

            buffer.Advance(written);
            return true;
        }
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
                    Assert.Equal("__Column_0_Formatter", bar.Formatter.Method.Value.Name);
                    Assert.Equal("__Column_0_Getter", bar.Getter.Method.Value.Name);
                    Assert.False(bar.ShouldSerialize.HasValue);
                },
                fizz =>
                {
                    Assert.True(fizz.IsBackedByGeneratedMethod);
                    Assert.Equal("Fizz", fizz.Name);
                    Assert.True(fizz.EmitDefaultValue);
                    Assert.Equal("__Column_1_Formatter", fizz.Formatter.Method.Value.Name);
                    Assert.Equal("__Column_1_Getter", fizz.Getter.Method.Value.Name);
                    Assert.False(fizz.ShouldSerialize.HasValue);
                },
                hello =>
                {
                    Assert.True(hello.IsBackedByGeneratedMethod);
                    Assert.Equal("Hello", hello.Name);
                    Assert.True(hello.EmitDefaultValue);
                    Assert.Equal("__Column_2_Formatter", hello.Formatter.Method.Value.Name);
                    Assert.Equal("__Column_2_Getter", hello.Getter.Method.Value.Name);
                    Assert.False(hello.ShouldSerialize.HasValue);
                }
            );

            var rows =
                Create(
                    type,
                    r => { r.Bar = 123; r.Fizz = "abcd"; },
                    r => { r.Bar = 456; r.Fizz = "hello world"; },
                    r => { r.Bar = 789; r.Fizz = ""; }
                );

            var csv = Write(type, rows);
            Assert.Equal("Bar,Fizz,Hello\r\n123,abcd,2020\r\n456,hello world,2020\r\n789,,2020", csv);
        }

        private static string Write(System.Reflection.TypeInfo rowType, ImmutableArray<object> rows)
        {
            var writeImpl = WriteImplOfT.MakeGenericMethod(rowType);

            var ret = writeImpl.Invoke(null, new object[] { rows });

            return (string)ret;
        }

        private static readonly MethodInfo WriteImplOfT = typeof(GenerationTests).GetMethod(nameof(WriteImpl), BindingFlags.NonPublic | BindingFlags.Static);
        private static string WriteImpl<T>(ImmutableArray<object> rows)
        {
            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(TypeDescribers.AheadOfTime).ToOptions();
            var config = Configuration.For<T>(opts);

            using (var str = new StringWriter())
            {
                using (var csv = config.CreateWriter(str))
                {
                    csv.WriteAll(rows.Cast<T>());

                }

                return str.ToString();
            }
        }

        private static ImmutableArray<dynamic> Create(System.Reflection.TypeInfo rowType, params Action<dynamic>[] callbacks)
        {
            var builder = ImmutableArray.CreateBuilder<dynamic>();

            foreach(var callback in callbacks)
            {
                var row = Activator.CreateInstance(rowType);
                callback(row);
                builder.Add(row);
            }

            return builder.ToImmutable();
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
