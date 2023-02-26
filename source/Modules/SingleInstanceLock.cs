using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FnSync
{
    class SingleInstanceLock
    {
        private static readonly string PATH = SavedPhones.ConfigRoot + "\\INSTANCE_LOCK";
        private static FileStream? LockFile = null;
        public static bool IsSingleInstance()
        {
            if( LockFile != null )
            {
                return true;
            }

            try
            {
                LockFile = File.Open(PATH, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                return true;
            } catch (Exception)
            {
                return false;
            }
        }
    }
}
