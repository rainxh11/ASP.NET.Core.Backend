using System;
using System.Threading.Tasks;

namespace OrthancCrawler
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            //Worker.Start();
            await Worker.StartAsync();
            Console.ReadKey();
        }
    }
}
