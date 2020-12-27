using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            Bytes += n;
            return Bytes;
        }

        public double BytesPerSec(long WhenAfter = 1)
        {
            long Elapsed = DateTimeOffset.Now.ToUnixTimeMilliseconds() - Start;
            if(Elapsed >= WhenAfter)
            {
                return (double)Bytes / (double)Elapsed * 1000.0;
            } else
            {
                return -1;
            }
        }

        public void Reset()
        {
            Start = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            Bytes = 0;
        }
    }
}
