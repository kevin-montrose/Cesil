using System;

namespace Cesil
{
    internal interface ITestableCancellableProvider
    {
        int? CancelAfter { get; set; }
        int CancelCounter { get; set; }
    }
}
