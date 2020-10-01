using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Windows;

namespace FnSync
{
    class SelectTask
    {
        public interface ITime
        {
            int TimeRemain { get; set; }
        }

        public class TimeQueue<T> where T : ITime
        {

            private readonly LinkedList<T> list = new LinkedList<T>();

            public TimeQueue() { }

            public void Add(T time)
            {
                if (object.Equals(time, default(T)))
                {
                    return;
                }

                LinkedListNode<T> node = list.First;

                while (node != null)
                {
                    if (time.TimeRemain < node.Value.TimeRemain)
                    {
                        list.AddBefore(node, time);
                        return;
                    }

                    node = node.Next;
                }

                list.AddLast(time);
            }

            public void Remove(T time)
            {
                if (object.Equals(time, default(T)))
                {
                    return;
                }

                list.Remove(time);
            }

            public void Decrease(int amount)
            {
                if (amount > 0)
                {
                    foreach (T t in list)
                    {
                        t.TimeRemain -= amount;
                    }
                }
            }

            public T Head()
            {
                if (list.First == null)
                {
                    return default;
                }

                return list.First.Value;
            }

            public delegate void TimeoutAction(T obj);

            public void HandleTimeout(TimeoutAction action)
            {
                LinkedListNode<T> node = list.First;
                while (node != null)
                {
                    if (node.Value.TimeRemain <= 0)
                    {
                        var next = node.Next;
                        action?.Invoke(node.Value);
                        node = next;
                    }
                    else
                    {
                        node = node.Next;
                    }
                }
            }
        }

        public static SelectTask Singleton = new SelectTask();

        private class ControlMsg
        {
            public Object WrapperObj;
            public bool ListenRead;
            public bool ListenWrite;
            public OnRaise ReadyEvent;
            public int TimeoutMills;
            public bool OnMainThread;
        }

        private class Info : ITime
        {
            public OnRaise ReadyEvent;
            public Object WrapperObj;
            public int TimeoutMills;
            public bool OnMainThread;

            public Socket socket;

            public int TimeRemain { get; set; }
        }

        public enum Result
        {
            DISPOSE = -2,
            RETREAT = -1,
            KEEP = 0,
        };

        public delegate Result OnRaise(Object target, bool read, bool write, bool error);

        private readonly HashSet<Socket> ReadSet = new HashSet<Socket>();
        private readonly HashSet<Socket> WriteSet = new HashSet<Socket>();
        private readonly Dictionary<Socket, Info> InfoMap = new Dictionary<Socket, Info>();
        private readonly TimeQueue<Info> TimeoutList = new TimeQueue<Info>();

        private readonly SocketMsgQueue<ControlMsg> ControlQueue = new SocketMsgQueue<ControlMsg>();

        private readonly int SelectThreadId;
        private int WorkerThreadId = -1;

        public SelectTask()
        {
            AddInner(ControlQueue, true, false, 0, OnRegister, false);
            Thread thread = new Thread(() => Select());
            SelectThreadId = thread.ManagedThreadId;
            thread.Start();
        }

        private Result OnRegister(Object _, bool __, bool ___, bool ____)
        {
            ControlMsg msg = ControlQueue.GetMsg();
            if (msg != null)
            {
                AddInner(msg.WrapperObj, msg.ListenRead, msg.ListenWrite, msg.TimeoutMills, msg.ReadyEvent, msg.OnMainThread);
            }

            return Result.KEEP;
        }

        private Socket FetchSocket(Object WrapperObj)
        {
            switch (WrapperObj)
            {
                case Socket sock1:
                    return sock1;

                case TcpListener tcpListener:
                    return tcpListener.Server;

                case TcpClient tcpClient:
                    return tcpClient.Client;

                case SocketMsgQueue<ControlMsg> msgQueue:
                    return msgQueue.Wait;

                case PhoneClient phoneClient:
                    return phoneClient.Client;

                default:
                    return null;
            }
        }

