using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cesil.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

using static Cesil.Tests.SourceGeneratorTestHelper;

namespace Cesil.Tests
{
    public class SourceGeneratorFixture : IAsyncLifetime
    {
        internal IMethodSymbol Method { get; private set; }
        internal IMethodSymbol Method2 { get; private set; }
        internal ITypeSymbol Type { get; private set; }
        internal ITypeSymbol Type2 { get; private set; }
        internal IParameterSymbol Parameter { get; private set; }
        internal IPropertySymbol Property { get; private set; }
        internal Location Location { get; private set; }

        public async Task InitializeAsync()
        {
            var comp =
                await GetCompilationAsync(
                    @"
                    namespace Foo 
                    {   
                        class FooC
                        {
                            public void Fizz() { }

                            public FooC(int a) { }

                            public string Bar { get; set; }
                        }

                        interface BuzzI
                        {
                        
                        }
                    }",
                    nameof(SourceGeneratorFixture),
                    NullableContextOptions.Enable
                );

            Type = Utils.NonNull(comp.GetTypeByMetadataName("Foo.FooC"));
            Method = Type.GetMembers().OfType<IMethodSymbol>().First();
            Parameter = Type.GetMembers().OfType<IMethodSymbol>().First(m => m.MethodKind == MethodKind.Constructor).Parameters.First();
            Property = Type.GetMembers().OfType<IPropertySymbol>().First();

            Type2 = Utils.NonNull(comp.GetTypeByMetadataName("System.String"));
            Method2 = Type2.GetMembers().OfType<IMethodSymbol>().First();

            var i = Utils.NonNull(comp.GetTypeByMetadataName("Foo.BuzzI"));
            Location = i.Locations.First();
        }

        public Task DisposeAsync()
        => Task.CompletedTask;
    }

    public class SourceGeneratorTests : IClassFixture<SourceGeneratorFixture>
    {
        private readonly ITypeSymbol Type;
        private readonly IParameterSymbol Parameter;
        private readonly IMethodSymbol Method;
        private readonly IPropertySymbol Property;
        private readonly Location Location;

        private readonly ITypeSymbol Type2;
        private readonly IMethodSymbol Method2;

        public SourceGeneratorTests(SourceGeneratorFixture fixture)
        {
            Type = fixture.Type;
            Type2 = fixture.Type2;
            Parameter = fixture.Parameter;
            Method = fixture.Method;
            Method2 = fixture.Method2;
            Property = fixture.Property;
            Location = fixture.Location;
        }

        [Fact]
        public async Task GetConstantsWithNameAsync()
        {
            // one value
            {
                // constant value unknown
                {
                    var comp =
                        await GetCompilationAsync(
                            @"using System;
                            namespace Test
                            {
                                class Foo 
                                {
                                    [SerializerMember(Name=?)]
                                    public string Bar { get; set; }
                                }
                            }",
                            nameof(GetConstantsWithNameAsync),
                            NullableContextOptions.Disable,
                            addCesilReferences: false
                        );

                    var attrMembers = comp.GetAttributedMembers();

                    var t = comp.GetTypeByMetadataName("Test.Foo");
                    var syntax = (ClassDeclarationSyntax)t.DeclaringSyntaxReferences.Single().GetSyntax();
                    var member = (PropertyDeclarationSyntax)syntax.Members.Single();
                    var attrList = member.AttributeLists.Single().Attributes.ToImmutableArray();

                    var diags = ImmutableArray<Diagnostic>.Empty;

                    var ret = SourceGenerator.Utils.GetConstantsWithName<string>(attrMembers, attrList, "Name", ref diags);
                    Assert.Empty(ret);

                    Assert.Collection(
                        diags,
                        d => Assert.Equal("CES1003", d.Id)
                    );
                }

                // constant value wrong type
                {
                    var comp =
                        await GetCompilationAsync(
                            @"using System;
                            namespace Test
                            {
                                class Foo 
                                {
                                    [SerializerMember(Name=1234)]
                                    public string Bar { get; set; }
                                }
                            }",
                            nameof(GetConstantsWithNameAsync),
                            NullableContextOptions.Disable,
                            addCesilReferences: false
                        );

                    var attrMembers = comp.GetAttributedMembers();

                    var t = comp.GetTypeByMetadataName("Test.Foo");
                    var syntax = (ClassDeclarationSyntax)t.DeclaringSyntaxReferences.Single().GetSyntax();
                    var member = (PropertyDeclarationSyntax)syntax.Members.Single();
                    var attrList = member.AttributeLists.Single().Attributes.ToImmutableArray();

                    var diags = ImmutableArray<Diagnostic>.Empty;

                    var ret = SourceGenerator.Utils.GetConstantsWithName<string>(attrMembers, attrList, "Name", ref diags);
                    Assert.Empty(ret);

                    Assert.Collection(
                        diags,
                        d => Assert.Equal("CES1004", d.Id)
                    );
                }

                // constant value null when non-nullable
                {
                    var comp =
                        await GetCompilationAsync(
                            @"using System;
                            namespace Test
                            {
                                class Foo 
                                {
                                    [SerializerMember(Name=null)]
                                    public string Bar { get; set; }
                                }
                            }",
                            nameof(GetConstantsWithNameAsync),
                            NullableContextOptions.Disable,
                            addCesilReferences: false
                        );

                    var attrMembers = comp.GetAttributedMembers();

                    var t = comp.GetTypeByMetadataName("Test.Foo");
                    var syntax = (ClassDeclarationSyntax)t.DeclaringSyntaxReferences.Single().GetSyntax();
                    var member = (PropertyDeclarationSyntax)syntax.Members.Single();
                    var attrList = member.AttributeLists.Single().Attributes.ToImmutableArray();

                    var diags = ImmutableArray<Diagnostic>.Empty;

                    var ret = SourceGenerator.Utils.GetConstantsWithName<int>(attrMembers, attrList, "Name", ref diags);
                    Assert.Empty(ret);

                    Assert.Collection(
                        diags,
                        d => Assert.Equal("CES1004", d.Id)
                    );
                }
            }

            // two values
            {
                // constant value unknown
                {
                    var comp =
                        await GetCompilationAsync(
                            @"using System;
                            namespace Test
                            {
                                class Foo 
                                {
                                    [SerializerMember(Name=?)]
                                    public string Bar { get; set; }
                                }
                            }",
                            nameof(GetConstantsWithNameAsync),
                            NullableContextOptions.Disable,
                            addCesilReferences: false
                        );

                    var attrMembers = comp.GetAttributedMembers();

                    var t = comp.GetTypeByMetadataName("Test.Foo");
                    var syntax = (ClassDeclarationSyntax)t.DeclaringSyntaxReferences.Single().GetSyntax();
                    var member = (PropertyDeclarationSyntax)syntax.Members.Single();
                    var attrList = member.AttributeLists.Single().Attributes.ToImmutableArray();

                    var diags = ImmutableArray<Diagnostic>.Empty;

                    var (ret1, ret2) = SourceGenerator.Utils.GetConstantsWithName<string, int>(attrMembers, attrList, "Name", ref diags);
                    Assert.Empty(ret1);
                    Assert.Empty(ret2);

                    Assert.Collection(
                        diags,
                        d => Assert.Equal("CES1003", d.Id)
                    );
                }

                // constant value wrong type
                {
                    var comp =
                        await GetCompilationAsync(
                            @"using System;
                            namespace Test
                            {
                                class Foo 
                                {
                                    [SerializerMember(Name=1.234)]
                                    public string Bar { get; set; }
                                }
                            }",
                            nameof(GetConstantsWithNameAsync),
                            NullableContextOptions.Disable,
                            addCesilReferences: false
                        );

                    var attrMembers = comp.GetAttributedMembers();

                    var t = comp.GetTypeByMetadataName("Test.Foo");
                    var syntax = (ClassDeclarationSyntax)t.DeclaringSyntaxReferences.Single().GetSyntax();
                    var member = (PropertyDeclarationSyntax)syntax.Members.Single();
                    var attrList = member.AttributeLists.Single().Attributes.ToImmutableArray();

                    var diags = ImmutableArray<Diagnostic>.Empty;

                    var (ret1, ret2) = SourceGenerator.Utils.GetConstantsWithName<string, bool>(attrMembers, attrList, "Name", ref diags);
                    Assert.Empty(ret1);
                    Assert.Empty(ret2);

                    Assert.Collection(
                        diags,
                        d => Assert.Equal("CES1004", d.Id)
                    );
                }

                // constant value null when non-nullable
                {
                    var comp =
                        await GetCompilationAsync(
                            @"using System;
                            namespace Test
                            {
                                class Foo 
                                {
                                    [SerializerMember(Name=null)]
                                    public string Bar { get; set; }
                                }
                            }",
                            nameof(GetConstantsWithNameAsync),
                            NullableContextOptions.Disable,
                            addCesilReferences: false
                        );

                    var attrMembers = comp.GetAttributedMembers();

                    var t = comp.GetTypeByMetadataName("Test.Foo");
                    var syntax = (ClassDeclarationSyntax)t.DeclaringSyntaxReferences.Single().GetSyntax();
                    var member = (PropertyDeclarationSyntax)syntax.Members.Single();
                    var attrList = member.AttributeLists.Single().Attributes.ToImmutableArray();

                    var diags = ImmutableArray<Diagnostic>.Empty;

                    var (ret1, ret2) = SourceGenerator.Utils.GetConstantsWithName<int, double>(attrMembers, attrList, "Name", ref diags);
                    Assert.Empty(ret1);
                    Assert.Empty(ret2);

                    Assert.Collection(
                        diags,
                        d => Assert.Equal("CES1004", d.Id)
                    );
                }
            }
        }

