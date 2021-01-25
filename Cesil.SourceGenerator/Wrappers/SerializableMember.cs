using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
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

            var attrName = Utils.GetNameFromAttributes(compilation, mtdLoc, attrs, ref diags);
            if (attrName == null)
            {
                var diag = Diagnostics.SerializableMemberMustHaveNameSetForMethod(mtdLoc, mtd);
                diags = diags.Add(diag);

                attrName = "--UNKNOWN--";
            }

            var getter = GetGetterForMethod(compilation, types, serializingType, mtd, mtdLoc, ref diags);

            int? order = Utils.GetOrderFromAttributes(compilation, mtdLoc, types.Framework, types.OurTypes.SerializerMemberAttribute, attrs, ref diags);

            var emitDefaultValue = true;
            var attrEmitDefaultValue = GetEmitDefaultValueFromAttributes(compilation, mtdLoc, attrs, ref diags);
            emitDefaultValue = attrEmitDefaultValue ?? emitDefaultValue;

            var shouldSerialize = GetShouldSerialize(compilation, types, serializingType, mtdLoc, attrs, ref diags);

            // after this point, we need to know what we're working with
            if (getter == null)
            {
                return (null, diags);
            }

            var formatter = GetFormatter(compilation, types, getter.ForType, mtdLoc, attrs, ref diags);

            return MakeMember(mtdLoc, types, attrName, getter, formatter, shouldSerialize, emitDefaultValue, order, diags);
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
            var attrName = Utils.GetNameFromAttributes(compilation, fieldLoc, attrs, ref diags);
            name = attrName ?? name;

            var getter = new Getter(field);

            int? order = Utils.GetOrderFromAttributes(compilation, fieldLoc, types.Framework, types.OurTypes.SerializerMemberAttribute, attrs, ref diags);

            var emitDefaultValue = true;
            var attrEmitDefaultValue = GetEmitDefaultValueFromAttributes(compilation, fieldLoc, attrs, ref diags);
            emitDefaultValue = attrEmitDefaultValue ?? emitDefaultValue;

            var formatter = GetFormatter(compilation, types, field.Type, fieldLoc, attrs, ref diags);
            var shouldSerialize = GetShouldSerialize(compilation, types, serializingType, fieldLoc, attrs, ref diags);

            return MakeMember(fieldLoc, types, name, getter, formatter, shouldSerialize, emitDefaultValue, order, diags);
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
                var diag = Diagnostics.NoGetterOnSerializableProperty(propLoc);
                diags = diags.Add(diag);
            }

            if (prop.Parameters.Any())
            {
                var diag = Diagnostics.SerializablePropertyCannotHaveParameters(propLoc);
                diags = diags.Add(diag);
            }

            var name = prop.Name;
            var attrName = Utils.GetNameFromAttributes(compilation, propLoc, attrs, ref diags);
            name = attrName ?? name;

            var getter = new Getter(prop);

            int? order = Utils.GetOrderFromAttributes(compilation, propLoc, types.Framework, types.OurTypes.SerializerMemberAttribute, attrs, ref diags);

            var emitDefaultValue = true;
            var attrEmitDefaultValue = GetEmitDefaultValueFromAttributes(compilation, propLoc, attrs, ref diags);
            emitDefaultValue = attrEmitDefaultValue ?? emitDefaultValue;

            var formatter = GetFormatter(compilation, types, prop.Type, propLoc, attrs, ref diags);
            var shouldSerialize = GetShouldSerialize(compilation, types, serializingType, propLoc, attrs, ref diags);

            return MakeMember(propLoc, types, name, getter, formatter, shouldSerialize, emitDefaultValue, order, diags);
        }

        private static (SerializableMember? Member, ImmutableArray<Diagnostic> Diagnostics) MakeMember(
            Location? location,
            SerializerTypes types,
            string name,
            Getter getter,
            Formatter? formatter,
            ShouldSerialize? shouldSerialize,
            bool emitDefaultValue,
            int? order,
            ImmutableArray<Diagnostic> diags
        )
        {
            if (diags.IsEmpty)
            {
                if (formatter == null && !Formatter.TryGetDefault(types, getter.ForType, out formatter))
                {
                    var diag = Diagnostics.NoBuiltInFormatter(location, getter.ForType);
                    diags = diags.Add(diag);
                    return (null, diags);
                }

                formatter = Utils.NonNull(formatter);

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
            var shouldSerialize = Utils.GetMethodFromAttribute(
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

            var shouldSerializeMtd = Utils.GetMethod(type, mtd, location, ref diags);

            if (shouldSerializeMtd == null)
            {
                return null;
            }

            if (shouldSerializeMtd.IsGenericMethod)
            {
                var diag = Diagnostics.MethodCannotBeGeneric(location, shouldSerializeMtd);
                diags = diags.Add(diag);

                return null;
            }

            var accessible =
                shouldSerializeMtd.DeclaredAccessibility == Accessibility.Public ||
                (compilation.Assembly.Equals(shouldSerializeMtd.ContainingAssembly, SymbolEqualityComparer.Default) &&
                 shouldSerializeMtd.DeclaredAccessibility == Accessibility.Internal);

            if (!accessible)
            {
                var diag = Diagnostics.MethodNotPublicOrInternal(location, shouldSerializeMtd);
                diags = diags.Add(diag);

                return null;
            }

            if (!shouldSerializeMtd.ReturnType.Equals(types.BuiltIn.Bool, SymbolEqualityComparer.Default))
            {
                var diag = Diagnostics.MethodMustReturnBool(location, shouldSerializeMtd);
                diags = diags.Add(diag);

                return null;
            }

            var shouldSerializeParams = shouldSerializeMtd.Parameters;

            var isStatic = shouldSerializeMtd.IsStatic;
            bool takesContext;
            bool takesRow;
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
                    takesRow = false;
                }
                else if (shouldSerializeParams.Length == 1)
                {
                    var p0 = shouldSerializeParams[0];
                    if (!p0.IsNormalParameterOfType(compilation, declaringType))
                    {
                        var diag = Diagnostics.BadShouldSerializeParameters_StaticOne(location, shouldSerializeMtd, declaringType);
                        diags = diags.Add(diag);

                        return null;
                    }

                    takesRow = true;
                    takesContext = false;
                }
                else if (shouldSerializeParams.Length == 2)
                {
                    var p0 = shouldSerializeParams[0];
                    if (!p0.IsNormalParameterOfType(compilation, declaringType))
                    {
                        var diag = Diagnostics.BadShouldSerializeParameters_StaticTwo(location, shouldSerializeMtd, declaringType);
                        diags = diags.Add(diag);

                        return null;
                    }

                    var p1 = shouldSerializeParams[1];
                    if (!p1.IsInWriteContext(types.OurTypes))
                    {
                        var diag = Diagnostics.BadShouldSerializeParameters_StaticTwo(location, shouldSerializeMtd, declaringType);
                        diags = diags.Add(diag);

                        return null;
                    }

                    takesRow = true;
                    takesContext = true;
                }
                else
                {
                    var diag = Diagnostics.BadShouldSerializeParameters_TooMany(location, shouldSerializeMtd);
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
                    var diag = Diagnostics.ShouldSerializeInstanceOnWrongType(location, shouldSerializeMtd, declaringType);
                    diags = diags.Add(diag);

                    return null;
                }

                takesRow = true;

                if (shouldSerializeParams.Length == 0)
                {
                    takesContext = false;
                }
                else if (shouldSerializeParams.Length == 1)
                {
                    var p0 = shouldSerializeParams[0];
                    if (!p0.IsInWriteContext(types.OurTypes))
                    {
                        var diag = Diagnostics.BadShouldSerializeParameters_InstanceOne(location, shouldSerializeMtd);
                        diags = diags.Add(diag);

                        return null;
                    }

                    takesContext = true;
                }
                else
                {
                    var diag = Diagnostics.BadShouldSerializeParameters_TooMany(location, shouldSerializeMtd);
                    diags = diags.Add(diag);

                    return null;
                }
            }

            return new ShouldSerialize(shouldSerializeMtd, isStatic, takesRow, takesContext);
        }

        private static Getter? GetGetterForMethod(
            Compilation compilation,
            SerializerTypes types,
            ITypeSymbol rowType,
            IMethodSymbol method,
            Location? location,
            ref ImmutableArray<Diagnostic> diags
        )
        {
            var methodReturnType = method.ReturnType;
            if (methodReturnType.SpecialType == SpecialType.System_Void)
            {
                var diag = Diagnostics.MethodMustReturnNonVoid(location, method);
                diags = diags.Add(diag);

                return null;
            }

            if (method.IsGenericMethod)
            {
                var diag = Diagnostics.MethodCannotBeGeneric(location, method);
                diags = diags.Add(diag);

                return null;
            }

            bool takesContext;
            bool takesRow;

            if (method.IsStatic)
            {
                // 0 parameters are allow
                // if there is 1 parameter, it may be an `in WriteContext` or the row type
                // if there are 2 parameters, the first must be the row type, and the second must be `in WriteContext`

                var ps = method.Parameters;
                if (ps.Length == 0)
                {
                    takesContext = false;
                    takesRow = false;
                }
                else if (ps.Length == 1)
                {
                    var p0 = ps[0];
                    if (p0.IsInWriteContext(types.OurTypes))
                    {
                        takesContext = true;
                        takesRow = false;
                    }
                    else if (p0.IsNormalParameterOfType(compilation, rowType))
                    {
                        takesContext = false;
                        takesRow = true;
                    }
                    else
                    {
                        var diag = Diagnostics.BadGetterParameters_StaticOne(location, method, rowType);
                        diags = diags.Add(diag);

                        return null;
                    }
                }
                else if (ps.Length == 2)
                {
                    var p0 = ps[0];
                    if (!p0.IsNormalParameterOfType(compilation, rowType))
                    {
                        var diag = Diagnostics.BadGetterParameters_StaticTwo(location, method, rowType);
                        diags = diags.Add(diag);

                        return null;
                    }

                    var p1 = ps[1];
                    if (!p1.IsInWriteContext(types.OurTypes))
                    {
                        var diag = Diagnostics.BadGetterParameters_StaticTwo(location, method, rowType);
                        diags = diags.Add(diag);

                        return null;
                    }

                    takesContext = true;
                    takesRow = true;
                }
                else
                {
                    var diag = Diagnostics.BadGetterParameters_TooMany(location, method);
                    diags = diags.Add(diag);

                    return null;
                }
            }
            else
            {
                // 0 is allowed
                // if it takes a parameter, it must be an `in WriteContext`

                takesRow = false;

                var ps = method.Parameters;

                if (ps.Length == 0)
                {
                    takesContext = false;
                }
                else if (ps.Length == 1)
                {
                    var p0 = ps[0];
                    if (!p0.IsInWriteContext(types.OurTypes))
                    {
                        var diag = Diagnostics.BadGetterParameters_InstanceOne(location, method);
                        diags = diags.Add(diag);

                        return null;
                    }

                    takesContext = true;
                }
                else
                {
                    var diag = Diagnostics.BadGetterParameters_TooMany(location, method);
                    diags = diags.Add(diag);

                    return null;
                }
            }

            return new Getter(method, takesRow, takesContext);
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
                Utils.GetMethodFromAttribute(
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

            var formatterMtd = Utils.GetMethod(type, mtd, location, ref diags);

            if (formatterMtd == null)
            {
                return null;
            }

            if (formatterMtd.IsGenericMethod)
            {
                var diag = Diagnostics.MethodCannotBeGeneric(location, formatterMtd);
                diags = diags.Add(diag);

                return null;
            }

            var accessible =
                formatterMtd.DeclaredAccessibility == Accessibility.Public ||
                (compilation.Assembly.Equals(formatterMtd.ContainingAssembly, SymbolEqualityComparer.Default) &&
                 formatterMtd.DeclaredAccessibility == Accessibility.Internal);

            if (!accessible)
            {
                var diag = Diagnostics.MethodNotPublicOrInternal(location, formatterMtd);
                diags = diags.Add(diag);

                return null;
            }

            if (!formatterMtd.IsStatic)
            {
                var diag = Diagnostics.MethodNotStatic(location, formatterMtd);
                diags = diags.Add(diag);

                return null;
            }

            if (!formatterMtd.ReturnType.Equals(types.BuiltIn.Bool, SymbolEqualityComparer.Default))
            {
                var diag = Diagnostics.MethodMustReturnBool(location, formatterMtd);
                diags = diags.Add(diag);

                return null;
            }

            var formatterParams = formatterMtd.Parameters;
            if (formatterParams.Length != 3)
            {
                var diag = Diagnostics.BadFormatterParameters(location, formatterMtd, toFormatType);
                diags = diags.Add(diag);

                return null;
            }

            var p0 = formatterParams[0];
            if (!p0.IsNormalParameterOfType(compilation, toFormatType))
            {
                var diag = Diagnostics.BadFormatterParameters(location, formatterMtd, toFormatType);
                diags = diags.Add(diag);

                return null;
            }

            var p1 = formatterParams[1];
            if (!p1.IsInWriteContext(types.OurTypes))
            {
                var diag = Diagnostics.BadFormatterParameters(location, formatterMtd, toFormatType);
                diags = diags.Add(diag);

                return null;
            }

            var p2 = formatterParams[2];
            if (!p2.IsNormalParameterOfType(compilation, types.Framework.IBufferWriterOfChar))
            {
                var diag = Diagnostics.BadFormatterParameters(location, formatterMtd, toFormatType);
                diags = diags.Add(diag);

                return null;
            }

            return new Formatter(formatterMtd, toFormatType);
        }

        private static bool? GetEmitDefaultValueFromAttributes(Compilation compilation, Location? location, ImmutableArray<AttributeSyntax> attrs, ref ImmutableArray<Diagnostic> diags)
        {
            var (emitsByte, emitsBool) = Utils.GetConstantsWithName<byte, bool>(compilation, attrs, "EmitDefaultValue", ref diags);

            var total = emitsByte.Length + emitsBool.Length;

            if (total > 1)
            {
                var diag = Diagnostics.EmitDefaultValueSpecifiedMultipleTimes(location);
                diags = diags.Add(diag);

                return null;
            }

            if (total == 0)
            {
                return null;
            }

            if (emitsByte.Length == 1)
            {
                var byteVal = emitsByte.Single();

                return
                    byteVal switch
                    {
                        1 => true,
                        2 => false,
                        _ => null
                    };
            }

            var boolVal = emitsBool.Single();
            return boolVal;
        }
    }
}
