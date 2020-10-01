using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Windows.Media.Core;

namespace FnSync
{
    class ClipboardManager
    {
        public const string MSG_TYPE_NEW_CLIPBOARD_DATA = "phone_clipboard_data_sync";

        private static class NativeMethods
        {
            public const int WM_CLIPBOARDUPDATE = 0x031D;
            public static IntPtr HWND_MESSAGE = new IntPtr(-3);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool AddClipboardFormatListener(IntPtr hwnd);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

            public delegate IntPtr WindowProcedureHandler(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

            [StructLayout(LayoutKind.Sequential)]
            public struct WindowClass
            {
                public uint style;
                public WindowProcedureHandler lpfnWndProc;
                public int cbClsExtra;
                public int cbWndExtra;
                public IntPtr hInstance;
                public IntPtr hIcon;
                public IntPtr hCursor;
                public IntPtr hbrBackground;
                //[MarshalAs(UnmanagedType.LPWStr)] public string lpszMenuName;
                public IntPtr lpszMenuName;
                [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
            }

            [DllImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true)]
            public static extern IntPtr CreateWindowEx(int dwExStyle, [MarshalAs(UnmanagedType.LPWStr)] string lpClassName,
                 [MarshalAs(UnmanagedType.LPWStr)] string lpWindowName, int dwStyle, int x, int y,
                 int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance,
                 IntPtr lpParam);

            [DllImport("user32.dll", EntryPoint = "RegisterClassW", SetLastError = true)]
            public static extern short RegisterClass(ref WindowClass lpWndClass);

            [DllImport("user32.dll")]
            public static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wparam, IntPtr lparam);
        }

        private bool monitorClipboard = false;
        public bool MonitorClipboard
        {
            get
            {
                return monitorClipboard;
            }
            set
            {
                if (monitorClipboard != value)
                {
                    monitorClipboard = value;

                    if (value)
                    {
                        NativeMethods.AddClipboardFormatListener(MessageWindowHandle);
                    }
                    else
                    {
                        NativeMethods.RemoveClipboardFormatListener(MessageWindowHandle);
                    }
                }
            }
        }

        public class ClipboardEventArgs : EventArgs
        {
            public readonly String Text;

            public ClipboardEventArgs(String Text)
            {
                this.Text = Text;
            }
        }

        public static ClipboardManager Singleton = new ClipboardManager();

        private readonly IntPtr MessageWindowHandle;
        private NativeMethods.WindowClass MessageWindowClass;

        public ClipboardManager()
        {
            string WindowId = "FnSync_" + Guid.NewGuid();

            MessageWindowClass.style = 0;
            MessageWindowClass.lpfnWndProc = WndProc;
            MessageWindowClass.cbClsExtra = 0;
            MessageWindowClass.cbWndExtra = 0;
            MessageWindowClass.hInstance = IntPtr.Zero;
            MessageWindowClass.hIcon = IntPtr.Zero;
            MessageWindowClass.hCursor = IntPtr.Zero;
            MessageWindowClass.hbrBackground = IntPtr.Zero;
            MessageWindowClass.lpszMenuName = IntPtr.Zero;
            MessageWindowClass.lpszClassName = WindowId;

            NativeMethods.RegisterClass(ref MessageWindowClass);

            MessageWindowHandle = NativeMethods.CreateWindowEx(
                0,
                WindowId, "StubWindow",
                0,
                0, 0, 0, 0,
                NativeMethods.HWND_MESSAGE,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero
            );

            var a = Marshal.GetLastWin32Error();

            PhoneMessageCenter.Singleton.Register(
                null,
                MSG_TYPE_NEW_CLIPBOARD_DATA, 
                SetClipboardText,
                true
                );
        }

        private string LastText = "";

        private long LastClipboardChangedTime = 0;
        private void OnClipboardChanged()
        {
            long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (now - LastClipboardChangedTime < 150)
            {
                return;
            }

            LastClipboardChangedTime = now;

            new AutoDisposableTimer((state) =>
            {
                Application.Current.Dispatcher.InvokeAsyncCatchable(() =>
                {
                    try
                    {
                        if (Clipboard.ContainsText())
                        {
                            LastText = Clipboard.GetText();
                            JObject msg = new JObject
                            {
                                ["text"] = LastText
                            };

                            AlivePhones.Singleton.PushMsg(msg, MSG_TYPE_NEW_CLIPBOARD_DATA);
                        }
                    }
                    catch (Exception e) { }
                });
            }, 150);
        }

        private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
            {
                OnClipboardChanged();
                return IntPtr.Zero;
            }

            return NativeMethods.DefWindowProc(hwnd, msg, wParam, lParam);
        }

        private void SetClipboardText(string id, string msgType, object msgObject, PhoneClient client)
        {
            if( !(msgObject is JObject msg))
            {
                return;
            }

            String NewText = (string)msg["text"];
            try
            {
                if (LastText != NewText)
                {
                    Clipboard.SetDataObject(NewText, true);
                }
            }
            catch (Exception e) { }
        }
    }
}
