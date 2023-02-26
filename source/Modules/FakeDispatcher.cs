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
        public static bool IsOnUIThread()
        {
            return System.Windows.Application.Current.Dispatcher.CheckAccess();
        }

        public static FakeDispatcher Init()
        {
            if (!IsOnUIThread())
            {
                throw new InvalidOperationException("FakeDispatcher must be created in UI thread.");
            }

            FakeDispatcher dispatcher = new();
            dispatcher.Start();
            return dispatcher;
        }

        private class DispatcherAction
        {
            public readonly TaskCompletionSource<object?>? CompletionSource;
            public readonly Func<object?> Action;

            public DispatcherAction(Func<object?> Action, bool Awaitable)
            {
                this.Action = Action;

                if (Awaitable)
                {
                    CompletionSource = new TaskCompletionSource<object?>();
                }
                else
                {
                    CompletionSource = null;
                }
            }
        }

        private readonly BufferBlock<DispatcherAction> Queue = new();

        private FakeDispatcher() { }

        private async void Start()
        {
            while (true)
            {
                DispatcherAction dispatcherAction = await Queue.ReceiveAsync();

                try
                {
                    object? result = dispatcherAction.Action.Invoke();
                    dispatcherAction.CompletionSource?.SetResult(result);
                }
                catch (Exception e)
                {
                    dispatcherAction.CompletionSource?.SetException(e);
                    WindowUnhandledException.ShowException(e);
                }
            }
        }

        public Task<object?> InvokeAwaitable(Func<object?> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            DispatcherAction dispatcherAction = new(action, true);
            Queue.Post(dispatcherAction);
            return dispatcherAction.CompletionSource!.Task;
        }

        public void Invoke(Func<object?> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            DispatcherAction dispatcherAction = new(action, false);
            Queue.Post(dispatcherAction);
        }
    }
}
