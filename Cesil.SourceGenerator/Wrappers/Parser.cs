using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cesil.SourceGenerator
{
    internal sealed class Parser : IEquatable<Parser?>
    {
        private const string DEFAULT_TYPE_PARSERS_RESOURCE_NAME = "Cesil.SourceGenerator.Resources.DefaultTypeParsers.cs";

        private static readonly ImmutableDictionary<string, Parser> DefaultLookup = BuildDefaultLookup();

        private static readonly ImmutableHashSet<string> RemoveEnumParserMethods =
            ImmutableHashSet.CreateRange(
                new[]
                {
                    "CreateTryParseEnumParser",
                    "CreateTryParseNullableEnumParser",
                    "GetParsingClass",
                    "CreateTryParseFlagsEnumParser",
                    "CreateTryParseBasicEnumParser",
                    "CreateTryParseNullableFlagsEnumParser",
                    "CreateTryParseNullableBasicEnumParser"
                }
            );
        private static readonly ImmutableHashSet<string> RemoveEnumParserFields =
            ImmutableHashSet.CreateRange(
                new[]
                {
                    "TryParseEnumParser",
                    "TryParseNullableEnumParser"
                }
            );
        private static readonly ImmutableHashSet<string> RemoveEnumParserMethodsWhenNotFlags =
            ImmutableHashSet.CreateRange(
                new[]
                {
                    "CreateULongValues",
                    "TryParseFlagsEnum",
                    "TryParseNullableFlagsEnum",
                    "TryParseFlagsEnum"
                }
            );
        private static readonly ImmutableHashSet<string> RemoveEnumParserFieldsWhenNotFlags =
            ImmutableHashSet.CreateRange(
                new[]
                {
                    "ULongValues"
                }
            );
        private static readonly ClassDeclarationSyntax EnumTypeParserTemplate = GetEnumTypeParserTemplate();

        internal readonly bool IsDefault;
        internal readonly bool DefaultIsMethod;
        internal readonly string? ForDefaultType;
        internal readonly string? DefaultCode;

        internal readonly IMethodSymbol? Method;
        internal readonly ITypeSymbol? CreatesType;

        internal Parser(IMethodSymbol method, ITypeSymbol createsType)
        {
            IsDefault = false;

            Method = method;
            CreatesType = createsType;
        }

        internal Parser(bool isMethod, string forType, string defaultCode)
        {
            IsDefault = true;

            DefaultIsMethod = isMethod;
            ForDefaultType = forType;
            DefaultCode = defaultCode;
        }

        internal static bool TryGetDefault(DeserializerTypes types, ITypeSymbol forType, out Parser? parser)
        {
            if (forType.TypeKind == TypeKind.Enum)
            {
                return TryCreateDefaultEnumParser(types.Framework, forType, out parser);
            }

            if (forType.TypeKind == TypeKind.Struct && forType is INamedTypeSymbol maybeNullableEnum && maybeNullableEnum.Arity == 1)
            {
                var typeDecl = maybeNullableEnum.ConstructedFrom;
                if (typeDecl.Equals(types.BuiltIn.NullableOfT, SymbolEqualityComparer.Default))
                {
                    var arg = maybeNullableEnum.TypeArguments.Single();
                    if (arg.TypeKind == TypeKind.Enum)
                    {
                        return TryCreateNullableEnumParser(types.Framework, arg, out parser);
                    }
                }
            }

            var key = forType.ToFullyQualifiedName();

            return DefaultLookup.TryGetValue(key, out parser);
        }

        private static bool TryCreateDefaultEnumParser(FrameworkTypes types, ITypeSymbol forEnum, out Parser? parser)
        => TryCreateEnumParser(types, false, forEnum, out parser);

        private static bool TryCreateNullableEnumParser(FrameworkTypes types, ITypeSymbol forEnum, out Parser? parser)
        => TryCreateEnumParser(types, true, forEnum, out parser);

        private static bool TryCreateEnumParser(FrameworkTypes types, bool nullable, ITypeSymbol forEnum, out Parser? parser)
        {
            var isFlags = Utils.IsFlagsEnum(types, forEnum);
            var forEnumFullyQualified = forEnum.ToFullyQualifiedName();

            // strip out type parameters
            var nonGeneric = Utils.MakeNonGenericType(EnumTypeParserTemplate, "T", forEnumFullyQualified);

            // remove all the stuff we _don't_ need
            var toRemoveMtd =
                nonGeneric
                    .DescendantNodesAndSelf()
                    .OfType<MethodDeclarationSyntax>()
                    .Where(
                        t =>
                        {
                            var val = t.Identifier.ValueText;

                            if (RemoveEnumParserMethods.Contains(val))
                            {
                                return true;
                            }

                            if (!isFlags && RemoveEnumParserMethodsWhenNotFlags.Contains(val))
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
                                    if (RemoveEnumParserFields.Contains(val))
                                    {
                                        return true;
                                    }

                                    if (!isFlags && RemoveEnumParserFieldsWhenNotFlags.Contains(val))
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
            string expectedMethodName;
            if (isFlags)
            {
                if (nullable)
                {
                    expectedMethodName = "TryParseNullableFlagsEnum";
                }
                else
                {
                    expectedMethodName = "TryParseFlagsEnum";
                }
            }
            else
            {
                if (nullable)
                {
                    expectedMethodName = "TryParseNullableEnum";
                }
                else
                {
                    expectedMethodName = "TryParseEnum";
                }
            }

            var mtd =
                trimmed
                    .DescendantNodesAndSelf()
                    .OfType<MethodDeclarationSyntax>()
                    .Single(m => m.Identifier.ValueText == expectedMethodName);

            // construct final class
            var publicFormatMethod =
                mtd
                    .WithIdentifier(SyntaxFactory.ParseToken("__TryParse"))
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.InternalKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)));

            var retClass = trimmed.ReplaceNode(mtd, publicFormatMethod);
            retClass = retClass
                .WithIdentifier(SyntaxFactory.ParseToken("__Class"))
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.InternalKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)));

            if (!nullable)
            {
                // remove the other TryXXX method, but only if we're not going to call it
                //   have to do this because we rename the method it'd call
                var extraMethods = retClass.DescendantNodesAndSelf().OfType<MethodDeclarationSyntax>().Where(m => m.Identifier.ValueText.StartsWith("Try"));
                retClass = Utils.NonNull(retClass.RemoveNodes(extraMethods, SyntaxRemoveOptions.KeepNoTrivia));
            }

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

            // replace Utils.ULongToEnum calls with direct casts (since we've removed all the generic Ts, this will compile now)
            var ulongToEnumCalls =
                retClass
                    .DescendantNodesAndSelf()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(i => i.Expression is MemberAccessExpressionSyntax)
                    .Where(i => ((MemberAccessExpressionSyntax)i.Expression).Name.Identifier.ValueText == "ULongToEnum")
                    .ToImmutableArray();

            var enumTypeName = SyntaxFactory.ParseTypeName(forEnumFullyQualified);
            retClass =
                retClass.ReplaceNodes(
                    ulongToEnumCalls,
                    (_, old) =>
                    {
                        var i = old;
                        var ps = i.ArgumentList.Arguments;
                        var cast = SyntaxFactory.CastExpression(enumTypeName, ps[0].Expression);

                        return cast;
                    }
                );

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

            // make TryParseFlagsEnum private
            var tryParseFlagsEnumMtd = retClass.DescendantNodesAndSelf().OfType<MethodDeclarationSyntax>().SingleOrDefault(s => s.Identifier.ValueText == "TryParseFlagsEnum");

            if (tryParseFlagsEnumMtd != null)
            {
                var privateTryParseFlagsEnumMtd =
                       tryParseFlagsEnumMtd.WithModifiers(
                           SyntaxFactory.TokenList(
                               SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                               SyntaxFactory.Token(SyntaxKind.StaticKeyword)
                           )
                       );

                retClass =
                    retClass.ReplaceNode(
                        tryParseFlagsEnumMtd,
                        privateTryParseFlagsEnumMtd
                    );
            }

            // get the string we're using
            retClass = retClass.NormalizeWhitespace();
            var code = retClass.ToFullString();

            var forType = nullable ? forEnumFullyQualified + "?" : forEnumFullyQualified;

            parser = new Parser(false, forType, code);
            return true;
        }

        private static ClassDeclarationSyntax GetEnumTypeParserTemplate()
        {
            var defaultTypeParsersCS = Utils.GetResourceText(DEFAULT_TYPE_PARSERS_RESOURCE_NAME);
            var parsedDefaultTypeParsers = SyntaxFactory.ParseCompilationUnit(defaultTypeParsersCS);
            return parsedDefaultTypeParsers.DescendantNodesAndSelf().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == "DefaultEnumTypeParser");
        }

        private static ImmutableDictionary<string, Parser> BuildDefaultLookup()
        {
            var defaultTypeParsersCS = Utils.GetResourceText(DEFAULT_TYPE_PARSERS_RESOURCE_NAME);
            var parsedDefaultTypeParsers = SyntaxFactory.ParseCompilationUnit(defaultTypeParsersCS);
            var defaultTypeParsers = parsedDefaultTypeParsers.DescendantNodesAndSelf().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == "DefaultTypeParsers");

            var builder = ImmutableDictionary.CreateBuilder<string, Parser>();

            // primitives
            Add(builder, "System.Boolean", true, false, defaultTypeParsers, "TryParseBool");
            Add(builder, "System.Byte", true, false, defaultTypeParsers, "TryParseByte");
            Add(builder, "System.SByte", true, false, defaultTypeParsers, "TryParseSByte");
            Add(builder, "System.Char", true, false, defaultTypeParsers, "TryParseChar");
            Add(builder, "System.Int16", true, false, defaultTypeParsers, "TryParseShort");
            Add(builder, "System.UInt16", true, false, defaultTypeParsers, "TryParseUShort");
            Add(builder, "System.Int32", true, false, defaultTypeParsers, "TryParseInt");
            Add(builder, "System.UInt32", true, false, defaultTypeParsers, "TryParseUInt");
            Add(builder, "System.Int64", true, false, defaultTypeParsers, "TryParseLong");
            Add(builder, "System.UInt64", true, false, defaultTypeParsers, "TryParseULong");
            Add(builder, "System.Single", true, false, defaultTypeParsers, "TryParseFloat");
            Add(builder, "System.Double", true, false, defaultTypeParsers, "TryParseDouble");
            Add(builder, "System.Decimal", true, false, defaultTypeParsers, "TryParseDecimal");

            // built in structs
            Add(builder, "System.Index", true, false, defaultTypeParsers, "TryParseIndex");
            Add(builder, "System.Range", true, false, defaultTypeParsers, "TryParseRange");
            Add(builder, "System.Guid", true, false, defaultTypeParsers, "TryParseGuid");
            Add(builder, "System.DateTime", true, false, defaultTypeParsers, "TryParseDateTime");
            Add(builder, "System.DateTimeOffset", true, false, defaultTypeParsers, "TryParseDateTimeOffset");
            Add(builder, "System.TimeSpan", true, false, defaultTypeParsers, "TryParseTimeSpan");

            // reference types
            Add(builder, "System.String", false, false, defaultTypeParsers, "TryParseString");
            Add(builder, "System.Uri", false, false, defaultTypeParsers, "TryParseUri");
            Add(builder, "System.Version", false, false, defaultTypeParsers, "TryParseVersion");

            // nullable primitives
            Add(builder, "System.Boolean", true, true, defaultTypeParsers, "TryParseNullableBool");
            Add(builder, "System.Byte", true, true, defaultTypeParsers, "TryParseNullableByte");
            Add(builder, "System.SByte", true, true, defaultTypeParsers, "TryParseNullableSByte");
            Add(builder, "System.Char", true, true, defaultTypeParsers, "TryParseNullableChar");
            Add(builder, "System.Int16", true, true, defaultTypeParsers, "TryParseNullableShort");
            Add(builder, "System.UInt16", true, true, defaultTypeParsers, "TryParseNullableUShort");
            Add(builder, "System.Int32", true, true, defaultTypeParsers, "TryParseNullableInt");
            Add(builder, "System.UInt32", true, true, defaultTypeParsers, "TryParseNullableUInt");
            Add(builder, "System.Int64", true, true, defaultTypeParsers, "TryParseNullableLong");
            Add(builder, "System.UInt64", true, true, defaultTypeParsers, "TryParseNullableULong");
            Add(builder, "System.Single", true, true, defaultTypeParsers, "TryParseNullableFloat");
            Add(builder, "System.Double", true, true, defaultTypeParsers, "TryParseNullableDouble");
            Add(builder, "System.Decimal", true, true, defaultTypeParsers, "TryParseNullableDecimal");

            // nullable built in structs
            Add(builder, "System.Index", true, true, defaultTypeParsers, "TryParseNullableIndex");
            Add(builder, "System.Range", true, true, defaultTypeParsers, "TryParseNullableRange");
            Add(builder, "System.Guid", true, true, defaultTypeParsers, "TryParseNullableGuid");
            Add(builder, "System.DateTime", true, true, defaultTypeParsers, "TryParseNullableDateTime");
            Add(builder, "System.DateTimeOffset", true, true, defaultTypeParsers, "TryParseNullableDateTimeOffset");
            Add(builder, "System.TimeSpan", true, true, defaultTypeParsers, "TryParseNullableTimeSpan");

            return builder.ToImmutable();

            static void Add(
                ImmutableDictionary<string, Parser>.Builder builder,
                //System.Reflection.TypeInfo type,
                string fullyQualifiedTypeName,
                bool valueType,
                bool nullable,
                ClassDeclarationSyntax defaultTypeParsersSyntax,
                string methodName
            )
            {
                var methodBody = Utils.ExtractMethodBody(defaultTypeParsersSyntax, methodName);

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

                builder.Add(typeName, new Parser(true, generateTypeName, methodBody));
            }
        }

        public bool Equals(Parser? other)
        {
            if (other == null)
            {
                return false;
            }

            if (CreatesType != null)
            {
                if (other.CreatesType == null)
                {
                    return false;
                }

                if (!other.CreatesType.Equals(CreatesType, SymbolEqualityComparer.IncludeNullability))
                {
                    return false;
                }
            }
            else
            {
                if (other.CreatesType != null)
                {
                    return false;
                }
            }

            if (Method != null)
            {
                if (other.Method == null)
                {
                    return false;
                }

                if (!other.Method.Equals(Method, SymbolEqualityComparer.IncludeNullability))
                {
                    return false;
                }
            }
            else
            {
                if (other.Method != null)
                {
                    return false;
                }
            }

            return
                other.DefaultCode == DefaultCode &&
                other.DefaultIsMethod == DefaultIsMethod &&
                other.ForDefaultType == ForDefaultType &&
                other.IsDefault == IsDefault;
        }

        public override bool Equals(object obj)
        => Equals(obj as Parser);

        public override int GetHashCode()
        => Utils.HashCode(CreatesType, Method, DefaultCode, DefaultIsMethod, ForDefaultType, IsDefault);
    }
}
