using System.Net;
using Newtonsoft.Json;

namespace MonoRemoteDebugger.SharedLib.Settings
{
    public class DebugOptions
    {
        public bool UseSSH { get; set; }
        public UserSettings UserSettings { get; set; }        
        public string OutputDirectory { get; set; }
        public string TargetExeFileName { get; set; }
        public string StartArguments { get; set; }
        public string StartupAssemblyPath { get; set; }

        public DebugOptions()
        { }
        
        public string SerializeToJson()
        {
            string json = JsonConvert.SerializeObject(this);
            return json;
        }

        public static DebugOptions DeserializeFromJson(string json)
        {
            var result = JsonConvert.DeserializeObject<DebugOptions>(json);
            return result;
        }

        public IPAddress GetHostIP()
        {
            var hostIp = IPAddress.Loopback;
            if (UseSSH)
            {
                hostIp = IPAddress.Parse(UserSettings.SSHHostIP);
            }
            else
            {
                hostIp = IPAddress.Parse(UserSettings.LastIp);
            }
            return hostIp;
        }

        public int GetMonoDebugPort()
        {
            return UseSSH ? UserSettings.SSHMonoDebugPort : GlobalConfig.Current.DebuggerAgentPort;            
        }
    }
}
