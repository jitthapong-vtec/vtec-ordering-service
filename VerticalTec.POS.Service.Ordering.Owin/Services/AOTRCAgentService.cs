using RCAgentAOTRR;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace VerticalTec.POS.Service.Ordering.Owin.Services
{
    public class AOTRCAgentService
    {
        public const string RCAgentPath = @"C:\Program Files (x86)\admin\RCAgentInstaller\AIRPORTS OF THAILAND\RC Agent";

        private Assembly _assembly;
        private dynamic _rcAgent;
        private dynamic _rcConfig;

        public AOTRCAgentService()
        {
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += new ResolveEventHandler(AssemblyResolveEventHandler);

            _assembly = GetRCAgentAssembly();
            _rcConfig = CreateRCConfig(_assembly);

            _rcAgent = Activator.CreateInstance(_assembly.GetType("RCAgentAOTRR.RCAgent"), _rcConfig);
        }

        public dynamic SendLoginStatus()
        {
            var loginResp = _rcAgent.SendLoginStatus(DateTime.Now);
            return loginResp;
        }

        public dynamic SendLogoutStatus()
        {
            var logoutResp = _rcAgent.SendLogoutStatus();
            return logoutResp;
        }

        public dynamic RequestRcCode(OrderTransaction order)
        {
            dynamic rc = Activator.CreateInstance(_assembly.GetType("RCAgentAOTRR.Receipt"));
            //var rc = new RCAgentAOTRR.Receipt();
            rc.companyCode = _rcConfig.companyCode;
            rc.ipAddress = _rcConfig.posIPAddress;
            rc.posName = _rcConfig.posName;
            rc.rdId = _rcConfig.rdId;
            rc.shopId = _rcConfig.rdId;
            rc.transactionDatetime = DateTime.Now;
            rc.receiptDate = DateTime.Today;
            rc.receiptType = "1";
            rc.receiptStatus = "1";
            rc.taxInvoice = "1234";
            rc.refNo = "1234";

            var rcCode = _rcAgent.RequestRcCode(rc);
            return rcCode;
        }

        public dynamic ConfirmPrintRcCode(string rcCode)
        {
            var resp = _rcAgent.ConfirmPrintRcCode(rcCode);
            return resp;
        }

        public dynamic CreateRCConfig(Assembly assembly)
        {
            dynamic rcConfig = Activator.CreateInstance(assembly.GetType("RCAgentAOTRR.RCConfig"));
            rcConfig.companyCode = "660026";
            rcConfig.posIPAddress = "117.117.117.001";
            rcConfig.posName = "BACpos01";
            rcConfig.posId = "BC0947635463521";
            rcConfig.shopCode = "BangchakShop";
            rcConfig.clientId = "111";
            rcConfig.clientSecret = "RHeCOkUnxYhiIsYFdl7vVN2xeHKFakTyoUTxvDMc";
            return rcConfig;
        }

        public Assembly GetRCAgentAssembly()
        {
            return Assembly.LoadFile(Path.Combine(RCAgentPath, "RCAgent.dll"));
        }

        private Assembly AssemblyResolveEventHandler(object sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name);
            return Assembly.LoadFile(Path.Combine(RCAgentPath, $"{assemblyName.Name}.dll"));
        }
    }
}
