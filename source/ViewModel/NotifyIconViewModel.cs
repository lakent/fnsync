﻿using System;
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

        public ICommand TriggerClipboardSyncCommand
        {
            get
            {
                return new NotifyIconContextMenuCommand
                {
                    CommandAction = () =>
                    {
                        ClipboardManager.Singleton.SyncClipboardText(true);
                    },
                    CanExecuteFunc = () => true
                };
            }
        }

        public ICommand TrayDoubleClickCommand
        {
            get
            {
                return new NotifyIconContextMenuCommand
                {
                    CommandAction = () =>
                    {
                        if (AlivePhones.Singleton.AliveCount == 0)
                        {
                            WindowMain.NewOne();
                        }
                        else
                        {
                            switch (MainConfig.Config.TrayDoubleClickAction.Value)
                            {
                                default:
                                case "OpenMainWindow":
                                    WindowMain.NewOne();
                                    break;

                                case "FileManager":
                                    WindowFileManager.NewOne();
                                    break;

                                case "TriggerClipboardSync":
                                    ClipboardManager.Singleton.SyncClipboardText(true);
                                    break;
                            }
                        }
                    }
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