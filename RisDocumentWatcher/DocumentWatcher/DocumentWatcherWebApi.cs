using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentWatcher.Helpers;
using EmbedIO;
using EmbedIO.Actions;
using EmbedIO.WebApi;
using EmbedIO.Cors;
using Swan.Logging;

namespace DocumentWatcher
{
    public class DocumentWatcherWebApi
    {
        public static WebServer WebServer;

        public static void StartWebserver()
        {
            var config = ConfigHelper.GetConfig();

            WebServer = new WebServer(o => o
                    .WithUrlPrefix(config.WebapiUri)
                    .WithMode(HttpListenerMode.EmbedIO))
                .WithLocalSessionManager()
                .WithCors()
                .WithWebApi("/api", m =>
                {
                    m.WithController<Controllers.DocumentController>();
                    m.WithController<Controllers.OpenDicomController>();
                })
                .WithModule(new ActionModule("/", HttpVerbs.Any, ctx => ctx.SendDataAsync(new { Message = "Error" })));

            // Listen for state changes.
            WebServer.StateChanged += (s, e) => $"WebServer New State - {e.NewState}".Info();
            WebServer.Start();
        }
    }
}