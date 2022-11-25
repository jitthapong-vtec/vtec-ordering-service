using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VerticalTec.POS.Service.Ordering.Owin
{
    public class InvariantCultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture;

        public InvariantCultureScope()
        {
            _originalCulture = CultureInfo.CurrentCulture;

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        }

        public void Dispose()
        {
            Thread.CurrentThread.CurrentCulture = _originalCulture;
        }
    }
}
