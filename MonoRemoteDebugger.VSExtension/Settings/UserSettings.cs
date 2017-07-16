using MonoRemoteDebugger.SharedLib;

namespace MonoRemoteDebugger.VSExtension.Settings
{
    public class UserSettings
    {
        public UserSettings()
        {
            LastIp = "127.0.0.1";
            LastTimeout = 10000;

            SSHHostIP = "127.0.0.1";
            SSHPort = 22;
            SSHUsername = string.Empty;
            SSHPassword = string.Empty;
            SSHDeployPath = "./MonoDebugTemp/";
            SSHMonoDebugPort = GlobalConfig.Current.DebuggerAgentPort;
            SSHPdb2mbdCommand = "pdb2mdb";
        }

        public string LastIp { get; set; }
        public int LastTimeout { get; set; }

        public string SSHHostIP { get; set; }
        public int SSHPort { get; set; }
        public string SSHUsername { get; set; }
        public string SSHPassword { get; set; }
        public string SSHDeployPath { get; set; }
        public int SSHMonoDebugPort { get; set; }
        public string SSHPdb2mbdCommand { get; set; }
    }
}