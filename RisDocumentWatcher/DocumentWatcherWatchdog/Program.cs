using System;
using System.Threading.Tasks;

namespace DocumentWatcherWatchdog
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            WatchDog.StartWorker();


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
