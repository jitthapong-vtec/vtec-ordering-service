using System;
using System.Collections.Generic;
using System.Text;

namespace VerticalTec.POS
{
    public class VtecPOSException : Exception
    {
        public VtecPOSException(string tag) : base()
        {
            Tag = tag;
        }

        public VtecPOSException(string tag, string message) : base(message)
        {
            Tag = tag;
        }

        public VtecPOSException(string tag, string message, Exception innerException) : base(message, innerException)
        {
            Tag = tag;
        }

        public string Tag { get; set; }
    }
}
