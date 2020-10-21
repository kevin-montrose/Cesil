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

        internal bool EmitDefaultValue;

        internal int? Order;

        private SerializableMember(string name, Getter getter, Formatter formatter, ShouldSerialize shouldSerialize, bool emitDefaultValue, int? order)
        {
            Name = name;
            Getter = getter;
            Formatter = formatter;
            ShouldSerialize = shouldSerialize;
            EmitDefaultValue = emitDefaultValue;
            Order = order;
        }

        internal static (SerializableMember? Member, ImmutableArray<Diagnostic> Diagnostics) ForProperty(Compilation compilation, IPropertySymbol prop, ImmutableArray<AttributeSyntax> attrs)
        {
            var diags = ImmutableArray<Diagnostic>.Empty;

            var propLoc = prop.Locations.FirstOrDefault();

            if(prop.GetMethod == null)
            {
                var diag = Diagnostic.Create(Diagnostics.NoGetterOnSerializableProperty, propLoc);
                diags.Add(diag);
            }

            if(prop.Parameters.Any())
            {
                var diag = Diagnostic.Create(Diagnostics.SerializablePropertyCannotHaveParameters, propLoc);
                diags.Add(diag);
            }

            var name = prop.Name;
            var attrName = GetNameFromAttributes(compilation, propLoc, attrs, ref diags);
            name = attrName ?? name;

            int? order = GetOrderFromAttributes(compilation, propLoc, attrs, ref diags);

            var emitDefaultValue = true;
            var attrEmitDefaultValue = GetEmitDefaultValueFromAttributes(compilation, propLoc, attrs, ref diags);
            emitDefaultValue = attrEmitDefaultValue ?? emitDefaultValue;

            // todo: formatter
            // todo: should serialize

            if (diags.IsEmpty)
            {
                return (new SerializableMember(name, null, null, null, emitDefaultValue, order), diags);
            }

            return (null, diags);
        }

        private static string? GetNameFromAttributes(Compilation compilation, Location? location, ImmutableArray<AttributeSyntax> attrs, ref ImmutableArray<Diagnostic> diags)
        {
            var names = GetConstantsWithName<string>(compilation, attrs, "Name", ref diags);

            if(names.Length > 1)
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

            return emits.SingleOrDefault();
        }

        private static int? GetOrderFromAttributes(Compilation compilation, Location? location, ImmutableArray<AttributeSyntax> attrs, ref ImmutableArray<Diagnostic> diags)
        {
            
        }

        private static ImmutableArray<T> GetConstantsWithName<T>(Compilation compilation, ImmutableArray<AttributeSyntax> attrs, string name, ref ImmutableArray<Diagnostic> diags)
        {
            var ret = ImmutableArray<T>.Empty;

            foreach (var attr in attrs)
            {
                var argList = attr.ArgumentList;
                if (argList == null) continue;

                var model = comp.GetSemanticModel(attr.SyntaxTree);

                var values = argList.Arguments.Where(a => a.NameEquals != null && a.NameEquals.Name.Identifier.ValueText == name);
                foreach(var value in values)
                {
                    var trueValue = model.GetConstantValue(value.Expression);
                    if (!trueValue.HasValue)
                    {
                        var diag = Diagnostic.Create(Diagnostics.CouldNotExtractConstantValue, value.Expression.GetLocation());
                        diags = diags.Add(diag);
                        continue;
                    }

                    if(trueValue.Value is T asT)
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
