using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FnSync
{
    interface IMessageWithBinary
    {
        JObject Message { get; }
        byte[] Binary { get; }
    }

    class MessageWithBinary : IMessageWithBinary
    {
        public JObject Message { get; set; }
        public byte[] Binary { get; set; }

        public IMessageWithBinary CloneTo()
        {
            return new MessageWithBinary()
            {
                Message = this.Message,
                Binary = this.Binary,
            };
        }

        public MessageWithBinary Reset()
        {
            this.Message = null;
            this.Binary = null;
            return this;
        }

    }
}
