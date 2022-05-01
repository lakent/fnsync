using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;

namespace FnSync.ViewModel
{
    public class WindowMainViewModel : INotifyPropertyChanged, IDisposable
    {
        public abstract class LeftPanelItemAbstract : INotifyPropertyChanged
        {
            public ImageSource Icon { get; protected set; } = null;
            //private string name;
            public abstract string Name { get; set; }
            /*
        {
            get => name;
            protected set
            {
                this.name = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Name"));
            }
        }
            */
            public abstract UserControlExtension View { get; } // Should be lazy initializing

            private bool isSelected = false;

            public event PropertyChangedEventHandler PropertyChanged;

            public bool IsSelected
            {
                get => isSelected;
                set
                {
                    this.isSelected = value;
                    if (value)
                    {
                        this.ViewModel.SelectedView = this.View;
                    }
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsSelected"));
                }
            }

            public WindowMainViewModel ViewModel { get; private set; }

            public LeftPanelItemAbstract(WindowMainViewModel ViewModel)
            {
                this.ViewModel = ViewModel;
            }

            public void NotifyPropertyChanged(string PropertyName)
            {
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
            }
        }

        private class ControlWrapper<C> : LeftPanelItemAbstract where C : UserControlExtension, new()
        {
            public override string Name { get; set; }

            private C view = null;
            public override UserControlExtension View
            {
                get
                {
                    if (view == null)
                    {
                        view = new C();
                    }

                    return view;
                }
            }

            public ControlWrapper(string Name, WindowMainViewModel ViewModel, bool IsResourceName = false) : base(ViewModel)
            {
                this.Name = IsResourceName ? (string)App.Current.FindResource(Name) : Name;
            }
        }

        private class DeviceItem : LeftPanelItemAbstract
        {
            public override string Name
            {
                get => this.Saved.Name;
                set
                {
                    this.Saved.Name = value;
                }
            }
            public string Id { get; private set; }
            public PhoneClient Client => AlivePhones.Singleton[this.Id];
            public SavedPhones.Phone Saved { get; private set; }
            public bool IsAlive => this.Client?.IsAlive == true;

            private ControlDevice view = null;
            public override UserControlExtension View
            {
                get
                {
                    if (view == null)
                    {
                        view = new ControlDevice(this.Saved);
                    }

                    return view;
                }
            }

            public DeviceItem(SavedPhones.Phone Saved, WindowMainViewModel ViewModel) : base(ViewModel)
            {
                this.Id = Saved.Id;
                this.Saved = Saved;
                this.Icon = IconUtil.CellPhone;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<LeftPanelItemAbstract> LeftPanelItemSet { get; } = new ObservableCollection<LeftPanelItemAbstract>();

        private UserControlExtension selected = null;
        public UserControlExtension SelectedView
        {
            get => selected;
            private set
            {
                selected = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedView"));
            }
        }

        public WindowMainViewModel()
        {
            LeftPanelItemSet.Add(new ControlWrapper<ControlConnectionByQR>("ConnectByQR", this, true));
            LeftPanelItemSet.Add(new ControlWrapper<ControlConnectionByCode>("ConnectionCode", this, true));
            LeftPanelItemSet.Add(new ControlWrapper<ControlSettings>("Setting", this, true));

            foreach (SavedPhones.Phone Phone in SavedPhones.Singleton.Values)
            {
                LeftPanelItemSet.Add(new DeviceItem(Phone, this));
            }

            PhoneMessageCenter.Singleton.Register(
                null,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_CONNECTED,
                OnDeviceConnected,
                false
            );

            PhoneMessageCenter.Singleton.Register(
                null,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_NAME_CHANGED,
                OnNameChanged,
                false
            );

            PhoneMessageCenter.Singleton.Register(
                null,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_REMOVED,
                OnRemoved,
                false
                );
        }

        private DeviceItem FindDeviceById(string Id)
        {
            foreach (LeftPanelItemAbstract Item in LeftPanelItemSet)
            {
                if (Item is DeviceItem Device && Device.Id == Id)
                {
                    return Device;
                }
            }

            return null;
        }

        private void RemoveDeviceById(string Id)
        {
            for (int i = 0; i < LeftPanelItemSet.Count; ++i)
            {
                LeftPanelItemAbstract Item = LeftPanelItemSet[i];
                if (Item is DeviceItem Device && Device.Id == Id)
                {
                    LeftPanelItemSet.RemoveAt(i);
                    return;
                }
            }
        }

        private void OnDeviceConnected(string Id, string msgType, object msg, PhoneClient client)
        {
            if (FindDeviceById(Id) == null)
            {
                LeftPanelItemSet.Add(new DeviceItem(SavedPhones.Singleton[Id], this));
            }
        }

        private void OnNameChanged(string Id, string msgType, object msg, PhoneClient client)
        {
            if (!(msg is string NewName)) return;

            DeviceItem Device = FindDeviceById(Id);
            if (Device == null)
            {
                return;
            }

            Device.NotifyPropertyChanged("Name");
        }

        private void OnRemoved(string Id, string __, object ___, PhoneClient ____)
        {
            RemoveDeviceById(Id);
        }

        public void Dispose()
        {
            PhoneMessageCenter.Singleton.Unregister(
                null,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_CONNECTED,
                OnDeviceConnected
            );

            PhoneMessageCenter.Singleton.Unregister(
                null,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_NAME_CHANGED,
                OnNameChanged
            );

            PhoneMessageCenter.Singleton.Unregister(
                null,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_REMOVED,
                OnRemoved
                );
        }
    }
}
