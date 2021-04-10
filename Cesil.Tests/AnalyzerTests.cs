using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Cesil.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Cesil.Tests
{
    public class AnalyzerTests
    {
        private static Task<Compilation> GetCompilationAsync(
            string testFile,
            IEnumerable<string> includeFromCesil,
            [CallerMemberName] string caller = null
        )
        {
            var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator);
            var systemAssemblies = trustedAssemblies.Where(p => Path.GetFileName(p).StartsWith("System.")).ToList();

            var references = systemAssemblies.Select(s => MetadataReference.CreateFromFile(s)).ToList();

            var projectName = $"Cesil.Tests.{nameof(AnalyzerTests)}";
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

            foreach (var file in includeFromCesil)
            {
                var filePath = Path.Combine(cesilRootDir, file);
                var fileText = File.ReadAllText(filePath);
                project = project.AddDocument(Path.GetFileName(file), fileText).Project;
            }

            return project.GetCompilationAsync();
        }

        private static async Task<IEnumerable<(Diagnostic Diagnostic, string FlaggedSource, string FlaggedStatement)>> GetDiagnosticsForAsync(
            string testFile,
            IEnumerable<string> includeFromCesil,
            DiagnosticAnalyzer analyzer,
            [CallerMemberName] string caller = null)
        {
            var compilation = await GetCompilationAsync(testFile, includeFromCesil, caller);
            var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create(analyzer));
            var diagnostics = await withAnalyzers.GetAllDiagnosticsAsync();

            var inOrder = diagnostics.OrderBy(d => d.Location.SourceTree?.FilePath ?? "").ThenBy(d => d.Location.SourceSpan.Start);

            var ret = new List<(Diagnostic Diagnostic, string FlaggedSource, string FlaggedStatement)>();

            foreach (var diag in inOrder)
            {
                var tree = diag.Location.SourceTree;
                if (tree == null)
                {
                    throw new Exception("Couldn't find source for diagnostic");
                }

                var root = await tree.GetRootAsync();
                var node = root.FindNode(diag.Location.SourceSpan);
                var sourceFlagged = node.ToFullString();

                var statement = node;
                while (statement != null && !(statement is StatementSyntax))
                {
                    statement = statement.Parent;
                }

                var statementFlagged = statement?.ToFullString();

                ret.Add((diag, sourceFlagged, statementFlagged));
            }

            return ret;
        }

        // use this so tests aren't bound to any particular OS's filesystem conventions
        private static string MakePath(string a, params string[] b)
        {
            var ret = a;
            foreach (var i in b)
            {
                ret = Path.Combine(ret, i);
            }

            return ret;
        }

        [Fact]
        public async Task GetTypeByMetadataNameNonNullAsync()
        {
            var compilation = await GetCompilationAsync("class MyClass {}", Enumerable.Empty<string>());

            var type = ExtensionMethods.GetTypeByMetadataNameNonNull(compilation, "System.String");
            Assert.NotNull(type);
            Assert.Throws<InvalidOperationException>(() => ExtensionMethods.GetTypeByMetadataNameNonNull(compilation, "Type.Does.Not.Exist"));
        }

        [Fact]
        public void Expect()
        {
            var str = "foo";
            object obj = str;

            var res = ExtensionMethods.Expect<object, string>(obj);
            Assert.Equal("foo", res);

            Assert.Throws<InvalidOperationException>(() => ExtensionMethods.Expect<object, List<char>>(obj));
        }

        [Fact]
        public void NonNull()
        {
            var str = "foo";
            object obj = null;

            var res = ExtensionMethods.NonNull(str);
            Assert.Equal("foo", res);

            Assert.Throws<InvalidOperationException>(() => ExtensionMethods.NonNull(obj));
        }

        [Fact]
        public async Task BindingFlagsAsync()
        {
            // check things that _should_ get flagged are flagged
            {
                var diags =
                    await GetDiagnosticsForAsync(
                        @"
using System;
using System.Reflection;

namespace Cesil
{
    internal sealed class Foo
    {
        internal static void Bar()
        {
            // this should get flagged since BindingFlags is forbidden
            Console.WriteLine(BindingFlags.Public);
        }

        internal static void Fizz()
        {
            // this should get flagged since we don't have _static using_
            Console.WriteLine(BindingFlagsConstants.PublicInstance);
        }
    }
}",
                        new[] { MakePath("Common", "BindingFlagsConstants.cs") },
                        new BindingFlagsAnalyzer()
                    );

                Assert.Collection(
                    diags,
                    a =>
                    {
                        Assert.Equal(Diagnostics.BindingFlagsConstants.Id, a.Diagnostic.Id);
                        Assert.Equal("BindingFlags.Public", a.FlaggedSource);
                        Assert.Equal(
                            "            // this should get flagged since BindingFlags is forbidden\r\n            Console.WriteLine(BindingFlags.Public);\r\n",
                            a.FlaggedStatement
                        );
                    },
                    b =>
                    {
                        Assert.Equal(Diagnostics.UsingStaticBindingFlagsConstants.Id, b.Diagnostic.Id);
                        Assert.Equal("BindingFlagsConstants.PublicInstance", b.FlaggedSource);
                        Assert.Equal(
                            "            // this should get flagged since we don't have _static using_\r\n            Console.WriteLine(BindingFlagsConstants.PublicInstance);\r\n",
                            b.FlaggedStatement
                        );
                    }
                );
            }

            // check that things that shouldn't get flagged aren't
            {
                var diags =
                    await GetDiagnosticsForAsync(
                        @"
using System;

using static Cesil.BindingFlagsConstants;

namespace Conflicting
{
    internal enum BindingFlags
    {
        Public = 1
    }
}

namespace Cesil
{
    using Conflicting;

    internal sealed class Foo
    {
        internal static void Bar()
        {
            // this shouldn't get flagged since it's BindingFlagsConstants via a static using
            Console.WriteLine(PublicInstance);
        }

        internal static void Fizz()
        {
            // this shouldn't get flagged even though it looks like System.Reflection.BindingFlags because it actually isn't
            Console.WriteLine(BindingFlags.Public);
        }

        internal static void Buzz()
        {
            var x = new { Public = 2 };

            // shouldn't be flagged, but it's an unnamed type that looks like System.Reflection.BindingFlags
            Console.WriteLine(x.Public);
        }
    }
}",
                        new[] { MakePath("Common", "BindingFlagsConstants.cs") },
                        new BindingFlagsAnalyzer()
                    );

                Assert.Empty(diags);
            }

            // requires errors
            {
                var diags =
                    await GetDiagnosticsForAsync(
                        @"
using System;

namespace Cesil
{
    using Conflicting;

    internal sealed class Foo
    {
        internal static void Bar()
        {
            Console.WriteLine(x.Public);
        }
    }
}",
                        new[] { MakePath("Common", "BindingFlagsConstants.cs") },
                        new BindingFlagsAnalyzer()
                    );

                diags = diags.Where(d => d.Diagnostic.Id.StartsWith("CES"));

                Assert.Empty(diags);
            }
        }

        [Fact]
        public async Task ConfigureCancellableAwait()
        {
            // simple
            {
                var diags =
                    await GetDiagnosticsForAsync(@"
using System.Threading.Tasks;
using System.Threading;
using System;

using static Cesil.AwaitHelper;

namespace Foo
{
    public class Bar
    {
        public async Task FizzAsync()
        {
            var someTask = new ValueTask<int>(1);

            // should be flagged, since we haven't wrapped it
            await someTask;
        }

        public async Task FuzzAsync()
        {
            var someTask = new ValueTask<int>(2);

            // shouldn't be flagged
            await ConfigureCancellableAwait(this, someTask, CancellationToken.None);
        }

        public async Task BazzAsync()
        {
            var someTask = new ValueTask<int>(2);
            var arr = new Func<ValueTask<int>>[] { () => someTask };

            // particularly weird looking thing that should be flagged
            await arr[0]();
        }
    }
}",
                        new[] { MakePath("Common", "AwaitHelper.cs") },
                        new ConfigureCancellableAwaitAnalyzer()
                    );

                Assert.Collection(
                    diags,
                    a =>
                    {
                        Assert.Equal(Diagnostics.ConfigureCancellableAwait.Id, a.Diagnostic.Id);
                        Assert.Equal("someTask", a.FlaggedSource);
                        Assert.Equal("\r\n            // should be flagged, since we haven't wrapped it\r\n            await someTask;\r\n", a.FlaggedStatement);
                    },
                    b =>
                    {
                        Assert.Equal(Diagnostics.ConfigureCancellableAwait.Id, b.Diagnostic.Id);
                        Assert.Equal("arr[0]()", b.FlaggedSource);
                        Assert.Equal("\r\n            // particularly weird looking thing that should be flagged\r\n            await arr[0]();\r\n", b.FlaggedStatement);
                    }
                );
            }

            // non-static
            {
                var diags =
                    await GetDiagnosticsForAsync(@"
using System.Threading.Tasks;
using System.Threading;

using Cesil;

namespace Foo
{
    public class Bar
    {
        public async Task FuzzAsync()
        {
            var someTask = new ValueTask<int>(2);

            // shouldn't be flagged
            await AwaitHelper.ConfigureCancellableAwait(this, someTask, CancellationToken.None);
        }
    }
}",
                        new[] { MakePath("Common", "AwaitHelper.cs") },
                        new ConfigureCancellableAwaitAnalyzer()
                    );

                Assert.Empty(diags);
            }

            // bogus type
            {
                var diags =
                    await GetDiagnosticsForAsync(@"
using System.Threading.Tasks;
using System.Threading;

namespace Foo
{
    public class Bar
    {
        public async Task FuzzAsync()
        {
            var someTask = new ValueTask<int>(2);

            // should be flagged, since this isn't the real ConfigureCancellableAwait
            await ConfigureCancellableAwait(this, someTask, CancellationToken.None);
        }

        private static ValueTask<int> ConfigureCancellableAwait(object _, ValueTask<int> __, CancellationToken ___)
        => new ValueTask<int>(3);
    }
}",
                        new[] { MakePath("Common", "AwaitHelper.cs") },
                        new ConfigureCancellableAwaitAnalyzer()
                    );

                Assert.Collection(
                    diags,
                    a =>
                    {
                        Assert.Equal(Diagnostics.ConfigureCancellableAwait.Id, a.Diagnostic.Id);
                        Assert.Equal("ConfigureCancellableAwait(this, someTask, CancellationToken.None)", a.FlaggedSource);
                        Assert.Equal("\r\n            // should be flagged, since this isn't the real ConfigureCancellableAwait\r\n            await ConfigureCancellableAwait(this, someTask, CancellationToken.None);\r\n", a.FlaggedStatement);
                    }
                );
            }
        }

        [Fact]
        public async Task IsCompletedSuccessfullyAsync()
        {
            var diags =
                await GetDiagnosticsForAsync(@"
using System;
using System.Threading.Tasks;

namespace Test
{
    public class Foo
    {
        public bool IsFaulted => false;

        public void Bar(Task t)
        {
            // should be flagged, IsCompleted
            Console.WriteLine(t.IsCompleted);
        }

        public void Fizz()
        {
            // should not be flagged, property is on the wrong type
            Console.WriteLine(this.IsFaulted);
        }
    }
}",
                    new[] { MakePath("Common", "AsyncTestHelper.cs") },
                    new IsCompletedSuccessfullyAnalyzer()
                );

            Assert.Collection(
                diags,
                a =>
                {
                    Assert.Equal(Diagnostics.IsCompletedSuccessfully.Id, a.Diagnostic.Id);
                    Assert.Equal("t.IsCompleted", a.FlaggedSource);
                    Assert.Equal("            // should be flagged, IsCompleted\r\n            Console.WriteLine(t.IsCompleted);\r\n", a.FlaggedStatement);
                }
            );
        }

        [Fact]
        public async Task NullForgivenessAsync()
        {
            var diags =
                await GetDiagnosticsForAsync(@"
using System;

namespace Test
{
    public class Foo
    {
        public void Bar(string? s)
        {
            // should be flagged!
            string val = s!;

            Console.WriteLine(val);
        }
    }
}",
                    Enumerable.Empty<string>(),
                    new NullForgivenessAnalyzer()
                );

            Assert.Collection(
                diags,
                a =>
                {
                    Assert.Equal(Diagnostics.NullForgiveness.Id, a.Diagnostic.Id);
                    Assert.Equal("s!", a.FlaggedSource);
                    Assert.Equal("            // should be flagged!\r\n            string val = s!;\r\n", a.FlaggedStatement);
                }
            );
        }

        [Fact]
        public async Task PublicMemberAsync()
        {
            // normal
            {
                var diags =
                    await GetDiagnosticsForAsync(@"
using System.IO;
using System;

namespace Test
{
    public class PublicType
    {
        // shouldn't be flagged, containing type is public
        public bool SomeProp => true;
    }

    internal class InternalType
    {
        // should be flagged, containing type is not public
        public bool SomeOtherProp => false;

        // shouldn't be flagged, not public
        private void Foo() { }

        // shouldn't be flagged, is an operator
        public static InternalType operator +(InternalType a, InternalType b)
        => a;

        // shouldn't be flagged, is an operator
        public static explicit operator int(InternalType a)
        => 2;
    }

    internal class InternalInterfaceType : IEquatable<InternalInterfaceType>
    {
        // shouldn't be flagged, it's an interface member
        public bool Equals(InternalInterfaceType? other)
        => other == this;
    }

    internal class InternalSubType : TextReader
    {   
        // shouldn't be flagged, it's an override
        public override int Read()
        => -1;
    }
}
",
                        Enumerable.Empty<string>(),
                        new PublicMemberAnalyzer()
                    );

                Assert.Collection(
                    diags,
                    a =>
                    {
                        Assert.Equal(Diagnostics.PublicMember.Id, a.Diagnostic.Id);
                        Assert.Equal("        // should be flagged, containing type is not public\r\n        public bool SomeOtherProp => false;\r\n", a.FlaggedSource);
                        Assert.Null(a.FlaggedStatement);
                    }
                );
            }
        }

        [Fact]
        public async Task ThrowAsync()
        {
            var diags =
                await GetDiagnosticsForAsync(@"
using System;
using Cesil;

// need these definitions, but don't want to pull everything
//   needed for them into the test
namespace Cesil
{
    public class ImpossibleException : Exception
    {
        private ImpossibleException(): base() { }

        internal static ImpossibleException Create(string reason, string fileName, string memberName, int lineNumber)
        => new ImpossibleException();

        internal static ImpossibleException Create(string reason, string fileName, string memberName, int lineNumber, object _)
        => new ImpossibleException();
    }

    public class Options { }

    public interface IReader<T> { }
    
    public interface IAsyncReader<T> { }

    public interface IWriter<T> { }

    public interface IAsyncWriter<T> { }

    public interface IBoundConfiguration<T> { }

    public class PoisonableBase
    {
        public void SetPoison(Exception e) { }
    }

    public class Parser { }

    public struct ReadContext 
    {
        public bool HasColumn => true;
        public int Column => 1;
    }
}

namespace Test
{
    class Foo
    {
        public void Bar()
        {
            // should be flagged, plain old statement
            throw new Exception();
        }

        public void Fizz()
        {
            // should not be flagged
            Throw.InvalidOperationException_Returns<object>(""test"");
        } 

        public void Buzz()
        => throw new Exception();   // flag the short form too

        public void Bop()
        {
            try
            {
                Console.WriteLine();
            }
            catch
            {
                // should be flagged, use rethrow instead
                throw;
            }
        }
    }
}",
                    new[]
                    {
                        MakePath("Common", "Throw.cs")
                    },
                    new ThrowAnalyzer()
                );

            Assert.Collection(
                diags,
                a =>
                {
                    Assert.Equal(Diagnostics.Throw.Id, a.Diagnostic.Id);
                    Assert.Equal("            // should be flagged, plain old statement\r\n            throw new Exception();\r\n", a.FlaggedSource);
                    Assert.Equal("            // should be flagged, plain old statement\r\n            throw new Exception();\r\n", a.FlaggedStatement);
                },
                b =>
                {
                    Assert.Equal(Diagnostics.Throw.Id, b.Diagnostic.Id);
                    Assert.Equal("throw new Exception()", b.FlaggedSource);
                    Assert.Null(b.FlaggedStatement);
                },
                c =>
                {
                    Assert.Equal(Diagnostics.Throw.Id, c.Diagnostic.Id);
                    Assert.Equal("                // should be flagged, use rethrow instead\r\n                throw;\r\n", c.FlaggedSource);
                    Assert.Equal("                // should be flagged, use rethrow instead\r\n                throw;\r\n", c.FlaggedStatement);
                }
            );
        }

        [Fact]
        public async Task TypesAsync()
        {
            const string CESIL_TYPE_DECLS = @"
// just declare all these types, rather than import them
namespace Cesil
{
    // delegates
    public delegate bool ColumnWriterDelegate();
    public delegate void ParserDelegate<T>(T _);
    public delegate void SetterDelegate<T,V>(T _, V __);
    public delegate void SetterByRefDelegate<T,V>(T _, V __);
    public delegate void StaticSetterDelegate<T>(T _);
    public delegate void ResetDelegate<T>(T _);
    public delegate void ResetByRefDelegate<T>(T _);
    public delegate void StaticResetDelegate();
    public delegate void GetterDelegate<T,V>(T _, V __);
    public delegate void StaticGetterDelegate<T>(T _);
    public delegate void FormatterDelegate<T>(T _);
    public delegate void ShouldSerializeDelegate<T>(T _);
    public delegate void StaticShouldSerializeDelegate();
    public delegate void DynamicRowConverterDelegate<T>(T _);
    public delegate void ParseAndSetOnDelegate<T>(T _);
    public delegate void MoveFromHoldToRowDelegate<T,V>(T _, V __);
    public delegate void GetInstanceGivenHoldDelegate<T,V>(T _, V __);
    public delegate void ClearHoldDelegate<T>(T _);
    public delegate void InstanceProviderDelegate<T>(T _);
    public delegate void NeedsHoldRowConstructor<T,V>(T _, V __);
    public delegate void StartRowDelegate(in ReadContext _);
    public delegate bool TryPreAllocateDelegate<T>(in ReadContext _, bool __, ref T ___);
    public delegate bool GeneratedColumnAvailableDelegate(ReadOnlySpan<char> _, in ReadContext __);
    public delegate ref Memory<int> GetColumnMapDelegate();


    // enums
    public enum ReadRowEnding { }
    public enum ReadHeader { }
    public enum WriteHeader { }
    public enum WriteRowEnding { }
    public enum WriteTrailingRowEnding { }
    public enum DynamicRowDisposal { }
    public enum ManualTypeDescriberFallbackBehavior { }
    public enum ExtraColumnTreatment { }
    public enum SurrogateTypeDescriberFallbackBehavior { }

    // structs
    public struct ColumnIdentifier { }
    public struct NonNull<T> { }
    public struct ReadContext { }
    public struct WriteContext { }

    // static classes
    public class DefaultTypeInstanceProviders { }
    public class DefaultTypeParsers { public class DefaultEnumTypeParser<T> { } }
    public class DefaultTypeFormatters {  public class DefaultEnumTypeFormatter<T> { } }
    public class DisposableHelper { }
    public class Throw { }
    public class TupleDynamicRowConverters<T> { }
    public class RecordDynamicRowConverter<T> { }
    public class WellKnownRowTypes { public class WellKnownEnumRowType<T> { } }
    public class Utils { }

    // interfaces
    public interface IDynamicRowOwner { }
    public interface ITypeDescriber { }
    public interface ITestableDisposable { }

    // classes
    public class DynamicCell { }
    public class DynamicRow { }
    public class DynamicRowEnumerable<T> { }
    public class DynamicRowEnumerableNonGeneric { }
    public class DynamicRowRange { }
    public class PassthroughRowEnumerable { }
    public class DefaultTypeDescriber { }
    public class GeneratedSourceVersionAttribute { }
    

    public class ReaderStateMachine
    {
        public enum State { }
        public enum CharacterType { }
    }
}
";
            // normal
            {
                var diags =
                    await GetDiagnosticsForAsync(@"
using System;
using Cesil;

" + CESIL_TYPE_DECLS + @"

namespace Test
{
    public class Foo<T>
    {
        public void Bar()
        {
            // should be flagged
            Console.WriteLine(typeof(int));
            
            // shouldn't be flagged
            Console.WriteLine(Types.Int);

            // shouldn't be flagged (generic)
            Console.WriteLine(typeof(T));
        }
    }
}",
                        new[] { MakePath("Common", "Types.cs") },
                        new TypesAnalyzer()
                    );

                Assert.Collection(
                    diags,
                    a =>
                    {
                        Assert.Equal(Diagnostics.Types.Id, a.Diagnostic.Id);
                        Assert.Equal("typeof(int)", a.FlaggedSource);
                        Assert.Equal("            // should be flagged\r\n            Console.WriteLine(typeof(int));\r\n", a.FlaggedStatement);
                    }
                );
            }

            // requires errors
            {
                var diags =
                    await GetDiagnosticsForAsync(@"
using System;

" + CESIL_TYPE_DECLS + @"

namespace Test
{
    public class Foo<T>
    {
        public void Bar()
        {
            // should be flagged, as we don't know what it is
            Console.WriteLine(typeof('.'));
        }
    }
}",
                        new[] { MakePath("Common", "Types.cs") },
                        new TypesAnalyzer()
                    );

                diags = diags.Where(d => d.Diagnostic.Id.StartsWith("CES"));

                Assert.Collection(
                    diags,
                    a =>
                    {
                        Assert.Equal(Diagnostics.Types.Id, a.Diagnostic.Id);
                        Assert.Equal("typeof(", a.FlaggedSource);
                        Assert.Equal("            // should be flagged, as we don't know what it is\r\n            Console.WriteLine(typeof('.'))", a.FlaggedStatement);
                    }
                );
            }
        }

        [DiagnosticAnalyzer(LanguageNames.CSharp)]
        private sealed class BadAnalyzer : AnalyzerBase<object>
        {
            public BadAnalyzer() : base(false, Array.Empty<DiagnosticDescriptor>(), SyntaxKind.AbstractKeyword) { }

            protected override void OnSyntaxNode(SyntaxNodeAnalysisContext context, object state) { }
        }

        [Fact]
        public void AnalyzerBase()
        {
            Assert.Throws<ArgumentException>(() => new BadAnalyzer());
        }
    }
}