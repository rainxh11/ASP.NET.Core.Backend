using Dicom.Network;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace DicomForwarder
{
    partial class DicomForwardService : ServiceBase
    {
        private IDicomServer _dicomStoreScpServer;

        public DicomForwardService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            var port = ConfigHandler.GetConfig().ServerPort;

            _dicomStoreScpServer = DicomServer.Create<DicomCStoreSCP>(port);
        }

        protected override void OnStop()
        {
            try
            {
                if (_dicomStoreScpServer.IsListening) _dicomStoreScpServer.Stop();
            }
            catch
            {
                Process.GetCurrentProcess().Kill();
            }
        }
    }
}