using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.IO;
using Windows.Graphics.Printing;
using Windows.UI.Xaml;

namespace FnSync
{
    static class Utils
    {
        [DllImport("user32.dll")]
        public static extern int LockWorkStation();

        public static String GetAllInterface(bool IPv6LinkLocal)
        {
            NetworkInterface[] Adapters = NetworkInterface.GetAllNetworkInterfaces().OrderBy(x => Unirandom.Next()).ToArray();
            StringBuilder Builder = new StringBuilder();

            foreach (NetworkInterface adapter in Adapters)
            {
                var ips = adapter.GetIPProperties().UnicastAddresses;
                foreach (var ip in ips)
                {
                    if (ip.Address.AddressFamily != AddressFamily.InterNetwork &&
                        ip.Address.AddressFamily != AddressFamily.InterNetworkV6
                        )
                    {
                        continue;
                    }

                    if (IPAddress.IsLoopback(ip.Address))
                    {
                        continue;
                    }

                    if (IPv6LinkLocal != ip.Address.IsIPv6LinkLocal)
                    {
                        continue;
                    }

                    string str = ip.Address.ToString();
                    if (ip.Address.IsIPv6LinkLocal && str.Contains("%"))
                    {
                        str = str.Substring(0, str.IndexOf('%'));
                    }

                    Builder.Append(str).Append("|");

                }
            }

            if (Builder.EndsWith("|"))
            {
                Builder.Remove(Builder.Length - 1, 1);
            }

            return Builder.ToString();
        }

        public static void WriteAllBytes(String file, byte[] bs)
        {
            FileStream stream = File.Open(file, FileMode.Create, FileAccess.Write, FileShare.Read);
            stream.Write(bs, 0, bs.Length);
            stream.Flush();
            stream.Close();
        }

        public static int RoundUp(int number, int whole)
        {
            if (number % whole == 0) return number;
            return (whole - number % 10) + number;
        }

        public static string ToHumanReadableSize(long LengthInBytes)
        {
            // https://stackoverflow.com/questions/281640/how-do-i-get-a-human-readable-file-size-in-bytes-abbreviation-using-net
            // Get absolute value
            long absolute_i = (LengthInBytes < 0 ? -LengthInBytes : LengthInBytes);
            // Determine the suffix and readable value
            string suffix;
            double readable;
            if (absolute_i >= 0x1000000000000000) // Exabyte
            {
                suffix = "EB";
                readable = (LengthInBytes >> 50);
            }
            else if (absolute_i >= 0x4000000000000) // Petabyte
            {
                suffix = "PB";
                readable = (LengthInBytes >> 40);
            }
            else if (absolute_i >= 0x10000000000) // Terabyte
            {
                suffix = "TB";
                readable = (LengthInBytes >> 30);
            }
            else if (absolute_i >= 0x40000000) // Gigabyte
            {
                suffix = "GB";
                readable = (LengthInBytes >> 20);
            }
            else if (absolute_i >= 0x100000) // Megabyte
            {
                suffix = "MB";
                readable = (LengthInBytes >> 10);
            }
            else if (absolute_i >= 0x400) // Kilobyte
            {
                suffix = "KB";
                readable = LengthInBytes;
            }
            else
            {
                return LengthInBytes.ToString("0 B"); // Byte
            }
            // Divide by 1024 to get fractional value
            readable /= 1024;
            // Return formatted number with suffix
            return readable.ToString("0.### ") + suffix;
        }

        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

        public static string ReplaceInvalidFileName(string src)
        {
            StringBuilder sb = new StringBuilder(src);

            foreach(char ch in InvalidFileNameChars)
            {
                sb.Replace(ch, '_');
            }

            return sb.ToString();
        }
    }
}
