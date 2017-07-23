using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using MonoRemoteDebugger.SharedLib.Settings;
using SshFileSync;

namespace MonoRemoteDebugger.SharedLib.SSH
{
    public class SSHDebugger
    {
        public static async Task<string> DeployAndDebugAsync(SshDeltaCopy.Options options, DebugOptions debugOptions)
        {
            return await StartDebuggerAsync(options, debugOptions, true, true);
        }
        
        public static async Task<string> DeployAsync(SshDeltaCopy.Options options, DebugOptions debugOptions)
        {
            return await StartDebuggerAsync(options, debugOptions, true, false);
        }
        
        public static async Task<string> DebugAsync(SshDeltaCopy.Options options, DebugOptions debugOptions)
        {
            return await StartDebuggerAsync(options, debugOptions, false, true);
        }

        private static async Task<string> StartDebuggerAsync(SshDeltaCopy.Options options, DebugOptions debugOptions, bool deploy, bool debug)
        {
            var sb = new StringBuilder();

            using (SshDeltaCopy sshDeltaCopy = new SshDeltaCopy(options))
            {
                if (deploy)
                {
                    sshDeltaCopy.DeployDirectory(options.SourceDirectory, options.DestinationDirectory);
                    var createMdbCommand = sshDeltaCopy.RunSSHCommand($@"find . -regex '.*\(exe\|dll\)' -exec {debugOptions.UserSettings.SSHPdb2mdbCommand} {{}} \;", false);
                    sb.AppendLine(createMdbCommand.Result);
                }
                
                if (debug)
                {
                    var killCommandText = $"kill $(lsof -i | grep 'mono' | grep '\\*:{debugOptions.UserSettings.SSHMonoDebugPort}' | awk '{{print $2}}')";//$"kill $(ps w | grep '[m]ono --debugger-agent=address' | awk '{{print $1}}')";
                    var killCommand = sshDeltaCopy.RunSSHCommand(killCommandText, false);
                    sb.AppendLine(killCommand.Result);

                    var monoDebugCommand = $"mono --debugger-agent=address={IPAddress.Any}:{debugOptions.UserSettings.SSHMonoDebugPort},transport=dt_socket,server=y --debug=mdb-optimizations {debugOptions.TargetExeFileName} {debugOptions.StartArguments} &";
                    var cmd = sshDeltaCopy.CreateSSHCommand(monoDebugCommand);
                    return await Task.Factory.FromAsync(cmd.BeginExecute(), result => cmd.Result);
                }
            }

            return sb.ToString();
        }
    }
}
