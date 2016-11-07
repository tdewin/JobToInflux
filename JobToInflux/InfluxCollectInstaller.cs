using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace JobToInflux
{
    [RunInstaller(true)]
    public class InfluxCollectInstaller : Installer
    {
        private ServiceProcessInstaller processInstaller;
        private ServiceInstaller serviceInstaller;

        public InfluxCollectInstaller()
        {
            processInstaller = new ServiceProcessInstaller();
            serviceInstaller = new ServiceInstaller();

            processInstaller.Account = ServiceAccount.LocalSystem;
            serviceInstaller.StartType = ServiceStartMode.Automatic;
            serviceInstaller.ServiceName = "InfluxCollectService"; //must match servicename

            Installers.Add(serviceInstaller);
            Installers.Add(processInstaller);
        }
    }
}
