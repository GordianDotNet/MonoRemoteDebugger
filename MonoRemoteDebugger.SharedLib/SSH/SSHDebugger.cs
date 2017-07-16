using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SshFileSync;

namespace MonoRemoteDebugger.SharedLib.SSH
{
    public class SSHDebugger
    {
        public static async Task<bool> DeployAndDebug(SshDeltaCopy.Options options, string pdb2mbdCommand, int monoDebugPort, string targetExePath)
        {
            return await StartDebuggerAsync(options, true, true, pdb2mbdCommand, monoDebugPort, targetExePath);
        }
        
        public static async Task<bool> Deploy(SshDeltaCopy.Options options, string pdb2mbdCommand)
        {
            return await StartDebuggerAsync(options, true, false, pdb2mbdCommand, monoDebugPort: 0, targetExePath: string.Empty);
        }
        
        public static async Task<bool> DebugAsync(SshDeltaCopy.Options options, string pdb2mbdCommand, int monoDebugPort, string targetExePath)
        {
            return await StartDebuggerAsync(options, false, true, pdb2mbdCommand, monoDebugPort, targetExePath);
        }

        private static async Task<bool> StartDebuggerAsync(SshDeltaCopy.Options options, bool deploy, bool debug, string pdb2mbdCommand, int monoDebugPort, string targetExePath)
        {
            using (SshDeltaCopy sshDeltaCopy = new SshDeltaCopy(options))
            {
                if (deploy)
                {
                    sshDeltaCopy.DeployDirectory(options.SourceDirectory, options.DestinationDirectory);
                }

                sshDeltaCopy.RunSSHCommand($@"find . -regex '.*\(exe\| dll\)' -exec {pdb2mbdCommand} {{}} \;", false);

                if (debug)
                {
                    var killCommand = $"kill $(ps w | grep '[m]ono --debugger-agent=address' | awk '{{print $1}}')";
                    sshDeltaCopy.RunSSHCommand(killCommand, false);

                    var monoDebugCommand = $"mono --debugger-agent=address={IPAddress.Any}:{monoDebugPort},transport=dt_socket,server=y --debug=mdb-optimizations {targetExePath} &";
                    var cmd = sshDeltaCopy.CreateSSHCommand(monoDebugCommand);
                    return await Task.Factory.FromAsync<bool>(cmd.BeginExecute(), result => cmd.ExitStatus == 0);
                }
            }

            return true;
        }
    }
}
