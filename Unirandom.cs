using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FnSync
{
    class Unirandom
    {
        private static Random random = new Random();

        public static int Next()
        {
            return random.Next();
        }
        public static int Next(int l, int u)
        {
            return random.Next(l, u);
        }

    }
}
