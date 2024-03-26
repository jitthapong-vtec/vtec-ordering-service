using System;
using System.Collections.Generic;
using System.Text;

namespace VerticalTec.POS
{
    public class VtecPOSException : Exception
    {
        public VtecPOSException() { }

        public VtecPOSException(string message) : base(message) { }

        public VtecPOSException(string message, Exception inner) : base(message, inner) { }
    }
}
