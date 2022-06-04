using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace ReniwnMailServiceApi;

public class WindowsServiceInstaller
{
    public static void InstallService()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var name = Assembly.GetExecutingAssembly().GetName().Name;
                var fullName = Process.GetCurrentProcess().MainModule.FileName;
                var sc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "sc",
                        Arguments =
                            $"create {name} binpath= {fullName} displayname= {name} start= auto error= ignore",
                        RedirectStandardInput = true,
                        UseShellExecute = false
                    }
                };
                Console.WriteLine(sc.StartInfo.Arguments);
                sc.Start();
                sc.WaitForExit();

                var service = new ServiceController(name);
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, new TimeSpan(0, 0, 10));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}