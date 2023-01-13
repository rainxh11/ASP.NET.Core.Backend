using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Hosting.Systemd;
using Microsoft.AspNetCore.Hosting.WindowsServices;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using Syncfusion.DocIO;
using Syncfusion.DocIO.Utilities;
using Syncfusion.DocIORenderer;
using RisDocumentServer.Helpers.Models;
using Serilog;
using Serilog.Sinks.File;

namespace RisDocumentServer
{
    public class Program
    {
        public static Serilog.Core.Logger? Logger;

        public static void InstallWindowsService()
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
                            Arguments = $"create RisDocumentServer binpath= {Path.Combine(AppContext.BaseDirectory, "RisDocumentServer.exe")} displayname= RisDocumentServer start= auto error= ignore",
                            RedirectStandardInput = true,
                            UseShellExecute = false
                        }
                    };
                    Console.WriteLine(sc.StartInfo.Arguments);
                    sc.Start();
                    sc.WaitForExit();

                    ServiceController service = new ServiceController("RisDocumentServer");
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, new TimeSpan(0, 0, 10));
                }
            }
            catch
            {
            }
        }

        public static async Task Main(string[] args)
        {
            if (args.Length != 0)
            {
                if (args[0] == "install")
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) InstallWindowsService();
                }
                else
                {
                    await CreateWebHostBuilder(args).Build().RunAsync();
                }
            }
            else
            {
                Logger = new LoggerConfiguration()
                    .WriteTo.File(AppContext.BaseDirectory + @"\Logs\log.txt", rollingInterval: RollingInterval.Day)
                    .CreateLogger();

                await Helpers.DatabaseHelper.InitDatabase();
                Helpers.DatabaseHelper.StartWatchers();

                await CreateWebHostBuilder(args).Build().RunAsync();
            }
        }

        public static IHostBuilder CreateWebHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .UseSystemd()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<RisDocumentService>();
                })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls(ConfigModel.GetConfig().GetHost());
                webBuilder.UseStartup<Startup>();
            });
        }
    }
}