using Newtonsoft.Json.Linq;
using QRCoder;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace FnSync.ViewModel.ControlConnecting
{
    public partial class ViewModel : INotifyPropertyChanged, IDisposable
    {
        private static string GetAdditionalIPs()
        {
            string[] ips = MainConfig.Config.AdditionalIPs.Split(new char[] { ';', '|' });
            StringBuilder Builder = new StringBuilder();

            foreach (string ip in ips)
            {
                string ipTrimed = ip.Trim();
                _ = Builder.Append(ipTrimed).Append("|");
            }

            if (Builder.EndsWith("|"))
            {
                _ = Builder.Remove(Builder.Length - 1, 1);
                _ = Builder.Insert(0, "|");
            }

            return Builder.ToString();
        }

        private static ImageSource GenerateQrCode()
        {
            string token = Guid.NewGuid().ToString();
            PhoneListener.Singleton.Code = token;

            JObject helloJson = HandShake.MakeHelloJson(
                false,
                token,
                null
            );

            helloJson["ips"] = Utils.GetAllInterface(false) + GetAdditionalIPs();
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

            ImageSource result = qrCodeImage.ToImageSource();
            qrCodeImage.Dispose();

            return result;
        }

        private ImageSource _qrcode = null;

        public event PropertyChangedEventHandler PropertyChanged;

        public ImageSource QRCode
        {
            get => _qrcode;
            set
            {
                _qrcode = GenerateQrCode();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("QRCode"));
            }
        }

        private bool _isConnectingByCode = false;

        private void IsConnectingByCodeFalse(string id, string msgType, object msg, PhoneClient client)
        {
            IsConnectingByCode = false;
        }

        public bool IsConnectingByCode
        {
            get => _isConnectingByCode;

            set
            {
                if (_isConnectingByCode != value)
                {
                    _isConnectingByCode = value;

                    if (value && !string.IsNullOrWhiteSpace(ConnectionCode))
                    {
                        PhoneMessageCenter.Singleton.Register(
                            null,
                            PhoneMessageCenter.MSG_FAKE_TYPE_ON_CONNECTED,
                            IsConnectingByCodeFalse,
                            true
                            );

                        PhoneListener.Singleton.StartReachInitiatively(ConnectionCode, false, null);
                    } else
                    {
                        PhoneListener.Singleton.StopReach();

                        PhoneMessageCenter.Singleton.Unregister(
                            null,
                            PhoneMessageCenter.MSG_FAKE_TYPE_ON_CONNECTED,
                            IsConnectingByCodeFalse
                            );
                    }

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsConnectingByCode"));
                }
            }
        }

        private string _connectionCode = "";

        public string ConnectionCode
        {
            get { return _connectionCode; }
            set
            {
                _connectionCode = value.Trim();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ConnectionCode"));
            }
        }

        public ICommand NewQRCodeCommand { get; private set; }
        public ICommand NavigateCommand { get; } = new NavigateCommand();
        public ICommand ConnectByCodeCommand { get; private set; }
        public ICommand ConnectByCodeCancelCommand { get; private set; }

        public ViewModel()
        {
            NewQRCodeCommand = new NewQRCodeCommand(this);
            ConnectByCodeCommand = new ConnectByCodeCommand(this);
            ConnectByCodeCancelCommand = new ConnectByCodeCancelCommand(this);
        }

        public void Dispose()
        {
            IsConnectingByCode = false;
        }

    }

    internal class NewQRCodeCommand : ICommand
    {
        private readonly ViewModel ViewModel;
        public NewQRCodeCommand(ViewModel viewModel)
        {
            this.ViewModel = viewModel;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            this.ViewModel.QRCode = null; // Regenerated QR code
        }
    }

    internal class NavigateCommand : ICommand
    {
        public NavigateCommand()
        {
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            if (parameter is Hyperlink link)
            {
                _ = System.Diagnostics.Process.Start(link.NavigateUri.AbsoluteUri);
            }
            else if (parameter is Uri uri)
            {
                _ = System.Diagnostics.Process.Start(uri.AbsoluteUri);
            }
            else if (parameter is string text)
            {
                _ = System.Diagnostics.Process.Start(text);
            }
        }
    }

    internal class ConnectByCodeCommand : ICommand
    {
        private readonly ViewModel ViewModel;
        public ConnectByCodeCommand(ViewModel viewModel)
        {
            this.ViewModel = viewModel;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            this.ViewModel.IsConnectingByCode = true;
        }
    }

    internal class ConnectByCodeCancelCommand : ICommand
    {
        private readonly ViewModel ViewModel;
        public ConnectByCodeCancelCommand(ViewModel viewModel)
        {
            this.ViewModel = viewModel;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            this.ViewModel.IsConnectingByCode = false;
        }
    }

    [ValueConversion(typeof(object), typeof(Visibility))]
    public class EnableOnNonEmptyStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is string str))
            {
                throw new ArgumentException();
            }

            bool inverse = Equals("true", parameter);
            return string.IsNullOrWhiteSpace(str) == inverse;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

