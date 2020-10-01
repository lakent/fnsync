using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace FnSync
{
    class SocketMsgQueue<T>
    {
        public Socket Wait { get; private set; } = new Socket(SocketType.Dgram, ProtocolType.Udp);
        private Socket Send = new Socket(SocketType.Dgram, ProtocolType.Udp);
        private IPEndPoint to = null;

        private ConcurrentQueue<T> queue = new ConcurrentQueue<T>();

        public SocketMsgQueue()
        {
            do
            {
                to = new IPEndPoint(IPAddress.Loopback, Unirandom.Next(10000, 60000));

                try
                {
                    Wait.Bind(to);
                }
                catch (Exception)
                {
                    continue;
                }

                break;

            } while (true);
        }

        public void Push(T msg)
        {
            queue.Enqueue(msg);
            Send.SendTo(new byte[] { 0 }, to);
        }

        public T GetMsg()
        {
            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            EndPoint senderRemote = (EndPoint)sender;

            Wait.ReceiveFrom(new byte[1], ref senderRemote);

            if (queue.TryDequeue(out T msg))
            {
                return msg;
            }
            else
            {
                return default;
            }
        }
    }
}
