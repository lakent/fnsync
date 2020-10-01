using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FnSync
{
    class AutoDisposableTimer
    {
        private readonly Timer timer = null;
        
        public AutoDisposableTimer(TimerCallback callback, int delay)
        {
            timer = new Timer((state) =>
            {
                try
                {
                    callback?.Invoke(state);
                }
                finally
                {
                    timer?.Dispose();
                }
            }, null, delay, Timeout.Infinite);
        }
    }
}
