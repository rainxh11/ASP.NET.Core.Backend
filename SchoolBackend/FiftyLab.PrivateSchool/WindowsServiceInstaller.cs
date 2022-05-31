using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace FiftyLab.PrivateSchool
{
    public class WindowsServiceInstaller
    {
        public static void InstallService()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process sc = new Process()
                    {
                        StartInfo = new ProcessStartInfo()
                        {
                            FileName = "sc",
                            Arguments = $"create 50LabSchool binpath= {Path.Combine(AppContext.BaseDirectory, "FiftyLabSchool.exe")} displayname= 50LabSchool start= auto error= ignore",
                            RedirectStandardInput = true,
                            UseShellExecute = false
                        }
                    };
                    Console.WriteLine(sc.StartInfo.Arguments);
                    sc.Start();
                    sc.WaitForExit();

                    ServiceController service = new ServiceController("50LabSchool");
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
}
