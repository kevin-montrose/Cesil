using System.Reflection;

namespace Cesil
{
    internal static class BindingFlagsConstants
    {
        internal const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
        internal const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;

        internal const BindingFlags InternalInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        internal const BindingFlags InternalStatic = BindingFlags.NonPublic | BindingFlags.Static;

        internal const BindingFlags AllInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        internal const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    }
}
