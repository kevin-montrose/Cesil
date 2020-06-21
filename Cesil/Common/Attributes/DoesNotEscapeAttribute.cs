using System;

namespace Cesil
{
    // indicates that a type, despite implementing a public interface, will never
    //  have an instance returned to a consumer.
    //
    // In other words, the instance only lives on a field of another type (which
    //   may or may not escape) or in a method (which doesn't escape, by definition).
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    internal sealed class DoesNotEscapeAttribute : Attribute
    {
        internal DoesNotEscapeAttribute(string explanation)
        => Utils.CheckArgumentNull(explanation, nameof(explanation));
    }
}
