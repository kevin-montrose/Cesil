using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Cesil.SourceGenerator
{
    internal sealed class CesilTypes
    {
        internal readonly INamedTypeSymbol GenerateSerializableAttribute;
        internal readonly INamedTypeSymbol GenerateSerializableMemberAttribute;
        internal readonly INamedTypeSymbol WriteContext;

        private CesilTypes(INamedTypeSymbol generateSerializableAttribute, INamedTypeSymbol generateSerializableMemberAttribute, INamedTypeSymbol writeContext)
        {
            GenerateSerializableAttribute = generateSerializableAttribute;
            GenerateSerializableMemberAttribute = generateSerializableMemberAttribute;
            WriteContext = writeContext;
        }

        internal static bool TryCreate(Compilation compilation, [MaybeNullWhen(returnValue: false)]out CesilTypes types)
        {
            var generateSerializable = compilation.GetTypeByMetadataName("Cesil.GenerateSerializableAttribute");
            if(generateSerializable == null)
            {
                types = null;
                return false;
            }

            var generateSerializableMember = compilation.GetTypeByMetadataName("Cesil.GenerateSerializableMemberAttribute");
            if (generateSerializableMember == null)
            {
                types = null;
                return false;
            }
            
            var writeContext = compilation.GetTypeByMetadataName("Cesil.WriteContext");
            if(writeContext == null)
            {
                types = null;
                return false;
            }

            types = new CesilTypes(generateSerializable, generateSerializableMember, writeContext);
            return true;
        }
    }
}