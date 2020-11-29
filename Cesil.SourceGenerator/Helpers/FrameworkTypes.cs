using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Cesil.SourceGenerator
{
    internal sealed class FrameworkTypes
    {
        // required
        internal readonly INamedTypeSymbol IBufferWriterOfChar;
        internal readonly INamedTypeSymbol FlagsAttribute;
        internal readonly INamedTypeSymbol ReadOnlySpanOfChar;

        // optional
        internal readonly INamedTypeSymbol? DataMemberAttribute;
        internal readonly INamedTypeSymbol? IgnoreDataMemberAttribute;

        private FrameworkTypes(
            INamedTypeSymbol iBufferWriterOfChar, 
            INamedTypeSymbol flagsAttribute, 
            INamedTypeSymbol readOnlySpanOfChar, 
            INamedTypeSymbol? dataMemberAttribute,
            INamedTypeSymbol? ignoreDataMemberAttribute
        )
        {
            IBufferWriterOfChar = iBufferWriterOfChar;
            FlagsAttribute = flagsAttribute;
            ReadOnlySpanOfChar = readOnlySpanOfChar;

            DataMemberAttribute = dataMemberAttribute;
            IgnoreDataMemberAttribute = ignoreDataMemberAttribute;
        }

        internal static bool TryCreate(Compilation compilation, BuiltInTypes builtIns, out FrameworkTypes? types)
        {
            var iBufferWriter = compilation.GetTypeByMetadataName("System.Buffers.IBufferWriter`1");
            if (iBufferWriter == null)
            {
                types = null;
                return false;
            }

            var flagsAttribute = compilation.GetTypeByMetadataName("System.FlagsAttribute");
            if (flagsAttribute == null)
            {
                types = null;
                return false;
            }

            var readOnlySpan = compilation.GetTypeByMetadataName("System.ReadOnlySpan`1");
            if (readOnlySpan == null)
            {
                types = null;
                return false;
            }

            var iBufferWriterOfChar = iBufferWriter.Construct(builtIns.Char);

            var readOnlySpanOfChar = readOnlySpan.Construct(builtIns.Char);

            var dataMember = compilation.GetTypeByMetadataName("System.Runtime.Serialization.DataMemberAttribute");
            var ignoreDataMember = compilation.GetTypeByMetadataName("System.Runtime.Serialization.IgnoreDataMemberAttribute");

            types = new FrameworkTypes(iBufferWriterOfChar, flagsAttribute, readOnlySpanOfChar, dataMember, ignoreDataMember);
            return true;
        }
    }
}
