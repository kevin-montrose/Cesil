using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cesil.SourceGenerator
{
    internal sealed class SerializableMember
    {
        internal readonly string Name;

        internal readonly Getter Getter;
        internal readonly Formatter Formatter;

        internal readonly ShouldSerialize? ShouldSerialize;

        internal readonly bool EmitDefaultValue;

        internal readonly int? Order;

        private SerializableMember(string name, Getter getter, Formatter formatter, ShouldSerialize? shouldSerialize, bool emitDefaultValue, int? order)
        {
            Name = name;
            Getter = getter;
            Formatter = formatter;
            ShouldSerialize = shouldSerialize;
            EmitDefaultValue = emitDefaultValue;
            Order = order;
        }

        internal static (SerializableMember? Member, ImmutableArray<Diagnostic> Diagnostics) ForMethod(
            Compilation compilation,
            SerializerTypes types,
            INamedTypeSymbol serializingType,
            IMethodSymbol mtd,
            ImmutableArray<AttributeSyntax> attrs
        )
        {
            var diags = ImmutableArray<Diagnostic>.Empty;

            var mtdLoc = mtd.Locations.FirstOrDefault();

            var attrName = GetNameFromAttributes(compilation, mtdLoc, attrs, ref diags);
            if (attrName == null)
            {
                var diag = Diagnostic.Create(Diagnostics.SerializableMemberMustHaveNameSetForMethod, mtdLoc, mtd.Name);
                diags = diags.Add(diag);

                attrName = "--UNKNOWN--";
            }

            var mtdType = mtd.ReturnType;
            if (mtdType.SpecialType == SpecialType.System_Void)
            {
                var diag = Diagnostic.Create(Diagnostics.MethodMustReturnNonVoid, mtdLoc, mtd.Name);
                diags = diags.Add(diag);
            }

            var getter = new Getter(mtd);

            int? order = GetOrderFromAttributes(compilation, mtdLoc, attrs, ref diags);

            var emitDefaultValue = true;
            var attrEmitDefaultValue = GetEmitDefaultValueFromAttributes(compilation, mtdLoc, attrs, ref diags);
            emitDefaultValue = attrEmitDefaultValue ?? emitDefaultValue;

            var formatter = mtdType.SpecialType == SpecialType.System_Void ? null : GetFormatter(compilation, types, mtdType, mtdLoc, attrs, ref diags);
            var shouldSerialize = GetShouldSerialize(compilation, types, serializingType, mtdLoc, attrs, ref diags);

            if (diags.IsEmpty)
            {
                // todo: defaults!
                if (formatter == null)
                {
                    throw new System.Exception();
                }

                return (new SerializableMember(attrName, getter, formatter, shouldSerialize, emitDefaultValue, order), ImmutableArray<Diagnostic>.Empty);
            }

            return (null, diags);
        }

        internal static (SerializableMember? Member, ImmutableArray<Diagnostic> Diagnostics) ForField(
            Compilation compilation,
            SerializerTypes types,
            INamedTypeSymbol serializingType,
            IFieldSymbol field,
            ImmutableArray<AttributeSyntax> attrs
        )
        {
            var diags = ImmutableArray<Diagnostic>.Empty;

            var fieldLoc = field.Locations.FirstOrDefault();

            var name = field.Name;
            var attrName = GetNameFromAttributes(compilation, fieldLoc, attrs, ref diags);
            name = attrName ?? name;

            var getter = new Getter(field);

            int? order = GetOrderFromAttributes(compilation, fieldLoc, attrs, ref diags);

            var emitDefaultValue = true;
            var attrEmitDefaultValue = GetEmitDefaultValueFromAttributes(compilation, fieldLoc, attrs, ref diags);
            emitDefaultValue = attrEmitDefaultValue ?? emitDefaultValue;

            var formatter = GetFormatter(compilation, types, field.Type, fieldLoc, attrs, ref diags);
            var shouldSerialize = GetShouldSerialize(compilation, types, serializingType, fieldLoc, attrs, ref diags);

            if (diags.IsEmpty)
            {
                // todo: defaults!
                if (formatter == null)
                {
                    throw new System.Exception();
                }

                return (new SerializableMember(name, getter, formatter, shouldSerialize, emitDefaultValue, order), ImmutableArray<Diagnostic>.Empty);
            }

            return (null, diags);
        }

        internal static (SerializableMember? Member, ImmutableArray<Diagnostic> Diagnostics) ForProperty(
            Compilation compilation,
            SerializerTypes types,
            INamedTypeSymbol serializingType,
            IPropertySymbol prop,
            ImmutableArray<AttributeSyntax> attrs
        )
        {
            var diags = ImmutableArray<Diagnostic>.Empty;

            var propLoc = prop.Locations.FirstOrDefault();

            if (prop.GetMethod == null)
            {
                var diag = Diagnostic.Create(Diagnostics.NoGetterOnSerializableProperty, propLoc);
                diags = diags.Add(diag);
            }

            if (prop.Parameters.Any())
            {
                var diag = Diagnostic.Create(Diagnostics.SerializablePropertyCannotHaveParameters, propLoc);
                diags = diags.Add(diag);
            }

            var name = prop.Name;
            var attrName = GetNameFromAttributes(compilation, propLoc, attrs, ref diags);
            name = attrName ?? name;

            var getter = new Getter(prop);

            int? order = GetOrderFromAttributes(compilation, propLoc, attrs, ref diags);

            var emitDefaultValue = true;
            var attrEmitDefaultValue = GetEmitDefaultValueFromAttributes(compilation, propLoc, attrs, ref diags);
            emitDefaultValue = attrEmitDefaultValue ?? emitDefaultValue;

            var formatter = GetFormatter(compilation, types, prop.Type, propLoc, attrs, ref diags);
            var shouldSerialize = GetShouldSerialize(compilation, types, serializingType, propLoc, attrs, ref diags);

            if (diags.IsEmpty)
            {
                // todo: defaults!
                if (formatter == null)
                {
                    throw new System.Exception();
                }

                return (new SerializableMember(name, getter, formatter, shouldSerialize, emitDefaultValue, order), ImmutableArray<Diagnostic>.Empty);
            }

            return (null, diags);
        }

        private static ShouldSerialize? GetShouldSerialize(
            Compilation compilation,
            SerializerTypes types,
            INamedTypeSymbol declaringType,
            Location? location,
            ImmutableArray<AttributeSyntax> attrs,
            ref ImmutableArray<Diagnostic> diags
        )
        {
            var shouldSerialize = GetMethodFromAttribute(
                    compilation,
                    "ShouldSerializeType",
                    Diagnostics.ShouldSerializeTypeSpecifiedMultipleTimes,
                    "ShouldSerializeMethodName",
                    Diagnostics.ShouldSerializeMethodNameSpecifiedMultipleTimes,
                    Diagnostics.ShouldSerializeBothMustBeSet,
                    location,
                    attrs,
                    ref diags
                );

            if (shouldSerialize == null)
            {
                return null;
            }

            var (type, mtd) = shouldSerialize.Value;

            var shouldSerializeMtd = GetMethod(type, mtd, location, ref diags);

            if (shouldSerializeMtd == null)
            {
                return null;
            }

            if (shouldSerializeMtd.IsGenericMethod)
            {
                var diag = Diagnostic.Create(Diagnostics.MethodCannotBeGeneric, location, shouldSerializeMtd.Name);
                diags = diags.Add(diag);

                return null;
            }

            var accessible =
                shouldSerializeMtd.DeclaredAccessibility == Accessibility.Public ||
                (compilation.Assembly.Equals(shouldSerializeMtd.ContainingAssembly, SymbolEqualityComparer.Default) &&
                 shouldSerializeMtd.DeclaredAccessibility == Accessibility.Internal);

            if (!accessible)
            {
                var diag = Diagnostic.Create(Diagnostics.MethodNotPublicOrInternal, location, shouldSerializeMtd.Name);
                diags = diags.Add(diag);

                return null;
            }

            if (!shouldSerializeMtd.ReturnType.Equals(types.BuiltIn.Bool, SymbolEqualityComparer.Default))
            {
                var diag = Diagnostic.Create(Diagnostics.MethodMustReturnBool, location, shouldSerializeMtd.Name);
                diags = diags.Add(diag);

                return null;
            }

            var shouldSerializeParams = shouldSerializeMtd.Parameters;

            var isStatic = shouldSerializeMtd.IsStatic;
            bool takesContext;
            if (isStatic)
            {
                // can legally take
                // 1. no parameters
                // 2. one parameter of a type which declares the annotated member
                // 3. two parameters
                //    1. the type which declares the annotated member, or one which can be assigned to it
                //    2. in WriteContext

                if (shouldSerializeParams.Length == 0)
                {
                    // fine!
                    takesContext = false;
                }
                else if (shouldSerializeParams.Length == 1)
                {
                    var p0 = shouldSerializeParams[0];
                    if (!p0.IsNormalParameterOfType(compilation, declaringType))
                    {
                        var diag = Diagnostic.Create(Diagnostics.BadShouldSerializeParameters_StaticOne, location, shouldSerializeMtd.Name, declaringType.Name);
                        diags = diags.Add(diag);

                        return null;
                    }

                    takesContext = false;
                }
                else if (shouldSerializeParams.Length == 2)
                {
                    var p0 = shouldSerializeParams[0];
                    if (!p0.IsNormalParameterOfType(compilation, declaringType))
                    {
                        var diag = Diagnostic.Create(Diagnostics.BadShouldSerializeParameters_StaticTwo, location, shouldSerializeMtd.Name, declaringType.Name);
                        diags = diags.Add(diag);

                        return null;
                    }

                    var p1 = shouldSerializeParams[1];
                    if(!p1.IsInWriteContext(types.OurTypes))
                    {
                        var diag = Diagnostic.Create(Diagnostics.BadShouldSerializeParameters_StaticTwo, location, shouldSerializeMtd.Name, declaringType.Name);
                        diags = diags.Add(diag);

                        return null;
                    }

                    takesContext = true;
                }
                else
                {
                    var diag = Diagnostic.Create(Diagnostics.BadShouldSerializeParameters_TooMany, location, shouldSerializeMtd.Name);
                    diags = diags.Add(diag);

                    return null;
                }
            }
            else
            {
                // can legally take
                //   1. no parameters
                //   2. one in WriteContext parameter

                var onType = shouldSerializeMtd.ContainingType;

                if (!onType.Equals(declaringType, SymbolEqualityComparer.Default))
                {
                    var diag = Diagnostic.Create(Diagnostics.ShouldSerializeInstanceOnWrongType, location, shouldSerializeMtd.Name, declaringType.Name);
                    diags = diags.Add(diag);

                    return null;
                }

                if (shouldSerializeParams.Length == 0)
                {
                    takesContext = false;
                }
                else if (shouldSerializeParams.Length == 1)
                {
                    var p0 = shouldSerializeParams[0];
                    if(!p0.IsInWriteContext(types.OurTypes))
                    {
                        var diag = Diagnostic.Create(Diagnostics.BadShouldSerializeParameters_InstanceOne, location, shouldSerializeMtd.Name, declaringType.Name);
                        diags = diags.Add(diag);

                        return null;
                    }

                    takesContext = true;
                }
                else
                {
                    var diag = Diagnostic.Create(Diagnostics.BadShouldSerializeParameters_TooMany, location, shouldSerializeMtd.Name);
                    diags = diags.Add(diag);

                    return null;
                }
            }

            return new ShouldSerialize(shouldSerializeMtd, isStatic, takesContext);
        }

        private static Formatter? GetFormatter(
            Compilation compilation,
            SerializerTypes types,
            ITypeSymbol toFormatType,
            Location? location,
            ImmutableArray<AttributeSyntax> attrs,
            ref ImmutableArray<Diagnostic> diags
        )
        {
            var formatter =
                GetMethodFromAttribute(
                    compilation,
                    "FormatterType",
                    Diagnostics.FormatterTypeSpecifiedMultipleTimes,
                    "FormatterMethodName",
                    Diagnostics.FormatterMethodNameSpecifiedMultipleTimes,
                    Diagnostics.FormatterBothMustBeSet,
                    location,
                    attrs,
                    ref diags
                );

            if (formatter == null)
            {
                return null;
            }

            var (type, mtd) = formatter.Value;

            var formatterMtd = GetMethod(type, mtd, location, ref diags);

            if (formatterMtd == null)
            {
                return null;
            }

            if (formatterMtd.IsGenericMethod)
            {
                var diag = Diagnostic.Create(Diagnostics.MethodCannotBeGeneric, location, formatterMtd.Name);
                diags = diags.Add(diag);

                return null;
            }

            var accessible =
                formatterMtd.DeclaredAccessibility == Accessibility.Public ||
                (compilation.Assembly.Equals(formatterMtd.ContainingAssembly, SymbolEqualityComparer.Default) &&
                 formatterMtd.DeclaredAccessibility == Accessibility.Internal);

            if (!accessible)
            {
                var diag = Diagnostic.Create(Diagnostics.MethodNotPublicOrInternal, location, formatterMtd.Name);
                diags = diags.Add(diag);

                return null;
            }

            if (!formatterMtd.IsStatic)
            {
                var diag = Diagnostic.Create(Diagnostics.MethodNotStatic, location, formatterMtd.Name);
                diags = diags.Add(diag);

                return null;
            }

            if (!formatterMtd.ReturnType.Equals(types.BuiltIn.Bool, SymbolEqualityComparer.Default))
            {
                var diag = Diagnostic.Create(Diagnostics.MethodMustReturnBool, location, formatterMtd.Name);
                diags = diags.Add(diag);

                return null;
            }

            var formatterParams = formatterMtd.Parameters;
            if (formatterParams.Length != 3)
            {
                var diag = Diagnostic.Create(Diagnostics.BadFormatterParameters, location, formatterMtd.Name, toFormatType.Name);
                diags = diags.Add(diag);

                return null;
            }

            var p0 = formatterParams[0];
            if(!p0.IsNormalParameterOfType(compilation, toFormatType))
            {
                var diag = Diagnostic.Create(Diagnostics.BadFormatterParameters, location, formatterMtd.Name, toFormatType.Name);
                diags = diags.Add(diag);

                return null;
            }

            var p1 = formatterParams[1];
            if (!p1.IsInWriteContext(types.OurTypes))
            {
                var diag = Diagnostic.Create(Diagnostics.BadFormatterParameters, location, formatterMtd.Name, toFormatType.Name);
                diags = diags.Add(diag);

                return null;
            }
           
            var p2 = formatterParams[2];
            if(!p2.IsNormalParameterOfType(compilation, types.Framework.IBufferWriterOfChar))
            {
                var diag = Diagnostic.Create(Diagnostics.BadFormatterParameters, location, formatterMtd.Name, toFormatType.Name);
                diags = diags.Add(diag);

                return null;
            }

            return new Formatter(formatterMtd);
        }

        private static IMethodSymbol? GetMethod(ITypeSymbol type, string mtd, Location? location, ref ImmutableArray<Diagnostic> diags)
        {
            var mtds = type.GetMembers().OfType<IMethodSymbol>().Where(m => m.Name == mtd).ToImmutableArray();
            if (mtds.Length == 0)
            {
                var diag = Diagnostic.Create(Diagnostics.CouldNotFindMethod, location, type.Name, mtd);
                diags = diags.Add(diag);

                return null;
            }
            else if (mtds.Length > 1)
            {
                var diag = Diagnostic.Create(Diagnostics.MultipleMethodsFound, location, type.Name, mtd);
                diags = diags.Add(diag);

                return null;
            }

            return mtds.Single();
        }

        private static (INamedTypeSymbol Type, string Method)? GetMethodFromAttribute(
            Compilation compilation,
            string typeNameProperty,
            DiagnosticDescriptor multipleTypeDefinitionDiagnostic,
            string methodNameProperty,
            DiagnosticDescriptor multipleMethodDefinitionDiagnostic,
            DiagnosticDescriptor notBothSetDefinitionDiagnostic,
            Location? location,
            ImmutableArray<AttributeSyntax> attrs,
            ref ImmutableArray<Diagnostic> diags
        )
        {
            var types = GetTypeConstantWithName(compilation, attrs, typeNameProperty, ref diags);
            if (types.Length > 1)
            {
                var diag = Diagnostic.Create(multipleTypeDefinitionDiagnostic, location);
                diags = diags.Add(diag);

                return null;
            }

            var type = types.SingleOrDefault();

            var methods = GetConstantsWithName<string>(compilation, attrs, methodNameProperty, ref diags);
            if (methods.Length > 1)
            {
                var diag = Diagnostic.Create(multipleMethodDefinitionDiagnostic, location);
                diags = diags.Add(diag);

                return null;
            }

            var method = methods.SingleOrDefault();

            if (type == null && method == null)
            {
                return null;
            }

            if (type == null || method == null)
            {
                var diag = Diagnostic.Create(notBothSetDefinitionDiagnostic, location);
                diags = diags.Add(diag);

                return null;
            }

            return (type, method);
        }

        private static string? GetNameFromAttributes(Compilation compilation, Location? location, ImmutableArray<AttributeSyntax> attrs, ref ImmutableArray<Diagnostic> diags)
        {
            var names = GetConstantsWithName<string>(compilation, attrs, "Name", ref diags);

            if (names.Length > 1)
            {
                var diag = Diagnostic.Create(Diagnostics.NameSpecifiedMultipleTimes, location);
                diags = diags.Add(diag);

                return null;
            }

            return names.SingleOrDefault();
        }

        private static bool? GetEmitDefaultValueFromAttributes(Compilation compilation, Location? location, ImmutableArray<AttributeSyntax> attrs, ref ImmutableArray<Diagnostic> diags)
        {
            var emits = GetConstantsWithName<bool>(compilation, attrs, "EmitDefaultValue", ref diags);

            if (emits.Length > 1)
            {
                var diag = Diagnostic.Create(Diagnostics.EmitDefaultValueSpecifiedMultipleTimes, location);
                diags = diags.Add(diag);

                return null;
            }

            if (emits.Length == 0)
            {
                return null;
            }

            return emits.Single();
        }

        private static int? GetOrderFromAttributes(Compilation compilation, Location? location, ImmutableArray<AttributeSyntax> attrs, ref ImmutableArray<Diagnostic> diags)
        {
            if (attrs.IsEmpty)
            {
                return null;
            }

            var cesilMemberAttr = compilation.GetTypeByMetadataName("Cesil.GenerateSerializableMemberAttribute");
            if (cesilMemberAttr == null)
            {
                throw new System.Exception();
            }

            // this might be legitimately not included
            var dataMemberAttr = compilation.GetTypeByMetadataName("System.Runtime.Serialization.DataMemberAttribute");

            var values = ImmutableArray.CreateBuilder<int>();

            foreach (var attr in attrs)
            {
                var model = compilation.GetSemanticModel(attr.Name.SyntaxTree);
                var type = model.GetTypeInfo(attr.Name).Type;

                if (type == null)
                {
                    continue;
                }

                if (type.Equals(cesilMemberAttr, SymbolEqualityComparer.Default))
                {
                    var value = GetConstantsWithName<int?>(compilation, ImmutableArray.Create(attr), "Order", ref diags);
                    foreach (var val in value)
                    {
                        if (val == null)
                        {
                            continue;
                        }
                        values.Add(val.Value);
                    }

                    continue;
                }

                if (dataMemberAttr != null && type.Equals(dataMemberAttr, SymbolEqualityComparer.Default))
                {
                    var value = GetConstantsWithName<int>(compilation, ImmutableArray.Create(attr), "Order", ref diags);
                    foreach (var val in value)
                    {
                        if (val == -1)
                        {
                            continue;
                        }
                        else
                        {
                            values.Add(val);
                        }
                    }

                    continue;
                }
            }

            var vs = values.ToImmutable();

            if (vs.IsEmpty)
            {
                return null;
            }
            else if (vs.Length == 1)
            {
                return vs[0];
            }
            else
            {
                var diag = Diagnostic.Create(Diagnostics.OrderSpecifiedMultipleTimes, location);
                diags = diags.Add(diag);

                return null;
            }
        }

        private static ImmutableArray<INamedTypeSymbol> GetTypeConstantWithName(Compilation compilation, ImmutableArray<AttributeSyntax> attrs, string name, ref ImmutableArray<Diagnostic> diags)
        {
            var ret = ImmutableArray<INamedTypeSymbol>.Empty;

            foreach (var attr in attrs)
            {
                var argList = attr.ArgumentList;
                if (argList == null) continue;

                var model = compilation.GetSemanticModel(attr.SyntaxTree);

                var values = argList.Arguments.Where(a => a.NameEquals != null && a.NameEquals.Name.Identifier.ValueText == name);
                foreach (var value in values)
                {

                    if (value.Expression is TypeOfExpressionSyntax typeofExp)
                    {
                        var type = model.GetTypeInfo(typeofExp.Type);

                        if (type.Type is INamedTypeSymbol namedType)
                        {
                            ret = ret.Add(namedType);
                        }
                        else
                        {
                            var diag = Diagnostic.Create(Diagnostics.CouldNotExtractConstantValue, value.Expression.GetLocation());
                            diags = diags.Add(diag);
                            continue;
                        }
                    }
                }
            }

            return ret;

        }

        private static ImmutableArray<T> GetConstantsWithName<T>(Compilation compilation, ImmutableArray<AttributeSyntax> attrs, string name, ref ImmutableArray<Diagnostic> diags)
        {
            var ret = ImmutableArray<T>.Empty;

            foreach (var attr in attrs)
            {
                var argList = attr.ArgumentList;
                if (argList == null) continue;

                var model = compilation.GetSemanticModel(attr.SyntaxTree);

                var values = argList.Arguments.Where(a => a.NameEquals != null && a.NameEquals.Name.Identifier.ValueText == name);
                foreach (var value in values)
                {
                    var trueValue = model.GetConstantValue(value.Expression);
                    if (!trueValue.HasValue)
                    {
                        var diag = Diagnostic.Create(Diagnostics.CouldNotExtractConstantValue, value.Expression.GetLocation());
                        diags = diags.Add(diag);
                        continue;
                    }

                    if (trueValue.Value is T asT)
                    {
                        ret = ret.Add(asT);
                    }
                    else
                    {
                        var actualType = trueValue.Value?.GetType()?.Name ?? "null";
                        var diag = Diagnostic.Create(Diagnostics.UnexpectedConstantValueType, value.Expression.GetLocation(), new[] { typeof(T).Name, actualType });
                        diags = diags.Add(diag);
                        continue;
                    }
                }
            }

            return ret;
        }
    }
}
