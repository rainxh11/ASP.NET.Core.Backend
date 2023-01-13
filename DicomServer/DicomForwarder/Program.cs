using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using Dicom;
using Dicom.Network;

namespace DicomForwarder
{
    internal partial class Program
    {
        public static string ServiceName = "SkLabDicomForwarder";

        public static void InstallWindowsService()
        {
            try
            {
                Process sc = new Process()
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = "sc",
                        Arguments = $"create SkLabDicomForwarder binpath= {Path.Combine(AppContext.BaseDirectory, "DicomForwarder.exe")} displayname= SkLabDicomForwarder start= auto error= ignore",
                        RedirectStandardInput = true,
                        UseShellExecute = false
                    }
                };
                Console.WriteLine(sc.StartInfo.Arguments);
                sc.Start();
                sc.WaitForExit();

				sc.StartInfo.Arguments = $"failure SkLabDicomForwarder actions= restart/5000/restart/5000/restart/5000 reset= 60";
                sc.WaitForExit();

                ServiceController service = new ServiceController("SkLabDicomForwarder");
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, new TimeSpan(0, 0, 10));
            }
            catch
            {
            }
        }

        private static void Main(string[] args)
        {
            if (Environment.UserInteractive)
            {
                if (args.Length != 0)
                {
                    if (args[0] == "install")
                    {
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) InstallWindowsService();
                    }
                }
                else
                {
                    var port = ConfigHandler.GetConfig().ServerPort;

                    Console.WriteLine($"Starting C-Store SCP server on port {port}");

                    using (var server = DicomServer.Create<DicomCStoreSCP>(port))
                    {
                        //

                        Console.WriteLine("Press <return> to end...");
                        Console.ReadLine();
                    }
                }
            }
            else
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    using (var service = new DicomForwardService())
                    {
                        ServiceBase.Run(service);
                    }
                }
            }
        }
    }
}