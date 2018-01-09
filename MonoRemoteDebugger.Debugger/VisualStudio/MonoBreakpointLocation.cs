using System.Collections.Generic;
using Mono.Debugger.Soft;

namespace MonoRemoteDebugger.Debugger.VisualStudio
{
    internal class MonoBreakpointLocation
    {
        public MethodMirror Method { get; set; }
        public long IlOffset { get; set; }
        public int LineDifference { get; internal set; }
    }
}