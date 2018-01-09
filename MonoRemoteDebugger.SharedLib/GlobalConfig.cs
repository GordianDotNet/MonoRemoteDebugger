using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoRemoteDebugger.SharedLib
{
    public class GlobalConfig
    {
        #region Current Members
        private static readonly GlobalConfig _Current = new GlobalConfig();
        public static GlobalConfig Current
        {
            get
            {
                return _Current;
            }
        }
        #endregion

        public int ServerPort { get; set; } = AppSettings.Get("ServerPort", 13001);
        public int DebuggerAgentPort { get; set; } = AppSettings.Get("DebuggerAgentPort", 11000);
        public string LibMonoApplicationPath { get; set; } = AppSettings.Get("LibMonoApplicationPath", "");
        public string ShellScriptInstallPath { get; set; } = AppSettings.Get("ShellScriptInstallPath", "");
        public int SkipLastUsedContentDirectories { get; set; } = AppSettings.Get("SkipLastUsedContentDirectories", 3);
    }

    public class AppSettings
    {
        public static T Get<T>(string key, T defaultValue)
        {
            string text = ConfigurationManager.AppSettings.Get(key);
            bool flag = text == null;
            T result;
            if (flag)
            {
                result = defaultValue;
            }
            else
            {
                result = (T)((object)Convert.ChangeType(text, typeof(T)));
            }
            return result;
        }
    }
}