        [Fact]
        public async Task CantLoadTypesAsync()
        {
            // serializer generator
            {
                // no cesil reference
                {
                    var comp =
                        await GetCompilationAsync(
                            @"using System;
                            namespace Test
                            {
                                class Foo { }
                            }",
                            nameof(CantLoadTypesAsync),
                            NullableContextOptions.Disable,
                            addCesilReferences: false
                        );

                    var parseOptions = (CSharpParseOptions)comp.SyntaxTrees.ElementAt(0).Options;

                    var generators = ImmutableArray.Create(new SerializerGenerator());

                    GeneratorDriver driver = CSharpGeneratorDriver.Create(generators, parseOptions: parseOptions);

                    driver.RunGeneratorsAndUpdateCompilation(comp, out var producedCompilation, out var diagnostics);

                    var d = Assert.Single(diagnostics);

                    Assert.Equal("CES1000", d.Id);
                }

                // no system.memory referece
                {
                    var comp =
                        await GetCompilationAsync(
                            @"using System;
                            namespace Test
                            {
                                class Foo { }
                            }",
                            nameof(CantLoadTypesAsync),
                            NullableContextOptions.Disable,
                            doNotAddReferences: new[] { "System.Memory" }
                        );

                    var parseOptions = (CSharpParseOptions)comp.SyntaxTrees.ElementAt(0).Options;

                    var generators = ImmutableArray.Create(new SerializerGenerator());

                    GeneratorDriver driver = CSharpGeneratorDriver.Create(generators, parseOptions: parseOptions);

                    driver.RunGeneratorsAndUpdateCompilation(comp, out var producedCompilation, out var diagnostics);

                    var d = Assert.Single(diagnostics);

                    Assert.Equal("CES1027", d.Id);
                }
            }

            // deserializer generator
            {
                // no cesil reference
                {
                    var comp =
                        await GetCompilationAsync(
                            @"using System;
                            namespace Test
                            {
                                class Foo { }
                            }",
                            nameof(CantLoadTypesAsync),
                            NullableContextOptions.Disable,
                            addCesilReferences: false
                        );

                    var parseOptions = (CSharpParseOptions)comp.SyntaxTrees.ElementAt(0).Options;

                    var generators = ImmutableArray.Create(new DeserializerGenerator());

                    GeneratorDriver driver = CSharpGeneratorDriver.Create(generators, parseOptions: parseOptions);

                    driver.RunGeneratorsAndUpdateCompilation(comp, out var producedCompilation, out var diagnostics);

                    var d = Assert.Single(diagnostics);

                    Assert.Equal("CES1000", d.Id);
                }

                // no system.memory referece
                {
                    var comp =
                        await GetCompilationAsync(
                            @"using System;
                            namespace Test
                            {
                                class Foo { }
                            }",
                            nameof(CantLoadTypesAsync),
                            NullableContextOptions.Disable,
                            doNotAddReferences: new[] { "System.Memory" }
                        );

                    var parseOptions = (CSharpParseOptions)comp.SyntaxTrees.ElementAt(0).Options;

                    var generators = ImmutableArray.Create(new DeserializerGenerator());

                    GeneratorDriver driver = CSharpGeneratorDriver.Create(generators, parseOptions: parseOptions);

                    driver.RunGeneratorsAndUpdateCompilation(comp, out var producedCompilation, out var diagnostics);

                    var d = Assert.Single(diagnostics);

                    Assert.Equal("CES1027", d.Id);
                }
            }
        }

        [Fact]
        public async Task IsIgnoredAsync()
        {
            var comp =
                await GetCompilationAsync(
                    @"using System;
                    namespace Test
                    {
                        class Foo { }
                    }",
                    nameof(IsIgnoredAsync),
                    NullableContextOptions.Disable,
                    doNotAddReferences: new[] { "System.Runtime.Serialization.Primitives" }
                );

            Assert.True(FrameworkTypes.TryCreate(comp, BuiltInTypes.Create(comp), out var framework));
            Assert.Null(framework.DataMemberAttribute);

            var t = comp.GetTypeByMetadataName("Test.Foo");
            Assert.NotNull(t);

            Assert.False(GeneratorBase<string, string>.IsIgnored(t, framework));
        }

        [Fact]
        public void ReusedDelegates()
        {
            var tok1 = SyntaxFactory.Token(SyntaxKind.AbstractKeyword);
            var tok2 = SyntaxFactory.Token(SyntaxKind.PropertyKeyword);

            Assert.Equal(tok2, SourceGenerator.Utils.TakeUpdatedToken(tok1, tok2));

            var tr1 = SyntaxFactory.Whitespace("\t");
            var tr2 = SyntaxFactory.Whitespace("    ");

            Assert.Equal(tr2, SourceGenerator.Utils.TakeUpdatedTrivia(tr1, tr2));
        }

        [Fact]
        public async Task GetTypeConstantWithNameAsync()
        {
            // bad typeof
            {
                var comp =
                    await GetCompilationAsync(
                        @"using System;
                        namespace Test
                        {
                            [AttributeUsage(AttributeTargets.All)]
                            class BarAttribute: Attribute
                            {
                                public Type Name { get; set; }
                            }

                            [BarAttribute(Name = typeof(new()))]
                            class Foo
                            {
                            }
                        }",
                        nameof(GetMethodFromAttributeAsync),
                        NullableContextOptions.Disable
                    );

                var attrMembers = comp.GetAttributedMembers();

                var t = comp.GetTypeByMetadataName("Test.Foo");
                Assert.NotNull(t);

                var syntax = (ClassDeclarationSyntax)t.DeclaringSyntaxReferences.Single().GetSyntax();
                var attrs = syntax.AttributeLists.Single().Attributes.Select(a => a).ToImmutableArray();

                var diags = ImmutableArray<Diagnostic>.Empty;

                var res = SourceGenerator.Utils.GetTypeConstantWithName(
                    attrMembers,
                    attrs,
                    "Name",
                    ref diags
                );

                Assert.Empty(res);

                var d = Assert.Single(diags);
                Assert.Equal("CES1003", d.Id);
            }

            // no value
            {
                var comp =
                    await GetCompilationAsync(
                        @"using System;
                        namespace Test
                        {
                            [AttributeUsage(AttributeTargets.All)]
                            class BarAttribute: Attribute
                            {
                                public Type Name { get; set; }
                            }

                            [BarAttribute(Name = )]
                            class Foo
                            {
                            }
                        }",
                        nameof(GetMethodFromAttributeAsync),
                        NullableContextOptions.Disable
                    );

                var attrMembers = comp.GetAttributedMembers();

                var t = comp.GetTypeByMetadataName("Test.Foo");
                Assert.NotNull(t);

                var syntax = (ClassDeclarationSyntax)t.DeclaringSyntaxReferences.Single().GetSyntax();
                var attrs = syntax.AttributeLists.Single().Attributes.Select(a => a).ToImmutableArray();

                var diags = ImmutableArray<Diagnostic>.Empty;

                var res = SourceGenerator.Utils.GetTypeConstantWithName(
                    attrMembers,
                    attrs,
                    "Name",
                    ref diags
                );

                Assert.Empty(res);

                var d = Assert.Single(diags);
                Assert.Equal("CES1003", d.Id);
            }

            // non-type value
            {
                var comp =
                    await GetCompilationAsync(
                        @"using System;
                        namespace Test
                        {
                            [AttributeUsage(AttributeTargets.All)]
                            class BarAttribute: Attribute
                            {
                                public string Name { get; set; }
                            }

                            [BarAttribute(Name = ""hello"")]
                            class Foo
                            {
                            }
                        }",
                        nameof(GetMethodFromAttributeAsync),
                        NullableContextOptions.Disable
                    );

                var attrMembers = comp.GetAttributedMembers();

                var t = comp.GetTypeByMetadataName("Test.Foo");
                Assert.NotNull(t);

                var syntax = (ClassDeclarationSyntax)t.DeclaringSyntaxReferences.Single().GetSyntax();
                var attrs = syntax.AttributeLists.Single().Attributes.Select(a => a).ToImmutableArray();

                var diags = ImmutableArray<Diagnostic>.Empty;

                var res = SourceGenerator.Utils.GetTypeConstantWithName(
                    attrMembers,
                    attrs,
                    "Name",
                    ref diags
                );

                Assert.Empty(res);

                var d = Assert.Single(diags);
                Assert.Equal("CES1003", d.Id);
            }
        }

        [Fact]
        public async Task GetMethodFromAttributeAsync()
        {
            // type multiple times
            {
                var comp =
                    await GetCompilationAsync(
                        @"using System;
                        namespace Test
                        {
                            [AttributeUsage(AttributeTargets.All)]
                            class BarAttribute: Attribute
                            {
                                public Type Name { get; set; }
                            }

                            [BarAttribute(Name = typeof(string), Name = typeof(object))]
                            class Foo
                            {
                            }
                        }",
                        nameof(GetMethodFromAttributeAsync),
                        NullableContextOptions.Disable
                    );

                var attrMembers = comp.GetAttributedMembers();

                var t = comp.GetTypeByMetadataName("Test.Foo");
                Assert.NotNull(t);

                var syntax = (ClassDeclarationSyntax)t.DeclaringSyntaxReferences.Single().GetSyntax();
                var attrs = syntax.AttributeLists.Single().Attributes.Select(a => a).ToImmutableArray();

                var diags = ImmutableArray<Diagnostic>.Empty;

                var multipleTypes = false;
                var multipleMethods = false;

                var res = SourceGenerator.Utils.GetMethodFromAttribute(
                    attrMembers,
                    "Name",
                    loc => { multipleTypes = true; return Diagnostics.NameSpecifiedMultipleTimes(loc); },
                    "Method",
                    loc => { multipleMethods = true; return Diagnostics.MultipleMethodsFound(loc, "foo", "bar"); },
                    loc => Diagnostics.FormatterBothMustBeSet(loc),
                    null,
                    attrs,
                    ref diags
                );

                Assert.Null(res);
                Assert.True(multipleTypes);
                Assert.False(multipleMethods);
            }

            // method multiple times
            {
                var comp =
                    await GetCompilationAsync(
                        @"using System;
                        namespace Test
                        {
                            [AttributeUsage(AttributeTargets.All)]
                            class BarAttribute: Attribute
                            {
                                public string Method { get; set; }
                            }

                            [BarAttribute(Method = ""blurgh"", Method = ""foo"")]
                            class Foo
                            {
                            }
                        }",
                        nameof(GetMethodFromAttributeAsync),
                        NullableContextOptions.Disable
                    );

                var attrMembers = comp.GetAttributedMembers();

                var t = comp.GetTypeByMetadataName("Test.Foo");
                Assert.NotNull(t);

                var syntax = (ClassDeclarationSyntax)t.DeclaringSyntaxReferences.Single().GetSyntax();
                var attrs = syntax.AttributeLists.Single().Attributes.Select(a => a).ToImmutableArray();

                var diags = ImmutableArray<Diagnostic>.Empty;

                var multipleTypes = false;
                var multipleMethods = false;

                var res = SourceGenerator.Utils.GetMethodFromAttribute(
                    attrMembers,
                    "Name",
                    loc => { multipleTypes = true; return Diagnostics.NameSpecifiedMultipleTimes(loc); },
                    "Method",
                    loc => { multipleMethods = true; return Diagnostics.MultipleMethodsFound(loc, "foo", "bar"); },
                    loc => Diagnostics.FormatterBothMustBeSet(loc),
                    null,
                    attrs,
                    ref diags
                );

                Assert.Null(res);
                Assert.False(multipleTypes);
                Assert.True(multipleMethods);
            }
        }

