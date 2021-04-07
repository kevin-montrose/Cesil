using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace Cesil.SourceGenerator
{
    internal sealed class DeserializableMember
    {
        internal readonly string Name;

        internal readonly Setter Setter;
        internal readonly Parser Parser;

        internal readonly Reset? Reset;

        internal readonly bool IsRequired;

        internal readonly int? Order;

        private DeserializableMember(string name, Setter setter, Parser parser, Reset? reset, bool isRequired, int? order)
        {
            Name = name;
            Setter = setter;
            Parser = parser;
            Reset = reset;
            IsRequired = isRequired;
            Order = order;
        }

        internal static (DeserializableMember? Member, ImmutableArray<Diagnostic> Diagnostics) ForMethod(
            Compilation compilation,
            AttributedMembers attrMembers,
            DeserializerTypes types,
            INamedTypeSymbol deserializingType,
            IMethodSymbol mtd,
            ImmutableArray<AttributeSyntax> attrs
        )
        {
            var diags = ImmutableArray<Diagnostic>.Empty;

            var mtdLoc = mtd.Locations.FirstOrDefault();

            var attrName = Utils.GetNameFromAttributes(attrMembers, mtdLoc, attrs, ref diags);
            if (attrName == null)
            {
                var diag = Diagnostics.DeserializableMemberMustHaveNameSetForMethod(mtdLoc, mtd);
                diags = diags.Add(diag);

                attrName = "--UNKNOWN--";
            }

            var setter = GetSetterForMethod(compilation, types, deserializingType, mtd, mtdLoc, ref diags);

            int? order = Utils.GetOrderFromAttributes(attrMembers, mtdLoc, types.Framework, types.OurTypes.DeserializerMemberAttribute, attrs, ref diags);

            var isRequired = false;
            var attrIsRequiredValue = GetMemberRequiredFromAttributes(attrMembers, mtdLoc, attrs, ref diags);
            isRequired = attrIsRequiredValue ?? isRequired;

            var reset = GetReset(compilation, attrMembers, types, deserializingType, mtdLoc, attrs, ref diags);

            // after this point, we need to know what we're working with
            if (setter == null)
            {
                return (null, diags);
            }

            var parser = GetParser(compilation, attrMembers, types, setter.ValueType, mtdLoc, attrs, ref diags);

            return MakeMember(mtdLoc, types, attrName, setter, parser, reset, isRequired, order, diags);
        }

        internal static (DeserializableMember? Member, ImmutableArray<Diagnostic> Diagnostics) ForProperty(
            Compilation compilation,
            AttributedMembers attrMembers,
            DeserializerTypes types,
            INamedTypeSymbol deserializingType,
            IPropertySymbol prop,
            ImmutableArray<AttributeSyntax> attrs
        )
        {
            var diags = ImmutableArray<Diagnostic>.Empty;

            var propLoc = prop.Locations.FirstOrDefault();

            if (prop.SetMethod == null)
            {
                var diag = Diagnostics.NoSetterOnDeserializableProperty(propLoc);
                diags = diags.Add(diag);
            }

            if (prop.Parameters.Any())
            {
                var diag = Diagnostics.DeserializablePropertyCannotHaveParameters(propLoc);
                diags = diags.Add(diag);
            }

            var attrName = Utils.GetNameFromAttributes(attrMembers, propLoc, attrs, ref diags);
            var name = attrName ?? prop.Name;
            var setter = new Setter(prop);

            int? order = Utils.GetOrderFromAttributes(attrMembers, propLoc, types.Framework, types.OurTypes.DeserializerMemberAttribute, attrs, ref diags);

            var isRequired = false;
            var attrIsRequiredValue = GetMemberRequiredFromAttributes(attrMembers, propLoc, attrs, ref diags);
            isRequired = attrIsRequiredValue ?? isRequired;

            var reset = GetReset(compilation, attrMembers, types, deserializingType, propLoc, attrs, ref diags);

            var parser = GetParser(compilation, attrMembers, types, setter.ValueType, propLoc, attrs, ref diags);

            // only do this for properties
            reset ??= InferDefaultReset(attrMembers, types, deserializingType, prop.Name, attrs, propLoc, ref diags);

            return MakeMember(propLoc, types, name, setter, parser, reset, isRequired, order, diags);
        }

        internal static (DeserializableMember? Member, ImmutableArray<Diagnostic> Diagnostics) ForField(
            Compilation compilation,
            AttributedMembers attrMembers,
            DeserializerTypes types,
            INamedTypeSymbol deserializingType,
            IFieldSymbol field,
            ImmutableArray<AttributeSyntax> attrs
        )
        {
            var diags = ImmutableArray<Diagnostic>.Empty;

            var fieldLoc = field.Locations.FirstOrDefault();

            var attrName = Utils.GetNameFromAttributes(attrMembers, fieldLoc, attrs, ref diags);
            var name = attrName ?? field.Name;
            var setter = new Setter(field);

            int? order = Utils.GetOrderFromAttributes(attrMembers, fieldLoc, types.Framework, types.OurTypes.DeserializerMemberAttribute, attrs, ref diags);

            var isRequired = false;
            var attrIsRequiredValue = GetMemberRequiredFromAttributes(attrMembers, fieldLoc, attrs, ref diags);
            isRequired = attrIsRequiredValue ?? isRequired;

            var reset = GetReset(compilation, attrMembers, types, deserializingType, fieldLoc, attrs, ref diags);

            var parser = GetParser(compilation, attrMembers, types, setter.ValueType, fieldLoc, attrs, ref diags);

            return MakeMember(fieldLoc, types, name, setter, parser, reset, isRequired, order, diags);
        }

        internal static (DeserializableMember? Member, ImmutableArray<Diagnostic> Diagnostics) ForConstructorParameter(
            Compilation compilation,
            AttributedMembers attrMembers,
            DeserializerTypes types,
            INamedTypeSymbol deserializingType,
            IParameterSymbol parameter,
            ImmutableArray<AttributeSyntax> attrs
        )
        {
            var diags = ImmutableArray<Diagnostic>.Empty;

            var parameterLoc = parameter.Locations.FirstOrDefault();

            var attrName = Utils.GetNameFromAttributes(attrMembers, parameterLoc, attrs, ref diags);
            var name = attrName ?? parameter.Name;
            var setter = new Setter(parameter);

            int? order = Utils.GetOrderFromAttributes(attrMembers, parameterLoc, types.Framework, types.OurTypes.DeserializerMemberAttribute, attrs, ref diags);

            // note that this defaults to TRUE
            var isRequired = true;
            var attrIsRequiredValue = GetMemberRequiredFromAttributes(attrMembers, parameterLoc, attrs, ref diags);
            isRequired = attrIsRequiredValue ?? isRequired;

            if (!isRequired)
            {
                var diag = Diagnostics.ParametersMustBeRequired(parameterLoc, deserializingType, parameter);

                diags = diags.Add(diag);

                return (null, diags);
            }

            var reset = GetReset(compilation, attrMembers, types, deserializingType, parameterLoc, attrs, ref diags);
            if (reset != null && !reset.IsStatic)
            {
                var diag = Diagnostics.BadReset_MustBeStaticForParameters(parameterLoc, deserializingType, parameter, reset.Method);
                diags = diags.Add(diag);
                return (null, diags);
            }

            var parser = GetParser(compilation, attrMembers, types, setter.ValueType, parameterLoc, attrs, ref diags);

            return MakeMember(parameterLoc, types, name, setter, parser, reset, isRequired, order, diags);
        }

        private static (DeserializableMember? Member, ImmutableArray<Diagnostic> Diagnostics) MakeMember(
            Location? location,
            DeserializerTypes types,
            string name,
            Setter setter,
            Parser? parser,
            Reset? reset,
            bool isRequired,
            int? order,
            ImmutableArray<Diagnostic> diags
        )
        {
            if (diags.IsEmpty)
            {
                if (parser == null && !Parser.TryGetDefault(types, setter.ValueType, out parser))
                {
                    var diag = Diagnostics.NoBuiltInParser(location, setter.ValueType);
                    diags = diags.Add(diag);
                    return (null, diags);
                }

                parser = Utils.NonNull(parser);

                return (new DeserializableMember(name, setter, parser, reset, isRequired, order), ImmutableArray<Diagnostic>.Empty);
            }

            return (null, diags);
        }


        private static Setter? GetSetterForMethod(
            Compilation compilation,
            DeserializerTypes types,
            ITypeSymbol rowType,
            IMethodSymbol method,
            Location? location,
            ref ImmutableArray<Diagnostic> diags
        )
        {
            var methodReturnType = method.ReturnType;
            if (methodReturnType.SpecialType != SpecialType.System_Void)
            {
                var diag = Diagnostics.MethodMustReturnVoid(location, method);
                diags = diags.Add(diag);

                return null;
            }

            if (method.IsGenericMethod)
            {
                var diag = Diagnostics.MethodCannotBeGeneric(location, method);
                diags = diags.Add(diag);

                return null;
            }

            ITypeSymbol takesValue;
            bool takesRow;
            bool takesRowByRef;
            bool takesContext;

            var ps = method.Parameters;

            if (method.IsStatic)
            {
                // Options:
                //  - take 1 parameter (the result of the parser) or
                //  - take 2 parameters, the result of the parser and an `in ReadContext` or
                //  - take 2 parameters, the row type (which may be passed by ref), and the result of the parser or
                //  - take 3 parameters, the row type (which may be passed by ref), the result of the parser, and `in ReadContext`

                if (ps.Length == 0)
                {
                    var diag = Diagnostics.BadSetterParameters_TooFew(location, method);
                    diags = diags.Add(diag);

                    return null;
                }
                else if (ps.Length == 1)
                {
                    // take 1 parameter (the result of the parser)

                    var p0 = ps[0];
                    if (p0.RefKind != RefKind.None)
                    {
                        var diag = Diagnostics.BadSetterParameters_StaticOne(location, method);
                        diags = diags.Add(diag);

                        return null;
                    }

                    takesValue = p0.Type;
                    takesRow = false;
                    takesRowByRef = false;
                    takesContext = false;
                }
                else if (ps.Length == 2)
                {
                    var p1 = ps[1];
                    if (p1.IsInReadContext(types.OurTypes))
                    {
                        // take 2 parameters, the result of the parser and an `in ReadContext`
                        var p0 = ps[0];
                        if (p0.RefKind != RefKind.None)
                        {
                            var diag = Diagnostics.BadSetterParameters_StaticTwo(location, method, rowType);
                            diags = diags.Add(diag);

                            return null;
                        }

                        takesValue = p0.Type;
                        takesRow = false;
                        takesRowByRef = false;
                        takesContext = true;
                    }
                    else
                    {
                        // take 2 parameters, the row type (which may be passed by ref), and the result of the parser

                        var p0 = ps[0];
                        if (!p0.Type.Equals(rowType, SymbolEqualityComparer.Default))
                        {
                            var diag = Diagnostics.BadSetterParameters_StaticTwo(location, method, rowType);
                            diags = diags.Add(diag);

                            return null;
                        }

                        var p0RefKind = p0.RefKind;

                        if (!p0RefKind.IsNormalOrByRef())
                        {
                            var diag = Diagnostics.BadSetterParameters_StaticTwo(location, method, rowType);
                            diags = diags.Add(diag);

                            return null;
                        }

                        if (p1.RefKind != RefKind.None)
                        {
                            var diag = Diagnostics.BadSetterParameters_StaticTwo(location, method, rowType);
                            diags = diags.Add(diag);

                            return null;
                        }

                        takesValue = p1.Type;
                        takesRow = true;
                        takesRowByRef = p0RefKind == RefKind.Ref;
                        takesContext = false;
                    }
                }
                else if (ps.Length == 3)
                {
                    // take 3 parameters, the row type (which may be passed by ref), the result of the parser, and `in ReadContext`

                    var p0 = ps[0];
                    if (!p0.RefKind.IsNormalOrByRef())
                    {
                        var diag = Diagnostics.BadSetterParameters_StaticThree(location, method, rowType);
                        diags = diags.Add(diag);

                        return null;
                    }

                    if (!p0.Type.Equals(rowType, SymbolEqualityComparer.Default))
                    {
                        var diag = Diagnostics.BadSetterParameters_StaticThree(location, method, rowType);
                        diags = diags.Add(diag);

                        return null;
                    }

                    var p1 = ps[1];
                    if (p1.RefKind != RefKind.None)
                    {
                        var diag = Diagnostics.BadSetterParameters_StaticThree(location, method, rowType);
                        diags = diags.Add(diag);

                        return null;
                    }

                    var p2 = ps[2];
                    if (!p2.IsInReadContext(types.OurTypes))
                    {
                        var diag = Diagnostics.BadSetterParameters_StaticThree(location, method, rowType);
                        diags = diags.Add(diag);

                        return null;
                    }

                    takesValue = p1.Type;
                    takesRow = true;
                    takesRowByRef = p0.RefKind == RefKind.Ref;
                    takesContext = true;
                }
                else
                {
                    var diag = Diagnostics.BadSetterParameters_TooMany(location, method);
                    diags = diags.Add(diag);

                    return null;
                }
            }
            else
            {
                takesRow = false;
                takesRowByRef = false;

                // Options:
                //  - be on the row type, and take 1 parameter (the result of the parser) or
                //  - be on the row type, and take 2 parameters, the result of the parser and an `in ReadContext`

                if (ps.Length == 0)
                {
                    var diag = Diagnostics.BadSetterParameters_TooFew(location, method);
                    diags = diags.Add(diag);

                    return null;
                }
                else if (ps.Length == 1)
                {
                    // take 1 parameter (the result of the parser) or

                    var p0 = ps[0];
                    if (p0.RefKind != RefKind.None)
                    {
                        var diag = Diagnostics.BadSetterParameters_InstanceOne(location, method);
                        diags = diags.Add(diag);

                        return null;
                    }

                    takesValue = p0.Type;
                    takesContext = false;
                }
                else if (ps.Length == 2)
                {
                    // take 2 parameters, the result of the parser and an `in ReadContext`

                    var p0 = ps[0];
                    if (p0.RefKind != RefKind.None)
                    {
                        var diag = Diagnostics.BadSetterParameters_InstanceTwo(location, method);
                        diags = diags.Add(diag);

                        return null;
                    }

                    var p1 = ps[1];
                    if (!p1.IsInReadContext(types.OurTypes))
                    {
                        var diag = Diagnostics.BadSetterParameters_InstanceTwo(location, method);
                        diags = diags.Add(diag);

                        return null;
                    }

                    takesValue = p0.Type;
                    takesContext = true;
                }
                else
                {
                    var diag = Diagnostics.BadSetterParameters_TooMany(location, method);
                    diags = diags.Add(diag);

                    return null;
                }
            }

            return new Setter(method, takesValue, takesRow, takesRowByRef, takesContext);
        }

        private static bool? GetMemberRequiredFromAttributes(
            AttributedMembers attrMembers,
            Location? location, 
            ImmutableArray<AttributeSyntax> attrs, 
            ref ImmutableArray<Diagnostic> diags
        )
        {
            var requiredByte = Utils.GetConstantsWithName<byte>(attrMembers, attrs, "MemberRequired", ref diags);
            var requiredBool = Utils.GetConstantsWithName<bool>(attrMembers, attrs, "IsRequired", ref diags);

            var total = requiredByte.Length + requiredBool.Length;

            if (total > 1)
            {
                var diag = Diagnostics.IsRequiredSpecifiedMultipleTimes(location);
                diags = diags.Add(diag);

                return null;
            }

            if (total == 0)
            {
                return null;
            }

            if (requiredByte.Length == 1)
            {
                var byteVal = requiredByte.Single();

                var logicalVal =
                    byteVal switch
                    {
                        1 => true,
                        2 => false,
                        _ => default(bool?)
                    };

                if (logicalVal == null)
                {
                    var diag = Diagnostics.UnexpectedConstantValue(location, byteVal.ToString(), new[] { "Yes", "No" });
                    diags = diags.Add(diag);

                    return null;
                }

                return logicalVal;
            }

            var boolVal = requiredBool.Single();
            return boolVal;
        }

        private static Reset? GetReset(
            Compilation compilation,
            AttributedMembers attrMembers,
            DeserializerTypes types,
            INamedTypeSymbol rowType,
            Location? location,
            ImmutableArray<AttributeSyntax> attrs,
            ref ImmutableArray<Diagnostic> diags
        )
        {
            var reset = 
                Utils.GetMethodFromAttribute(
                    attrMembers,
                    "ResetType",
                    Diagnostics.ResetTypeSpecifiedMultipleTimes,
                    "ResetMethodName",
                    Diagnostics.ResetMethodNameSpecifiedMultipleTimes,
                    Diagnostics.ResetBothMustBeSet,
                    location,
                    attrs,
                    ref diags
                );

            if (reset == null)
            {
                return null;
            }

            var (type, name) = reset.Value;

            var resetMtd = Utils.GetMethod(type, name, location, ref diags);
            if (resetMtd == null)
            {
                return null;
            }

            var methodReturnType = resetMtd.ReturnType;
            if (methodReturnType.SpecialType != SpecialType.System_Void)
            {
                var diag = Diagnostics.MethodMustReturnVoid(location, resetMtd);
                diags = diags.Add(diag);

                return null;
            }

            if (resetMtd.IsGenericMethod)
            {
                var diag = Diagnostics.MethodCannotBeGeneric(location, resetMtd);
                diags = diags.Add(diag);

                return null;
            }

            var accessible = resetMtd.IsAccessible(attrMembers);

            if (!accessible)
            {
                var diag = Diagnostics.MethodNotPublicOrInternal(location, resetMtd);
                diags = diags.Add(diag);

                return null;
            }

            var ps = resetMtd.Parameters;

            bool isStatic;
            bool takesRow;
            bool takesRowByRef;
            bool takesContext;

            if (resetMtd.IsStatic)
            {
                isStatic = true;

                // Options:
                //  - zero parameters or
                //  - a single parameter of the row type or
                //  - a single parameter of `in ReadContext` or
                //  - two parameters, the first of the row type (which may be by ref) and the second of `in ReadContext`

                if (ps.Length == 0)
                {
                    // zero parameters

                    takesRow = false;
                    takesRowByRef = false;
                    takesContext = false;
                }
                else if (ps.Length == 1)
                {
                    var p0 = ps[0];
                    if (p0.RefKind == RefKind.In)
                    {
                        // a single parameter of `in ReadContext`

                        if (!p0.IsInReadContext(types.OurTypes))
                        {
                            var diag = Diagnostics.BadResetParameters_StaticOne(location, resetMtd, rowType);
                            diags = diags.Add(diag);

                            return null;
                        }

                        takesRow = false;
                        takesRowByRef = false;
                        takesContext = true;
                    }
                    else if (p0.RefKind == RefKind.None)
                    {
                        // a single parameter of the row type

                        if (!p0.IsNormalParameterOfType(compilation, rowType))
                        {
                            var diag = Diagnostics.BadResetParameters_StaticOne(location, resetMtd, rowType);
                            diags = diags.Add(diag);

                            return null;
                        }

                        takesRow = true;
                        takesRowByRef = false;
                        takesContext = false;
                    }
                    else
                    {
                        var diag = Diagnostics.BadResetParameters_StaticOne(location, resetMtd, rowType);
                        diags = diags.Add(diag);

                        return null;
                    }
                }
                else if (ps.Length == 2)
                {
                    // two parameters, the first of the row type (which may be by ref) and the second of `in ReadContext`

                    var p0 = ps[0];
                    if (!p0.RefKind.IsNormalOrByRef())
                    {
                        var diag = Diagnostics.BadResetParameters_StaticTwo(location, resetMtd, rowType);
                        diags = diags.Add(diag);

                        return null;
                    }

                    if (!p0.Type.Equals(rowType, SymbolEqualityComparer.Default))
                    {
                        var diag = Diagnostics.BadResetParameters_StaticTwo(location, resetMtd, rowType);
                        diags = diags.Add(diag);

                        return null;
                    }

                    var p1 = ps[1];
                    if (!p1.IsInReadContext(types.OurTypes))
                    {
                        var diag = Diagnostics.BadResetParameters_StaticTwo(location, resetMtd, rowType);
                        diags = diags.Add(diag);

                        return null;
                    }

                    takesRow = true;
                    takesRowByRef = p0.RefKind == RefKind.Ref;
                    takesContext = true;
                }
                else
                {
                    var diag = Diagnostics.BadResetParameters_TooMany(location, resetMtd);
                    diags = diags.Add(diag);

                    return null;
                }
            }
            else
            {
                isStatic = false;

                // be on the row type...
                var declaredOn = Utils.NonNull(resetMtd.ContainingType);
                var conversion = compilation.ClassifyConversion(rowType, declaredOn);
                var canConvert = conversion.IsImplicit || conversion.IsIdentity;
                if (!canConvert)
                {
                    var diag = Diagnostics.BadReset_NotOnRow(location, resetMtd, rowType);
                    diags = diags.Add(diag);

                    return null;
                }

                // Options:
                //  - zero parameters or 
                //  - a single `in ReadContext` parameter.

                if (ps.Length == 0)
                {
                    takesRow = true;
                    takesRowByRef = false;
                    takesContext = false;
                }
                else if (ps.Length == 1)
                {
                    var p0 = ps[0];
                    if (!p0.IsInReadContext(types.OurTypes))
                    {
                        var diag = Diagnostics.BadResetParameters_InstanceOne(location, resetMtd);
                        diags = diags.Add(diag);

                        return null;
                    }

                    takesRow = true;
                    takesRowByRef = false;
                    takesContext = true;
                }
                else
                {
                    var diag = Diagnostics.BadResetParameters_TooMany(location, resetMtd);
                    diags = diags.Add(diag);

                    return null;
                }
            }

            return new Reset(resetMtd, isStatic, takesRow, takesRowByRef, takesContext);
        }

        internal static Reset? InferDefaultReset(
            AttributedMembers attrMembers,
            DeserializerTypes types,
            ITypeSymbol declaringType,
            string propertyName,
            ImmutableArray<AttributeSyntax> attrs,
            Location? location,
            ref ImmutableArray<Diagnostic> diags
        )
        {
            var ignoredDiags = ImmutableArray<Diagnostic>.Empty;
            var resetType = Utils.GetTypeConstantWithName(attrMembers, attrs, "ResetType", ref ignoredDiags);
            var resetMethod = Utils.GetConstantsWithName<string>(attrMembers, attrs, "ResetMethodName", ref ignoredDiags);

            // if anything _tried_ to set a non-default, don't include any defaults
            if (!resetType.IsEmpty || !resetMethod.IsEmpty)
            {
                return null;
            }

            foreach (var candidate in declaringType.GetMembers("Reset" + propertyName))
            {
                var mtd = candidate as IMethodSymbol;
                if (mtd == null)
                {
                    continue;
                }

                if (mtd.ReturnType.SpecialType != SpecialType.System_Void)
                {
                    continue;
                }

                bool isStatic, takesContext;
                var ps = mtd.Parameters;

                if (mtd.IsStatic)
                {
                    isStatic = true;

                    if (ps.Length == 0)
                    {
                        takesContext = false;
                    }
                    else if (ps.Length == 1)
                    {
                        takesContext = ps[0].IsInReadContext(types.OurTypes);

                        if (!takesContext && !ps[0].Equals(declaringType, SymbolEqualityComparer.Default))
                        {
                            var diag = Diagnostics.BadResetParameters_StaticOne(location, mtd, ps[0].Type);
                            diags = diags.Add(diag);
                            return null;
                        }
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    isStatic = false;
                    takesContext = false;

                    if (ps.Length != 0)
                    {
                        continue;
                    }
                }

                return new Reset(mtd, isStatic, false, false, takesContext);
            }

            return null;
        }

        private static Parser? GetParser(
            Compilation compilation,
            AttributedMembers attrMembers,
            DeserializerTypes types,
            ITypeSymbol toParseType,
            Location? location,
            ImmutableArray<AttributeSyntax> attrs,
            ref ImmutableArray<Diagnostic> diags
        )
        {
            var parser = 
                Utils.GetMethodFromAttribute(
                    attrMembers,
                    "ParserType",
                    Diagnostics.ParserTypeSpecifiedMultipleTimes,
                    "ParserMethodName",
                    Diagnostics.ParserMethodNameSpecifiedMultipleTimes,
                    Diagnostics.ParserBothMustBeSet,
                    location,
                    attrs,
                    ref diags
                );

            if (parser == null)
            {
                return null;
            }

            var (type, name) = parser.Value;

            var parserMtd = Utils.GetMethod(type, name, location, ref diags);
            if (parserMtd == null)
            {
                return null;
            }

            var methodReturnType = parserMtd.ReturnType;
            if (methodReturnType.SpecialType != SpecialType.System_Boolean)
            {
                var diag = Diagnostics.MethodMustReturnBool(location, parserMtd);
                diags = diags.Add(diag);

                return null;
            }

            if (!parserMtd.IsStatic)
            {
                var diag = Diagnostics.MethodNotStatic(location, parserMtd);
                diags = diags.Add(diag);

                return null;
            }

            var accessible = parserMtd.IsAccessible(attrMembers);

            if (!accessible)
            {
                var diag = Diagnostics.MethodNotPublicOrInternal(location, parserMtd);
                diags = diags.Add(diag);

                return null;
            }

            var ps = parserMtd.Parameters;
            if (ps.Length != 3)
            {
                var diag = Diagnostics.BadParserParameters(location, parserMtd);
                diags = diags.Add(diag);

                return null;
            }

            //  Parameters
            //     * ReadOnlySpan(char)
            //     * in ReadContext, 
            //     * out assignable to outputType

            var p0 = ps[0];
            if (!p0.IsNormalParameterOfType(compilation, types.Framework.ReadOnlySpanOfChar))
            {
                var diag = Diagnostics.BadParserParameters(location, parserMtd);
                diags = diags.Add(diag);

                return null;
            }

            var p1 = ps[1];
            if (!p1.IsInReadContext(types.OurTypes))
            {
                var diag = Diagnostics.BadParserParameters(location, parserMtd);
                diags = diags.Add(diag);

                return null;
            }

            var p2 = ps[2];
            if (p2.RefKind != RefKind.Out)
            {
                var diag = Diagnostics.BadParserParameters(location, parserMtd);
                diags = diags.Add(diag);

                return null;
            }

            var conversion = compilation.ClassifyConversion(p2.Type, toParseType);
            var canConvert = conversion.IsImplicit || conversion.IsIdentity;
            if (!canConvert)
            {
                var diag = Diagnostics.BadParserParameters(location, parserMtd);
                diags = diags.Add(diag);

                return null;
            }

            return new Parser(parserMtd, p2.Type);
        }
    }
}
