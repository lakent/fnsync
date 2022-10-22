using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FnSync
{
    class FileCutInside : FileCopyInside
    {
        public class CutInsideEntry : BaseEntry { }
        public FileCutInside(): base()
        {
            Operation = Operations.CUT;
        }
    }
}

