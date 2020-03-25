using System.Reflection;

using static Cesil.BindingFlagsConstants;

namespace Cesil
{
    internal static class Constructors
    {
        internal static readonly ConstructorInfo PassthroughRowEnumerable = Types.PassthroughRowEnumerable.GetConstructorNonNull(InternalInstance, null, new[] { Types.Object }, null);
    }
}
