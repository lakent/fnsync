using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace FnSync
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static TaskbarIcon NotifyIcon { get; protected set; } = null!;

        public static DeviceMenuList MenuList { get; protected set; } = null!;
        public static readonly FakeDispatcher FakeDispatcher = FakeDispatcher.Init();

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

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            WindowUnhandledException.ShowException(e.Exception);
            e.Handled = true;
        }

        private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
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
                string? exceptionInfo = Environment.GetEnvironmentVariable("LAST_ERROR_STRING");
                if (exceptionInfo != null)
                {
                    new WindowUnhandledException(exceptionInfo).Show();
                }
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(OnAppDomainUnhandledException);
            this.Dispatcher.UnhandledException += OnDispatcherUnhandledException;

            if (!SingleInstanceLock.IsSingleInstance())
            {
                Shutdown();
                Environment.Exit(Environment.ExitCode);
                return;
            }

            // FakeDispatcher = FakeDispatcher.Init();

            ToastNotificationManagerCompat.OnActivated += NotificationSubchannel.OnActivated;

            SetLanguageDictionary();

            NotificationSubchannel._Force = 0;

            NotifyIcon = (TaskbarIcon)FindResource("NotifyIcon");
            MenuList = new DeviceMenuList(NotifyIcon.ContextMenu);

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

            if (string.IsNullOrWhiteSpace(MainConfig.Config.ThisId))
            {
                MainConfig.Config.ThisId = Guid.NewGuid().ToString();

                MainConfig.Config.ConnectOnStartup = true;
                MainConfig.Config.HideOnStartup = true;
                MainConfig.Config.DontToastConnected = false;
                MainConfig.Config.ClipboardSync = true;
                MainConfig.Config.TextCastAutoCopy = true;

                MainConfig.Config.Update();
            }
            else
            {
                int SavedCount = SavedPhones.Singleton.Count;
                if (MainConfig.Config.ConnectOnStartup && SavedCount > 0)
                {
                    if (!MainConfig.Config.HideNotificationOnStartup)
                    {
                        ToastConnectingKnown();
                    }

                    PhoneListener.Singleton.StartReachInitiatively(null, true, SavedPhones.Singleton.Values);
                }
            }

            if (!MainConfig.Config.HideOnStartup)
            {
                WindowMain.NewOne();
            }

            ClipboardManager.Singleton.MonitorClipboardOn = MainConfig.Config.ClipboardSync;

            Casting._Force = 0;
            FileReceive._Force = 0;

            NotifyIcon.ToolTipText = string.Format(
                (string)Current.FindResource("FnSyncTooltip"),
                PhoneListener.Singleton.Port
                );
        }

        public static void ExitApp()
        {
            NotifyIcon?.Dispose();
            SavedPhones.Singleton.DisposeAll();
            Current.Shutdown();
            Environment.Exit(Environment.ExitCode);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ExitApp();
            base.OnExit(e);
        }

        private static void ToastConnectingKnown()
        {
            ToastContentBuilder Builder = new ToastContentBuilder()
                .AddText((string)Current.FindResource("ConnectingKnown"))
                .AddButton(new ToastButton()
                    .SetContent((string)Current.FindResource("OpenMainWindow"))
                    .AddArgument("OpenMainWindow")
                    .SetBackgroundActivation());

            NotificationSubchannel.Singleton.Push(Builder);
        }

        private void SetLanguageDictionary()
        {
            ResourceDictionary dict = new();

            switch (Thread.CurrentThread.CurrentCulture.Name)
            {
                case "zh-CN":
                    dict.Source = new Uri("..\\Resources\\String.zh-CN.xaml", UriKind.Relative);
                    break;

                default:
                    dict.Source = new Uri("..\\Resources\\String.xaml", UriKind.Relative);
                    break;
            }

            Resources.MergedDictionaries.Add(dict);

#if DEBUG
            dict.Source = new Uri("..\\Resources\\String.xaml", UriKind.Relative);
            Resources.MergedDictionaries.Add(dict);
#endif

            CONNECTED = (string)FindResource("Connected");
        }

        private static string CONNECTED = "";
        private static void ToastConnected(string id, string msgType, object? msg, PhoneClient? client)
        {
            if (MainConfig.Config.DontToastConnected)
            {
                return;
            }

            ToastContentBuilder Builder = new ToastContentBuilder()
                .AddHeader(id, client?.Name, "")
                .AddText(CONNECTED)
                ;

            NotificationSubchannel.Singleton.Push(Builder);
        }

        private static void OnDeviceChangedUIWorks(string id, string msgType, object? msg, PhoneClient? client)
        {
            RefreshIcon();
        }
    }
}

