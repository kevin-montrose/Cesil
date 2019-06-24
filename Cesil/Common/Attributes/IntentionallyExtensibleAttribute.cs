using System;

namespace Cesil
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal sealed class IntentionallyExtensibleAttribute : Attribute
    {
        public string Reason { get; }

        public IntentionallyExtensibleAttribute(string reason)
        {
            Reason = reason;
        }

        public override string ToString()
        => $"{nameof(Reason)}={Reason}";
    }
}
