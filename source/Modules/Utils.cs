using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.IO;
using System.Drawing;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Collections.Generic;

namespace FnSync
{
    static class Utils
    {
        [DllImport("user32.dll")]
        public static extern int LockWorkStation();

        public static string GetAllInterface(bool IPv6LinkLocal)
        {
            NetworkInterface[] Adapters = NetworkInterface.GetAllNetworkInterfaces().OrderBy(x => Unirandom.Next()).ToArray();
            StringBuilder Builder = new();

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

#if DEBUG
                    if (str.StartsWith("10."))
                    {
                        continue;
                    }
#endif
                    if (ip.Address.IsIPv6LinkLocal && str.Contains('%'))
                    {
                        str = str[..str.IndexOf('%')];
                    }

                    Builder.Append(str).Append('|');

                }
            }

            if (Builder.EndsWith("|"))
            {
                Builder.Remove(Builder.Length - 1, 1);
            }

            return Builder.ToString();
        }

        public static string[] GetIpv4BroadcastAddress()
        {
            NetworkInterface[] Adapters = NetworkInterface.GetAllNetworkInterfaces();
            HashSet<string> Result = new();

            foreach (NetworkInterface adapter in Adapters)
            {
                var ips = adapter.GetIPProperties().UnicastAddresses;
                foreach (var ip in ips)
                {
                    if (IPAddress.IsLoopback(ip.Address) || ip.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    int IpNumber = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(ip.Address.GetAddressBytes(), 0));
                    int Prefix = unchecked((int)0x80000000) >> (ip.PrefixLength - 1);
                    int Broadcast = IpNumber | ~Prefix;
                    byte[] bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(Broadcast));

                    string IpStr =
                        bytes[0].ToString() + '.' +
                        bytes[1].ToString() + '.' +
                        bytes[2].ToString() + '.' +
                        bytes[3].ToString();

#if DEBUG
                    if (IpStr.StartsWith("10."))
                    {
                        continue;
                    }
#endif

                    Result.Add(IpStr);
                }
            }

            return Result.ToArray<string>();
        }

        public static string[] GetIpv6BroadcastAddress()
        {
            NetworkInterface[] Adapters = NetworkInterface.GetAllNetworkInterfaces();
            HashSet<string> Result = new();

            foreach (NetworkInterface adapter in Adapters)
            {
                try
                {
                    var Ipv6Index = adapter.GetIPProperties().GetIPv6Properties().Index;
                    Result.Add("ff02::2%" + Ipv6Index);
                }
                catch (Exception) { }
            }

            return Result.ToArray<string>();
        }

        public static void WriteAllBytes(string file, byte[] bs)
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

        public static string ToHumanReadableSize(long Bytes)
        {
            long abs = Math.Abs(Bytes);
            int sign = Math.Sign(Bytes);

            string unit;
            double scale;

            if (abs >= 0x1000000000000000)
            {
                unit = "EB";
                scale = (Bytes >> 50);
            }
            else if (abs >= 0x4000000000000)
            {
                unit = "PB";
                scale = (Bytes >> 40);
            }
            else if (abs >= 0x10000000000)
            {
                unit = "TB";
                scale = (Bytes >> 30);
            }
            else if (abs >= 0x40000000)
            {
                unit = "GB";
                scale = (Bytes >> 20);
            }
            else if (abs >= 0x100000)
            {
                unit = "MB";
                scale = (Bytes >> 10);
            }
            else if (abs >= 0x400)
            {
                unit = "KB";
                scale = Bytes;
            }
            else // Byte
            {
                return Bytes.ToString("0 B");
            }

            scale /= 1024;
            scale *= sign;

            return scale.ToString("0.### ") + unit;
        }

        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
        private static readonly char[] InvalidFileNameCharsWithoutLinuxPathSlash =
            InvalidFileNameChars.Except(new char[] { '/' }).ToArray();

        public static string ReplaceInvalidFileNameChars(string src, bool PreserveLinuxPathSlash = false)
        {
            StringBuilder sb = new(src);

            foreach (char ch in (PreserveLinuxPathSlash ? InvalidFileNameCharsWithoutLinuxPathSlash : InvalidFileNameChars))
            {
                sb.Replace(ch, '_');
            }

            return sb.ToString();
        }
        public static IEnumerable<string> TraverseFile(string root)
        {
            if (!Directory.Exists(root) && !File.Exists(root))
            {
                yield break;
            }

            if (File.Exists(root))
            {
                yield return root;
                yield break;
            }
            else
            {
                Stack<string> dirs = new();

                dirs.Push(root);

                while (dirs.Count > 0)
                {
                    string currentDir = dirs.Pop();
                    string[] subDirs;
                    try
                    {
                        subDirs = Directory.GetDirectories(currentDir);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        continue;
                    }
                    catch (DirectoryNotFoundException)
                    {
                        continue;
                    }

                    foreach (string dir in subDirs)
                    {
                        dirs.Push(dir);
                        yield return dir.AppendIfNotEnding("\\")!;
                    }

                    string[] files;

                    try
                    {
                        files = Directory.GetFiles(currentDir);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        continue;
                    }
                    catch (DirectoryNotFoundException)
                    {
                        continue;
                    }

                    foreach (string file in files)
                    {
                        yield return file;
                    }
                }

                yield break;
            }

        }

        public static string GetRelativePath(string absPath, string folder)
        {
            folder = folder.AppendIfNotEnding("\\")!;
            return absPath[folder.Length..];
        }

        public static bool IsFolder(string Path)
        {
            if (File.Exists(Path))
            {
                return false;
            }
            else
            {
                return Directory.Exists(Path);
            }
        }
    }

    public static class IconUtil
    {
        [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHSTOCKICONINFO
        {
            public UInt32 cbSize;
            public IntPtr hIcon;
            public Int32 iSysIconIndex;
            public Int32 iIcon;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szPath;
        }

        [DllImport("shell32.dll")]
        private static extern int SHGetStockIconInfo(int siid, uint uFlags, ref SHSTOCKICONINFO psii);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        };

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            ref SHFILEINFO psfi,
            uint cbFileInfo,
            uint uFlags
        );

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr h);

        private const int SIID_DOCNOASSOC = 0x0;
        private const int SIID_FOLDER = 0x3;
        private const int SIID_MEDIACOMPACTFLASH = 98;
        private const int SIID_DEVICECELLPHONE = 99;
        private const uint SHGSI_ICON = 0x100;
        private const uint SHGSI_SMALLICON = 0x1;

        private const uint SHGFI_ICON = 0x100;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
        private const uint SHGFI_SMALLICON = 0x1;

        private static ImageSource ByType(int Type)
        {
            SHSTOCKICONINFO info = new();
            info.cbSize = (uint)Marshal.SizeOf(info);

            _ = SHGetStockIconInfo(Type, SHGSI_ICON | SHGSI_SMALLICON, ref info);
            ImageSource ret = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            DestroyIcon(info.hIcon);

            return ret;
        }

        private static Lazy<ImageSource> folder = new(() => ByType(SIID_FOLDER));
        public static ImageSource Folder
        {
            get
            {
                return folder.Value;
            }
        }

        private static Lazy<ImageSource> unknown = new(() => ByType(SIID_DOCNOASSOC));
        public static ImageSource Unknown
        {
            get
            {
                return unknown.Value;
            }
        }

        private static Lazy<ImageSource> cellPhone = new(() => ByType(SIID_DEVICECELLPHONE));
        public static ImageSource CellPhone
        {
            get
            {
                return cellPhone.Value;
            }
        }

        private static Lazy<ImageSource> storage = new(() => ByType(SIID_MEDIACOMPACTFLASH));
        public static ImageSource Storage
        {
            get
            {
                return storage.Value;
            }
        }

        private static readonly Dictionary<string, ImageSource> Cache = new();

        public static ImageSource ByExtension(string path)
        {
            string ext = Path.GetExtension(path);

            if (string.IsNullOrWhiteSpace(ext))
                return Unknown;

            if (Cache.ContainsKey(ext))
                return Cache[ext];

            SHFILEINFO info = new();

            SHGetFileInfo(ext,
                FILE_ATTRIBUTE_NORMAL,
                ref info,
                (uint)Marshal.SizeOf(info),
                SHGFI_ICON | SHGFI_USEFILEATTRIBUTES | SHGFI_SMALLICON
            );

            ImageSource ret = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            DestroyIcon(info.hIcon);

            Cache[ext] = ret;

            return ret;
        }

        public static void ClearCache()
        {
            folder = null!;
            unknown = null!;
            cellPhone = null!;
            storage = null!;

            Cache.Clear();
        }
    }

    class Unirandom
    {
        private static readonly Random random = new();

        public static int Next()
        {
            return random.Next();
        }
        public static int Next(int l, int u)
        {
            return random.Next(l, u);
        }
    }
}
