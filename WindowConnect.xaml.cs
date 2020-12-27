using Newtonsoft.Json.Linq;
using QRCoder;
using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace FnSync
{
    /// <summary>
    /// Interaction logic for Connect.xaml
    /// </summary>

    public partial class WindowConnect : Window
    {
        public static WindowConnect CurrentWindow { get; protected set; } = null;
        private bool Connecting = false;

        public static void NewOne()
        {
            App.FakeDispatcher.Invoke(() =>
            {
                if (CurrentWindow == null)
                {
                    new WindowConnect().Show();
                }
                else
                {
                    CurrentWindow.Activate();
                }

                return null;
            });
        }

        ////////////////////////////////////////////////////////

        public WindowConnect()
        {
            CurrentWindow = this;
            InitializeComponent();
            ConnectionCode.Focus();

            PhoneMessageCenter.Singleton.Register(
                null,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_CONNECTED,
                CloseIfConnecting,
                true
            );

            PhoneMessageCenter.Singleton.Register(
                null,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_CONNECTION_FAILED,
                RefreshQRCodeOnFailedConnection,
                true
            );

            RefreshQrCode();
        }

        private void RefreshQrCode()
        {
            string token = Guid.NewGuid().ToString();
            ClientListener.Singleton.Code = token;

            JObject helloJson = HandShake.MakeHelloJson(
                false,
                token,
                null
            );

            helloJson["ips"] = Utils.GetAllInterface(false);
            /*
            helloJson["i"] = Utils.GetAllInterface(false);
            helloJson["l"] = Utils.GetAllInterface(true);
            */

            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(
                helloJson.ToString(Newtonsoft.Json.Formatting.None),
                QRCodeGenerator.ECCLevel.L
            );
            QRCode qrCode = new QRCode(qrCodeData);
            Bitmap qrCodeImage = qrCode.GetGraphic(20);

            using (MemoryStream memory = new MemoryStream())
            {
                qrCodeImage.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                ByQRCode.Source = bitmapimage;
            }
        }

        private void CloseIfConnecting(string id, string msgType, object msg, PhoneClient client)
        {
            if (Connecting)
            {
                Close();
            }
        }

        private void RefreshQRCodeOnFailedConnection(string id, string msgType, object msg, PhoneClient client)
        {
            RefreshQrCode();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            PhoneMessageCenter.Singleton.Unregister(
                null,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_CONNECTED,
                CloseIfConnecting
            );

            PhoneMessageCenter.Singleton.Unregister(
                null,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_CONNECTION_FAILED,
                RefreshQRCodeOnFailedConnection
            );

            CurrentWindow = null;

            base.OnClosing(e);
            if (AlivePhones.Singleton.Count == 0)
            {
                App.ExitApp();
            }
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
                ClientListener.Singleton.StartReachInitiatively(code, false, null);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            ClientListener.Singleton.StopReach();
            SwitchCodeConnectiongState(false);
        }

        private void OnKeyDownHandler(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Return:
                    if (ConnectButton.Visibility == Visibility.Visible)
                    {
                        ConnectButton_Click(sender, e);
                    }
                    break;

                case Key.Escape:
                    Close();
                    break;

                default:
                    break;
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            ContextMenu contextMenu = button.ContextMenu;
            contextMenu.PlacementTarget = button;
            contextMenu.IsOpen = true;
            e.Handled = true;
        }

        private void Menu_Instruction_Click(object sender, RoutedEventArgs e)
        {
            WindowInstruction.NewOne();
        }

        private void Menu_Setting_Click(object sender, RoutedEventArgs e)
        {
            WindowSetting.NewOne();
        }

        private void SwitchToEnter_Click(object sender, RoutedEventArgs e)
        {
            ByConnCode.Visibility = Visibility.Visible;
            SwitchToEnter.Visibility = Visibility.Collapsed;
            ByQRCode.Visibility = Visibility.Collapsed;
            SwitchToScan.Visibility = Visibility.Visible;

            ConnectionCode.Focus();
        }

        private void SwitchToScan_Click(object sender, RoutedEventArgs e)
        {
            CancelButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            ByConnCode.Visibility = Visibility.Hidden;
            SwitchToEnter.Visibility = Visibility.Visible;
            ByQRCode.Visibility = Visibility.Visible;
            SwitchToScan.Visibility = Visibility.Collapsed;
        }

        public bool Scanning()
        {
            return ByQRCode.Visibility == Visibility.Visible;
        }

        private void ByQRCode_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            RefreshQrCode();
        }
    }
}
