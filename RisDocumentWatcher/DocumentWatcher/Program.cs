using DocumentWatcher.Helpers;
using Refit;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Linq;
using IWshRuntimeLibrary;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using Task = System.Threading.Tasks.Task;

namespace DocumentWatcher
{
    internal class Program
    {
        public static DocumentServerApi documentServerApi;
        public static DicomServerApi dicomServerApi;
        public static string ServiceName = "SkLabDocumentWatcher";
        public static void Install(DirectoryInfo dir)
        {
            var file = new FileInfo(AppContext.BaseDirectory + @"\SkLabDocumentWatcher.exe");
            var destination = new FileInfo(dir.FullName + @"\SkLabDocumentWatcher.exe");

            try
            {
                if (!destination.Exists)
                {
                    file.CopyTo(destination.FullName, true);
                }
                Process sc = new Process()
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = destination.FullName
                    }
                };
                sc.Start();
            }
            catch
            {

            }
        }
        public static void InstallWindowsService()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    Process sc = new Process()
                    {
                        StartInfo = new ProcessStartInfo()
                        {
                            FileName = "sc",
                            Arguments = $"create {ServiceName} binpath= {Path.Combine(AppContext.BaseDirectory, "SkLabDocumentWatcher.exe")} displayname= {ServiceName} start= auto error= ignore type= interact type= own ",
                            RedirectStandardInput = true,
                            UseShellExecute = false
                        }
                    };
                    //Console.WriteLine(sc.StartInfo.Arguments);
                    sc.Start();
                    sc.WaitForExit();

                    ServiceController service = new ServiceController(ServiceName);
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, new TimeSpan(0, 0, 10));
                }
                catch
                {
                }
            }
        }
        public static void CreateShortcut(DirectoryInfo dir)
        {
            var file = new FileInfo(AppContext.BaseDirectory + @"\SkLabDocumentWatcher.exe");
            var destination = new FileInfo(dir.FullName + @"\SkLabDocumentWatcher.lnk");

            IWshRuntimeLibrary.WshShell shell = new WshShell();
            IWshShortcut shortcut = shell.CreateShortcut(destination.FullName);
            shortcut.TargetPath = file.FullName;
            shortcut.Save();
        }
        public static string GetCurrentUser()
        {
            return $"{Environment.UserDomainName}\\{Environment.UserName}";
        }
        public static void CreateTaskScheduler()
        {
            var file = new FileInfo(AppContext.BaseDirectory + @"DocumentWatcherWatchdog.exe");

            using (TaskService ts = new TaskService())
            {
                TaskDefinition td = ts.NewTask();

                td.RegistrationInfo.Description = "Monitor 50LAB RIS Server for new studies.";
                td.Principal.RunLevel = TaskRunLevel.Highest;
                td.Triggers.Add(new BootTrigger());
                td.Triggers.Add(new IdleTrigger());
                
                td.Actions.Add(new ExecAction(file.FullName));

                td.Settings.RestartCount = int.MaxValue;

                try
                {
                    TaskService.Instance.RootFolder.RegisterTaskDefinition("DocumentWatcherWatchdog", td, TaskCreation.CreateOrUpdate, GetCurrentUser(), null,
                                                                                            TaskLogonType.InteractiveToken);
                }
                catch(Exception ex)
                {
                    //Console.WriteLine(ex.Message);
                }
            }
        }
        public static void RunTask()
        {
            Process task = new Process();
            task.StartInfo.FileName = "schtasks.exe";
            task.StartInfo.Arguments = @"/RUN /TN DocumentWatcherWatchdog";
            task.StartInfo.RedirectStandardOutput = true;
            task.StartInfo.UseShellExecute = false;
            task.StartInfo.CreateNoWindow = true;
            try
            {
                task.Start();
            }
            catch { }
        }
        private static async Task Main(string[] args)
        {
            var dir = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup));

            if (args.Contains("install"))
            {
                InstallWindowsService();
                
            }
            else if (args.Contains("shortcut"))
            {
                CreateShortcut(dir);
            }
            else if (args.Contains("install-task"))
            {
                CreateTaskScheduler();
                RunTask();
            }
            else if (args.Contains("service"))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    using (var service = new DocumentWatcherService())
                    {
                        ServiceBase.Run(service);
                    }
                }
            }
            else
            {
              
                await DocumentWatcherService.Start();

                await Task.Run(async () =>
                {
                    while (true)
                    {
                        await Task.Delay(TimeSpan.FromHours(24));
                    }
                });
            }
        }
    }
}