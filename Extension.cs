using FnSync.Properties;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1.BC;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
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
        public static void InvokeIfNecessaryWithThrow(this Dispatcher dispatcher, Action action, bool Necessary = true)
        {
            if (dispatcher.CheckAccess() || !Necessary)
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

    /*
    static class SettingsExtension
    {
        private static void Save(Settings self)
        {
            if (self.IsSynchronized)
            {
                self.Save();
            }
            else
            {
                lock (self)
                {
                    self.Save();
                }
            }
        }

        public static void SaveInLine(this Settings self)
        {
            try
            {
                Save(self);
            }
            catch (ConfigurationErrorsException e)
            {
                new AutoDisposableTimer(delegate
                {
                    Save(self);
                }, 1000);
            }
        }
    }
    */
}
