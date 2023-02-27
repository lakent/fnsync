using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

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

        private static readonly string TagFormat = "FnSyncTag";

        private bool monitorClipboard = false;
        public bool MonitorClipboardOn
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
            public readonly string Text;

            public ClipboardEventArgs(string Text)
            {
                this.Text = Text;
            }
        }

        public static readonly ClipboardManager Singleton = new();

        private readonly Lazy<IntPtr> messageWindowHandle;
        private IntPtr MessageWindowHandle => messageWindowHandle.Value;

        // This cannot be garbage-collected since there is a reference in native code, so we hold a reference to this
        private NativeMethods.WindowClass messageWindowClass;

        private ClipboardManager()
        {
            messageWindowHandle = new(() =>
            {
                string WindowId = "FnSync_" + Guid.NewGuid();

                messageWindowClass.style = 0;
                messageWindowClass.lpfnWndProc = WndProc;
                messageWindowClass.cbClsExtra = 0;
                messageWindowClass.cbWndExtra = 0;
                messageWindowClass.hInstance = IntPtr.Zero;
                messageWindowClass.hIcon = IntPtr.Zero;
                messageWindowClass.hCursor = IntPtr.Zero;
                messageWindowClass.hbrBackground = IntPtr.Zero;
                messageWindowClass.lpszMenuName = IntPtr.Zero;
                messageWindowClass.lpszClassName = WindowId;

                NativeMethods.RegisterClass(ref messageWindowClass);

                return NativeMethods.CreateWindowEx(
                        0,
                        WindowId, "FnSyncClipboardMonitorWindow",
                        0,
                        0, 0, 0, 0,
                        NativeMethods.HWND_MESSAGE,
                        IntPtr.Zero, IntPtr.Zero, IntPtr.Zero
                    );

                // int a = Marshal.GetLastWin32Error();
            });

            PhoneMessageCenter.Singleton.Register(
                null,
                MSG_TYPE_NEW_CLIPBOARD_DATA,
                SetClipboardTextCallback,
                true
                );
        }

        private string? GetClipboardText(IDataObject dataObject)
        {
            if (dataObject.GetDataPresent(DataFormats.UnicodeText, true))
            {
                object data = dataObject.GetData(DataFormats.UnicodeText, true);
                if (data is string str)
                    return str;
            }

            if (dataObject.GetDataPresent(DataFormats.Text, true))
            {
                object data = dataObject.GetData(DataFormats.Text, true);
                if (data is string str)
                    return str;
            }

            return null;
        }

        public bool ContainsFileList()
        {
            try
            {
                return Clipboard.ContainsFileDropList();
            }
            catch (Exception)
            {
                return false;
            }
        }

        public IList<string>? GetFileList()
        {
            try
            {
                return Clipboard.GetFileDropList()?.Cast<string>().ToList();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void SyncClipboardText(bool force = false)
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    IDataObject dataObject = Clipboard.GetDataObject();
                    if (!force && dataObject.GetData(TagFormat) != null)
                    {
                        return;
                    }

                    string? text = GetClipboardText(dataObject);
                    if (text == null)
                    {
                        return;
                    }

                    JObject msg = new()
                    {
                        ["text"] = text
                    };

                    AlivePhones.Singleton.PushMsg(msg, MSG_TYPE_NEW_CLIPBOARD_DATA);
                }
            }
            catch (Exception) { }
        }

        private int preventReentry = 0;
        private async void OnClipboardChanged()
        {
            // https://stackoverflow.com/a/313965/1968839
            if (Interlocked.CompareExchange(ref preventReentry, 1, 0) == 0)
            {
                // We're not in the function
                try
                {
                    await Task.Delay(150);
                    SyncClipboardText();
                }
                finally
                {
                    preventReentry = 0;
                }
            }
            else
            {
                // We're already in the function
            }
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

        public async void SetClipboardText(string Text, bool DontSync)
        {
            DataObject dataObject = new(DataFormats.UnicodeText, Text);

            if (DontSync)
            {
                dataObject.SetData(TagFormat, "", false);
            }

            for (int i = 0; i < 5; ++i)
            {
                try
                {
                    Clipboard.SetDataObject(dataObject, true);
                    break;
                }
                catch { }

                await Task.Delay(10);
            }
        }

        private void SetClipboardTextCallback(string id, string msgType, object? msgObject, PhoneClient? client)
        {
            if (msgObject is not JObject msg)
            {
                return;
            }

            msg.OptString("text")?.Apply(it =>
            {
                SetClipboardText(it, true);
            });
        }
    }
}