        private void AddInner(Socket target, Object wrapper, bool read, bool write, int timeoutMills, OnRaise onReady, bool OnMainThread)
        {
            if (read)
            {
                this.ReadSet.Add(target);
            }
            else
            {
                this.ReadSet.Remove(target);
            }

            if (write)
            {
                this.WriteSet.Add(target);
            }
            else
            {
                this.WriteSet.Remove(target);
            }

            if (onReady != null)
            {
                Info one = new Info()
                {
                    ReadyEvent = onReady,
                    WrapperObj = wrapper,
                    TimeoutMills = timeoutMills,
                    OnMainThread = OnMainThread,
                    socket = target,

                    TimeRemain = timeoutMills
                };

                if (InfoMap.TryGetValue(target, out Info old))
                {
                    TimeoutList.Remove(old);
                }

                if (timeoutMills > 0)
                {
                    TimeoutList.Add(one);
                }

                InfoMap[target] = one;
            }
            else if (InfoMap.TryGetValue(target, out Info old))
            {
                TimeoutList.Remove(old);
                InfoMap.Remove(target);

                if (wrapper is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        private void AddInner(Object target, bool read, bool write, int timeoutMills, OnRaise onReady, bool OnMainThread)
        {
            Socket socket = FetchSocket(target);
            if (socket != null)
            {
                AddInner(socket, target, read, write, timeoutMills, onReady, OnMainThread);
            }
        }

        private void Delete(Info info, bool Dispose)
        {
            if (info != null)
            {
                Socket target = info.socket;

                ReadSet.Remove(target);
                WriteSet.Remove(target);

                if (Dispose && info.WrapperObj is IDisposable disposable)
                {
                    Application.Current.Dispatcher.InvokeIfNecessaryNoThrow(delegate
                    {
                        disposable.Dispose();
                    }, info.OnMainThread);
                }

                TimeoutList.Remove(info);
                InfoMap.Remove(target);
            }
        }

        private void Select()
        {
            while (true)
            {
                List<Socket> read_set = new List<Socket>(ReadSet);
                List<Socket> write_set = new List<Socket>(WriteSet);
                List<Socket> error_set = new List<Socket>(read_set.Count + write_set.Count);
                error_set.AddRange(read_set);
                error_set.AddRange(write_set.Except(error_set));

                Info head = TimeoutList.Head();
                int elapsed = -1;
                if (head != null)
                {
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    Socket.Select(read_set, write_set, error_set, head.TimeRemain * 1000 /* microseconds */);
                    stopwatch.Stop();

                    elapsed = (int)(stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    Socket.Select(read_set, write_set, error_set, -1);
                }

                HashSet<Socket> final_set = new HashSet<Socket>(read_set);
                final_set.UnionWith(write_set);
                final_set.UnionWith(error_set);

                if (elapsed >= 0)
                {
                    TimeoutList.Decrease(elapsed);
                    TimeoutList.HandleTimeout(delegate (Info info)
                    {
                        if (!final_set.Contains(info.socket))
                        {
                            Application.Current.Dispatcher.InvokeIfNecessaryNoThrow(delegate
                            {
                                info.ReadyEvent(info.WrapperObj, false, false, true);
                            }, info.OnMainThread);

                            Delete(info, true);
                        }
                        else 
                        { 
                            // Handle it below
                        }
                    });
                }

                bool SocketErrored(Socket s, Info i)
                {
                    try
                    {
                        return
                            (head != null && i.TimeoutMills > 0 && i.TimeRemain <= 0) // Timeout
                            ||
                            (read_set.Contains(s) && s.Available == 0 && !s.IsListening()) /* Closed by peer */ || error_set.Contains(s);
                    }
                    catch (Exception e)
                    {
                        // Any exception indicates an error
                        return true;
                    }
                }

                foreach (Socket socket in final_set)
                {
                    if (InfoMap.TryGetValue(socket, out Info info))
                    {
                        bool onError = SocketErrored(socket, info);
                        bool pendingReading = read_set.Contains(socket);
                        bool pendingWriting = write_set.Contains(socket);

                        if (pendingReading && !pendingWriting && !onError && info.WrapperObj is LengthPrefixedStreamBuffer buffer && !buffer.StreamIsFinished())
                        {
                            // Unfinished stream
                            continue;
                        }

                        Result state = Result.KEEP;
                        Application.Current.Dispatcher.InvokeIfNecessaryNoThrow(delegate
                        {
                            WorkerThreadId = Thread.CurrentThread.ManagedThreadId;
                            state = info.ReadyEvent(info.WrapperObj, pendingReading, pendingWriting, onError);
                            WorkerThreadId = -1;
                        }, info.OnMainThread);

                        if (state != Result.KEEP || onError)
                        {
                            Delete(info, state == Result.DISPOSE || onError);
                        }
                        else
                        {
                            if (info.TimeoutMills > 0 && info.TimeRemain <= 0)
                            {
                                TimeoutList.Remove(info);
                                info.TimeRemain = info.TimeoutMills;
                                TimeoutList.Add(info);
                            }
                        }
                    }
                    else
                    {
                        Delete(info, false);
                    }
                }
            }
        }

        public void AddOrUpdate(Object target, bool read, bool write, int timeoutMills, OnRaise onReady, bool OnMainThread)
        {
            const int MAX_TIMEOUT = int.MaxValue /* microseconds */ / 1000;
            timeoutMills = Math.Min(timeoutMills, MAX_TIMEOUT);

            if (Thread.CurrentThread.ManagedThreadId == WorkerThreadId)
            {
                AddInner(target, read, write, timeoutMills, onReady, OnMainThread);
            }
            else
            {
                ControlMsg msg = new ControlMsg()
                {
                    WrapperObj = target,
                    ListenRead = read,
                    ListenWrite = write,
                    ReadyEvent = onReady,
                    TimeoutMills = timeoutMills,
                    OnMainThread = OnMainThread
                };

                ControlQueue.Push(msg);
            }
        }

        public void RetreatAndDispose(Object target)
        {
            AddOrUpdate(target, false, false, 0, null, true);
        }
    }
}


/*
 
 1、超时、无响应
 2、超时、有相应
 3、未超时、有相应
 
 */
