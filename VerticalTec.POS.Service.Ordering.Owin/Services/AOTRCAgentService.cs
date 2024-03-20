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

        public AOTRCAgentService()
        {
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += new ResolveEventHandler(AssemblyResolveEventHandler);
        }

        public dynamic SendLoginStatus()
        {
            var assembly = GetRCAgentAssembly();
            var rcConfig = CreateRCConfig(assembly);

            dynamic rcAgent = Activator.CreateInstance(assembly.GetType("RCAgentAOTRR.RCAgent"), rcConfig);
            var loginResp = rcAgent.SendLoginStatus(DateTime.Now);
            return loginResp;
        }

        public dynamic SendLogoutStatus()
        {
            var assembly = GetRCAgentAssembly();
            var rcConfig = CreateRCConfig(assembly);

            dynamic rcAgent = Activator.CreateInstance(assembly.GetType("RCAgentAOTRR.RCAgent"), rcConfig);
            var logoutResp = rcAgent.SendLogoutStatus();
            return logoutResp;
        }

        public dynamic RequestRcCode(OrderTransaction order)
        {
            var assembly = GetRCAgentAssembly();
            var rcConfig = CreateRCConfig(assembly);

            dynamic rcAgent = Activator.CreateInstance(assembly.GetType("RCAgentAOTRR.RCAgent"), rcConfig);
            //var rcAgent = new RCAgentAOTRR.RCAgent(new RCAgentAOTRR.RCConfig { });
            dynamic rc = Activator.CreateInstance(assembly.GetType("RCAgentAOTRR.Receipt"));
            //var rc = new RCAgentAOTRR.Receipt();
            rc.companyCode = rcConfig.companyCode;
            rc.ipAddress = rcConfig.posIPAddress;
            rc.posName = rcConfig.posName;
            rc.rdId = rcConfig.rdId;
            rc.shopId = "";
            rc.transactionDatetime = DateTime.Now;
            rc.receiptDate = DateTime.Today;
            rc.receiptType = "1";
            rc.receiptStatus = "1";

            var rcCode = rcAgent.RequestRcCode(rc);
            return rcCode;
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
