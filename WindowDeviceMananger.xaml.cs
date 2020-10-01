using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using Windows.Data.Text;

namespace FnSync
{
    static class RichTextBoxExtension
    {
        public static Object GetSelectedHistory(this RichTextBox box)
        {
            if (!box.Selection.IsEmpty)
            {
                List<JObject> ret = new List<JObject>();

                Paragraph begin = box.Selection.Start.Paragraph;
                Paragraph end = box.Selection.End.Paragraph;

                if (begin == null || end == null)
                {
                    return null;
                }

                foreach (Block block in box.Document.Blocks)
                {
                    if (ret.Any() || block == begin)
                    {
                        if (block.Tag is JObject json)
                        {
                            ret.Add(json);
                        }
                    }

                    if (block == end)
                    {
                        break;
                    }
                }

                if (ret.Any())
                {
                    return ret;
                }
                else
                {
                    return null;
                }
            }
            else if (box.CaretPosition.Paragraph != null)
            {
                return box.CaretPosition.Paragraph.Tag;
            }
            else
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Interaction logic for WindowDeviceMananger.xaml
    /// </summary>
    public partial class WindowDeviceMananger : Window
    {
        public const string MSG_TYPE_REQUEST_PHONE_STATE = "request_phone_state";
        public const string MSG_TYPE_REPLY_PHONE_STATE = "reply_phone_state";

        public static WindowDeviceMananger CurrentWindow { get; protected set; } = null;
        private static readonly Object ExecutionLock = new object();
        public static void NewOne(string id)
        {
            Application.Current.Dispatcher.InvokeAsyncCatchable(() =>
            {
                if (CurrentWindow == null)
                {
                    WindowDeviceMananger window = new WindowDeviceMananger();
                    window.Show();
                    if (id != null)
                    {
                        window.SelectDevice(id);
                        window.DeviceList.Focus();
                    }
                }
                else
                {
                    CurrentWindow.Activate();
                    if (id != null)
                    {
                        CurrentWindow.SelectDevice(id);
                        CurrentWindow.DeviceList.Focus();
                    }
                }
            });
        }

        public ICommand MarkAsImportantCommand { get; private set; }

        public WindowDeviceMananger()
        {
            CurrentWindow = this;
            InitializeComponent();
            PromptToSelectDevice.Visibility = Visibility.Visible;
            DeviceList.ItemsSource = SavedPhones.Singleton.PhoneList;
            Count.DataContext = SavedPhones.Singleton.PhoneList;
            HistoryBox.DataContext = this;
            MarkAsImportantCommand = new MarkAsImportantCommand(HistoryBox);
            PhoneMessageCenter.Singleton.Register(
                null,
                MSG_TYPE_REPLY_PHONE_STATE,
                RefreshPhoneStateCallback,
                true
                );
        }

        private void SelectDevice(string id)
        {
            DeviceList.SelectedItem = SavedPhones.Singleton[id];
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            PhoneMessageCenter.Singleton.Unregister(null, MSG_TYPE_REPLY_PHONE_STATE, RefreshPhoneStateCallback);
            CurrentWindow = null;
        }

        private void Menu_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (DeviceList.SelectedItem is SavedPhones.Phone item)
            {
                if (MessageBox.Show(
                        (string)FindResource("ConfirmDeletion"),
                        (string)FindResource("Delete"),
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    ) == MessageBoxResult.Yes)
                {
                    SavedPhones.Singleton.Remove(item);
                }
            }
        }

        private void DeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TabStatus.DataContext = null;
            TabNotificationHistory.DataContext = null;

            if (DeviceList.SelectedItem == null)
            {
                PromptToSelectDevice.Visibility = Visibility.Visible;
            }
            else
            {
                PromptToSelectDevice.Visibility = Visibility.Collapsed;
                DeviceInfoTabs_SelectionChanged(DeviceInfoTabs, null);
            }
        }

        private void DeviceInfoTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e != null && !(e.Source is TabControl))
            {
                return;
            }

            if (!(DeviceList.SelectedItem is SavedPhones.Phone item))
            {
                return;
            }

