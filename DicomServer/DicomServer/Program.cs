using DicomServer.Helper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading.Tasks;
using MongoDB.Entities;
using Akavache;
using System.Reactive.Linq;
using MongoDB.Driver;
using DicomServer.Helpers;
using DicomServer.Models;
using Dicom.Log;
using DicomServerWorkList;
using Akavache.Sqlite3;

namespace DicomServer
{
    public class Program
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
                            Arguments = $"create SklabDicomServer binpath= {Path.Combine(AppContext.BaseDirectory, "SklabDicomServer.exe")} displayname= SklabDicomServer start= auto error= ignore",
                            RedirectStandardInput = true,
                            UseShellExecute = false
                        }
                    };
                    Console.WriteLine(sc.StartInfo.Arguments);
                    sc.Start();
                    sc.WaitForExit();

                    ServiceController service = new ServiceController("SklabDicomServer");
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
                    InstallService();
                }
                else
                {
                    Console.WriteLine($"Argument '{args[0]}' undefined!");
                }
            }
            else
            {
                await Init();

                Console.CancelKeyPress += OnConsoleCancel;
                    LogManager.SetImplementation(ConsoleLogManager.Instance);
                var config = ConfigHelper.GetConfig();

                Console.WriteLine($"Starting Worklist server with AET: {config.WorkerListAET} on port {config.WorkerListPort}");

                WorklistServer.Start(config.WorkerListPort, config.WorkerListAET);

                Console.WriteLine("Press [CTRL + C] to stop the service");

                ModalitiesRetrieveWorker.StartWorker();
                
                await CreateHostBuilder(args).Build().RunAsync();
            }
        }

        private static void OnConsoleCancel(object sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("Stopping service");

            WorklistServer.Stop();
            Process.GetCurrentProcess().Kill();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .UseSystemd()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<DicomService>();
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls(ConfigHelper.GetConfig().GetHost());
                    webBuilder.UseStartup<Startup>();
                });
        }
        public static async Task Init()
        {

            try
            {
                var config = ConfigHelper.GetConfig();

                if (!Directory.Exists(AppContext.BaseDirectory + @"\Cache"))
                {
                    Directory.CreateDirectory(AppContext.BaseDirectory + @"\Cache");
                }
   
                BlobCache.LocalMachine = new SqlRawPersistentBlobCache(AppContext.BaseDirectory + @"\Cache\imagecache.db");

                Akavache.Registrations.Start("DicomServer_ImageCache");
                await BlobCache.LocalMachine.Vacuum();

                var setting = MongoClientSettings.FromConnectionString(config.MongoDBConnectionString);
                setting.ConnectTimeout = TimeSpan.FromSeconds(90);
                await DB.InitAsync(config.MongoDBDatabase, setting);


                MongoHelper.StartWatchers();

                CacheHelper.Init();
                RefreshHelper.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine("Press Any Key to Exit.");
                Console.ReadKey();
                Process.GetCurrentProcess().Kill();
            }
            
        }
            
    }
}
