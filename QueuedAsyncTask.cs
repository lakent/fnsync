using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace FnSync
{
    public abstract class QueuedAsyncTask<I, O> : IDisposable
    {
        public class IOPair
        {
            public I Input { get; private set; }
            public Task<O> Output { get; private set; }

            public IOPair(I Input, QueuedAsyncTask<I, O> QueuedTask)
            {
                this.Input = Input;

                if (QueuedTask == null)
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

            public IOPair()
            {
                this.Input = default;
                this.Output = null;
            }
        }

        public class WontOperateThis : Exception { };
        public class Exited : Exception { };

        protected abstract Task<I> InputSource();
        protected abstract O TaskBody(I input);
        protected abstract void OnDone(I input, O output);
        protected virtual void OnException(QueuedAsyncTask<I, O> sender, Exception e) { }

        private readonly BufferBlock<IOPair> TaskQueue = new BufferBlock<IOPair>();

        public bool LoopStarted { get; private set; } = false;

        private bool IsDisposed = false;

        public async Task FetchOneFromSource()
        {
            I Input = await InputSource();

            if (IsDisposed)
                throw new Exception("Has disposed");

            InputManually(Input);
        }

        public void InputManually(I Input)
        {
            if (Input == null)
                throw new ArgumentNullException();

            TaskQueue.Post(new IOPair(Input, this));
        }

        public async Task PerformOnDoneOnce(IOPair iOPair)
        {
            O output = await iOPair.Output;
            OnDone(iOPair.Input, output);
        }

        private async void InputLoop()
        {
            while (true)
            {
                try
                {
                    await FetchOneFromSource();
                }
                catch (WontOperateThis e)
                {
                    return;
                }
                catch (Exception e)
                {
                    OnException(this, e);
                    Dispose();
                    return;
                }
            }
        }

        private async void OutputLoop()
        {
            while (true)
            {
                try
                {
                    IOPair iOPair = await FetchOutputTaskOnce();
                    await PerformOnDoneOnce(iOPair);
                }
                catch (WontOperateThis e)
                {
                    return;
                }
                catch (Exception e)
                {
                    OnException(this, e);
                    Dispose();
                    return;
                }
            }
        }

        public void StartLoop()
        {
            LoopStarted = true;
            InputLoop();
            OutputLoop();
        }

        public async Task<IOPair> FetchOutputTaskOnce()
        {
            IOPair iOPair = await TaskQueue.ReceiveAsync();

            if (iOPair.Output == null || iOPair.Input == null)
                throw new Exited();

            return iOPair;
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;
            TaskQueue.Post(new IOPair());
        }
    }
}
