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

    /// <summary>
    /// Interaction logic for WindowDeviceMananger.xaml
    /// </summary>
    public partial class WindowDeviceMananger : Window
    {
        public static WindowDeviceMananger CurrentWindow { get; protected set; } = null;
        public static void NewOne(string id)
        {
            App.FakeDispatcher.Invoke(() =>
            {
                if (CurrentWindow == null)
                {
                    WindowDeviceMananger window = new WindowDeviceMananger();
                    window.Show();
                    if (id != null)
                    {
                        window.SelectDevice(id); window.DeviceList.Focus();
                    }
                }
                else
                {
                    CurrentWindow.Activate();
                    if (id != null)
                    {
                        CurrentWindow.SelectDevice(id); CurrentWindow.DeviceList.Focus();
                    }
                }
                return null;
            });
        }

        public WindowDeviceMananger()
        {
            CurrentWindow = this;
            InitializeComponent();
            PromptToSelectDevice.Visibility = Visibility.Visible;
            DeviceList.ItemsSource = SavedPhones.Singleton.PhoneList;
            Count.DataContext = SavedPhones.Singleton.PhoneList;
        }

        private void SelectDevice(string id)
        {
            DeviceList.SelectedItem = SavedPhones.Singleton[id];
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
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
                TabStatus.DataContext = item;
            }
            else if (TabNotificationHistory.DataContext == null && TabNotificationHistory.IsSelected)
            {
                TabNotificationHistory.DataContext = item;
            }
        }
    }
}
