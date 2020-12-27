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
    public abstract class LengthPrefixedStreamBuffer : IDisposable
    {
        public class StreamClosedException : IOException { }

        private readonly NetworkStream Stream;

        protected EncryptionManager encryptionManager;

        private class ReceiveQueueClass : QueuedAsyncTask<byte[], byte[]>
        {
            public readonly LengthPrefixedStreamBuffer BufferObject;
            public ReceiveQueueClass(LengthPrefixedStreamBuffer BufferObject)
            {
                this.BufferObject = BufferObject;
            }

            protected override Task<byte[]> InputSource()
            {
                return BufferObject.ReadRawPackage();
            }

            protected override void OnDone(byte[] input, byte[] output)
            {
                BufferObject.PackageProcessing(input, output);
            }

            protected override byte[] TaskBody(byte[] Input)
            {
                return BufferObject.encryptionManager.Decrypt(Input, 0, Input.Length);
            }

            protected override void OnException(QueuedAsyncTask<byte[], byte[]> sender, Exception e)
            {
                BufferObject.Dispose();
            }
        }

        protected readonly QueuedAsyncTask<byte[], byte[]> ReceiveQueue;

        public LengthPrefixedStreamBuffer(NetworkStream Stream, String code) : this(Stream, new EncryptionManager(code))
        {
        }

        public LengthPrefixedStreamBuffer(NetworkStream Stream, EncryptionManager manager)
        {
            this.Stream = Stream;
            encryptionManager = manager;

            ReceiveQueue = new ReceiveQueueClass(this);
        }

        public async Task<byte[]> ReadBytes(int len, byte[] buf = null)
        {
            if (buf == null)
            {
                buf = new byte[len];
            }

            int read = 0;

            while (read < len)
            {
                bool DataAvailable;
                try
                {
                    DataAvailable = Stream.DataAvailable;
                }
                catch (Exception e)
                {
                    throw new StreamClosedException();
                }

                int recv;
                if (DataAvailable)
                {
                    recv = Stream.Read(buf, read, len - read);
                }
                else
                {
                    recv = await Stream.ReadAsync(buf, read, len - read);
                }

                if (recv == 0)
                {
                    throw new StreamClosedException();
                }

                read += recv;
            }

            return buf;
        }

        private readonly byte[] LengthBuffer = new byte[4];

        private async Task<int> GetPackageLength()
        {
            byte[] buf = await ReadBytes(4, LengthBuffer);
            return IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buf, 0));
        }

        private async Task<byte[]> ReadRawPackage()
        {
            int len = await GetPackageLength();
            byte[] buf = await ReadBytes(len);
            return buf;
        }

        public async Task<string> ReadUncryptedString()
        {
            if (!ReceiveQueue.LoopStarted)
                await ReceiveQueue.FetchOneFromSource();

            QueuedAsyncTask<byte[], byte[]>.IOPair Pair = await ReceiveQueue.FetchOutputTaskOnce();
            string ret = Encoding.UTF8.GetString(Pair.Input);
            return ret;
        }

        public async Task<JObject> ReadUncryptedJSON()
        {
            return JObject.Parse(await ReadUncryptedString());
        }

        public async Task<byte[]> ReadBytes()
        {
            if (!ReceiveQueue.LoopStarted)
                await ReceiveQueue.FetchOneFromSource();

            QueuedAsyncTask<byte[], byte[]>.IOPair Pair = await ReceiveQueue.FetchOutputTaskOnce();
            byte[] ret = await Pair.Output;
            return ret;
        }

        public async Task<JObject> ReadJSON()
        {
            return EncryptionManager.ExtractJSON(await ReadBytes());
        }

        protected virtual void PackageProcessing(byte[] raw, byte[] decrypted)
        {
            throw new ReceiveQueueClass.WontOperateThis();
        }

        public virtual void Dispose()
        {
            ReceiveQueue.Dispose();
        }
    }
}
