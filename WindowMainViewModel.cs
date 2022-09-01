using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;

namespace FnSync.ViewModel.WindowMain
{
    namespace LeftPanel
    {
        public abstract class PanelItemAbstract : INotifyPropertyChanged
        {
            public ImageSource Icon { get; protected set; } = null;
            //private string name;

            public abstract string Name { get; set; }

            public abstract UserControlExtension View { get; } // Should be lazy initializing

            private bool isSelected = false;

            public event PropertyChangedEventHandler PropertyChanged;

            public bool IsSelected
            {
                get => isSelected;
                set
                {
                    if (this.isSelected != value)
                    {
                        this.isSelected = value;
                        if (value)
                        {
                            this.ViewModel.SelectedView = this.View;
                        }
                        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsSelected"));
                    }
                }
            }

            public ViewModel ViewModel { get; private set; }

            public PanelItemAbstract(ViewModel ViewModel)
            {
                this.ViewModel = ViewModel;
            }

            public void NotifyPropertyChanged(string PropertyName)
            {
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
            }
        }

        internal class ControlWrapper<C> : PanelItemAbstract where C : UserControlExtension, new()
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

            public ControlWrapper(string Name, ViewModel ViewModel, bool IsResourceName = false) : base(ViewModel)
            {
                this.Name = IsResourceName ? (string)System.Windows.Application.Current.FindResource(Name) : Name;
            }
        }

        internal class DeviceItem : PanelItemAbstract
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

            public DeviceItem(SavedPhones.Phone Saved, ViewModel ViewModel) : base(ViewModel)
            {
                this.Id = Saved.Id;
                this.Saved = Saved;
                this.Icon = IconUtil.CellPhone;
            }
        }
    }

    public class ViewModel : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<LeftPanel.PanelItemAbstract> LeftPanelItemSet { get; } = new ObservableCollection<LeftPanel.PanelItemAbstract>();

        private UserControlExtension selected = null;
        public UserControlExtension SelectedView
        {
            get => selected;
            internal set
            {
                if (selected != value)
                {
                    selected = value;
                    selected.OnShow();
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedView"));
                }
            }
        }

        public ViewModel() : this(null) { }

        public ViewModel(string id)
        {
            LeftPanelItemSet.Add(new LeftPanel.ControlWrapper<ControlConnectionByQR>("ConnectByQR", this, true));
            LeftPanelItemSet.Add(new LeftPanel.ControlWrapper<ControlConnectionByCode>("ConnectionCode", this, true));
            LeftPanelItemSet.Add(new LeftPanel.ControlWrapper<ControlSettings>("Setting", this, true));

            foreach (SavedPhones.Phone Phone in SavedPhones.Singleton.Values)
            {
                LeftPanelItemSet.Add(new LeftPanel.DeviceItem(Phone, this));
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

            if (id != null)
            {
                JumpToDevice(id);
            }
        }

        private LeftPanel.DeviceItem FindDeviceById(string Id)
        {
            foreach (LeftPanel.PanelItemAbstract Item in LeftPanelItemSet)
            {
                if (Item is LeftPanel.DeviceItem Device && Device.Id == Id)
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
                LeftPanel.PanelItemAbstract Item = LeftPanelItemSet[i];
                if (Item is LeftPanel.DeviceItem Device && Device.Id == Id)
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
                LeftPanelItemSet.Add(new LeftPanel.DeviceItem(SavedPhones.Singleton[Id], this));
            }
        }

        private void OnNameChanged(string Id, string msgType, object msg, PhoneClient client)
        {
            if (!(msg is string NewName))
            {
                return;
            }

            LeftPanel.DeviceItem Device = FindDeviceById(Id);
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

        public void JumpToDevice(string Id)
        {
            LeftPanel.DeviceItem deviceItem = FindDeviceById(Id);

            if (deviceItem == null)
            {
                return;
            }

            deviceItem.IsSelected = true;
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

