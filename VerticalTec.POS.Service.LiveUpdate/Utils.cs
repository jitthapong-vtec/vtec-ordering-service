using System;
using System.Collections.Generic;
using System.Text;

namespace VerticalTec.POS.Service.LiveUpdate
{
    public class Utils
    {
        public static bool CompareVersion(string versionLiveUpdate, string versionInfo)
        {
            return versionLiveUpdate.Equals(versionInfo);
        }
    }
}
