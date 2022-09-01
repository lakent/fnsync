using System;
using System.Windows;
using System.Windows.Input;

namespace FnSync
{
    /// <summary>
    /// Provides bindable properties and commands for the NotifyIcon. In this sample, the
    /// view model is assigned to the NotifyIcon in XAML. Alternatively, the startup routing
    /// in App.xaml.cs could have created this view model, and assigned it to the NotifyIcon.
    /// </summary>
    public class NotifyIconViewModel
    {
        public ICommand OpenMainWindowCommand
        {
            get
            {
                return new NotifyIconContextMenuCommand
                {
                    CommandAction = () =>
                    {
                        WindowMain.NewOne();
                    },
                    CanExecuteFunc = () => true
                };
            }
        }
        public ICommand FileManagerCommand
        {
            get
            {
                return new NotifyIconContextMenuCommand
                {
                    CommandAction = () =>
                    {
                        WindowFileManager.NewOne();
                    },
                    CanExecuteFunc = () => true
                };
            }
        }

        public ICommand SettingCommand
        {
            get
            {
                return new NotifyIconContextMenuCommand
                {
                    CommandAction = () =>
                    {
                        WindowMain.NewOne();
                    },
                    CanExecuteFunc = () => true
                };
            }
        }

        public ICommand ExitApplicationCommand
        {
            get
            {
                return new NotifyIconContextMenuCommand
                {
                    CommandAction = () =>
                    {
                        App.ExitApp();
                    }
                };
            }
        }
    }
}