using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Cesil.Analyzers
{
    [SuppressMessage("MicrosoftCodeAnalysisReleaseTracking", "RS2008:Enable analyzer release tracking", Justification = "These aren't intended to ever be used in another project, so tracking for release doesn't matter")]
    internal static class Diagnostics
    {
        internal static readonly DiagnosticDescriptor IsCompletedSuccessfully =
            new DiagnosticDescriptor(
                "CES0001",
                "Incorrect .IsCompletedSuccessfully property use",
                "Use .IsCompletedSuccessfully(object) extension method instead of .IsCompletedSuccessfully property",
                "Cesil",
                DiagnosticSeverity.Warning,
                true
            );

        internal static readonly DiagnosticDescriptor ConfigureCancellableAwait =
            new DiagnosticDescriptor(
                "CES0002",
                "Missing call to ConfigureCancellableAwait(...)",
                "Wrap awaitable with inline ConfigureCancellableAwait(...) when await'ing",
                "Cesil",
                DiagnosticSeverity.Warning,
                true
            );

        internal static readonly DiagnosticDescriptor Throw =
            new DiagnosticDescriptor(
                "CES0003",
                "Direct use of throw",
                "Call a member on Throw instead of throwing exceptions directly",
                "Cesil",
                DiagnosticSeverity.Warning,
                true
            );

        internal static readonly DiagnosticDescriptor Types =
            new DiagnosticDescriptor(
                "CES0004",
                "typeof(...) instead of Types",
                "Use a member of Types intead of typeof(...) if possible",
                "Cesil",
                DiagnosticSeverity.Warning,
                true
            );

        internal static readonly DiagnosticDescriptor NullForgiveness =
            new DiagnosticDescriptor(
                "CES0005",
                "Undocumented use of ! (null forgiving operator)",
                "Either remove !, or add a #pragma or [SuppressMessage] documenting why null forgiveness is necessary",
                "Cesil",
                DiagnosticSeverity.Warning,
                true
            );

        internal static readonly DiagnosticDescriptor PublicMember =
            new DiagnosticDescriptor(
                "CES0006",
                "Public member on non-public type",
                "Change member to internal or private",
                "Cesil",
                DiagnosticSeverity.Warning,
                true
            );

        internal static readonly DiagnosticDescriptor BindingFlagsConstants =
            new DiagnosticDescriptor(
                "CES0007",
                "BindingFlags instead of BindingFlagsConstants",
                "Use members on BindingFlagsConstants instead of BindingFlags if possible",
                "Cesil",
                DiagnosticSeverity.Warning,
                true
            );

        internal static readonly DiagnosticDescriptor UsingStaticBindingFlagsConstants =
            new DiagnosticDescriptor(
                "CES0008",
                "Add using static directive",
                "Add `using static Cesil.BindingFlagsConstants;` directive and omit BindingFlagsConstants for uses",
                "Cesil",
                DiagnosticSeverity.Warning,
                true
            );
    }
}
