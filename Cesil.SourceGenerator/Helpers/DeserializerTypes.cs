namespace Cesil.SourceGenerator
{
    internal sealed class DeserializerTypes
    {
        internal BuiltInTypes BuiltIn;
        internal FrameworkTypes Framework;
        internal CesilTypes OurTypes;

        internal DeserializerTypes(BuiltInTypes builtIn, FrameworkTypes framework, CesilTypes ourTypes)
        {
            BuiltIn = builtIn;
            Framework = framework;
            OurTypes = ourTypes;
        }
    }
}
