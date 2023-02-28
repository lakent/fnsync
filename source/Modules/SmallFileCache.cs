using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Perception;

namespace FnSync
{
    public class SmallFileCache : IDisposable
    {
        private readonly string Folder;

        private string GenFilePath(string key, string fileExtension)
        {
            return Path.Combine(
                Folder,
                Regex.Replace(key.ToLower(), @"[^a-z0-9._]{1}", "_"),
                string.IsNullOrWhiteSpace(fileExtension) ? "." + fileExtension.ToLower() : ""
                );
        }

        public SmallFileCache(string Folder)
        {
            this.Folder = Folder.AppendIfNotEnding("\\")!;
            if (!Directory.Exists(this.Folder))
            {
                Directory.CreateDirectory(this.Folder);
            }
        }

        private void SaveBytes(string path, byte[] bytes)
        {
            if (!Directory.Exists(this.Folder))
            {
                Directory.CreateDirectory(this.Folder);
            }

            File.WriteAllBytes(path, bytes);
        }

        public string? GetOrPutFromBase64(string key, string? b64, string suffix, bool ForceSave)
        {
            return GetOrPutFromBytes(key, b64 != null ? Convert.FromBase64String(b64) : null, suffix, ForceSave);
        }

        public string? GetOrPutFromBytes(string key, byte[]? bytes, string suffix, bool ForceSave)
        {
            string path = GenFilePath(key, suffix);
            bool FileExist = File.Exists(path);

            bool toPut = bytes != null && (!FileExist || ForceSave);

            if (toPut)
            {
                SaveBytes(path, bytes!);
                return path;
            } else if (FileExist)
            {
                return path;
            } else
            {
                return null;
            }
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Folder, true);
            }
            catch (Exception) { }
        }

        ~SmallFileCache()
        {
            Dispose();
        }
    }
}
