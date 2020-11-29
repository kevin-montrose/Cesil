namespace Cesil.SourceGenerator
{
    internal sealed class SerializerTypes
    {
        internal BuiltInTypes BuiltIn;
        internal FrameworkTypes Framework;
        internal CesilTypes OurTypes;

        internal SerializerTypes(BuiltInTypes builtIn, FrameworkTypes framework, CesilTypes ourTypes)
        {
            BuiltIn = builtIn;
            Framework = framework;
            OurTypes = ourTypes;
        }
    }
}
