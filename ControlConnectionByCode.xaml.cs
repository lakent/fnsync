using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FnSync
{
    /// <summary>
    /// Interaction logic for ControlConnectionByCode.xaml
    /// </summary>
    public partial class ControlConnectionByCode : UserControlExtension
    {
        private bool Connecting = false;

        public ControlConnectionByCode()
        {
            InitializeComponent();
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            String code = String.Copy(ConnectionCode.Text);
            if (string.IsNullOrEmpty(code))
            {
                MessageBox.Show(
                    (string)FindResource("PleaseEnterConnectionCode"),
                    (string)FindResource("Prompt"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation
                    );
            }
            else
            {
                SwitchCodeConnectiongState(true);
                PhoneListener.Singleton.StartReachInitiatively(code, false, null);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            PhoneListener.Singleton.StopReach();
            SwitchCodeConnectiongState(false);
        }

        private void SwitchCodeConnectiongState(bool connecting)
        {
            Connecting = connecting;
            ConnectButton.Visibility = connecting ? Visibility.Collapsed : Visibility.Visible;
            CancelButton.Visibility = connecting ? Visibility.Visible : Visibility.Collapsed;
            PromptAccept.Visibility = connecting ? Visibility.Visible : Visibility.Hidden;
            Progress.IsIndeterminate = connecting;
            ConnectionCode.IsReadOnly = connecting;
        }
    }
}