        [Fact]
        public void AllNullHashCode()
        {
            SourceGenerator.Utils.HashCode(default(object), default(object), default(object), default(object), default(object), default(object));
        }

        [Fact]
        public void NotNulls()
        {
            Assert.Throws<InvalidOperationException>(() => SourceGenerator.Utils.NonNull(default(object)));
            Assert.Throws<InvalidOperationException>(() => SourceGenerator.Utils.NonNullValue(default(int?)));
        }

        [Fact]
        public async Task ToFullQualifiedNameAsync()
        {
            var comp =
                await GetCompilationAsync(
                    @"namespace Test
                    {
                        struct NotNullable<T> { }

                        class Foo
                        {
                            public string A;
                            public int B;
                            public int? C;
                            public NotNullable<int> D;
                        }
                    }",
                    nameof(ToFullQualifiedNameAsync),
                    NullableContextOptions.Disable
                );

            Assert.True(FrameworkTypes.TryCreate(comp, BuiltInTypes.Create(comp), out var framework));

            var t = comp.GetTypeByMetadataName("Test.Foo");
            Assert.NotNull(t);

            var a = t.GetMembers().OfType<IFieldSymbol>().Single(x => x.Name == "A");
            var b = t.GetMembers().OfType<IFieldSymbol>().Single(x => x.Name == "B");
            var c = t.GetMembers().OfType<IFieldSymbol>().Single(x => x.Name == "C");
            var d = t.GetMembers().OfType<IFieldSymbol>().Single(x => x.Name == "D");

            Assert.Equal("System.String", SourceGenerator.Utils.ToFullyQualifiedName(a.Type));
            Assert.Equal("System.Int32", SourceGenerator.Utils.ToFullyQualifiedName(b.Type));
            Assert.Equal("System.Int32?", SourceGenerator.Utils.ToFullyQualifiedName(c.Type));
            Assert.Equal("Test.NotNullable", SourceGenerator.Utils.ToFullyQualifiedName(d.Type));
        }

        [Fact]
        public async Task IsFlagsEnumAsync()
        {
            var comp =
                await GetCompilationAsync(
                    @"namespace Test
                    {
                        [System.Flags]
                        enum Foo
                        {
                            None = 0
                        }

                        enum Bar
                        {
                            None = 0
                        }

                        [?]
                        enum Fizz
                        {
                            None = 0
                        }
                    }",
                    nameof(IsFlagsEnumAsync),
                    NullableContextOptions.Disable
                );

            Assert.True(FrameworkTypes.TryCreate(comp, BuiltInTypes.Create(comp), out var framework));

            var e1 = comp.GetTypeByMetadataName("Test.Foo");
            Assert.NotNull(e1);

            var e2 = comp.GetTypeByMetadataName("Test.Bar");
            Assert.NotNull(e2);

            var e3 = comp.GetTypeByMetadataName("Test.Fizz");
            Assert.NotNull(e3);

            Assert.True(SourceGenerator.Utils.IsFlagsEnum(framework, e1));
            Assert.False(SourceGenerator.Utils.IsFlagsEnum(framework, e2));
            Assert.False(SourceGenerator.Utils.IsFlagsEnum(framework, e3));
        }

        [Fact]
        public async Task ShouldIncludeAsync()
        {
            // public instance
            {
                var comp =
                    await GetCompilationAsync(
                        @"namespace Test
                        {
                            class Foo
                            {
                                public int Bar { get; set; }
                            }
                        }",
                        nameof(ShouldIncludeAsync),
                        NullableContextOptions.Disable
                    );

                var t = comp.GetTypeByMetadataName("Test.Foo");
                Assert.NotNull(t);

                var p = t.GetMembers().OfType<IPropertySymbol>().Single(x => x.Name == "Bar");

                Assert.True(p.ShouldInclude(ImmutableArray<AttributeSyntax>.Empty));
            }

            // non-public
            {
                var comp =
                    await GetCompilationAsync(
                        @"namespace Test
                        {
                            class Foo
                            {
                                internal int Bar { get; set; }
                            }
                        }",
                        nameof(ShouldIncludeAsync),
                        NullableContextOptions.Disable
                    );

                var t = comp.GetTypeByMetadataName("Test.Foo");
                Assert.NotNull(t);

                var p = t.GetMembers().OfType<IPropertySymbol>().Single(x => x.Name == "Bar");

                Assert.False(p.ShouldInclude(ImmutableArray<AttributeSyntax>.Empty));
            }

            // public static
            {
                var comp =
                    await GetCompilationAsync(
                        @"namespace Test
                        {
                            class Foo
                            {
                                public static int Bar { get; set; }
                            }
                        }",
                        nameof(ShouldIncludeAsync),
                        NullableContextOptions.Disable
                    );

                var t = comp.GetTypeByMetadataName("Test.Foo");
                Assert.NotNull(t);

                var p = t.GetMembers().OfType<IPropertySymbol>().Single(x => x.Name == "Bar");

                Assert.False(p.ShouldInclude(ImmutableArray<AttributeSyntax>.Empty));
            }

            // non-public, with attributes
            {
                var comp =
                    await GetCompilationAsync(
                        @"namespace Test
                        {
                            class Foo
                            {
                                internal int Bar { get; set; }
                            }
                        }",
                        nameof(ShouldIncludeAsync),
                        NullableContextOptions.Disable
                    );

                var t = comp.GetTypeByMetadataName("Test.Foo");
                Assert.NotNull(t);

                var p = t.GetMembers().OfType<IPropertySymbol>().Single(x => x.Name == "Bar");

                Assert.True(p.ShouldInclude(ImmutableArray.Create(default(AttributeSyntax))));
            }
        }

        [Fact]
        public async Task OtherAttributesAsync()
        {
            // conflicting with DataMember
            {
                var comp = await GetCompilationAsync(
                    @"
using System;
using System.Runtime.Serialization;
using Cesil;

namespace Foo 
{   
    class MyAttribute: Attribute
    {
        public int Order { get; set; }
    }

    [GenerateSerializer]
    public class WriteMe
    {
        [DataMember(Order = 3)]
        [My(Order = 17)]
        public int Bar { get; set; }
    }
}",
                    nameof(OtherAttributesAsync),
                    NullableContextOptions.Disable
                );

                var parseOptions = (CSharpParseOptions)comp.SyntaxTrees.ElementAt(0).Options;

                var generators = ImmutableArray.Create(new SerializerGenerator());

                GeneratorDriver driver = CSharpGeneratorDriver.Create(generators, parseOptions: parseOptions);

                driver.RunGeneratorsAndUpdateCompilation(comp, out comp, out var diagnostics);

                Assert.Empty(diagnostics);
            }

            // conflicting with SerializerMember
            {
                var comp = await GetCompilationAsync(
                    @"
using System;
using Cesil;

namespace Foo 
{   
    class MyAttribute: Attribute
    {
        public int Order { get; set; }
    }

    [GenerateSerializer]
    public class WriteMe
    {
        [SerializerMember(Order = 3)]
        [My(Order = 17)]
        public int Bar { get; set; }
    }
}",
                    nameof(OtherAttributesAsync),
                    NullableContextOptions.Disable,
                    doNotAddReferences: new[] { "System.Runtime.Serialization.Primitives" }
                );

                var parseOptions = (CSharpParseOptions)comp.SyntaxTrees.ElementAt(0).Options;

                var generators = ImmutableArray.Create(new SerializerGenerator());

                GeneratorDriver driver = CSharpGeneratorDriver.Create(generators, parseOptions: parseOptions);

                driver.RunGeneratorsAndUpdateCompilation(comp, out comp, out var diagnostics);

                Assert.Empty(diagnostics);
            }
        }

