using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace DicomForwarder
{
    [RunInstaller(true)]
    public class ServiceInstaller : Installer
    {
        public ServiceInstaller()
        {
            var spi = new ServiceProcessInstaller();
            var si = new System.ServiceProcess.ServiceInstaller();

            spi.Account = ServiceAccount.LocalSystem;
            spi.Username = null;
            spi.Password = null;

            si.DisplayName = Program.ServiceName;
            si.ServiceName = Program.ServiceName;
            si.StartType = ServiceStartMode.Automatic;

            Installers.Add(spi);
            Installers.Add(si);
        }
    }
}