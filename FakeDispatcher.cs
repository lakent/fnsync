using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace FnSync
{
    public class FakeDispatcher
    {
        public static FakeDispatcher Init()
        {
            FakeDispatcher dispatcher = new FakeDispatcher();
            dispatcher.Start();
            return dispatcher;
        }

        private class DispatcherAction
        {
            public readonly TaskCompletionSource<object> CompletionSource = new TaskCompletionSource<object>();
            public Func<object> Action { get; }

            public DispatcherAction(Func<object> Action)
            {
                this.Action = Action;
            }
        }

        private readonly BufferBlock<DispatcherAction> Queue = new BufferBlock<DispatcherAction>();

        private FakeDispatcher() { }

        private async void Start()
        {
            while (true)
            {
                DispatcherAction dispatcherAction = await Queue.ReceiveAsync();

                try
                {
                    object result = dispatcherAction.Action.Invoke();
                    dispatcherAction.CompletionSource.SetResult(result);
                }
                catch (Exception e)
                {
                    dispatcherAction.CompletionSource.SetException(e);
                    WindowUnhandledException.ShowException(e);
                }
            }
        }

        public Task<object> Invoke(Func<object> action)
        {
            if (action == null)
                throw new ArgumentNullException("action");

            DispatcherAction dispatcherAction = new DispatcherAction(action);
            Queue.Post(dispatcherAction);
            return dispatcherAction.CompletionSource.Task;
        }
    }
}
