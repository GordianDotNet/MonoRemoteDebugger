using System.Diagnostics;
using System.IO;

namespace MonoRemoteDebugger.SharedLib.Server
{
    internal class MonoDesktopProcess : MonoProcess
    {
        private readonly string _targetExe;
        private readonly string _arguments;

        public MonoDesktopProcess(string targetExe, string arguments)
        {
            _targetExe = targetExe;
            _arguments = arguments;
        }

        internal override Process Start(string workingDirectory)
        {
            string monoBin = MonoUtils.GetMonoPath();
            var dirInfo = new DirectoryInfo(workingDirectory);

            string args = GetProcessArgs();
            ProcessStartInfo procInfo = GetProcessStartInfo(workingDirectory, monoBin);
            procInfo.Arguments = $"{args} \"{_targetExe}\" {_arguments}";

            _proc = Process.Start(procInfo);
            RaiseProcessStarted();
            return _proc;
        }
    }
}