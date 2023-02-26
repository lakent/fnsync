using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FnSync
{
    class AutoDisposableTimer : IDisposable
    {
        public static readonly object STATE_TIMER_CANCELLED = new object();

        private Timer? timer = null;

        public delegate void DisposedEventHandler(object sender, object? state);

        public event DisposedEventHandler? DisposedEvent;

        private readonly TimerCallback Callback;
        private readonly int DelayInMills = Timeout.Infinite;
        private readonly CancellationToken? Cancellation;

        private int AlreadyDisposed = 0;

        public AutoDisposableTimer(TimerCallback? callback, int DelayInMills, CancellationToken? Cancellation = null, bool StartImmediately = true)
        {
            this.Callback = (state) =>
            {
                try
                {
                    callback?.Invoke(state);
                }
                finally
                {
                    Dispose();
                }
            };

            this.DelayInMills = DelayInMills;
            this.Cancellation = Cancellation;

            if (StartImmediately)
            {
                Start();
            }
        }

        public void Start()
        {
            if (Thread.VolatileRead(ref AlreadyDisposed) == 0) // Atomic
            {
                if (DelayInMills == 0)
                {
                    Dispose(null);
                }
                else if (DelayInMills > 0)
                {
                    timer = new Timer(this.Callback, null, DelayInMills, Timeout.Infinite);
                } else // DelayInMills < 0, never timeout
                {
                    // No timer
                }

                if(Cancellation != null)
                {
                    Cancellation.Value.Register(Dispose, STATE_TIMER_CANCELLED, true);
                }
            }
        }

        public void Dispose()
        {
            Dispose(null);
        }

        public void Dispose(object? state)
        {
            if (Interlocked.CompareExchange(ref AlreadyDisposed, 1, 0) == 0)
            {
                timer?.Dispose();
                timer = null;
                DisposedEvent?.Invoke(this, state);
            }
        }
    }
}
