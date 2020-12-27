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
        private Timer timer = null;

        public delegate void DisposedEventHandler(object sender, Object state);

        public event DisposedEventHandler DisposedEvent;

        private TimerCallback Callback = null;
        private int DelayInMills = Timeout.Infinite;

        public AutoDisposableTimer(TimerCallback callback, int DelayInMills, bool StartImmediately)
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

            if (StartImmediately)
                Start();
        }

        public void Start()
        {
            timer = new Timer(this.Callback, null, DelayInMills, Timeout.Infinite);
        }
        
        public AutoDisposableTimer(TimerCallback callback, int DelayInMills): this(callback, DelayInMills, true) {}

        public void Dispose()
        {
            Dispose(null);
        }

        public void Dispose(Object state)
        {
            lock (this)
            {
                if (timer != null)
                {
                    timer?.Dispose();
                    timer = null;
                    DisposedEvent?.Invoke(this, state);
                }
            }
        }
    }
}
