using System.Reflection;
using Microsoft.Win32;
using MonoRemoteDebugger.Debugger.VisualStudio;
using Mono.Debugging.VisualStudio;

namespace MonoRemoteDebugger.VSExtension
{
    public static class MonoRemoteDebuggerInstaller
    {
        private const string ENGINE_PATH = @"AD7Metrics\Engine\";
        private const string PORTSUPPLIER_PATH = @"AD7Metrics\PortSupplier\";
        private const string CLSID_PATH = @"CLSID\";

        public static void RegisterDebugEngine(string engineDllLocation, RegistryKey rootKey)
        {
            using (RegistryKey engine = rootKey.OpenSubKey(ENGINE_PATH, true))
            {
                string engineGuid = AD7Guids.EngineGuid.ToString("B").ToUpper();
                using (RegistryKey engineKey = engine.CreateSubKey(engineGuid))
                {
                    engineKey.SetValue("CLSID", AD7Guids.EngineGuid.ToString("B").ToUpper());
                    engineKey.SetValue("ProgramProvider", AD7Guids.ProgramProviderGuid.ToString("B").ToUpper());
                    engineKey.SetValue("Attach", 1, RegistryValueKind.DWord); // Check 0?
                    engineKey.SetValue("AddressBP", 0, RegistryValueKind.DWord);
                    engineKey.SetValue("AutoSelectPriority", 4, RegistryValueKind.DWord);
                    engineKey.SetValue("CallstackBP", 1, RegistryValueKind.DWord);
                    engineKey.SetValue("Name", AD7Guids.EngineName);
                    engineKey.SetValue("PortSupplier", AD7Guids.ProgramProviderGuid.ToString("B").ToUpper());
                    engineKey.SetValue("AlwaysLoadLocal", 1, RegistryValueKind.DWord);
                    engineKey.SetValue("Disassembly", 0, RegistryValueKind.DWord);
                    engineKey.SetValue("RemotingDebugging", 0, RegistryValueKind.DWord);
                    engineKey.SetValue("Exceptions", 1, RegistryValueKind.DWord); // Check 0?
                }
            }
            using (RegistryKey engine = rootKey.OpenSubKey(PORTSUPPLIER_PATH, true))
            {
                string portSupplierGuid = AD7Guids.ProgramProviderGuid.ToString("B").ToUpper();
                using (RegistryKey portSupplierKey = engine.CreateSubKey(portSupplierGuid))
                {
                    portSupplierKey.SetValue("CLSID", AD7Guids.ProgramProviderGuid.ToString("B").ToUpper());
                    portSupplierKey.SetValue("Name", AD7Guids.EngineName);
                }
            }

            using (RegistryKey clsid = rootKey.OpenSubKey(CLSID_PATH, true))
            {
                using (RegistryKey clsidKey = clsid.CreateSubKey(AD7Guids.EngineGuid.ToString("B").ToUpper()))
                {
                    clsidKey.SetValue("Assembly", Assembly.GetExecutingAssembly().GetName().Name);
                    switch (AD7Guids.UseAD7Engine)
                    {
                        case EngineType.AD7Engine:
                            clsidKey.SetValue("Class", typeof(AD7Engine).FullName);
                            break;
                        case EngineType.XamarinEngine:
                            clsidKey.SetValue("Class", typeof(XamarinEngine).FullName);
                            break;
                    }
                    clsidKey.SetValue("InprocServer32", @"c:\windows\system32\mscoree.dll");
                    clsidKey.SetValue("CodeBase", engineDllLocation);
                }

                using (RegistryKey programProviderKey = clsid.CreateSubKey(AD7Guids.ProgramProviderGuid.ToString("B").ToUpper()))
                {
                    programProviderKey.SetValue("Assembly", Assembly.GetExecutingAssembly().GetName().Name);
                    switch (AD7Guids.UseAD7Engine)
                    {
                        case EngineType.AD7Engine:
                            programProviderKey.SetValue("Class", typeof(AD7ProgramProvider).FullName);
                            break;
                        case EngineType.XamarinEngine:
                            programProviderKey.SetValue("Class", typeof(XamarinPortSupplier).FullName);
                            break;
                    }
                    programProviderKey.SetValue("InprocServer32", @"c:\windows\system32\mscoree.dll");
                    programProviderKey.SetValue("CodeBase", engineDllLocation);
                }
            }
        }
    }
}