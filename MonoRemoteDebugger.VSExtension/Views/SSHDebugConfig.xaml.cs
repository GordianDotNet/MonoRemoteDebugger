using System.Windows;

namespace MonoRemoteDebugger.VSExtension.Views
{
    /// <summary>
    ///     Interaktionslogik für DebugOverSSH.xaml
    /// </summary>
    public partial class SSHDebugConfig : Window
    {
        public SSHDebugConfig()
        {
            InitializeComponent();
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            ViewModel = new SSHDebugConfigModel();
            DataContext = ViewModel;
            Closing += (o, e) => ViewModel.SaveSSHDebugConfig();
        }

        public SSHDebugConfigModel ViewModel { get; set; }

        private void Save(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Cancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}