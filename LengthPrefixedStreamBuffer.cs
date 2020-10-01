using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FnSync
{
    public abstract class LengthPrefixedStreamBuffer
    {
        public class UnfinishedStreamException : Exception { }

        protected byte[] buffer;
        private int? PackageLength = null;

        public int BufferUsed { get; protected set; }

        protected EncryptionManager encryptionManager;

        public LengthPrefixedStreamBuffer(int InitialCapacity, String code) : this(InitialCapacity, new EncryptionManager(code))
        {
        }

        public LengthPrefixedStreamBuffer(int InitialCapacity, EncryptionManager manager)
        {
            buffer = new byte[InitialCapacity];
            BufferUsed = 0;
            encryptionManager = manager;
        }

        private void Consume(int count)
        {
            if (count > BufferUsed)
            {
                count = BufferUsed;
            }

            if (count == BufferUsed)
            {
                BufferUsed = 0;
            }
            else
            {
                Array.Copy(buffer, count, buffer, 0, BufferUsed - count);
                BufferUsed -= count;
            }

            this.PackageLength = null;
        }

        protected abstract void Load();

        public bool StreamIsFinished()
        {
            return GetPackageLengthNoThrow() >= 0;
        }

        private int GetPackageLengthNoThrow()
        {
            if (this.PackageLength != null)
            {
                return this.PackageLength.Value;
            }

            Load();

            if (BufferUsed <= 4)
            {
                return -1;
            }

            int packageLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, 0));

            if (packageLength + 4 > BufferUsed)
            {
                return -1;
            }

            this.PackageLength = packageLength;

            return packageLength;
        }

        private int GetPackageLength()
        {
            int ret = GetPackageLengthNoThrow();

            if( ret < 0)
            {
                throw new UnfinishedStreamException();
            }

            return ret;
        }

        public void Clear()
        {
            BufferUsed = 0;
            this.PackageLength = null;
        }

        public byte[] ReadUncryptedBytes()
        {
            int len = GetPackageLength();
            byte[] ret = new byte[len];
            Array.Copy(buffer, 4, ret, 0, len);
            Consume(len + 4);
            return ret;
        }

        public string ReadUncryptedString()
        {
            int len = GetPackageLength();
            string ret = Encoding.UTF8.GetString(buffer, 4, len);
            Consume(len + 4);
            return ret;
        }

        public JObject ReadUncryptedJSON()
        {
            return JObject.Parse(ReadUncryptedString());
        }

        public byte[] ReadBytes()
        {
            int len = GetPackageLength();
            byte[] ret = encryptionManager.Decrypt(buffer, 4, len);
            Consume(len + 4);
            return ret;
        }

        public string ReadString()
        {
            int len = GetPackageLength();
            string ret = encryptionManager.DecryptToString(buffer, 4, len);
            Consume(len + 4);
            return ret;
        }

        public JObject ReadJSON()
        {
            int len = GetPackageLength();
            JObject ret = encryptionManager.DecryptToJSON(buffer, 4, len);
            Consume(len + 4);
            return ret;
        }
    }
}
