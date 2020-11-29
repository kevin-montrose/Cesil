using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Cesil.SourceGenerator
{
    internal sealed class FrameworkTypes
    {
        // required
        internal readonly INamedTypeSymbol IBufferWriterOfChar;
        internal readonly INamedTypeSymbol FlagsAttribute;

        // optional
        internal readonly INamedTypeSymbol? DataMemberAttribute;
        
        private FrameworkTypes(INamedTypeSymbol iBufferWriterOfChar, INamedTypeSymbol flagsAttribute, INamedTypeSymbol? dataMemberAttribute)
        {
            IBufferWriterOfChar = iBufferWriterOfChar;
            FlagsAttribute = flagsAttribute;
            DataMemberAttribute = dataMemberAttribute;
        }

        internal static bool TryCreate(Compilation compilation, BuiltInTypes builtIns, [MaybeNullWhen(returnValue: false)] out FrameworkTypes types)
        {
            var iBufferWriter = compilation.GetTypeByMetadataName("System.Buffers.IBufferWriter`1");
            if(iBufferWriter == null)
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

            var iBufferWriterOfChar = iBufferWriter.Construct(builtIns.Char);

            var dataMember = compilation.GetTypeByMetadataName("System.Runtime.Serialization.DataMemberAttribute");

            types = new FrameworkTypes(iBufferWriterOfChar, flagsAttribute, dataMember);
            return true;
        }
    }
}
