using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace FnSync
{
    public abstract class QueuedAsyncTask<I, O> : IDisposable
    {
        public class IOPair
        {
            public I Input { get; private set; }
            public Task<O>? Output { get; private set; }

            public IOPair(I Input, QueuedAsyncTask<I, O> QueuedTask)
            {
                this.Input = Input;

                if (QueuedTask == null || QueuedTask.HasEmptyTask)
                {
                    this.Output = null;
                }
                else
                {
                    this.Output = Task.Run(() =>
                    {
                        return QueuedTask.TaskBody(Input);
                    });
                }
            }

            /*
            public IOPair()
            {
                // Used as a terminal signal
                this.Input = default;
                this.Output = null;
            }
            */
        }

        public class WontOperateThis : Exception { };
        public class Exited : Exception { };

        protected abstract Task<I> InputSource();
        protected abstract O TaskBody(I input);
        protected abstract void OnTaskDone(I input, O? output);
        protected virtual void OnException(QueuedAsyncTask<I, O> sender, Exception e) { }

        public readonly bool HasEmptyTask;

        private readonly BufferBlock<IOPair> TaskQueue = new();
        private readonly TaskCompletionSource<object?> QueueCompletion = new();

        private int _LoopStarted = 0;
        public bool LoopStarted => _LoopStarted != 0;

        private int IsDisposed = 0;

        private static void ClearQueue<T>(BufferBlock<T> Queue)
        {
            while (Queue.TryReceive(out _)) ;
        }

        public QueuedAsyncTask(bool EmptyTask = false)
        {
            this.HasEmptyTask = EmptyTask;
        }

        public async Task FeedFromSource()
        {
            Task<I> WaitInput = InputSource();
            await Task.WhenAny(WaitInput, QueueCompletion.Task);
            if (QueueCompletion.Task.IsCompleted)
            {
                throw new Exited();
            }

            I Input = await WaitInput;

            InputManually(Input);
        }

        public void InputManually(I Input)
        {
            if (Input == null)
            {
                throw new ArgumentNullException();
            }

            if (QueueCompletion.Task.IsCompleted)
            {
                throw new Exited();
            }

            TaskQueue.Post(new IOPair(Input, this));
        }

        public async Task PerformOnDoneOnce(IOPair iOPair)
        {
            if (iOPair.Output == null)
            {
                OnTaskDone(iOPair.Input, default);
            }
            else
            {
                O output = await iOPair.Output;
                OnTaskDone(iOPair.Input, output);
            }
        }

        private async void InputLoop()
        {
            while (true)
            {
                try
                {
                    await FeedFromSource();
                }
                catch (WontOperateThis)
                {
                    return;
                }
                catch (Exited)
                {
                    ClearQueue<IOPair>(TaskQueue);
                    return;
                }
                catch (Exception e)
                {
                    OnException(this, e);
                    //Dispose();
                    //return;
                }
            }
        }

        private async void OutputLoop()
        {
            while (true)
            {
                try
                {
                    Task<IOPair> iOPairTask = TaskQueue.ReceiveAsync();
                    await Task.WhenAny(iOPairTask, QueueCompletion.Task);
                    if (QueueCompletion.Task.IsCompleted)
                    {
                        throw new Exited();
                    }

                    IOPair iOPair = await iOPairTask;
                    await PerformOnDoneOnce(iOPair);
                }
                catch (WontOperateThis)
                {
                    return;
                }
                catch (Exited)
                {
                    return;
                }
                catch (Exception e)
                {
                    OnException(this, e);
                    //Dispose();
                    //return;
                }
            }
        }

        public void StartLoop()
        {
            if (Interlocked.CompareExchange(ref _LoopStarted, 1, 0) == 0)
            {
                InputLoop();
                OutputLoop();
            }
        }

        public Task<IOPair> FetchOutputTaskOnce()
        {
            if (QueueCompletion.Task.IsCompleted)
            {
                throw new Exited();
            }

            return TaskQueue.ReceiveAsync();
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref IsDisposed, 1, 0) == 0)
            {
                QueueCompletion.SetResult(null);
                TaskQueue.Complete();
                ClearQueue<IOPair>(TaskQueue);
            }
        }
    }
}
