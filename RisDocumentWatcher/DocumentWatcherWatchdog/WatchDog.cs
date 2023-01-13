using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentWatcherWatchdog
{
    public class WatchDog
    {
        static void StartWatcher()
        {
            try
            {
                var file = new FileInfo(AppContext.BaseDirectory + "SkLabDocumentWatcher.exe");

                var running = Process.GetProcesses().Any(p => p.ProcessName.Contains("SkLabDocumentWatcher", StringComparison.OrdinalIgnoreCase));
                if (!running)
                {
                    var process = new Process()
                    {
                        StartInfo = new ProcessStartInfo()
                        {
                            FileName = file.FullName,
                        }
                    };
                    process.Start();
                }                
            }
            catch(Exception ex)
            {
            }
        }

        public static void StartWorker()
        {
            StartWatcher();

            Observable
                .Interval(TimeSpan.FromSeconds(1))
                .Repeat()
                .Select(x => Process.GetProcesses().Any(p => p.ProcessName.Contains("SkLabDocumentWatcher", StringComparison.OrdinalIgnoreCase)))
                .DistinctUntilChanged(x => x)
                .Where(x => x == false)
                .Do(x => StartWatcher())
                .Subscribe();
        }
    }
}