        [Fact]
        public async Task IsAccessibleAsync()
        {
            // public
            {
                var comp =
                    await GetCompilationAsync(
                        @"namespace Test
                        {
                            class Foo
                            {
                                public void Bar() { }
                            }
                        }",
                        nameof(IsAccessibleAsync),
                        NullableContextOptions.Disable
                    );

                var attrMembers = comp.GetAttributedMembers();

                var t = comp.GetTypeByMetadataName("Test.Foo");
                Assert.NotNull(t);

                var m = t.GetMembers().OfType<IMethodSymbol>().Single(x => x.Name == "Bar");

                Assert.True(m.IsAccessible(attrMembers));
            }

            // private
            {
                var comp =
                    await GetCompilationAsync(
                        @"namespace Test
                        {
                            class Foo
                            {
                                private void Bar() { }
                            }
                        }",
                        nameof(IsAccessibleAsync),
                        NullableContextOptions.Disable
                    );

                var attrMembers = comp.GetAttributedMembers();

                var t = comp.GetTypeByMetadataName("Test.Foo");
                Assert.NotNull(t);

                var m = t.GetMembers().OfType<IMethodSymbol>().Single(x => x.Name == "Bar");

                Assert.False(m.IsAccessible(attrMembers));
            }

            // internal, same assembly
            {
                var comp =
                    await GetCompilationAsync(
                        @"namespace Test
                        {
                            class Foo
                            {
                                internal void Bar() { }
                            }
                        }",
                        nameof(IsAccessibleAsync),
                        NullableContextOptions.Disable
                    );

                var attrMembers = comp.GetAttributedMembers();

                var t = comp.GetTypeByMetadataName("Test.Foo");
                Assert.NotNull(t);

                var m = t.GetMembers().OfType<IMethodSymbol>().Single(x => x.Name == "Bar");

                Assert.True(m.IsAccessible(attrMembers));
            }

            // internal, different assembly
            {
                var (comp1, comp2) =
                    await GetTwoCompilationsAsync(
                        @"namespace Test1
                        {
                            class Foo
                            {
                                internal void Bar() { }
                            }
                        }",
                        @"namespace Test2
                        {
                            class Fizz
                            {
                                internal void Buzz() { }
                            }
                        }",
                        nameof(IsAccessibleAsync),
                        NullableContextOptions.Disable
                    );

                var attrMembers1 = comp1.GetAttributedMembers();
                var attrMembers2 = comp2.GetAttributedMembers();

                var t1 = comp1.GetTypeByMetadataName("Test1.Foo");
                Assert.NotNull(t1);

                var m1 = t1.GetMembers().OfType<IMethodSymbol>().Single(x => x.Name == "Bar");

                var t2 = comp2.GetTypeByMetadataName("Test2.Fizz");
                Assert.NotNull(t2);

                var m2 = t2.GetMembers().OfType<IMethodSymbol>().Single(x => x.Name == "Buzz");

                // can't cross assemblies
                Assert.False(m1.IsAccessible(attrMembers2));
                Assert.False(m2.IsAccessible(attrMembers1));

                // but can refer within same assemblies
                Assert.True(m1.IsAccessible(attrMembers1));
                Assert.True(m2.IsAccessible(attrMembers2));
            }

            static async Task<(Compilation, Compilation)> GetTwoCompilationsAsync(
                string testFile1,
                string testFile2,
                string caller,
                NullableContextOptions nullableContext
            )
            {
                var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator);
                var systemAssemblies = trustedAssemblies.Where(p => Path.GetFileName(p).StartsWith("System.")).ToList();

                var references = systemAssemblies.Select(s => MetadataReference.CreateFromFile(s)).ToList();

                var projectName1 = $"Cesil.Tests.{nameof(SourceGeneratorTests)}_1";
                var projectName2 = $"Cesil.Tests.{nameof(SourceGeneratorTests)}_2";
                var projectId1 = ProjectId.CreateNewId(projectName1);
                var projectId2 = ProjectId.CreateNewId(projectName2);

                var compilationOptions =
                    new CSharpCompilationOptions(
                        OutputKind.DynamicallyLinkedLibrary,
                        allowUnsafe: true,
                        nullableContextOptions: nullableContext
                    );

                var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp9);

                var projectInfo1 =
                    ProjectInfo.Create(
                        projectId1,
                        VersionStamp.Create(),
                        projectName1,
                        projectName1,
                        LanguageNames.CSharp,
                        compilationOptions: compilationOptions,
                        parseOptions: parseOptions
                    );

                var projectInfo2 =
                    ProjectInfo.Create(
                        projectId2,
                        VersionStamp.Create(),
                        projectName2,
                        projectName2,
                        LanguageNames.CSharp,
                        compilationOptions: compilationOptions,
                        parseOptions: parseOptions
                    );

                var workspace = new AdhocWorkspace();

                var solution =
                    workspace
                        .CurrentSolution
                        .AddProject(projectInfo1)
                        .AddProject(projectInfo2);

                foreach (var reference in references)
                {
                    solution = solution.AddMetadataReference(projectId1, reference);
                    solution = solution.AddMetadataReference(projectId2, reference);
                }

                var csFile1 = $"{caller}_1.cs";
                var docId1 = DocumentId.CreateNewId(projectId1, csFile1);

                var project1 = solution.GetProject(projectId1);

                project1 = project1.AddDocument(csFile1, testFile1).Project;

                // find the Cesil folder to include code from
                var cesilRef = GetCesilReference();
                project1 = project1.AddMetadataReference(cesilRef);

                var netstandardRef = GetNetStandard20Reference();
                project1 = project1.AddMetadataReference(netstandardRef);

                var comp1 = await project1.GetCompilationAsync();

                var csFile2 = $"{caller}_2.cs";
                var docId2 = DocumentId.CreateNewId(projectId2, csFile2);

                var project2 = solution.GetProject(projectId1);

                project2 = project2.AddDocument(csFile2, testFile2).Project;

                // find the Cesil folder to include code from
                project2 = project2.AddMetadataReference(cesilRef);

                project2 = project2.AddMetadataReference(netstandardRef);

                var comp2 = await project2.GetCompilationAsync();

