using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace FnSync
{
    /// <summary>
    /// Interaction logic for ControlConnectionByQR.xaml
    /// </summary>
    public partial class ControlConnectionByQR : UserControlExtension
    {
        private readonly ViewModel.ControlConnecting.ViewModel ViewModel = new();

        public ControlConnectionByQR()
        {
            this.DataContext = this.ViewModel;

            InitializeComponent();

            if (Thread.CurrentThread.CurrentCulture.Name.Equals("zh-CN"))
            {
                DownloadAndroidCompanionCoolApk.Visibility = Visibility.Visible;
            }
#if DEBUG
            DownloadAndroidCompanionCoolApk.Visibility = Visibility.Visible;
#endif
        }

        public override async void OnShow()
        {
            await Task.Delay(500);
            this.ViewModel.QRCode = null; // Regenerated QR code
        }

        protected override void OnClosing()
        {
            this.ViewModel.Dispose();
        }

        private void ConnectionCode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (ConnectByCode.IsEnabled)
                {
                    ConnectByCode.Command?.Execute(null);
                }
            }
        }
    }
}