            if (TabStatus.DataContext == null && TabStatus.IsSelected)
            {
                IPAddress.Content = item.LastIp;
                if (AlivePhones.Singleton.Contains(item.Id))
                {
                    PhoneClient client = AlivePhones.Singleton[item.Id];
                    client.SendMsgNoThrow(MSG_TYPE_REQUEST_PHONE_STATE);
                }
            }
            else if (TabNotificationHistory.DataContext == null && TabNotificationHistory.IsSelected)
            {
                SavedPhones.HistoryReader reader = new SavedPhones.HistoryReader(item.Id);
                TabNotificationHistory.DataContext = reader;

                HistoryDateChoose.ItemsSource = null;
                var list = reader.AllDates;
                HistoryDateChoose.ItemsSource = list;

                if (list.Any())
                {
                    HistoryDateChoose.SelectedIndex = list.Count - 1;
                    DeleteHistory.IsEnabled = true;
                }
                else
                {
                    HistoryBox.Document.Blocks.Clear();
                    HistoryBox.AppendText((string)FindResource("NoHistory"));
                    DeleteHistory.IsEnabled = false;
                }
            }
        }

        private void HistoryDateChoose_SelectionChanged(object _, SelectionChangedEventArgs e)
        {
            e.Handled = true;

            if (HistoryDateChoose.DataContext == null)
            {
                return;
            }

            Paragraph MakeParagraphForNotification(JObject ln)
            {
                string time = DateTimeOffset.FromUnixTimeMilliseconds((long)(ln["time"])).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
                string appName = "";
                if (ln.ContainsKey("appname"))
                {
                    appName = (string)ln["appname"];
                }
                else if (ln.ContainsKey("pkgname"))
                {
                    appName = (string)ln["pkgname"];
                }

                var paragraph = new Paragraph();
                paragraph.Inlines.Add(
                    new Bold(new Run(
                        $"{time} {appName}\n"
                    ))
                );
                paragraph.Inlines.Add(
                    new Run(
                        $"{(string)(ln["title"])}\n" +
                        $"{(string)(ln["text"])}"
                    )
                );
                paragraph.Tag = ln;

                return paragraph;
            }

            Paragraph MakeParagraphForTextCast(JObject ln)
            {
                string time = DateTimeOffset.FromUnixTimeMilliseconds((long)(ln["time"])).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");

                var paragraph = new Paragraph();
                paragraph.Inlines.Add(
                    new Bold(new Run(
                        $"{time} {Casting.TEXT_RECEIVED}\n"
                    ))
                );
                paragraph.Inlines.Add(
                    new Run(
                        $"{(string)(ln["text"])}"
                    )
                );
                paragraph.Tag = ln;

                return paragraph;
            }

            if (!(HistoryDateChoose.DataContext is SavedPhones.HistoryReader reader))
            {
                return;
            }

            FlowDocument flowDocument = new FlowDocument();

            try
            {
                reader.SetCurrent(HistoryDateChoose.SelectedItem as String);
                JObject line;
                while ((line = reader.ReadLine()) != null)
                {
                    Paragraph paragraph;

                    switch ((string)line[PhoneClient.MSG_TYPE_KEY])
                    {
                        case Casting.MSG_TYPE_TEXT_CAST:
                            paragraph = MakeParagraphForTextCast(line);
                            break;

                        case PhoneMessageCenter.MSG_TYPE_NEW_NOTIFICATION:
                            paragraph = MakeParagraphForNotification(line);
                            break;

                        default:
                            goto case PhoneMessageCenter.MSG_TYPE_NEW_NOTIFICATION;
                    }

                    flowDocument.Blocks.Add(paragraph);
                }
            }
            catch (Exception ee)
            {

            }
            finally
            {
                reader.Dispose();
            }

            HistoryBox.Document = flowDocument;
            HistoryBox.ScrollToEnd();
        }

        private void DeleteHistory_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryDateChoose.DataContext == null)
            {
                return;
            }
            if (MessageBox.Show(
                    (string)FindResource("ConfirmDeletion"),
                    (string)FindResource("Delete"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                ) == MessageBoxResult.Yes)
            {
                if (HistoryDateChoose.DataContext is SavedPhones.HistoryReader reader)
                {
                    reader.Clear();
                    RefreshHistory_Click(null, null);
                }
            }
        }

        private void RefreshHistory_Click(object sender, RoutedEventArgs e)
        {
            TabNotificationHistory.DataContext = null;
            DeviceInfoTabs_SelectionChanged(DeviceInfoTabs, null);
        }

        private void RefreshPhoneStateCallback(string id, string msgType, object msgObject, PhoneClient client)
        {
            if (!(msgObject is JObject msg)) return;

            if (!(DeviceList.SelectedItem is SavedPhones.Phone item) || item.Id != id)
            {
                return;
            }

            dynamic state = new ExpandoObject();

            if (!msg.ContainsKey("is_charging"))
            {
                state.ChargingState = "";
            }
            else
            {
                state.ChargingState =
                    FindResource((bool)msg["is_charging"] ? "Charging" : "NotCharging");
            }

            if (!msg.ContainsKey("battery_percentage"))
            {
                state.BatteryLevel = "";
            }
            else
            {
                state.BatteryLevel = $"{(double)msg["battery_percentage"]}%";
            }

            TabStatus.DataContext = state;
        }
    }

    class MarkAsImportantCommand : ICommand
    {
        public event EventHandler CanExecuteChanged;

        private readonly RichTextBox box;

        public MarkAsImportantCommand(RichTextBox box)
        {
            this.box = box;
        }

        public bool CanExecute(object parameter)
        {
            return !box.Selection.IsEmpty || box.CaretPosition.Paragraph != null;
        }

        public void Execute(object parameter)
        {
            box.GetSelectedHistory();
        }
    }
}
