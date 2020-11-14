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
        /// <summary>
        /// Shows a window, if none is already open.
        /// </summary>
        public ICommand ShowWindowCommand
        {
            get
            {
                return new NotifyIconContextMenuCommand
                {
                    CanExecuteFunc = () => true,
                    CommandAction = () =>
                    {
                        WindowConnect.NewOne();
                    }
                };
            }
        }

        /// <summary>
        /// Hides the main window. This command is only enabled if a window is open.
        /// </summary>
        public ICommand ConnectOtherCommand
        {
            get
            {
                return new NotifyIconContextMenuCommand
                {
                    CommandAction = () =>
                    {
                        WindowConnect.NewOne();
                    },
                    CanExecuteFunc = () => true
                };
            }
        }
        public ICommand DeviceManagerCommand
        {
            get
            {
                return new NotifyIconContextMenuCommand
                {
                    CommandAction = () =>
                    {
                        WindowDeviceMananger.NewOne(null);
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
                       WindowSetting.NewOne();
                    },
                    CanExecuteFunc = () => true
                };
            }
        }


        public ICommand InstructionCommand
        {
            get
            {
                return new NotifyIconContextMenuCommand
                {
                    CommandAction = () =>
                    {
                        WindowInstruction.NewOne();
                    },
                    CanExecuteFunc = () => true
                };
            }
        }

        /// <summary>
        /// Shuts down the application.
        /// </summary>
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