using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Gaming.Input;
using Windows.UI.StartScreen;

namespace FnSync
{
    class SpeedWatch
    {
        private long Start;
        private long Bytes;

        public SpeedWatch()
        {
            Reset();
        }

        public long Add(long n)
        {
            return Interlocked.Add(ref Bytes, n);
        }

        public double BytesPerSec(long WhenAfter = 1)
        {
            long Elapsed = DateTimeOffset.Now.ToUnixTimeMilliseconds() - Interlocked.Read(ref Start);
            if (Elapsed >= WhenAfter)
            {
                return (double)Interlocked.Read(ref Bytes) / (double)Elapsed * 1000.0;
            }
            else
            {
                return -1;
            }
        }

        public void Reset()
        {
            Interlocked.Exchange(ref Start, DateTimeOffset.Now.ToUnixTimeMilliseconds());
            Interlocked.Exchange(ref Bytes, 0);
        }
    }
}
