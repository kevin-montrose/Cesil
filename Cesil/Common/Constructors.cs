using System;
using System.Reflection;

namespace Cesil
{
    internal static class Constructors
    {
        public static readonly ConstructorInfo PassthroughRowEnumerable = Types.PassthroughRowEnumerableType.GetConstructorNonNull(BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { Types.ObjectType }, null);
    }
}
