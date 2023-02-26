using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FnSync.Model
{
    class TrayDoubleClickChoice
    {
        public string Text { get; private set; }
        public string Value { get; private set; }

        public TrayDoubleClickChoice(string text, string value)
        {
            Text = text;
            Value = value;
        }

    }
}
