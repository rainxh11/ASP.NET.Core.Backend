using System.ComponentModel;
using System.Configuration.Install;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace DocumentWatcher
{
    [RunInstaller(true)]
    public class ServiceInstaller : Installer
    {
        public ServiceInstaller()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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
            else
            {
                throw new System.Exception("Service Setup is only supported on Windows Platform.");
            }
        }
    }
}