                return (comp1, comp2);
            }
        }

        [Fact]
        public void ParserEquality()
        {
            var types = new[] { Type, Type2 };
            var methods = new[] { Method, Method2 };

            var parsersBuilder = ImmutableArray.CreateBuilder<SourceGenerator.Parser>();

            foreach (var t in types)
            {
                foreach (var m in methods)
                {
                    parsersBuilder.Add(new SourceGenerator.Parser(m, t));
                }
            }

            var bools = new[] { true, false };
            var str1 = new[] { "abcd", "efgh" };
            var str2 = new[] { "hello", "world" };

            foreach (var b in bools)
            {
                foreach (var s1 in str1)
                {
                    foreach (var s2 in str2)
                    {
                        parsersBuilder.Add(new SourceGenerator.Parser(b, s1, s2));
                    }
                }
            }

            var parsers = parsersBuilder.ToImmutable();

            for (var i = 0; i < parsers.Length; i++)
            {
                var p1 = parsers[i];

                Assert.False(p1.Equals(default(SourceGenerator.Formatter)));
                Assert.False(p1.Equals("hello"));

                for (var j = 0; j < parsers.Length; j++)
                {
                    var p2 = parsers[j];

                    if (i == j)
                    {
                        Assert.True(p1.Equals(p2));
                        Assert.Equal(p1.GetHashCode(), p2.GetHashCode());
                    }
                    else
                    {
                        Assert.NotEqual(p1, p2);
                    }
                }
            }
        }

        [Fact]
        public void FormatterEquality()
        {
            var types = new[] { Type, Type2 };
            var methods = new[] { Method, Method2 };

            var formattersBuilder = ImmutableArray.CreateBuilder<SourceGenerator.Formatter>();

            foreach (var t in types)
            {
                foreach (var m in methods)
                {
                    formattersBuilder.Add(new SourceGenerator.Formatter(m, t));
                }
            }

            var bools = new[] { true, false };
            var str1 = new[] { "abcd", "efgh" };
            var str2 = new[] { "hello", "world" };

            foreach (var b in bools)
            {
                foreach (var s1 in str1)
                {
                    foreach (var s2 in str2)
                    {
                        formattersBuilder.Add(new SourceGenerator.Formatter(b, s1, s2));
                    }
                }
            }

            var formatters = formattersBuilder.ToImmutable();

            for (var i = 0; i < formatters.Length; i++)
            {
                var f1 = formatters[i];

                Assert.False(f1.Equals(default(SourceGenerator.Formatter)));
                Assert.False(f1.Equals("hello"));

                for (var j = 0; j < formatters.Length; j++)
                {
                    var f2 = formatters[j];

                    if (i == j)
                    {
                        Assert.True(f1.Equals(f2));
                        Assert.Equal(f1.GetHashCode(), f2.GetHashCode());
                    }
                    else
                    {
                        Assert.NotEqual(f1, f2);
                    }
                }
            }
        }

        [Fact]
        public async Task FrameworkTypesAsync()
        {
            // no IBufferWriter
            {
                var comp = await GetMinimalCompilationAsync(@"");

                Assert.False(FrameworkTypes.TryCreate(comp, BuiltInTypes.Create(comp), out _));
            }

            // no FlagsAttribute
            {
                var comp =
                    await GetMinimalCompilationAsync(
                        @"namespace System.Buffers
                        {
                            public interface IBufferWriter<T> { }
                        }"
                    );

                Assert.False(FrameworkTypes.TryCreate(comp, BuiltInTypes.Create(comp), out _));
            }

            // no ReadOnlySpan
            {
                var comp =
                    await GetMinimalCompilationAsync(
                        @"namespace System.Buffers
                        {
                            public interface IBufferWriter<T> { }
                        }

                        namespace System
                        {
                            public class FlagsAttribute { }
                        }"
                    );

                Assert.False(FrameworkTypes.TryCreate(comp, BuiltInTypes.Create(comp), out _));
            }

            // succeeds
            {
                var comp =
                    await GetMinimalCompilationAsync(
                        @"namespace System.Buffers
                        {
                            public interface IBufferWriter<T> { }
                        }

                        namespace System
                        {
                            public class FlagsAttribute { }
                            public class ReadOnlySpan<T> { }
                        }"
                    );

                Assert.True(FrameworkTypes.TryCreate(comp, BuiltInTypes.Create(comp), out _));
            }

            static async Task<Compilation> GetMinimalCompilationAsync(string testFile)
            {
                var projectName = $"Cesil.Tests.{nameof(SourceGeneratorTests)}";
                var projectId = ProjectId.CreateNewId(projectName);

                var compilationOptions =
                    new CSharpCompilationOptions(
                        OutputKind.DynamicallyLinkedLibrary,
                        allowUnsafe: true
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

                var csFile = $"{nameof(FrameworkTypesAsync)}.cs";
                var docId = DocumentId.CreateNewId(projectId, csFile);

                var project = solution.GetProject(projectId);

                project = project.AddDocument(csFile, testFile).Project;

                return await project.GetCompilationAsync();
            }
        }

        [Fact]
        public async Task CesilTypesAsync()
        {
            // no GenerateSerializerAttribute
            {
                var comp = await GetCompilationAsync(@"", nameof(CesilTypesAsync), NullableContextOptions.Annotations, addCesilReferences: false);

                Assert.False(CesilTypes.TryCreate(comp, out _));
            }

            // no SerializerMemberAttribute
            {
                var comp =
                    await GetCompilationAsync(
                        @"namespace Cesil
                        {
                            public class GenerateSerializerAttribute { }
                        }",
                        nameof(CesilTypesAsync),
                        NullableContextOptions.Annotations,
                        addCesilReferences: false
                    );

                Assert.False(CesilTypes.TryCreate(comp, out _));
            }

            // no WriteContext
            {
                var comp =
                    await GetCompilationAsync(
                        @"namespace Cesil
                        {
                            public class GenerateSerializerAttribute { }
                            public class SerializerMemberAttribute { }
                        }",
                        nameof(CesilTypesAsync),
                        NullableContextOptions.Annotations,
                        addCesilReferences: false
                    );

                Assert.False(CesilTypes.TryCreate(comp, out _));
            }

            // no GenerateDeserializerAttribute
            {
                var comp =
                    await GetCompilationAsync(
                        @"namespace Cesil
                        {
                            public class GenerateSerializerAttribute { }
                            public class SerializerMemberAttribute { }
                            public class WriteContext { }
                        }",
                        nameof(CesilTypesAsync),
                        NullableContextOptions.Annotations,
                        addCesilReferences: false
                    );

                Assert.False(CesilTypes.TryCreate(comp, out _));
            }

            // no DeserializerMemberAttribute
            {
                var comp =
                    await GetCompilationAsync(
                        @"namespace Cesil
                        {
                            public class GenerateSerializerAttribute { }
                            public class SerializerMemberAttribute { }
                            public class WriteContext { }
                            public class GenerateDeserializerAttribute { }
                        }",
                        nameof(CesilTypesAsync),
                        NullableContextOptions.Annotations,
                        addCesilReferences: false
                    );

                Assert.False(CesilTypes.TryCreate(comp, out _));
            }

            // no DeserializerInstanceProviderAttribute
            {
                var comp =
                    await GetCompilationAsync(
                        @"namespace Cesil
                        {
                            public class GenerateSerializerAttribute { }
                            public class SerializerMemberAttribute { }
                            public class WriteContext { }
                            public class GenerateDeserializerAttribute { }
                            public class DeserializerMemberAttribute { }
                        }",
                        nameof(CesilTypesAsync),
                        NullableContextOptions.Annotations,
                        addCesilReferences: false
                    );

                Assert.False(CesilTypes.TryCreate(comp, out _));
            }

            // no ReadContext
            {
                var comp =
                    await GetCompilationAsync(
                        @"namespace Cesil
                        {
                            public class GenerateSerializerAttribute { }
                            public class SerializerMemberAttribute { }
                            public class WriteContext { }
                            public class GenerateDeserializerAttribute { }
                            public class DeserializerMemberAttribute { }
                            public class DeserializerInstanceProviderAttribute { }
                        }",
                        nameof(CesilTypesAsync),
                        NullableContextOptions.Annotations,
                        addCesilReferences: false
                    );

                Assert.False(CesilTypes.TryCreate(comp, out _));
            }

            // succeeds
            {
                var comp =
                    await GetCompilationAsync(
                        @"namespace Cesil
                        {
                            public class GenerateSerializerAttribute { }
                            public class SerializerMemberAttribute { }
                            public class WriteContext { }
                            public class GenerateDeserializerAttribute { }
                            public class DeserializerMemberAttribute { }
                            public class DeserializerInstanceProviderAttribute { }
                            public class ReadContext { }
                        }",
                        nameof(CesilTypesAsync),
                        NullableContextOptions.Annotations,
                        addCesilReferences: false
                    );

                Assert.True(CesilTypes.TryCreate(comp, out _));
            }
        }

        [Fact]
        public void RaiseDiagnostics()
        {
            var uniqueCodesBuilder = ImmutableHashSet.CreateBuilder<string>();

            {
                var diag = Diagnostics.AllConstructorParametersMustBeMembers(Location, Type);
                CheckDiag(
                    diag,
                    $"All parameters of [{nameof(DeserializerInstanceProviderAttribute)}] constructor must be annotated with [{nameof(DeserializerMemberAttribute)}]",
                    $"All of type FooC's {nameof(InstanceProvider)} constructor's parameters must be annotated with [{nameof(DeserializerMemberAttribute)}]"
                );
            }

            {
                var diag = Diagnostics.BadFormatterParameters(Location, Method, Type);
                CheckDiag(
                    diag,
                    $"Invalid {nameof(Formatter)} method parameters",
                    $"Method Fizz does not take the correct parameters - should take FooC (or a type it can be assigned to), in {nameof(WriteContext)}, and IBufferWriter<char>"
                );
            }

            {
                var diag = Diagnostics.BadGetterParameters_InstanceOne(Location, Method);
                CheckDiag(
                    diag,
                    $"Invalid {nameof(Getter)} method parameters",
                    $"Method Fizz, which is an instance method and takes one parameter, should take in {nameof(WriteContext)}"
                );
            }

            {
                var diag = Diagnostics.BadGetterParameters_StaticOne(Location, Method, Type);
                CheckDiag(
                    diag,
                    $"Invalid {nameof(Getter)} method parameters",
                    $"Method Fizz, which is static and takes one parameter, should take FooC (or a type it can be assigned to), or in {nameof(WriteContext)}"
                );
            }

            {
                var diag = Diagnostics.BadGetterParameters_StaticTwo(Location, Method, Type);
                CheckDiag(
                    diag,
                    $"Invalid {nameof(Getter)} method parameters",
                    $"Method Fizz, which is static and takes two parameters, should take FooC (or a type it can be assigned to) and in {nameof(WriteContext)}"
                 );
            }

            {
                var diag = Diagnostics.BadGetterParameters_TooMany(Location, Method);
                CheckDiag(
                    diag,
                    $"Invalid {nameof(Getter)} method parameters",
                    $"Method Fizz takes too many parameters"
                );
            }

            {
                var diag = Diagnostics.BadInstanceProviderParameters(Location, Method);
                CheckDiag(
                    diag,
                    $"Invalid {nameof(InstanceProvider)} method parameters",
                    $"Method Fizz must take an in {nameof(ReadContext)}, and produce an out value assignable to the attributed type"
                );
            }

            {
                var diag = Diagnostics.BadParserParameters(Location, Method);
                CheckDiag(
                    diag,
                    $"Invalid {nameof(Parser)} method parameters",
                    $"Method Fizz must take a ReadOnlySpan<char>, an in {nameof(ReadContext)}, and produce an out value"
                 );
            }

            {
                var diag = Diagnostics.BadResetParameters_InstanceOne(Location, Method);
                CheckDiag(
                    diag,
                    $"Invalid {nameof(Reset)} method parameters",
                    $"Method Fizz, which is instance and takes one parameter, must take an in {nameof(ReadContext)}"
                );
            }

            {
                var diag = Diagnostics.BadResetParameters_StaticOne(Location, Method, Type);
                CheckDiag(
                    diag,
                    $"Invalid {nameof(Reset)} method parameters",
                    $"Method Fizz, which is static and takes one parameter, must take an in {nameof(ReadContext)} or the row type (FooC)"
                );
            }

            {
                var diag = Diagnostics.BadResetParameters_StaticTwo(Location, Method, Type);
                CheckDiag(
                    diag,
                    $"Invalid {nameof(Reset)} method parameters",
                    $"Method Fizz, which is static and takes two parameters, must take the row type (FooC, potentially by ref) and in {nameof(ReadContext)}"
                );
            }

            {
                var diag = Diagnostics.BadResetParameters_TooMany(Location, Method);
                CheckDiag(
                    diag,
                    $"Invalid {nameof(Reset)} method parameters",
                    $"Method Fizz takes too many parameters, expect at most 1 for instance methods and 2 for static methods"
                );
            }

            {
                var diag = Diagnostics.BadReset_MustBeStaticForParameters(Location, Type, Parameter, Method);
                CheckDiag(
                    diag,
                    $"For constructor parameters, {nameof(Reset)} methods must be static",
                    $"Parameter a on type FooC's constructor has a non-static {nameof(Reset)} method Fizz"
                );
            }

            {
                var diag = Diagnostics.BadReset_NotOnRow(Location, Method, Type);
                CheckDiag(
                    diag,
                    $"Invalid {nameof(Reset)} method",
                    $"Method Fizz is an instance method, and so must be invokable on the row type (FooC)"
                );
            }

            {
                var diag = Diagnostics.BadSetterParameters_InstanceOne(Location, Method);
                CheckDiag(
                    diag,
                    $"Invalid {nameof(Setter)} method parameters",
                    $"Method Fizz, which is instance and takes one parameter, should take a value (not by ref)"
                );
            }

            {
                var diag = Diagnostics.BadSetterParameters_InstanceTwo(Location, Method);
                CheckDiag(
                    diag,
                    $"Invalid {nameof(Setter)} method parameters",
                    $"Method Fizz, which is instance and takes two parameters, should take a value (not by ref) and in {nameof(ReadContext)}"
                );
            }

            {
                var diag = Diagnostics.BadSetterParameters_StaticOne(Location, Method);
                CheckDiag(
                    diag,
                    $"Invalid {nameof(Setter)} method parameters",
                    $"Method Fizz, which is static and takes one parameter, should take a value (not by reference)"
                );
            }

            {
                var diag = Diagnostics.BadSetterParameters_StaticThree(Location, Method, Type);
                CheckDiag(
                    diag,
                    $"Invalid {nameof(Setter)} method parameters",
                    $"Method Fizz, which is static and takes three parameters, should take FooC (possible by ref), a value, and an in {nameof(ReadContext)}"
                );
            }

            {
                var diag = Diagnostics.BadSetterParameters_StaticTwo(Location, Method, Type);
                CheckDiag(
                    diag,
                    $"Invalid {nameof(Setter)} method parameters",
                    $"Method Fizz, which is static and takes two parameters, should take either a value and in {nameof(ReadContext)} or FooC (possible by ref) and a value"
                );
            }

            {
                var diag = Diagnostics.BadSetterParameters_TooFew(Location, Method);
                CheckDiag(
                    diag,
                    $"Invalid {nameof(Setter)} method parameters",
                    $"Method Fizz takes too few parameters, expected it to take at least a value"
                );
            }

            {
                var diag = Diagnostics.BadSetterParameters_TooMany(Location, Method);
                CheckDiag(
                    diag,
                    $"Invalid {nameof(Setter)} method parameters",
                    $"Method Fizz takes too many parameters, expected at most 2 for instance method and 3 for static methods"
                );
            }

            {
                var diag = Diagnostics.BadSetter_CannotHaveInitSettersWithNonConstructorInstanceProviders(Location, Type, Property);
                CheckDiag(
                    diag,
                    $"Properties with init setters cannot be used with non-constructor {nameof(InstanceProvider)}",
                    $"Property Bar on FooC has an init setter, but the {nameof(InstanceProvider)} for FooC is not backed by a constructor"
                );
            }

            {
                var diag = Diagnostics.BadSetter_NotOnRow(Location, Method, Type);
                CheckDiag(
                    diag,
                    $"Invalid {nameof(Setter)} method",
                    $"Method Fizz is an instance method, and must be invokable on the row type (FooC)"
                );
            }

            {
                var diag = Diagnostics.BadShouldSerializeParameters_InstanceOne(Location, Method);
                CheckDiag(
                    diag,
                    $"Invalid {nameof(ShouldSerialize)} method parameters",
                    $"Method Fizz, which is instance and takes one parameter, should take in {nameof(WriteContext)}"
                );
            }

            {
                var diag = Diagnostics.BadShouldSerializeParameters_StaticOne(Location, Method, Type);
                CheckDiag(
                    diag,
                    $"Invalid {nameof(ShouldSerialize)} method parameters",
                    $"Method Fizz, which is static and takes one parameter, should take FooC (or a type it can be assigned to)"
                );
            }

            {
                var diag = Diagnostics.BadShouldSerializeParameters_StaticTwo(Location, Method, Type);
                CheckDiag(
                    diag,
                    $"Invalid {nameof(ShouldSerialize)} method parameters",
                    $"Method Fizz, which is static and takes two parameters, should take FooC (or a type it can be assigned to), and in {nameof(WriteContext)}"
                );
            }

            {
                var diag = Diagnostics.BadShouldSerializeParameters_TooMany(Location, Method);
                CheckDiag(
                    diag,
                    $"Invalid {nameof(ShouldSerialize)} method parameters",
                    $"Method Fizz takes too many parameters"
                );
            }

            {
                var diag = Diagnostics.ConstructorHasMembersButIsntInstanceProvider(Location, Type);
                CheckDiag(
                    diag,
                    $"[{nameof(DeserializerMemberAttribute)}] applied to constructor parameters, but constructor isn't annotated with [{nameof(DeserializerInstanceProviderAttribute)}]",
                    $"Type FooC has constructor with annotated constructor parameters, but constructor is not an {nameof(InstanceProvider)}"
                );
            }

            {
                var diag = Diagnostics.CouldNotExtractConstantValue(Location);
                CheckDiag(
                    diag,
                    $"Constant expression's value could not extracted",
                    $"Constant expression's value could not extracted"
                );
            }

            {
                var diag = Diagnostics.CouldNotFindMethod(Location, "foo", "bar");
                CheckDiag(
                    diag,
                    $"Could not find method",
                    $"No method bar on foo found"
                );
            }

            {
                var diag = Diagnostics.DeserializableMemberMustHaveNameSetForMethod(Location, Method);
                CheckDiag(
                    diag,
                    $"[{nameof(DeserializerMemberAttribute)}] must have {nameof(DeserializerMemberAttribute.Name)} set",
                    $"Method Fizz with [{nameof(DeserializerMemberAttribute)}] must have property {nameof(DeserializerMemberAttribute.Name)} explicitly set"
                );
            }

            {
                var diag = Diagnostics.DeserializableMemberOnNonConstructorParameter(Location, Type, Method);
                CheckDiag(
                    diag,
                    $"[{nameof(DeserializerMemberAttribute)}] applied to non-constructor parameter",
                    $"Type FooC's method Fizz has parameter with [{nameof(DeserializerMemberAttribute)}], which is not permitted"
                );
            }

            {
                var diag = Diagnostics.DeserializablePropertyCannotHaveParameters(Location);
                CheckDiag(diag, $"Property cannot take parameters", $"Deserializable properties cannot take parameters");
            }

            {
                var diag = Diagnostics.EmitDefaultValueSpecifiedMultipleTimes(Location);
                CheckDiag(diag, $"Member's {nameof(SerializerMemberAttribute.EmitDefaultValue)} was specified multiple times", $"Only one attribute may specify {nameof(SerializerMemberAttribute.EmitDefaultValue)} per member");
            }

            {
                var diag = Diagnostics.FormatterBothMustBeSet(Location);
                CheckDiag(
                    diag,
                    $"Both {nameof(SerializerMemberAttribute.FormatterType)} and {nameof(SerializerMemberAttribute.FormatterMethodName)} must be set",
                    $"Either both must be set, or neither must be set - only one was set"
                );
            }

            {
                var diag = Diagnostics.FormatterMethodNameSpecifiedMultipleTimes(Location);
                CheckDiag(
                    diag,
                    $"Member's {nameof(SerializerMemberAttribute.FormatterMethodName)} was specified multiple times",
                    $"Only one attribute may specify {nameof(SerializerMemberAttribute.FormatterMethodName)} per member"
                );
            }

            {
                var diag = Diagnostics.FormatterTypeSpecifiedMultipleTimes(Location);
                CheckDiag(
                    diag,
                    $"Member's {nameof(SerializerMemberAttribute.FormatterType)} was specified multiple times",
                    $"Only one attribute may specify {nameof(SerializerMemberAttribute.FormatterType)} per member"
                );
            }

            {
                var diag = Diagnostics.FormatterTypeSpecifiedMultipleTimes(Location);
                CheckDiag(
                    diag,
                    $"Member's {nameof(SerializerMemberAttribute.FormatterType)} was specified multiple times",
                    $"Only one attribute may specify {nameof(SerializerMemberAttribute.FormatterType)} per member"
                );
            }

            {
                var diag = Diagnostics.GenericError(Location, "fizz");
                CheckDiag(diag, $"Unexpected error occurred", $"Something went wrong: fizz");
            }

            {
                var diag = Diagnostics.InstanceProviderBothMustBeSet(Location);
                CheckDiag(
                    diag,
                    $"Both {nameof(GenerateDeserializerAttribute.InstanceProviderType)} and {nameof(GenerateDeserializerAttribute.InstanceProviderMethodName)} must be set",
                    $"Either both must be set, or neither must be set - only one was set"
                );
            }

            {
                var diag = Diagnostics.InstanceProviderConstructorAndMethodProvided(Location, Type);
                CheckDiag(
                    diag,
                    $"Both method and constructor {nameof(InstanceProvider)} specified",
                    $"Type FooC has a constructor marked as an {nameof(InstanceProvider)}, and a method provided as an {nameof(InstanceProvider)} - only one may be specified"
                );
            }

            {
                var diag = Diagnostics.InstanceProviderMethodNameSpecifiedMultipleTimes(Location);
                CheckDiag(
                    diag,
                    $"{nameof(GenerateDeserializerAttribute.InstanceProviderMethodName)} was specified multiple times",
                    $"{nameof(GenerateDeserializerAttribute.InstanceProviderMethodName)} may only be specified once"
                );
            }

            {
                var diag = Diagnostics.InstanceProviderTypeSpecifiedMultipleTimes(Location);
                CheckDiag(
                    diag,
                    $"{nameof(GenerateDeserializerAttribute.InstanceProviderType)} was specified multiple times",
                    $"{nameof(GenerateDeserializerAttribute.InstanceProviderType)} may only be specified once"
                );
            }

            {
                var diag = Diagnostics.IsRequiredSpecifiedMultipleTimes(Location);
                CheckDiag(
                    diag,
                    $"Member's {nameof(System.Runtime.Serialization.DataMemberAttribute.IsRequired)} or {nameof(DeserializerMemberAttribute.MemberRequired)} was specified multiple times",
                    $"Only one attribute may specify {nameof(System.Runtime.Serialization.DataMemberAttribute.IsRequired)} or {nameof(DeserializerMemberAttribute.MemberRequired)} per member"
                );
            }

            {
                var diag = Diagnostics.MethodCannotBeGeneric(Location, Method);
                CheckDiag(diag, "Method cannot be generic", "Method Fizz is generic, which is not supported");
            }

            {
                var diag = Diagnostics.MethodMustReturnBool(Location, Method);
                CheckDiag(diag, "Method must return bool", $"Method Fizz does not return bool, but must do so");
            }

            {
                var diag = Diagnostics.MethodMustReturnNonVoid(Location, Method);
                CheckDiag(diag, $"Method cannot return void", $"Method Fizz must return a value, found void");
            }

            {
                var diag = Diagnostics.MethodMustReturnVoid(Location, Method);
                CheckDiag(diag, $"Method must return void", $"Method Fizz must return void, but returns a value");
            }

            {
                var diag = Diagnostics.MethodNotPublicOrInternal(Location, Method);
                CheckDiag(diag, $"Method not public or internal", $"Method Fizz is not accessible - it must either be public, or internal and declared in the compiled assembly");
            }

            {
                var diag = Diagnostics.MethodNotStatic(Location, Method);
                CheckDiag(diag, $"Method not static", $"Method Fizz must be static");
            }

            {
                var diag = Diagnostics.MultipleMethodsFound(Location, "foo", "bar");
                CheckDiag(diag, $"More than one method found", $"Multiple methods with name bar on foo were found");
            }

            {
                var diag = Diagnostics.NameSpecifiedMultipleTimes(Location);
                CheckDiag(diag, $"Member's {nameof(DeserializerMemberAttribute.Name)} was specified multiple times", $"Only one attribute may specify {nameof(DeserializerMemberAttribute.Name)} per member");
            }

            {
                var diag = Diagnostics.NoBuiltInFormatter(Location, Type);
                CheckDiag(diag, $"No default {nameof(Formatter)}", $"There is no default {nameof(Formatter)} for FooC, you must provide one");
            }

            {
                var diag = Diagnostics.NoBuiltInParser(Location, Type);
                CheckDiag(diag, $"No default {nameof(Parser)}", $"There is no default {nameof(Parser)} for FooC, you must provide one");
            }

            {
                var diag = Diagnostics.NoCesilReference(Location);
                CheckDiag(diag, $"Missing {nameof(Cesil)} Reference", $"Could not find a type exported by {nameof(Cesil)}, are you missing a reference?");
            }

            {
                var diag = Diagnostics.NoGetterOnSerializableProperty(Location);
                CheckDiag(diag, $"Property lacking getter", $"Serializable properties must declare a getter");
            }

            {
                var diag = Diagnostics.NoInstanceProvider(Location, Type);
                CheckDiag(
                    diag,
                    $"No {nameof(InstanceProvider)} configured",
                    $"Type FooC does not have an {nameof(InstanceProvider)} - a type must have an accessible parameterless constructor, or an explicit {nameof(InstanceProvider)} configured with {nameof(GenerateDeserializerAttribute.InstanceProviderType)} and {nameof(GenerateDeserializerAttribute.InstanceProviderMethodName)} on its [{nameof(GenerateDeserializerAttribute)}]"
                );
            }

            {
                var diag = Diagnostics.NoSetterOnDeserializableProperty(Location);
                CheckDiag(diag, $"Property lacking setter", $"Deserializable properties must declare a setter");
            }

            {
                var diag = Diagnostics.NoSystemMemoryReference(Location);
                CheckDiag(diag, $"Missing System.Memory Reference", $"Could not find a type exported by System.Memory, are you missing a reference?", allowDot: true);
            }

            {
                var diag = Diagnostics.OrderSpecifiedMultipleTimes(Location);
                CheckDiag(diag, $"Member's {nameof(SerializerMemberAttribute.Order)} was specified multiple times", $"Only one attribute may specify {nameof(SerializerMemberAttribute.Order)} per member");
            }

            {
                var diag = Diagnostics.ParametersMustBeRequired(Location, Type, Parameter);
                CheckDiag(
                    diag,
                    $"Parameter cannot be optional, {nameof(DeserializerMemberAttribute.MemberRequired)} or {nameof(System.Runtime.Serialization.DataMemberAttribute.IsRequired)} must be {nameof(MemberRequired.Yes)}, or true",
                    $"Parameter a on type FooC's constructor cannot be optional"
                );
            }

            {
                var diag = Diagnostics.ParserBothMustBeSet(Location);
                CheckDiag(
                    diag,
                    $"Both {nameof(DeserializerMemberAttribute.ParserType)} and {nameof(DeserializerMemberAttribute.ParserMethodName)} must be set",
                    $"Either both must be set, or neither must be set - only one was set"
                );
            }

            {
                var diag = Diagnostics.ParserMethodNameSpecifiedMultipleTimes(Location);
                CheckDiag(
                    diag,
                    $"Member's {nameof(DeserializerMemberAttribute.ParserMethodName)} was specified multiple times",
                    $"Only one attribute may specify {nameof(DeserializerMemberAttribute.ParserMethodName)} per member"
                );
            }

            {
                var diag = Diagnostics.ParserTypeSpecifiedMultipleTimes(Location);
                CheckDiag(
                    diag,
                    $"Member's {nameof(DeserializerMemberAttribute.ParserType)} was specified multiple times",
                    $"Only one attribute may specify {nameof(DeserializerMemberAttribute.ParserType)} per member"
                );
            }

            {
                var diag = Diagnostics.ResetBothMustBeSet(Location);
                CheckDiag(
                    diag,
                    $"Both {nameof(DeserializerMemberAttribute.ResetType)} and {nameof(DeserializerMemberAttribute.ResetMethodName)} must be set",
                    $"Either both must be set, or neither must be set - only one was set"
                );
            }

            {
                var diag = Diagnostics.ResetMethodNameSpecifiedMultipleTimes(Location);
                CheckDiag(
                    diag,
                    $"Member's {nameof(DeserializerMemberAttribute.ResetMethodName)} was specified multiple times",
                    $"Only one attribute may specify {nameof(DeserializerMemberAttribute.ResetMethodName)} per member"
                );
            }

            {
                var diag = Diagnostics.ResetTypeSpecifiedMultipleTimes(Location);
                CheckDiag(
                    diag,
                    $"Member's {nameof(DeserializerMemberAttribute.ResetType)} was specified multiple times",
                    $"Only one attribute may specify {nameof(DeserializerMemberAttribute.ResetType)} per member"
                );
            }

            {
                var diag = Diagnostics.SerializableMemberMustHaveNameSetForMethod(Location, Method);
                CheckDiag(
                    diag,
                    $"[{nameof(SerializerMemberAttribute)}] must have {nameof(SerializerMemberAttribute.Name)} set",
                    $"Method Fizz with [{nameof(SerializerMemberAttribute)}] must have property {nameof(SerializerMemberAttribute.Name)} explicitly set"
                );
            }

            {
                var diag = Diagnostics.SerializablePropertyCannotHaveParameters(Location);
                CheckDiag(
                    diag,
                    $"Property cannot take parameters",
                    $"Serializable properties cannot take parameters"
                );
            }

            {
                var diag = Diagnostics.ShouldSerializeBothMustBeSet(Location);
                CheckDiag(
                    diag,
                    $"Both {nameof(SerializerMemberAttribute.ShouldSerializeType)} and {nameof(SerializerMemberAttribute.ShouldSerializeMethodName)} must be set",
                    $"Either both must be set, or neither must be set - only one was set"
                );
            }

            {
                var diag = Diagnostics.ShouldSerializeInstanceOnWrongType(Location, Method, Type);
                CheckDiag(diag, $"{nameof(ShouldSerialize)} method on wrong type", $"Method Fizz, which is an instance method, is declared on the wrong type (expected declaration on FooC)");
            }

            {
                var diag = Diagnostics.ShouldSerializeMethodNameSpecifiedMultipleTimes(Location);
                CheckDiag(
                    diag,
                    $"Member's {nameof(SerializerMemberAttribute.ShouldSerializeMethodName)} was specified multiple times",
                    $"Only one attribute may specify {nameof(SerializerMemberAttribute.ShouldSerializeMethodName)} per member"
                );
            }

            {
                var diag = Diagnostics.ShouldSerializeTypeSpecifiedMultipleTimes(Location);
                CheckDiag(
                    diag,
                    $"Member's {nameof(SerializerMemberAttribute.ShouldSerializeType)} was specified multiple times",
                    $"Only one attribute may specify {nameof(SerializerMemberAttribute.ShouldSerializeType)} per member"
                );
            }

            {
                var diag = Diagnostics.UnexpectedConstantValueType(Location, new[] { Types.Bool }, Types.String);
                CheckDiag(diag, $"Constant expression's type was unexpected", $"Expected a value of type Boolean, found String");
            }

            {
                var diag = Diagnostics.UnexpectedConstantValue(Location, "foo", new[] { "bar", "fizz", "buzz" });
                CheckDiag(diag, $"Unexpected constant value", $"Found constant value \"foo\", but expected one of \"bar\", or \"fizz\", or \"buzz\"");
            }

            {
                var diag = Diagnostics.MethodMustBeOrdinary(Location, Method);
                CheckDiag(diag, $"Method must be ordinay", $"Method Fizz cannot be used, only ordinary (non-operator, non-constructor, non-local, non-destrector, non-property, and non-event) methods maybe used");
            }

            // we've covered every diagnostic
            var uniqueCodes = uniqueCodesBuilder.ToImmutable();

            var knownDiags = typeof(Diagnostics).GetFields(BindingFlagsConstants.All).Where(f => f.FieldType == typeof(DiagnosticDescriptor)).ToImmutableArray();
            var knownDiagCodes = knownDiags.Select(f => ((DiagnosticDescriptor)f.GetValue(null)).Id).ToImmutableHashSet();

            var notSeen = knownDiagCodes.Except(uniqueCodes);
            var notExpected = uniqueCodes.Except(knownDiagCodes);

            Assert.Empty(notSeen);
            Assert.Empty(notExpected);

            var reusedCodes = knownDiags.Select(f => ((DiagnosticDescriptor)f.GetValue(null)).Id).GroupBy(g => g).Where(g => g.Count() > 1).Select(g => g.Key).ToImmutableHashSet();
            Assert.Empty(reusedCodes);

            // can we deal with unknowns?
            {
                var diag = Diagnostics.UnexpectedConstantValueType(Location, new[] { Types.Bool }, null);
                CheckDiag(diag, "Constant expression's type was unexpected", "Expected a value of type Boolean, found --UNKNOWN--");
            }

            void CheckDiag(Diagnostic diag, string title, string msg, bool allowDot = false)
            {
                Assert.StartsWith("CES", diag.Descriptor.Id);

                uniqueCodesBuilder.Add(diag.Descriptor.Id);

                var actualMsg = diag.GetMessage();
                var actualTitle = diag.Descriptor.Title.ToString();

                Assert.Equal(title, actualTitle);
                Assert.Equal(msg, actualMsg);

                // don't do the weird type quoting
                Assert.DoesNotContain("`", actualTitle);
                Assert.DoesNotContain("`", actualMsg);

                // don't write in sentences
                if (!allowDot)
                {
                    Assert.DoesNotContain(".", actualTitle);
                    Assert.DoesNotContain(".", actualMsg);
                }

                // make sure attributes are always bracketed
                CheckAttributeNamesBracketed(actualMsg);
                CheckAttributeNamesBracketed(actualTitle);

                // todo: any other things to prevent?
            }

            static void CheckAttributeNamesBracketed(string txt)
            {
                var ix = -1;
                while ((ix = txt.IndexOf("Attribute", ix + 1)) != -1)
                {
                    var iy = txt.IndexOf("Attribute]", ix);
                    Assert.Equal(ix, iy);
                }
            }
        }

        [Fact]
        public async Task GetOrderFromAttributesAsync()
        {
            // attribute not in collection
            {
                var comp =
                    await GetCompilationAsync(
                        @"namespace Test
                        {
                            class MyAttribute : Attribute { }

                            class Foo
                            {
                                [My]
                                public void Bar() { }
                            }
                        }",
                        nameof(IsAccessibleAsync),
                        NullableContextOptions.Disable
                    );

                var attrMembers = comp.GetAttributedMembers();

                var notInAttr = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("NotInAttribute"));

                var t = comp.GetTypeByMetadataName("Test.Foo");
                Assert.NotNull(t);

                var m = t.GetMembers().OfType<IMethodSymbol>().Single(x => x.Name == "Bar");
                var mAttrs = ((MethodDeclarationSyntax)m.DeclaringSyntaxReferences.Single().GetSyntax()).AttributeLists;

                Assert.True(FrameworkTypes.TryCreate(comp, BuiltInTypes.Create(comp), out var framework));
                Assert.True(CesilTypes.TryCreate(comp, out var cesil));

                var diags = ImmutableArray<Diagnostic>.Empty;

                var val = SourceGenerator.Utils.GetOrderFromAttributes(
                    attrMembers,
                    null,
                    framework,
                    cesil.SerializerMemberAttribute,
                    ImmutableArray.Create(notInAttr),
                    ref diags
                );
                Assert.Null(val);
            }

            // unexpected attr
            {
                var comp =
                    await GetCompilationAsync(
                        @"namespace Test
                        {
                            class MyAttribute : Attribute { }

                            class Foo
                            {
                                [My]
                                public void Bar() { }
                            }
                        }",
                        nameof(IsAccessibleAsync),
                        NullableContextOptions.Disable
                    );

                var attrMembers = comp.GetAttributedMembers();

                var t = comp.GetTypeByMetadataName("Test.Foo");
                Assert.NotNull(t);

                var m = t.GetMembers().OfType<IMethodSymbol>().Single(x => x.Name == "Bar");
                var mAttrs = ((MethodDeclarationSyntax)m.DeclaringSyntaxReferences.Single().GetSyntax()).AttributeLists;

                Assert.True(FrameworkTypes.TryCreate(comp, BuiltInTypes.Create(comp), out var framework));
                Assert.True(CesilTypes.TryCreate(comp, out var cesil));

                var diags = ImmutableArray<Diagnostic>.Empty;

                var val = SourceGenerator.Utils.GetOrderFromAttributes(attrMembers, null, framework, cesil.SerializerMemberAttribute, mAttrs[0].Attributes.ToImmutableArray(), ref diags);
                Assert.Null(val);
            }

            // no [DataMemberAttribute] loaded
            {
                var comp =
                    await GetCompilationAsync(
                        @"namespace Test
                        {
                            class Foo
                            {
                                [Serializable]
                                public void Bar() { }
                            }
                        }",
                        nameof(IsAccessibleAsync),
                        NullableContextOptions.Disable,
                        doNotAddReferences: new[] { "System.Runtime.Serialization.Primitives" }
                    );

                var attrMembers = comp.GetAttributedMembers();

                var t = comp.GetTypeByMetadataName("Test.Foo");
                Assert.NotNull(t);

                var m = t.GetMembers().OfType<IMethodSymbol>().Single(x => x.Name == "Bar");
                var mAttrs = ((MethodDeclarationSyntax)m.DeclaringSyntaxReferences.Single().GetSyntax()).AttributeLists;

                Assert.True(FrameworkTypes.TryCreate(comp, BuiltInTypes.Create(comp), out var framework));
                Assert.Null(framework.DataMemberAttribute);
                Assert.True(CesilTypes.TryCreate(comp, out var cesil));

                var diags = ImmutableArray<Diagnostic>.Empty;

                var val = SourceGenerator.Utils.GetOrderFromAttributes(attrMembers, null, framework, cesil.SerializerMemberAttribute, mAttrs[0].Attributes.ToImmutableArray(), ref diags);
                Assert.Null(val);
            }
        }

        [Fact]
        public async Task AttributeRetargettingAsync()
        {
            var comp =
                await GetCompilationAsync(
                    @"using System;

                    [assembly:Test.My(A = 1)]
                    [module:Test.My(A = 2)]
                       
                    namespace Test
                    {
                        [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
                        class MyAttribute:Attribute
                        {
                            public int A { get; set; }
                        }

                        [My(A = 3)]
                        [type:My(A = 4)]
                        class Foo
                        {
                            [My(A = 5)]
                            [method:My(A = 6)]
                            [return:My(A = 7)]
                            public void Bar(
                                [My(A = 8)]
                                [param:My(A = 9)]
                                int a
                            ) { }

#pragma warning disable CS0649      // yeah, don't care that it's unused
                            [My(A = 10)]
                            [field:My(A = 11)]
                            public int Fizz;
#pragma warning restore CS0649
                                
                            [My(A = 12)]
                            [property:My(A = 13)]
                            [field:My(A = 14)]
                            public int Buzz 
                            { 
                                [return:My(A = 15)]
                                get; 
                                    
                                [param:My(A = 16)]                                    
                                set; 
                            }

#pragma warning disable CS0067      // yeah, don't care that it's unused
                            [My(A = 17)]
                            [event:My(A = 18)]
                            public event Action Hello;
#pragma warning restore CS0067
                        }

                        record World(
                            [My(A = 19)]
                            [param:My(A = 20)]
                            int b
                        );
                    }",
                    nameof(AttributeRetargettingAsync),
                    NullableContextOptions.Disable
                );

            var diags = comp.GetDiagnostics();
            Assert.Empty(diags);

            // prove that the stuff we actually care about works
            Assert.NotNull(comp.GetAttributedMembers());

            // test the things we don't care about (events, modules, etc.)
            var tree = comp.SyntaxTrees.Single().GetRoot();

            {
                var empty = ImmutableArray.CreateBuilder<(AttributeSyntax, object, AttributeTracker.AttributeTarget?)>();
                var asmNode = GetListContaining(tree, 1);
                AttributeTracker.RecordAttributes(new object(), AttributeTracker.AttributeTarget.Assembly, asmNode, empty);
                Assert.Empty(empty);
            }

            {
                var empty = ImmutableArray.CreateBuilder<(AttributeSyntax, object, AttributeTracker.AttributeTarget?)>();
                var modNode = GetListContaining(tree, 2);
                AttributeTracker.RecordAttributes(new object(), AttributeTracker.AttributeTarget.Module, modNode, empty);
                Assert.Empty(empty);
            }

            {
                var empty = ImmutableArray.CreateBuilder<(AttributeSyntax, object, AttributeTracker.AttributeTarget?)>();
                var evtNode1 = GetListContaining(tree, 17);
                var evtNode2 = GetListContaining(tree, 18);
                
                AttributeTracker.RecordAttributes(new object(), AttributeTracker.AttributeTarget.Event, evtNode1, empty);
                Assert.Empty(empty);

                AttributeTracker.RecordAttributes(new object(), AttributeTracker.AttributeTarget.Event, evtNode2, empty);
                Assert.Empty(empty);
            }

            // now one that is just bad
            {
                var badComp =
                    await GetCompilationAsync(
                        @"using System;
                        namespace Test
                        {
                            [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
                            class MyAttribute:Attribute
                            {
                                public int A { get; set; }
                            }

                            [nogc:My(A = 999)]
                            class Foo
                            {
                            
                            }
                        }",
                        nameof(AttributeRetargettingAsync),
                        NullableContextOptions.Disable
                    );

                Assert.NotNull(badComp.GetAttributedMembers());
            }

            // now one that is just bad
            {
                var badComp =
                    await GetCompilationAsync(
                        @"using System;
                        namespace Test
                        {
                            [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
                            class MyAttribute:Attribute
                            {
                                public int A { get; set; }
                            }

                            [nogc:My(A = 999)]
                            class Foo
                            {
                            
                            }
                        }",
                        nameof(AttributeRetargettingAsync),
                        NullableContextOptions.Disable
                    );

                Assert.NotNull(badComp.GetAttributedMembers());
            }

            // lookup on a non-declaration
            {
                var nonDeclComp =
                    await GetCompilationAsync(
                        @"using System;
                        namespace Test
                        {
                            [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
                            class MyAttribute:Attribute
                            {
                                public int A { get; set; }
                            }

                            [My]
                            class Foo
                            {
                                public void Bar()
                                {
                                    return;
                                }
                            }
                        }",
                        nameof(AttributeRetargettingAsync),
                        NullableContextOptions.Disable
                    );

                var root = nonDeclComp.SyntaxTrees.First().GetRoot();

                var foo = root.DescendantNodesAndSelf().OfType<TypeDeclarationSyntax>().Single(s => s.Identifier.ValueText == "Foo");
                var attr = foo.AttributeLists[0].Attributes.Single();

                var bar = foo.Members.OfType<MethodDeclarationSyntax>().Single(s => s.Identifier.ValueText == "Bar");
                var ret = (SyntaxNode)bar.Body.Statements.Single();

                var cache = ImmutableDictionary<SyntaxTree, SemanticModel>.Empty;
                var res = AttributeTracker.RetargetAttributes(nonDeclComp, new[] { (attr, ret, default(AttributeTracker.AttributeTarget?)) }, ref cache);

                Assert.Empty(res);
            }

            static SyntaxList<AttributeListSyntax> GetListContaining(SyntaxNode root, int num)
            {
                var allAttrs = root.DescendantNodesAndSelf().OfType<AttributeSyntax>();

                var withNum = allAttrs.Where(x => x.ToFullString().Contains("(A = " + num + ")")).Single();

                var list = withNum.ParentOrSelfOfType<AttributeListSyntax>();

                return SyntaxFactory.List(new[] { list });
            }
        }
    }
}