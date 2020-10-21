using Microsoft.CodeAnalysis;

namespace Cesil.SourceGenerator
{
    internal static class Diagnostics
    {
        internal static readonly DiagnosticDescriptor NoCesilReference =
            new DiagnosticDescriptor(
                "CES1000",
                "Missing Cesil Reference",
                "Could not find a type exported by Cesil, are you missing a reference?",
                "Cesil",
                DiagnosticSeverity.Error,
                true
            );

        internal static readonly DiagnosticDescriptor NoGetterOnSerializableProperty =
            new DiagnosticDescriptor(
                "CES1001",
                "Property lacking getter",
                "Serializable properties must declare a getter",
                "Cesil",
                DiagnosticSeverity.Error,
                true
            );

        internal static readonly DiagnosticDescriptor SerializablePropertyCannotHaveParameters =
            new DiagnosticDescriptor(
                "CES1002",
                "Property cannot take parameters",
                "Serializable properties cannot take parameters",
                "Cesil",
                DiagnosticSeverity.Error,
                true
            );

        internal static readonly DiagnosticDescriptor CouldNotExtractConstantValue =
           new DiagnosticDescriptor(
               "CES1003",
               "Constant expression's value could not extracted",
               "Constant expression's value could not extracted",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor UnexpectedConstantValueType =
           new DiagnosticDescriptor(
               "CES1004",
               "Constant expression's type was unexpected",
               "Expected a value of type {0}, found {1}",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor NameSpecifiedMultipleTimes =
            new DiagnosticDescriptor(
               "CES1005",
               "Member's Name was specified multiple times",
               "Only one attribute may specify Name per member",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor EmitDefaultValueSpecifiedMultipleTimes =
            new DiagnosticDescriptor(
               "CES1006",
               "Member's EmitDefaultValue was specified multiple times",
               "Only one attribute may specify EmitDefaultValue per member",
               "Cesil",
               DiagnosticSeverity.Error,
               true
           );

        internal static readonly DiagnosticDescriptor GenericError =
            new DiagnosticDescriptor(
                "CES1999",
                "Unexpected error occurred",
                "Something went wrong: {0}",
                "Cesil",
                DiagnosticSeverity.Error,
                true
            );
    }
}
