using System.Diagnostics;

namespace Cesil.Analyzers
{
    internal static class DebugHelper
    {
        [Conditional("DEBUG")]
        internal static void AttachDebugger(bool doAttach)
        {
            if (!doAttach)
            {
                return;
            }

            if(!Debugger.IsAttached)
            {
                Debugger.Launch();
            }
        }
    }
}
