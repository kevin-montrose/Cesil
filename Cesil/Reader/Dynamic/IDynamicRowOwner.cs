using System.Collections.Generic;

namespace Cesil
{
    internal interface IDynamicRowOwner
    {
        object Context { get; }

        // todo: don't love a list for this
        List<DynamicRow> NotifyOnDispose { get; }
    }
}
