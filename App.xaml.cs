using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Toolkit.Uwp.Notifications;
using Newtonsoft.Json.Linq;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace FnSync
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static TaskbarIcon NotifyIcon { get; protected set; } = null;

        public static DeviceMenuList MenuList { get; protected set; } = null;
        public static FakeDispatcher FakeDispatcher = null;

        private static void RefreshIcon()
        {
            switch (AlivePhones.Singleton.AliveCount)
            {
                case 0:
                    NotifyIcon.Icon = FnSync.Properties.Resources.icon;
                    break;

                case 1:
                    NotifyIcon.Icon = FnSync.Properties.Resources.icon1;
                    break;

                case 2:
                    NotifyIcon.Icon = FnSync.Properties.Resources.icon2;
                    break;

                case 3:
                    NotifyIcon.Icon = FnSync.Properties.Resources.icon3;
                    break;

                default:
                    NotifyIcon.Icon = FnSync.Properties.Resources.icon4;
                    break;
            }
        }

        void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            WindowUnhandledException.ShowException(e.Exception);
            e.Handled = true;
        }

        void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                WindowUnhandledException.ShowException(exception);
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (e.Args.Contains("-LE"))
            {
                new WindowUnhandledException(Environment.GetEnvironmentVariable("LAST_ERROR_STRING")).Show();
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(OnAppDomainUnhandledException);
            this.Dispatcher.UnhandledException += OnDispatcherUnhandledException;

            if (!SingleInstanceLock.IsInstance())
            {
                Shutdown();
                Environment.Exit(Environment.ExitCode);
                return;
            }

            FakeDispatcher = FakeDispatcher.Init();

            DesktopNotificationManagerCompat.RegisterAumidAndComServer<FnSyncNotificationActivator>("holmium.FnSync.A7F49234-CADC-4222-9291-42EDC7D8932E");
            DesktopNotificationManagerCompat.RegisterActivator<FnSyncNotificationActivator>();

            NotifyIcon = (TaskbarIcon)FindResource("NotifyIcon");
            MenuList = new DeviceMenuList(NotifyIcon.ContextMenu);

            SetLanguageDictionary();

            PhoneMessageCenter.Singleton.Register(
                null,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_CONNECTED,
                ToastConnected,
                false
            );

            PhoneMessageCenter.Singleton.Register(
                null,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_CONNECTED,
                OnDeviceChangedUIWorks,
                true
            );

            PhoneMessageCenter.Singleton.Register(
                null,
                PhoneMessageCenter.MSG_FAKE_TYPE_ON_DISCONNECTED,
                OnDeviceChangedUIWorks,
                true
            );

            if (String.IsNullOrWhiteSpace(MainConfig.Config.ThisId))
            {
                MainConfig.Config.ThisId = Guid.NewGuid().ToString();

                MainConfig.Config.ConnectOnStartup = true;
                MainConfig.Config.HideOnStartup = true;
                MainConfig.Config.DontToastConnected = false;
                MainConfig.Config.ClipboardSync = true;
                MainConfig.Config.TextCastAutoCopy = true;

                MainConfig.Config.Update();
                WindowInstruction.NewOne();
            }
            else
            {
                int SavedCount = SavedPhones.Singleton.Count;
                if (MainConfig.Config.ConnectOnStartup && SavedCount > 0)
                {
                    if (!e.Args.Contains("-ToastActivated"))
                    {
                        ToastConnectingKnown(MainConfig.Config.HideOnStartup);
                    }

                    ClientListener.Singleton.StartReachInitiatively(null, true, SavedPhones.Singleton.Values.ToArray<SavedPhones.Phone>());
                }

                if (!MainConfig.Config.HideOnStartup || SavedCount == 0)
                {
                    WindowConnect.NewOne();
                }
            }

            ClipboardManager.Singleton.MonitorClipboard = MainConfig.Config.ClipboardSync;

            Casting._Force = 0;
            FileTransmission._Force = 0;

            App.NotifyIcon.ToolTipText = string.Format(
                (string)App.Current.FindResource("FnSyncTooltip"),
                ClientListener.Singleton.Port
                );
        }

        public static void ExitApp()
        {
            App.NotifyIcon?.Dispose();
            SavedPhones.Singleton.DisposeAll();
            Application.Current.Shutdown();
            Environment.Exit(Environment.ExitCode);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ExitApp();
            base.OnExit(e);
        }

        private static void ToastConnectingKnown(bool ShowConnectOthers)
        {
            ToastContent toastContent = new ToastContent()
            {
                //Launch = "action=viewConversation&conversationId=5",

                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = (string)Application.Current.FindResource("ConnectingKnown"),
                                //HintMaxLines = 1
                            },
                            /*
                            new AdaptiveText()
                            {
                                Text = Id,
                            }
                            */
                        }
                    }
                },

                Actions = ShowConnectOthers ? new ToastActionsCustom()
                {
                    Buttons =
                    {
                        new ToastButton((string)Application.Current.FindResource("ConnectOther"), "ConnectOther")
                        {
                            ActivationType = ToastActivationType.Foreground
                        },
                        new ToastButton((string)Application.Current.FindResource("DeviceManager"), "DeviceManager")
                        {
                            ActivationType = ToastActivationType.Foreground
                        },
                    }
                } : null,

                DisplayTimestamp = DateTime.Now
            };

            // Create the XML document (BE SURE TO REFERENCE WINDOWS.DATA.XML.DOM)
            var doc = new XmlDocument();
            doc.LoadXml(toastContent.GetContent());

            // And create the Toast notification
            var Toast = new ToastNotification(doc);
            var ToastDup = new ToastNotification(doc);

            // And then show it
            NotificationSubchannel.Singleton.Push(Toast, ToastDup);
        }

        private void SetLanguageDictionary()
        {
            ResourceDictionary dict = new ResourceDictionary();

            dict.Source = new Uri("..\\Resources\\String.xaml", UriKind.Relative);
            Resources.MergedDictionaries.Add(dict);

            switch (Thread.CurrentThread.CurrentCulture.Name)
            {
                case "zh-CN":
                    dict.Source = new Uri("..\\Resources\\String.zh-CN.xaml", UriKind.Relative);
                    break;

                default:
                    break;
            }

            Resources.MergedDictionaries.Add(dict);

#if DEBUG
            dict.Source = new Uri("..\\Resources\\String.zh-CN.xaml", UriKind.Relative);
            Resources.MergedDictionaries.Add(dict);
#endif

            CONNECTED = (string)FindResource("Connected");
        }

        private static string CONNECTED;
        private static void ToastConnected(string id, string msgType, object msg, PhoneClient client)
        {
            if (MainConfig.Config.DontToastConnected)
            {
                return;
            }

            ToastContent toastContent = new ToastContent()
            {
                //Launch = "action=viewConversation&conversationId=5",

                Header = new ToastHeader(id, client.Name, ""),

                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = CONNECTED,
                                //HintMaxLines = 1
                            },
                            /*
                            new AdaptiveText()
                            {
                                Text = Id,
                            }
                            */
                        }
                    }
                },

                /*
                Actions = new ToastActionsCustom()
                {
                    Buttons =
                    {
                        new ToastButton((string)Application.Current.FindResource("Rename"), "rename")
                        {
                            ActivationType = ToastActivationType.Foreground
                        },
                    }
                }
                */

                DisplayTimestamp = DateTime.Now
            };

            // Create the XML document (BE SURE TO REFERENCE WINDOWS.DATA.XML.DOM)
            var doc = new XmlDocument();
            doc.LoadXml(toastContent.GetContent());

            // And create the Toast notification
            var Toast = new ToastNotification(doc);
            var ToastDup = new ToastNotification(doc);

            // And then show it
            NotificationSubchannel.Singleton.Push(Toast, ToastDup);
        }

        private static void OnDeviceChangedUIWorks(string id, string msgType, object msg, PhoneClient client)
        {
            RefreshIcon();
        }
    }
}
