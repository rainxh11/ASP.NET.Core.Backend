using System;
using System.IO;
using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.Actions;
using EmbedIO.Files;
using EmbedIO.WebApi;
using Swan.Logging;

namespace RisFrontendWebserver
{
    internal class Program
    {
        public static object UseFileCache { get; private set; }

        static async Task Main(string[] args)
        {
            var htmlDir = new DirectoryInfo(@"D:\WebProjects\50lab-ris\Frontend\dist");

            using(var webServer = CreateWebServer("http://192.168.1.100/", htmlDir))
            {

                await webServer.RunAsync();

                Console.ReadKey(true);
            }
        }

        private static WebServer CreateWebServer(string url, DirectoryInfo htmlDir)
        {
            var server = new WebServer(o => o
                    .WithUrlPrefix(url)
                    .WithMode(HttpListenerMode.Microsoft))
                .WithLocalSessionManager()
                .WithStaticFolder("/", htmlDir.FullName, true, m => m
                    .WithContentCaching(true)
                    .PreferCompressionFor("text/javascript", true)
                    .PreferCompressionFor("application/json", true)
                    .PreferCompressionFor("text/css", true)
                    .HandleMappingFailed((ctx,map) => {
                        ctx.Redirect("/");
                        return Task.CompletedTask;
                    })
                    )
                .WithModule(new ActionModule("/", HttpVerbs.Any, ctx =>
                {
                    ctx.Redirect("/");
                    return Task.CompletedTask;
                }) );

            // Listen for state changes.
            server.StateChanged += (s, e) => $"WebServer New State - {e.NewState}".Info();

            return server;
        }
    }
}

