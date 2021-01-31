using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cesil.SourceGenerator
{
    internal sealed class Formatter : IEquatable<Formatter?>
    {
        private const string DEFAULT_TYPE_FORMATTERS_RESOURCE_NAME = "Cesil.SourceGenerator.Resources.DefaultTypeFormatters.cs";

        private static readonly ImmutableDictionary<string, Formatter> DefaultLookup = BuildDefaultLookup();

        private static readonly ImmutableHashSet<string> RemoveEnumFormatterMethods =
            ImmutableHashSet.CreateRange(
                new[]
                {
                    "CreateTryEnumFormatter",
                    "CreateTryNullableEnumFormatter",
                    "GetFormattingClass",
                    "CreateTryFlagsEnumFormatter",
                    "CreateTryBasicEnumFormatter",
                    "CreateTryNullableFlagsEnumFormatter",
                    "CreateTryNullableBasicEnumFormatter"
                }
            );
        private static readonly ImmutableHashSet<string> RemoveEnumFormatterFields =
            ImmutableHashSet.CreateRange(
                new[]
                {
                    "TryEnumFormatter",
                    "TryNullableEnumFormatter"
                }
            );
        private static readonly ImmutableHashSet<string> RemoveEnumFormatterMethodsWhenNotFlags =
            ImmutableHashSet.CreateRange(
                new[]
                {
                    "CreateNames",
                    "GetMaxNameLength",
                    "CreateValues",
                    "FormatFlagsEnumImpl"
                }
            );
        private static readonly ImmutableHashSet<string> RemoveEnumFormatterFieldsWhenNotFlags =
            ImmutableHashSet.CreateRange(
                new[]
                {
                    "Names",
                    "Values",
                    "MaxNameLength"
                }
            );
        private static readonly ClassDeclarationSyntax EnumTypeFormatterTemplate = GetEnumTypeFormatterTemplate();

        internal readonly bool IsDefault;
        internal readonly bool DefaultIsMethod;
        internal readonly string? ForDefaultType;
        internal readonly string? DefaultCode;

        internal readonly IMethodSymbol? Method;
        internal readonly ITypeSymbol? TakesType;

        internal Formatter(IMethodSymbol method, ITypeSymbol takesType)
        {
            IsDefault = false;
            DefaultIsMethod = false;
            ForDefaultType = null;
            DefaultCode = null;

            Method = method;
            TakesType = takesType;
        }

        internal Formatter(bool isMethod, string forDefaultType, string defaultCode)
        {
            IsDefault = true;
            DefaultIsMethod = isMethod;
            ForDefaultType = forDefaultType;
            DefaultCode = defaultCode;

            Method = null;
            TakesType = null;
        }

        internal static bool TryGetDefault(SerializerTypes types, ITypeSymbol forType, out Formatter? formatter)
        {
            if (forType.TypeKind == TypeKind.Enum)
            {
                formatter = CreateEnumDefaultFormatter(types.Framework, forType);
                return true;
            }

            if (forType.TypeKind == TypeKind.Struct && forType is INamedTypeSymbol maybeNullableEnum && maybeNullableEnum.Arity == 1)
            {
                var typeDecl = maybeNullableEnum.ConstructedFrom;
                if (typeDecl.Equals(types.BuiltIn.NullableOfT, SymbolEqualityComparer.Default))
                {
                    var arg = maybeNullableEnum.TypeArguments.Single();
                    if (arg.TypeKind == TypeKind.Enum)
                    {
                        formatter = CreateNullableEnumDefaultFormatter(types.Framework, forType);
                        return true;
                    }
                }
            }

            var key = forType.ToFullyQualifiedName();

            return DefaultLookup.TryGetValue(key, out formatter);
        }

        private static Formatter CreateNullableEnumDefaultFormatter(FrameworkTypes types, ITypeSymbol forNullableEnum)
        {
            var forEnum = ((INamedTypeSymbol)forNullableEnum).TypeArguments.Single();
            var nonNullFormatter = CreateEnumDefaultFormatter(types, forEnum);

            var isFlags = Utils.IsFlagsEnum(types, forEnum);
            var forEnumFullyQualified = forEnum.ToFullyQualifiedName();
            var forNullableEnumFullyQualified = forNullableEnum.ToFullyQualifiedName();

            // get the formatter method for the nullable enum
            var nonGeneric = Utils.MakeNonGenericType(EnumTypeFormatterTemplate, "T", forEnumFullyQualified);
            var nullableMtd =
                nonGeneric
                    .Members
                    .OfType<MethodDeclarationSyntax>()
                    .Single(m => isFlags ? m.Identifier.ValueText == "TryFormatNullableFlagsEnum" : m.Identifier.ValueText == "TryFormatNullableBasicEnum");

            // get the formatter method for the NON-NULLABLE enum
            var nonNullFormatterParsed =
                    Utils.NonNull(SyntaxFactory.ParseMemberDeclaration(Utils.NonNull(nonNullFormatter.DefaultCode)));
            var nonNullFormatterMtd = nonNullFormatterParsed.DescendantNodesAndSelf().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == "__TryFormat");
            var nonNullMtdBody = Utils.NonNull(nonNullFormatterMtd.Body);

            // rewrite things to produce final code
            var replaceRet = nullableMtd.DescendantNodesAndSelf().OfType<ReturnStatementSyntax>().Last();
            var toReplace = ImmutableDictionary.CreateBuilder<ReturnStatementSyntax, (ParameterListSyntax Parameters, BlockSyntax Statements)>();
            toReplace.Add(replaceRet, (nonNullFormatterMtd.ParameterList, nonNullMtdBody));

            bool isMethod;
            SyntaxNode nullableFinal;
            if (nonNullFormatter.DefaultIsMethod)
            {
                // just a method, simple inlining is sufficient
                isMethod = true;

                nullableFinal = Utils.ReplaceIn(nullableMtd, toReplace.ToImmutable());
            }
            else
            {
                // whole class, so we need to inline and remove the old method
                isMethod = false;

                var nonNullClass = (ClassDeclarationSyntax)nonNullFormatterParsed;
                var nonNullClassWithoutTryFormat = Utils.NonNull(nonNullClass.RemoveNode(nonNullFormatterMtd, SyntaxRemoveOptions.KeepNoTrivia));

                var newNullableMethod =
                    Utils.ReplaceIn(nullableMtd, toReplace.ToImmutable())
                        .WithIdentifier(SyntaxFactory.ParseToken("__TryFormat"))
                        .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.InternalKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)));

                nullableFinal = nonNullClassWithoutTryFormat.AddMembers(newNullableMethod);
            }

            nullableFinal = nullableFinal.NormalizeWhitespace();

            var code = nullableFinal.ToFullString();

            return new Formatter(isMethod, forNullableEnumFullyQualified, code);
        }

        private static Formatter CreateEnumDefaultFormatter(FrameworkTypes types, ITypeSymbol forEnum)
        {
            var isFlags = Utils.IsFlagsEnum(types, forEnum);
            var forEnumFullyQualified = forEnum.ToFullyQualifiedName();

            // strip out type parameters
            var nonGeneric = Utils.MakeNonGenericType(EnumTypeFormatterTemplate, "T", forEnumFullyQualified);

            // remove all the stuff we _don't_ need
            var toRemoveMtd =
                nonGeneric
                    .DescendantNodesAndSelf()
                    .OfType<MethodDeclarationSyntax>()
                    .Where(
                        t =>
                       {
                           var val = t.Identifier.ValueText;

                           if (RemoveEnumFormatterMethods.Contains(val))
                           {
                               return true;
                           }

                           if (!isFlags && RemoveEnumFormatterMethodsWhenNotFlags.Contains(val))
                           {
                               return true;
                           }

                           return false;
                       }
                    )
                    .ToList();


            var toRemoveFields =
                nonGeneric
                    .DescendantNodesAndSelf()
                    .OfType<FieldDeclarationSyntax>()
                    .Where(
                        t =>
                            t.Declaration.Variables.Any(
                                x =>
                                {
                                    var val = x.Identifier.ValueText;
                                    if (RemoveEnumFormatterFields.Contains(val))
                                    {
                                        return true;
                                    }

                                    if (!isFlags && RemoveEnumFormatterFieldsWhenNotFlags.Contains(val))
                                    {
                                        return true;
                                    }

                                    return false;
                                }
                            )
                    )
                    .ToList();

            var toRemove = toRemoveMtd.Cast<SyntaxNode>().Concat(toRemoveFields);
            var trimmed = Utils.NonNull(nonGeneric.RemoveNodes(toRemove, SyntaxRemoveOptions.KeepNoTrivia));

            // now grab the right method
            var mtd =
                trimmed
                    .DescendantNodesAndSelf()
                    .OfType<MethodDeclarationSyntax>()
                    .Single(m => isFlags ? m.Identifier.ValueText == "TryFormatFlagsEnum" : m.Identifier.ValueText == "TryFormatBasicEnum");

            // inline anything that needs inlining
            var updatedMtd = Utils.InlineTailCalls(mtd, trimmed);

            // construct final class
            var publicFormatMethod =
                updatedMtd
                    .WithIdentifier(SyntaxFactory.ParseToken("__TryFormat"))
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.InternalKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)));

            var retClass = trimmed.ReplaceNode(mtd, publicFormatMethod);
            retClass = retClass
                .WithIdentifier(SyntaxFactory.ParseToken("__Class"))
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.InternalKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)));
            retClass = Utils.NonNull(retClass.RemoveNodes(retClass.Members.OfType<MethodDeclarationSyntax>().Where(m => m.Identifier.ValueText.StartsWith("Try")), SyntaxRemoveOptions.KeepNoTrivia));

            // replace calls to Utils.EnumToULong
            var enumToULongCalls =
                retClass
                    .DescendantNodesAndSelf()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(i => i.Expression is MemberAccessExpressionSyntax)
                    .Where(i => ((MemberAccessExpressionSyntax)i.Expression).Name.Identifier.ValueText == "EnumToULong")
                    .ToImmutableArray();

            var ulongType = SyntaxFactory.ParseTypeName("ulong");
            retClass =
                retClass.ReplaceNodes(
                    enumToULongCalls,
                    (_, old) =>
                    {
                        var i = old;
                        var ps = i.ArgumentList.Arguments;
                        var cast = SyntaxFactory.CastExpression(ulongType, ps[0].Expression);

                        return cast;
                    }
                );

            // replace calls to Utils.NonNull
            var nonNullCalls =
                retClass
                    .DescendantNodesAndSelf()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(i => i.Expression is MemberAccessExpressionSyntax)
                    .Where(i => ((MemberAccessExpressionSyntax)i.Expression).Name.Identifier.ValueText == "NonNull")
                    .ToImmutableArray();

            retClass =
                retClass.ReplaceNodes(
                    nonNullCalls,
                    (_, old) =>
                    {
                        var i = old;
                        var ps = i.ArgumentList.Arguments;
                        var val = ps[0].Expression;

                        var eq = SyntaxFactory.BinaryExpression(SyntaxKind.NotEqualsExpression, val, SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression));
                        var throwExp = SyntaxFactory.ParseExpression("throw new InvalidOperationException(\"Expected non-null value, but found null\")");

                        var cond = SyntaxFactory.ConditionalExpression(eq, val, throwExp);
                        var parened = SyntaxFactory.ParenthesizedExpression(cond);

                        return parened;
                    }
                );

            // replace IsFlagsEnum
            var isFlagsEnumCalls =
                retClass
                    .DescendantNodesAndSelf()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(i => i.Expression is MemberAccessExpressionSyntax)
                    .Where(i => ((MemberAccessExpressionSyntax)i.Expression).Name.Identifier.ValueText == "IsFlagsEnum")
                    .ToImmutableArray();

            var isFlagsEnumReplacement = SyntaxFactory.LiteralExpression(isFlags ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression);

            retClass =
                retClass.ReplaceNodes(
                    isFlagsEnumCalls,
                    (_, __) => isFlagsEnumReplacement
                );

            // make FormatFlagsEnumImpl private
            var formatFlagsEnumImpl =
                retClass
                    .Members
                    .OfType<MethodDeclarationSyntax>()
                    .SingleOrDefault(s => s.Identifier.ValueText == "FormatFlagsEnumImpl");

            if (formatFlagsEnumImpl != null)
            {
                var privateFormatFlagsEnumImpl =
                    formatFlagsEnumImpl.WithModifiers(
                        SyntaxFactory.TokenList(
                            SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                            SyntaxFactory.Token(SyntaxKind.StaticKeyword)
                        )
                    );

                retClass =
                    retClass.ReplaceNode(
                        formatFlagsEnumImpl,
                        privateFormatFlagsEnumImpl
                    );
            }

            // get the string we're using
            retClass = retClass.NormalizeWhitespace();

            bool isMethod;
            string code;
            if (retClass.Members.Count == 1)
            {
                isMethod = true;
                code = retClass.Members.Single().ToFullString();
            }
            else
            {
                // only include the full class if we're actually using fields or other members
                isMethod = false;
                code = retClass.ToFullString();
            }

            return new Formatter(isMethod, forEnumFullyQualified, code);
        }

        private static ClassDeclarationSyntax GetEnumTypeFormatterTemplate()
        {
            var defaultTypeFormattersCS = Utils.GetResourceText(DEFAULT_TYPE_FORMATTERS_RESOURCE_NAME);
            var parsedDefaultTypeFormatters = SyntaxFactory.ParseCompilationUnit(defaultTypeFormattersCS);
            return parsedDefaultTypeFormatters.DescendantNodesAndSelf().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == "DefaultEnumTypeFormatter");
        }

        private static ImmutableDictionary<string, Formatter> BuildDefaultLookup()
        {
            var defaultTypeFormattersCS = Utils.GetResourceText(DEFAULT_TYPE_FORMATTERS_RESOURCE_NAME);
            var parsedDefaultTypeFormatters = SyntaxFactory.ParseCompilationUnit(defaultTypeFormattersCS);
            var defaultTypeFormatters = parsedDefaultTypeFormatters.DescendantNodesAndSelf().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == "DefaultTypeFormatters");

            var builder = ImmutableDictionary.CreateBuilder<string, Formatter>();

            // primitives
            Add(builder, "System.Boolean", true, false,defaultTypeFormatters, "TryFormatBool");
            Add(builder, "System.Byte", true, false, defaultTypeFormatters, "TryFormatByte");
            Add(builder, "System.SByte", true, false, defaultTypeFormatters, "TryFormatSByte");
            Add(builder, "System.Char", true, false, defaultTypeFormatters, "TryFormatChar");
            Add(builder, "System.Int16", true, false, defaultTypeFormatters, "TryFormatShort");
            Add(builder, "System.UInt16", true, false, defaultTypeFormatters, "TryFormatUShort");
            Add(builder, "System.Int32", true, false, defaultTypeFormatters, "TryFormatInt");
            Add(builder, "System.UInt32", true, false, defaultTypeFormatters, "TryFormatUInt");
            Add(builder, "System.Int64", true, false, defaultTypeFormatters, "TryFormatLong");
            Add(builder, "System.UInt64", true, false, defaultTypeFormatters, "TryFormatULong");
            Add(builder, "System.IntPtr", true, false, defaultTypeFormatters, "TryFormatNInt");
            Add(builder, "System.UIntPtr", true, false, defaultTypeFormatters, "TryFormatNUInt");
            Add(builder, "System.Single", true, false, defaultTypeFormatters, "TryFormatFloat");
            Add(builder, "System.Double", true, false, defaultTypeFormatters, "TryFormatDouble");
            Add(builder, "System.Decimal", true, false, defaultTypeFormatters, "TryFormatDecimal");

            // built in structs
            Add(builder, "System.Index", true, false, defaultTypeFormatters, "TryFormatIndex");
            Add(builder, "System.Range", true, false, defaultTypeFormatters, "TryFormatRange");
            Add(builder, "System.Guid", true, false, defaultTypeFormatters, "TryFormatGuid");
            Add(builder, "System.DateTime", true, false, defaultTypeFormatters, "TryFormatDateTime");
            Add(builder, "System.DateTimeOffset", true, false, defaultTypeFormatters, "TryFormatDateTimeOffset");
            Add(builder, "System.TimeSpan", true, false, defaultTypeFormatters, "TryFormatTimeSpan");

            // reference types
            Add(builder, "System.String", false, false, defaultTypeFormatters, "TryFormatString");
            Add(builder, "System.Uri", false, false, defaultTypeFormatters, "TryFormatUri");
            Add(builder, "System.Version", false, false, defaultTypeFormatters, "TryFormatVersion");

            // nullable primitives
            Add(builder, "System.Boolean", true, true, defaultTypeFormatters, "TryFormatNullableBool");
            Add(builder, "System.Byte", true, true, defaultTypeFormatters, "TryFormatNullableByte");
            Add(builder, "System.SByte", true, true, defaultTypeFormatters, "TryFormatNullableSByte");
            Add(builder, "System.Char", true, true, defaultTypeFormatters, "TryFormatNullableChar");
            Add(builder, "System.Int16", true, true, defaultTypeFormatters, "TryFormatNullableShort");
            Add(builder, "System.UInt16", true, true, defaultTypeFormatters, "TryFormatNullableUShort");
            Add(builder, "System.Int32", true, true, defaultTypeFormatters, "TryFormatNullableInt");
            Add(builder, "System.UInt32", true, true, defaultTypeFormatters, "TryFormatNullableUInt");
            Add(builder, "System.Int64", true, true, defaultTypeFormatters, "TryFormatNullableLong");
            Add(builder, "System.UInt64", true, true, defaultTypeFormatters, "TryFormatNullableULong");
            Add(builder, "System.IntPtr", true, true, defaultTypeFormatters, "TryFormatNullableNInt");
            Add(builder, "System.UIntPtr", true, true, defaultTypeFormatters, "TryFormatNullableNUInt");
            Add(builder, "System.Single", true, true, defaultTypeFormatters, "TryFormatNullableFloat");
            Add(builder, "System.Double", true, true, defaultTypeFormatters, "TryFormatNullableDouble");
            Add(builder, "System.Decimal", true, true, defaultTypeFormatters, "TryFormatNullableDecimal");

            // nullable built in structs
            Add(builder, "System.Index", true, true, defaultTypeFormatters, "TryFormatNullableIndex");
            Add(builder, "System.Range", true, true, defaultTypeFormatters, "TryFormatNullableRange");
            Add(builder, "System.Guid", true, true, defaultTypeFormatters, "TryFormatNullableGuid");
            Add(builder, "System.DateTime", true, true, defaultTypeFormatters, "TryFormatNullableDateTime");
            Add(builder, "System.DateTimeOffset", true, true, defaultTypeFormatters, "TryFormatNullableDateTimeOffset");
            Add(builder, "System.TimeSpan", true, true, defaultTypeFormatters, "TryFormatNullableTimeSpan");

            return builder.ToImmutable();

            static void Add(
                ImmutableDictionary<string, Formatter>.Builder builder, 
                string fullyQualifiedTypeName,
                bool valueType,
                bool nullable,
                ClassDeclarationSyntax defaultTypeFormattersSyntax, 
                string methodName
            )
            {
                var methodBody = Utils.ExtractMethodBody(defaultTypeFormattersSyntax, methodName);

                string generateTypeName;
                string typeName;
                if (nullable)
                {
                    typeName = fullyQualifiedTypeName + "?";
                    generateTypeName = typeName;
                }
                else
                {
                    typeName = fullyQualifiedTypeName;
                    generateTypeName = typeName;
                    if (!valueType)
                    {
                        generateTypeName += "?";
                    }
                }

                builder.Add(typeName, new Formatter(true, generateTypeName, methodBody));
            }
        }

        public bool Equals(Formatter? other)
        {
            if (other == null)
            {
                return false;
            }

            if(Method != null && TakesType != null)
            {
                return
                    Method.Equals(other.Method, SymbolEqualityComparer.Default) &&
                    TakesType.Equals(other.TakesType, SymbolEqualityComparer.Default);
            }

            return
                other.DefaultCode == DefaultCode &&
                other.DefaultIsMethod == DefaultIsMethod &&
                other.ForDefaultType == ForDefaultType &&
                other.IsDefault == IsDefault;
        }

        public override bool Equals(object obj)
        => Equals(obj as Formatter);

        public override int GetHashCode()
        => Utils.HashCode(Method, TakesType, DefaultCode, DefaultIsMethod, ForDefaultType, IsDefault);
    }
}
