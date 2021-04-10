using Microsoft.CodeAnalysis;

namespace Cesil.SourceGenerator
{
    internal sealed class CesilTypes
    {
        internal readonly INamedTypeSymbol GenerateSerializerAttribute;
        internal readonly INamedTypeSymbol SerializerMemberAttribute;
        internal readonly INamedTypeSymbol WriteContext;

        internal readonly INamedTypeSymbol GenerateDeserializerAttribute;
        internal readonly INamedTypeSymbol DeserializerMemberAttribute;
        internal readonly INamedTypeSymbol DeserializerInstanceProviderAttribute;
        internal readonly INamedTypeSymbol ReadContext;

        private CesilTypes(
            INamedTypeSymbol generateSerializerAttribute,
            INamedTypeSymbol serializerMemberAttribute,
            INamedTypeSymbol writeContext,
            INamedTypeSymbol generateDeserializerAttribute,
            INamedTypeSymbol deserializerMemberAttribute,
            INamedTypeSymbol deserializerInstanceProviderAttribute,
            INamedTypeSymbol readContext
        )
        {
            GenerateSerializerAttribute = generateSerializerAttribute;
            SerializerMemberAttribute = serializerMemberAttribute;
            WriteContext = writeContext;

            GenerateDeserializerAttribute = generateDeserializerAttribute;
            DeserializerMemberAttribute = deserializerMemberAttribute;
            DeserializerInstanceProviderAttribute = deserializerInstanceProviderAttribute;
            ReadContext = readContext;
        }

        internal static bool TryCreate(Compilation compilation, out CesilTypes? types)
        {
            var generateSerializerAttribute = compilation.GetTypeByMetadataName("Cesil.GenerateSerializerAttribute");
            if (generateSerializerAttribute == null)
            {
                types = null;
                return false;
            }

            var serializerMemberAttribute = compilation.GetTypeByMetadataName("Cesil.SerializerMemberAttribute");
            if (serializerMemberAttribute == null)
            {
                types = null;
                return false;
            }

            var writeContext = compilation.GetTypeByMetadataName("Cesil.WriteContext");
            if (writeContext == null)
            {
                types = null;
                return false;
            }

            var generateDeserializerAttribute = compilation.GetTypeByMetadataName("Cesil.GenerateDeserializerAttribute");
            if (generateDeserializerAttribute == null)
            {
                types = null;
                return false;
            }

            var deserializerMemberAttribute = compilation.GetTypeByMetadataName("Cesil.DeserializerMemberAttribute");
            if (deserializerMemberAttribute == null)
            {
                types = null;
                return false;
            }

            var deserializerInstanceProviderAttribute = compilation.GetTypeByMetadataName("Cesil.DeserializerInstanceProviderAttribute");
            if (deserializerInstanceProviderAttribute == null)
            {
                types = null;
                return false;
            }

            var readContext = compilation.GetTypeByMetadataName("Cesil.ReadContext");
            if (readContext == null)
            {
                types = null;
                return false;
            }

            types = new CesilTypes(generateSerializerAttribute, serializerMemberAttribute, writeContext, generateDeserializerAttribute, deserializerMemberAttribute, deserializerInstanceProviderAttribute, readContext);
            return true;
        }
    }
}