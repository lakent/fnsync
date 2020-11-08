using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
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
    /// Interaction logic for ControlDeviceInfomation.xaml
    /// </summary>
    public partial class ControlDeviceInfomation : UserControl
    {
        public const string MSG_TYPE_REQUEST_PHONE_STATE = "request_phone_state";
        public const string MSG_TYPE_REPLY_PHONE_STATE = "reply_phone_state";

        private void RefreshPhoneStateCallback(string id, string msgType, object msgObject, PhoneClient client)
        {
            if (!(msgObject is JObject msg)) return;

            if (!(DataContext is SavedPhones.Phone phone) || phone.Id != id)
            {
                return;
            }

            if (!msg.ContainsKey("is_charging"))
            {
                ChargingState.Content = "";
            }
            else
            {
                ChargingState.Content =
                    FindResource((bool)msg["is_charging"] ? "Charging" : "NotCharging");
            }

            if (!msg.ContainsKey("battery_percentage"))
            {
                BatteryLevel.Content = "";
            }
            else
            {
                BatteryLevel.Content = $"{(double)msg["battery_percentage"]}%";
            }
        }

        public ControlDeviceInfomation()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;

            PhoneMessageCenter.Singleton.Register(
                null,
                MSG_TYPE_REPLY_PHONE_STATE,
                RefreshPhoneStateCallback,
                true
                );

            LoadDataContext();

            Loaded += (_, __) =>
            {
                Window.GetWindow(this).Closing += (___, ____) => OnClosing();
            };
        }

        private void LoadDataContext()
        {
            if (DataContext is SavedPhones.Phone phone)
            {
                ID.Text = phone.Id;
                IPAddress.Text = phone.LastIp;
                AlivePhones.Singleton[phone.Id]?.SendMsgNoThrow(MSG_TYPE_REQUEST_PHONE_STATE);
            }
            else
            {
                ID.Text = "";
                IPAddress.Text = "";
            }

            ChargingState.Content = "";
            BatteryLevel.Content = "";
        }

        private static void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(sender is ControlDeviceInfomation cdi))
                return;

            cdi.LoadDataContext();
        }

        private void OnClosing()
        {
            PhoneMessageCenter.Singleton.Unregister(
                null,
                MSG_TYPE_REPLY_PHONE_STATE,
                RefreshPhoneStateCallback
                );
        }
    }
}
