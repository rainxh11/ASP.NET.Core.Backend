using DocumentWatcher.Helpers;
using Refit;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.WebApi;

namespace DocumentWatcher
{
    partial class DocumentWatcherService : ServiceBase
    {
        public DocumentWatcherService()
        {
            InitializeComponent();
        }

        public static async Task Start()
        {
            await MongoHelper.Init();
            MongoHelper.StartWatchers();
            MongoHelper.WatchStudies();

            var config = ConfigHelper.GetConfig();
            Program.documentServerApi = RestService.For<DocumentServerApi>(config.DocumentServer);
            Program.dicomServerApi = RestService.For<DicomServerApi>(config.DicomServer);

            FileWatcher.Init();
            FileWatcher.Start();
            DocumentWatcherWebApi.StartWebserver();
        }

        protected override async void OnStart(string[] args)
        {
            await Start();
        }

        protected override void OnStop()
        {
            Process.GetCurrentProcess().Kill();
        }
    }
}