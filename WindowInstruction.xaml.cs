using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FnSync
{
    /// <summary>
    /// Interaction logic for WindowNewUser.xaml
    /// </summary>
    public partial class WindowInstruction : Window
    {
        public static WindowInstruction CurrentWindow { get; protected set; } = null;
        public static void NewOne()
        {
            App.FakeDispatcher.Invoke(() =>
            {
                if (CurrentWindow == null)
                {
                    new WindowInstruction().Show();
                }
                else
                {
                    CurrentWindow.Activate();
                }
                return null;
            });
        }

        public WindowInstruction()
        {
            CurrentWindow = this;
            InitializeComponent();
            if (Thread.CurrentThread.CurrentCulture.Name.Equals("zh-CN"))
            {
                DownloadAndroidCompanionCoolApk.Visibility = Visibility.Visible;
            }
#if DEBUG
            DownloadAndroidCompanionCoolApk.Visibility = Visibility.Visible;
#endif
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            CurrentWindow = null;
            if (AlivePhones.Singleton.Count == 0)
            {
                WindowMain.NewOne();
            }
        }

        private void GotIt_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void DownloadAndroidCompanion_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            if (sender is Hyperlink link)
            {
                System.Diagnostics.Process.Start(link.NavigateUri.AbsoluteUri);
            }
        }
    }
}
