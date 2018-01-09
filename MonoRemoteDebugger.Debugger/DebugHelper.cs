using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using NLog;

namespace MonoRemoteDebugger.Debugger
{
    public static class DebugHelper
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        internal static void TraceEnteringMethod([CallerMemberName] string callerMember = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            logger.Trace($"Entering: {callerMember} - {Path.GetFileName(callerFilePath)}({callerLineNumber})");

            //MethodBase mth = new StackTrace().GetFrame(1).GetMethod();
            //if (mth.ReflectedType != null)
            //{
            //    string className = mth.ReflectedType.Name;
            //    logger.Trace(className + " (entering) :  " + callerMember);
            //}
        }
    }
}