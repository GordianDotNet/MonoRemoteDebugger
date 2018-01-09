using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using EnvDTE;
using EnvDTE80;
using Microsoft.MIDebugEngine;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using MonoRemoteDebugger.Debugger.DebugEngineHost;
using MonoRemoteDebugger.Debugger.VisualStudio;
using MonoRemoteDebugger.SharedLib;
using MonoRemoteDebugger.SharedLib.Server;
using MonoRemoteDebugger.SharedLib.SSH;
using MonoRemoteDebugger.VSExtension.Settings;
using MonoRemoteDebugger.VSExtension.Views;
using NLog;
using SshFileSync;
using Process = System.Diagnostics.Process;

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
        private MonoVisualStudioExtension _monoExtension;
        private MonoDebugServer server = new MonoDebugServer();

        protected override void Initialize()
        {
            UserSettingsManager.Initialize(this);

            MonoLogger.Setup();

            base.Initialize();
            
            var dte = (DTE)GetService(typeof(DTE));
            _monoExtension = new MonoVisualStudioExtension(dte);

            TryRegisterAssembly();

            Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("/MonoRemoteDebugger.VSExtension;component/Resources/Resources.xaml", UriKind.Relative)
            });
            
            InstallMenu();

            // Workaround: Don't show Visual Studio WPF DataBinding errors in Output window
            System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Critical;
        }

        private void TryRegisterAssembly()
        {
            try
            {
                RegistryKey regKey = Registry.ClassesRoot.OpenSubKey($@"CLSID\{{{AD7Guids.EngineGuid.ToString()}}}");

                if (regKey != null)
                    return; // Already registered

                string location = typeof(AD7Engine).Assembly.Location;

                string regasm = @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe";
                if (!Environment.Is64BitOperatingSystem)
                {
                    regasm = @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe";
                }

                var regasmProcessStartInfo = new ProcessStartInfo(regasm, location);
                regasmProcessStartInfo.Verb = "runas";
                regasmProcessStartInfo.RedirectStandardOutput = true;
                regasmProcessStartInfo.UseShellExecute = false;
                regasmProcessStartInfo.CreateNoWindow = true;

                Process process = Process.Start(regasmProcessStartInfo);
                while (!process.HasExited)
                {
                    string txt = process.StandardOutput.ReadToEnd();
                }

                using (RegistryKey config = VSRegistry.RegistryRoot(__VsLocalRegistryType.RegType_Configuration))
                {
                    MonoRemoteDebuggerInstaller.RegisterDebugEngine(location, config);
                }
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(
                    "Failed finish installation of MonoRemoteDebugger - Please run Visual Studio once as Administrator...",
                    "MonoRemoteDebugger", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        private void InstallMenu()
        {
            var mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (mcs == null)
            {
                logger.Error($"Service {nameof(IMenuCommandService)} not found!");
            }
            
            AddMenuItem(mcs, PackageIds.cmdOpenLogFile, CheckOpenLogFile, OpenLogFile);
            AddMenuItem(mcs, PackageIds.cmdLocalDebugCode, CheckStartupProjects, DebugLocalClicked);
            AddMenuItem(mcs, PackageIds.cmdRemodeDebugCode, CheckStartupProjects, DebugRemoteClicked);
                
            AddMenuItem(mcs, PackageIds.cmdDeployAndDebugOverSSH, CheckStartupProjects, DeployAndDebugOverSSHClicked);
            AddMenuItem(mcs, PackageIds.cmdDeployOverSSH, CheckStartupProjects, DeployOverSSHClicked);
            AddMenuItem(mcs, PackageIds.cmdDebugOverSSH, CheckStartupProjects, DebugOverSSHClicked);
            AddMenuItem(mcs, PackageIds.cmdOpenSSHDebugConfig, CheckStartupProjects, OpenSSHDebugConfigDlg);
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
            else
            {
                MessageBox.Show(
                    $"Logfile {MonoLogger.LoggerPath} not found!",
                    "MonoRemoteDebugger", MessageBoxButton.OK, MessageBoxImage.Error);
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
                {
                    menuCommand.Enabled = ((Array)sb.StartupProjects).Cast<string>().Count() == 1;
                }
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
                // Stop old instance
                server?.Dispose();

                await _monoExtension.BuildSolutionAsync();

                using (server = new MonoDebugServer())
                {
                    server.Start();
                    var settings = UserSettingsManager.Instance.Load();
                    var debugOptions = this._monoExtension.CreateDebugOptions(settings);
                    debugOptions.UserSettings.LastIp = SharedLib.Server.MonoProcess.GetLocalIp().ToString();
                    await _monoExtension.AttachDebugger(debugOptions);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
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
                    await _monoExtension.BuildSolutionAsync();

                    var settings = UserSettingsManager.Instance.Load();
                    var debugOptions = this._monoExtension.CreateDebugOptions(settings);

                    if (dlg.ViewModel.SelectedServer != null)
                    {
                        debugOptions.UserSettings.LastIp = dlg.ViewModel.SelectedServer.IpAddress.ToString();
                        await _monoExtension.AttachDebugger(debugOptions);
                    }
                    else if (!string.IsNullOrWhiteSpace(dlg.ViewModel.ManualIp))
                    {
                        await _monoExtension.AttachDebugger(debugOptions);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex);
                    MessageBox.Show(ex.Message, "MonoRemoteDebugger", MessageBoxButton.OK, MessageBoxImage.Error);
                }
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
            // TODO stop monoRemoteSshDebugTask properly
            try
            {
                if (!deploy && !startDebugger)
                {
                    return true;
                }

                var settings = UserSettingsManager.Instance.Load();

                if (deploy)
                {
                    await _monoExtension.BuildSolutionAsync();
                }

                var debugOptions = _monoExtension.CreateDebugOptions(settings, true);
                
                var options = new SshDeltaCopy.Options()
                {
                    Host = settings.SSHHostIP,
                    Port = settings.SSHPort,
                    Username = settings.SSHUsername,
                    Password = settings.SSHPassword,
                    SourceDirectory = debugOptions.OutputDirectory,
                    DestinationDirectory = settings.SSHDeployPath,
                    RemoveOldFiles = true,
                    PrintTimings = true,
                    RemoveTempDeleteListFile = true,
                };

                if (deploy)
                {
                    await _monoExtension.ConvertPdb2Mdb(options.SourceDirectory, HostOutputWindowEx.WriteLineLaunchError);
                }

                System.Threading.Tasks.Task monoRemoteSshDebugTask;
                if (startDebugger)
                {
                    if (deploy)
                    {
                        monoRemoteSshDebugTask = await SSHDebugger.DeployAndDebugAsync(options, debugOptions, HostOutputWindowEx.WriteLineLaunchError);
                    }
                    else
                    {
                        monoRemoteSshDebugTask = await SSHDebugger.DebugAsync(options, debugOptions, HostOutputWindowEx.WriteLineLaunchError);
                    }

                    _monoExtension.AttachDebuggerToRunningProcess(debugOptions);
                }
                else
                {
                    monoRemoteSshDebugTask = await SSHDebugger.DeployAsync(options, debugOptions, HostOutputWindowEx.WriteLineLaunchError);
                }
                
                await monoRemoteSshDebugTask;
                
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
                server?.Dispose();
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