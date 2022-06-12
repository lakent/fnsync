using Hardcodet.Wpf.TaskbarNotification;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Windows.Services.Maps;

namespace FnSync
{
    public class DeviceMenuList
    {
        private readonly ContextMenu Menu;
        private readonly MenuItem NoneItem;
        private readonly Dictionary<String, MenuItem> Map = new Dictionary<string, MenuItem>();
        public int Count => Map.Count;

        public DeviceMenuList(ContextMenu Menu)
        {
            this.Menu = Menu;
            NoneItem = Menu.FindByName("ConnectedPhonesNone");

            PhoneMessageCenter.Singleton.Register(
                null,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_CONNECTED,
                OnConnected,
                true
            );

            PhoneMessageCenter.Singleton.Register(
                null,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_DISCONNECTED,
                OnDisconnected,
                true
            );

            PhoneMessageCenter.Singleton.Register(
                null,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_NAME_CHANGED,
                OnNameChanged,
                true
            );

            PhoneMessageCenter.Singleton.Register(
                null,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_REMOVED,
                OnRemoved,
                true
                );
        }

        private int FindStart()
        {
            for (int i = 0; i < Menu.Items.Count; ++i)
            {
                Object item = Menu.Items[i];
                if (item is MenuItem item1 && "ConnectedPhonesTitle" == item1.Name)
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindEnd(int Start)
        {
            for (int i = Start + 1; i < Menu.Items.Count; ++i)
            {
                Object item = Menu.Items[i];
                if (item is Separator separator && "PhonesAbove" == separator.Name)
                {
                    return i;
                }
            }

            return -1;
        }


        private MenuItem FindExist(String id)
        {
            if (Map.ContainsKey(id))
            {
                return Map[id];
            }
            else
            {
                return null;
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is String id)
            {
                if (AlivePhones.Singleton[id]?.IsAlive ?? false)
                {
                    WindowMain.NewOne();
                    WindowMain.JumpToDevice(id);
                }
                else if (SavedPhones.Singleton.ContainsKey(id))
                {
                    PhoneListener.Singleton.StartReachInitiatively(null, true, new SavedPhones.Phone[] { SavedPhones.Singleton[id] });
                }
            }
        }

        private void UpdateState(MenuItem item, PhoneClient client)
        {
            String Header = client.IsAlive ?
                (string)Application.Current.FindResource("Online") + " - " + client.Name :
                string.Format(
                    (string)Application.Current.FindResource("OfflineWithPhone"),
                    client.Name
                    );

            item.Header = Header;
        }

        private MenuItem NewItem(String id)
        {
            MenuItem New = new MenuItem
            {
                Tag = id
            };

            New.Click += MenuItem_Click;
            Map.Add(id, New);
            Menu.Items.Insert(FindEnd(0) - 1, New);
            return New;
        }

        public void AddOrUpdate(PhoneClient client)
        {
            MenuItem Exist = FindExist(client.Id);
            if (Exist != null)
            {
                UpdateState(Exist, client);
                return;
            }
            else
            {
                MenuItem New = NewItem(client.Id);
                UpdateState(New, client);
                if (NoneItem.Visibility != Visibility.Collapsed)
                {
                    NoneItem.Visibility = Visibility.Collapsed;
                }
            }
        }

        public void Update(PhoneClient client)
        {
            MenuItem Exist = FindExist(client.Id);
            if (Exist != null)
            {
                UpdateState(Exist, client);
            }
        }

        public void Remove(string id)
        {
            if (Map.ContainsKey(id))
            {
                Menu.Items.Remove(Map[id]);
                Map.Remove(id);
                if (Map.Count == 0)
                {
                    NoneItem.Visibility = Visibility.Visible;
                }
            }
        }

        private void OnConnected(string id, string msgType, object msg, PhoneClient client)
        {
            AddOrUpdate(client);
        }

        private void OnDisconnected(string id, string msgType, object msg, PhoneClient client)
        {
            Update(client);
        }

        private void OnNameChanged(string id, string msgType, object msg, PhoneClient client)
        {
            if (client == null) return;

            Update(client);
        }

        private void OnRemoved(string id, string __, object ___, PhoneClient ____)
        {
            Remove(id);
        }
    }
}
