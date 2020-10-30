using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Cesil.SourceGenerator
{
    internal sealed class FrameworkTypes
    {
        // required
        internal readonly INamedTypeSymbol IBufferWriterOfChar;

        // optional
        internal readonly INamedTypeSymbol? DataMemberAttribute;
        
        private FrameworkTypes(INamedTypeSymbol iBufferWriterOfChar, INamedTypeSymbol? dataMemberAttribute)
        {
            IBufferWriterOfChar = iBufferWriterOfChar;
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

            var iBufferWriterOfChar = iBufferWriter.Construct(builtIns.Char);

            var dataMember = compilation.GetTypeByMetadataName("System.Runtime.Serialization.DataMemberAttribute");

            types = new FrameworkTypes(iBufferWriterOfChar, dataMember);
            return true;
        }
    }
}
