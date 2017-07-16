using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
using Microsoft.Win32;
using MonoRemoteDebugger.SharedLib;
using MonoRemoteDebugger.SharedLib.Server;
using MonoRemoteDebugger.Debugger;
using MonoRemoteDebugger.VSExtension.Settings;
using MonoRemoteDebugger.VSExtension.Views;
using NLog;
using Process = System.Diagnostics.Process;
using Microsoft.MIDebugEngine;
using System.Threading.Tasks;
using SshFileSync;
using MonoRemoteDebugger.SharedLib.SSH;

namespace MonoRemoteDebugger.VSExtension
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", Vsix.Version, IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad("f1536ef8-92ec-443c-9ed7-fdadf150da82")]
    [Guid(PackageGuids.guidMonoDebugger_VS2013PkgString)]
    public sealed class VSPackage : Package, IDisposable
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private MonoVisualStudioExtension monoExtension;
        private MonoDebugServer server = new MonoDebugServer();

        protected override void Initialize()
        {
            var settingsManager = new ShellSettingsManager(this);
            var configurationSettingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);
            UserSettingsManager.Initialize(configurationSettingsStore);
            MonoLogger.Setup();
            base.Initialize();
            var dte = (DTE)GetService(typeof(DTE));
            monoExtension = new MonoVisualStudioExtension(dte);
            TryRegisterAssembly();
            
            Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("/MonoRemoteDebugger.VSExtension;component/Resources/Resources.xaml", UriKind.Relative)
            });

            var mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            InstallMenu(mcs);
        }

        private void TryRegisterAssembly()
        {
            try
            {
                RegistryKey regKey = Registry.ClassesRoot.OpenSubKey(@"CLSID\{8BF3AB9F-3864-449A-93AB-E7B0935FC8F5}");

                if (regKey != null)
                    return;

                string location = typeof(DebuggedProcess).Assembly.Location;

                string regasm = @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe";
                if (!Environment.Is64BitOperatingSystem)
                    regasm = @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe";

                var p = new ProcessStartInfo(regasm, location);
                p.Verb = "runas";
                p.RedirectStandardOutput = true;
                p.UseShellExecute = false;
                p.CreateNoWindow = true;

                Process proc = Process.Start(p);
                while (!proc.HasExited)
                {
                    string txt = proc.StandardOutput.ReadToEnd();
                }

                using (RegistryKey config = VSRegistry.RegistryRoot(__VsLocalRegistryType.RegType_Configuration))
                {
                    MonoRemoteDebuggerInstaller.RegisterDebugEngine(location, config);
                }
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(
                    "Failed finish installation of MonoRemoteDebugger - Please run Visual Studio once als Administrator...",
                    "MonoRemoteDebugger", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        private void InstallMenu(OleMenuCommandService mcs)
        {
            if (mcs != null)
            {
                AddMenuItem(mcs, PackageIds.cmdOpenLogFile, CheckOpenLogFile, OpenLogFile);
                AddMenuItem(mcs, PackageIds.cmdLocalDebugCode, CheckStartupProjects, DebugLocalClicked);
                AddMenuItem(mcs, PackageIds.cmdRemodeDebugCode, CheckStartupProjects, DebugRemoteClicked);
                
                AddMenuItem(mcs, PackageIds.cmdDeployAndDebugOverSSH, CheckStartupProjects, DeployAndDebugOverSSHClicked);                
                AddMenuItem(mcs, PackageIds.cmdDeployOverSSH, CheckStartupProjects, DeployOverSSHClicked);                
                AddMenuItem(mcs, PackageIds.cmdDebugOverSSH, CheckStartupProjects, DebugOverSSHClicked);                
                AddMenuItem(mcs, PackageIds.cmdOpenSSHDebugConfig, CheckStartupProjects, OpenSSHDebugConfigDlg);                
            }
        }

        private OleMenuCommand AddMenuItem(OleMenuCommandService mcs, int cmdCode, EventHandler check, EventHandler action)
        {
            var commandID = new CommandID(PackageGuids.guidMonoDebugger_VS2013CmdSet, cmdCode);
            var menuCommand = new OleMenuCommand(action, commandID);
            menuCommand.BeforeQueryStatus += check;
            mcs.AddCommand(menuCommand);
            return menuCommand;
        }

        private void CheckOpenLogFile(object sender, EventArgs e)
        {
            var menuCommand = sender as OleMenuCommand;
            if (menuCommand != null)
            {
                menuCommand.Enabled = File.Exists(MonoLogger.LoggerPath);
            }
        }

        private void OpenLogFile(object sender, EventArgs e)
        {
            if (File.Exists(MonoLogger.LoggerPath))
            {
                Process.Start(MonoLogger.LoggerPath);
            }
        }

        private void CheckStartupProjects(object sender, EventArgs e)
        {
            var menuCommand = sender as OleMenuCommand;
            if (menuCommand != null)
            {
                var dte = GetService(typeof(DTE)) as DTE;
                var sb = (SolutionBuild2)dte.Solution.SolutionBuild;
                menuCommand.Visible = sb.StartupProjects != null;
                if (menuCommand.Visible)
                    menuCommand.Enabled = ((Array)sb.StartupProjects).Cast<string>().Count() == 1;
            }
        }

        private void DebugLocalClicked(object sender, EventArgs e)
        {
            StartLocalServer();
        }

        private async void StartLocalServer()
        {
            try
            {
                System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Critical;

                if (server != null)
                {
                    server.Stop();
                    server = null;
                }

                BuildSolution();

                using (server = new MonoDebugServer())
                {
                    server.Start();
                    await monoExtension.AttachDebugger(MonoProcess.GetLocalIp().ToString());
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                if (server != null)
                    server.Stop();
                MessageBox.Show(ex.Message, "MonoRemoteDebugger", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DebugRemoteClicked(object sender, EventArgs e)
        {
            OpenRemoteConfigDlg();
        }

        private async void OpenRemoteConfigDlg()
        {
            var dlg = new ServersFound();

            if (dlg.ShowDialog().GetValueOrDefault())
            {
                try
                {
                    BuildSolution();

                    int timeout = dlg.ViewModel.AwaitTimeout;
                    
                    if (dlg.ViewModel.SelectedServer != null)
                        await monoExtension.AttachDebugger(dlg.ViewModel.SelectedServer.IpAddress.ToString(), timeout);
                    else if (!string.IsNullOrWhiteSpace(dlg.ViewModel.ManualIp))
                        await monoExtension.AttachDebugger(dlg.ViewModel.ManualIp, timeout);
                }
                catch (Exception ex)
                {
                    logger.Error(ex);
                    MessageBox.Show(ex.Message, "MonoRemoteDebugger", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BuildSolution()
        {
            var failedBuilds = monoExtension.BuildSolution();
            if (failedBuilds > 0)
            {
                throw new Exception($"Build failed! Project failed to build: {failedBuilds}.");
            }
        }

        private async void DeployAndDebugOverSSHClicked(object sender, EventArgs e)
        {
            await DeployAndRunCommandOverSSH(true, true);
        }
        
        private async void DeployOverSSHClicked(object sender, EventArgs e)
        {
            await DeployAndRunCommandOverSSH(true, false);
        }

        private async void DebugOverSSHClicked(object sender, EventArgs e)
        {
            await DeployAndRunCommandOverSSH(false, true);
        }
        
        private void OpenSSHDebugConfigDlg(object sender, EventArgs e)
        {
            var dlg = new SSHDebugConfig();

            if (dlg.ShowDialog().GetValueOrDefault())
            {
                // Saved
            }
        }

        private async Task<bool> DeployAndRunCommandOverSSH(bool deploy, bool startDebugger)
        {
            // TODO error handling
            // TODO show ssh output stream
            try
            {
                if (!deploy && !startDebugger)
                {
                    return true;
                }

                var settings = UserSettingsManager.Instance.Load();

                await System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    if (deploy)
                    {
                        BuildSolution();
                    }
                });

                string startupAssemblyPath = monoExtension.GetStartupAssemblyPath();
                string targetExeFileName = Path.GetFileName(startupAssemblyPath);
                string outputDirectory = Path.GetDirectoryName(startupAssemblyPath);

                var options = new SshDeltaCopy.Options()
                {
                    Host = settings.SSHHostIP,
                    Port = settings.SSHPort,
                    Username = settings.SSHUsername,
                    Password = settings.SSHPassword,
                    SourceDirectory = outputDirectory,
                    DestinationDirectory = settings.SSHDeployPath,
                    RemoveOldFiles = true,
                    PrintTimings = true,
                    RemoveTempDeleteListFile = true,
                };

                if (startDebugger)
                {
                    var arguments = monoExtension.GetStartArguments();

                    if (deploy)
                    {
                        var asyncTask = SSHDebugger.DeployAndDebug(options, settings.SSHPdb2mbdCommand, settings.SSHMonoDebugPort, targetExeFileName, arguments);
                    }
                    else
                    {
                        var asyncTask = SSHDebugger.DebugAsync(options, settings.SSHPdb2mbdCommand, settings.SSHMonoDebugPort, targetExeFileName, arguments);
                    }
                }
                else
                {
                    var asyncTask = SSHDebugger.Deploy(options, settings.SSHPdb2mbdCommand);
                }

                if (startDebugger)
                {
                    monoExtension.AttachDebuggerToRunningProcess(settings.SSHHostIP, settings.SSHMonoDebugPort);
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                MessageBox.Show(ex.Message, "MonoRemoteDebugger", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return false;
        }
        
        #region IDisposable Members
        private bool disposed = false;
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (this.disposed)
                return;

            if (disposing)
            {
                //Dispose managed resources
                this.server.Dispose();
            }

            //Dispose unmanaged resources here.

            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~VSPackage()
        {
            Dispose(false);
        }
        #endregion

    }
}