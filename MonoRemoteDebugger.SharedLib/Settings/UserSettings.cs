using Newtonsoft.Json;

namespace MonoRemoteDebugger.SharedLib.Settings
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
            SSHPdb2mdbCommand = "pdb2mdb";
            SSHDebugConnectionTimeout = 20;
        }

        public string SerializeToJson()
        {
            string json = JsonConvert.SerializeObject(this);
            return json;
        }

        public static UserSettings DeserializeFromJson(string json)
        {
            var result = JsonConvert.DeserializeObject<UserSettings>(json);
            return result;
        }

        public string LastIp { get; set; }
        public int LastTimeout { get; set; }

        public string SSHHostIP { get; set; }
        public int SSHPort { get; set; }
        public string SSHUsername { get; set; }
        public string SSHPassword { get; set; }
        public string SSHDeployPath { get; set; }
        public int SSHMonoDebugPort { get; set; }
        public string SSHPdb2mdbCommand { get; set; }
        public int SSHDebugConnectionTimeout { get; set; }
    }
}