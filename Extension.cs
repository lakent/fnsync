using FnSync.Properties;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1.BC;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FnSync
{
    static class SocketExtension
    {
        public static bool IsListening(this Socket socket)
        {
            // NOTE: probably wise to add some exception handling
            Int32 optVal = (Int32)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.AcceptConnection);
            if (0 >= optVal) return false;
            return true;
        }

        /*
        public static void SendSafe(this Socket self, byte[] buffer)
        {
            lock (self)
            {
                self.Send(buffer);
            }
        }
        */
    }

    public static class NetworkStreamExtension
    {
        public static Task WriteAsync(this NetworkStream s, byte[] buffer)
        {
            return s.WriteAsync(buffer, 0, buffer.Length);
        }

        public static void Write(this NetworkStream s, byte[] buffer)
        {
            s.Write(buffer, 0, buffer.Length);
        }
    }

    public static class ContextMenuExtension
    {
        public static MenuItem FindByName(this ContextMenu t, string name)
        {
            if (name == null)
            {
                return null;
            }

            foreach (Object item in t.Items)
            {
                if (item is MenuItem menu && name == menu.Name)
                {
                    return menu;
                }
            }

            return null;
        }
    }
    static class JObjectExtension
    {
        public static bool OptBool(this JObject jObject, string key, bool defval)
        {
            if (jObject == null || key == null)
            {
                return defval;
            }

            try
            {
                if (jObject.ContainsKey(key))
                {
                    return (bool)jObject[key];
                }
            }
            catch (Exception e)
            {
            }

            return defval;
        }

        public static string OptString(this JObject jObject, string key, string defval)
        {
            if (jObject == null || key == null)
            {
                return defval;
            }

            try
            {
                if (jObject.ContainsKey(key))
                {
                    return (string)jObject[key];
                }
            }
            catch (Exception e)
            {
            }

            return defval;
        }

        public static long OptLong(this JObject jObject, string key, long defval)
        {
            if (jObject == null || key == null)
            {
                return defval;
            }

            try
            {
                if (jObject.ContainsKey(key))
                {
                    return (long)jObject[key];
                }
            }
            catch (Exception e)
            {
            }

            return defval;
        }

    }

    public static class StringExtension
    {
        public static string Truncate(this string orig, int limit)
        {
            if (string.IsNullOrEmpty(orig))
            {
                return orig;
            }
            else
            {
                return orig.Length <= limit ? orig : orig.Substring(0, limit);
            }
        }

        public static string AppendIfNotEnding(this string orig, string text)
        {
            if (orig == null || String.IsNullOrEmpty(text))
            {
                return orig;
            }

            if (!orig.EndsWith(text))
            {
                return orig + text;
            }
            else
            {
                return orig;
            }
        }

        public static void AssureNotEmpty(this string str)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                throw new Exception();
            }
        }
    }

    static class StringBuilderExtension
    {
        public static bool EndsWith(this StringBuilder builder, string str)
        {
            if (builder.Length < str.Length)
                return false;

            string end = builder.ToString(builder.Length - str.Length, str.Length);
            return end.Equals(str);
        }
    }

    static class EndPointExtension
    {
        public static String ConvertToString(this EndPoint endPoint)
        {
            if (endPoint is IPEndPoint end)
            {
                if (end.Address.IsIPv4MappedToIPv6)
                {
                    byte[] b = end.Address.GetAddressBytes();
                    return $"{b[b.Length - 4]}.{b[b.Length - 3]}.{b[b.Length - 2]}.{b[b.Length - 1]}";
                }
                else
                {
                    return end.Address.ToString();
                }
            }
            else
            {
                return null;
            }
        }
    }

    static class DispatherExtension
    {
        public static void InvokeIfNecessaryWithThrow(this Dispatcher dispatcher, Action action, bool IsNecessary = true)
        {
            if (dispatcher.CheckAccess() || !IsNecessary)
            {
                action?.Invoke();
            }
            else
            {
                Exception exception = null;

                dispatcher.Invoke(delegate
                {
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception e)
                    {
                        exception = new Exception("Runtime Exception", e);
                    }
                });

                if (exception != null)
                {
                    throw exception;
                }
            }
        }

        public static void InvokeAsyncCatchable(this Dispatcher dispatcher, Action action)
        {
            dispatcher.InvokeAsync(delegate
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception e)
                {
                    WindowUnhandledException.ShowException(e);
                }
            });
        }

        public static void InvokeIfNecessaryNoThrow(this Dispatcher dispatcher, Action action, bool Necessary = true)
        {
            if (dispatcher.CheckAccess() || !Necessary)
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception e)
                {
                    return;
                }
            }
            else
            {
                dispatcher.Invoke(action);
            }
        }
    }

    static class ObjectExtension
    {
        public static T Apply<T>(this T self, Action<T> block)
        {
            block(self);
            return self;
        }
    }

    static class IListExtension
    {
        public static IList<T> CloneToTypedList<T>(this IList self)
        {
            if (self == null)
            {
                return null;
            }

            if (self is IList<T> TypedList)
            {
                return TypedList.ToList();
            }

            List<T> list = new List<T>(self.Count);

            foreach (object item in self)
            {
                if (item is T typed)
                {
                    list.Add(typed);
                }
            }

            return list;
        }
    }

    static class FileStreamExtension
    {
        public static long Available(this FileStream self)
        {
            return self.Length - self.Position;
        }
    }

    static class BitmapExtension
    {
        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);

        // https://stackoverflow.com/a/35274172/1968839
        public static ImageSource ToImageSource(this Bitmap bmp)
        {
            IntPtr handle = bmp.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                _ = DeleteObject(handle);
            }
        }
    }
}
