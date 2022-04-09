using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace FnSync.ViewModel
{
    public class WindowMainViewModel : INotifyPropertyChanged, IDisposable
    {
        public abstract class LeftPanelItemAbstract : INotifyPropertyChanged
        {
            public string Name { get; protected set; }
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

            public WindowMainViewModel ViewModel { get; protected set; }
        }

        private class ControlWrapper<C> : LeftPanelItemAbstract where C : UserControlExtension, new()
        {
            protected C view = null;
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

            public ControlWrapper(string Name, WindowMainViewModel ViewModel, bool IsResourceName = false)
            {
                this.Name = IsResourceName ? (string)App.Current.FindResource(Name) : Name;
                this.ViewModel = ViewModel;
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
        }

        public void Dispose()
        {

        }
    }
}
