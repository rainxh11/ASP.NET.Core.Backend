using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

namespace DocumentWatcher.Helpers
{
    public class ConfigHelper
    {
        public string DocumentFolder { get; set; } = @"D:\RIS_COMPTE_RENDU";
        public string DicomTemp { get; set; } = @"D:\DICOM_TEMP";
        public string DicomViewer { get; set; } = "RadiAntViewer.exe";
        public string ExtensionFilter { get; set; } = "*.docx";
        public string MongoDBDatabase { get; set; } = "RisDb";
        public string MongoDBConnectionString { get; set; } = "mongodb://127.0.0.1:27017/RisDb?replicaSet=rs0";
        public string DocumentServer { get; set; } = "http://127.0.0.1:5000";
        public string WebapiUri { get; set; } = "http://127.0.0.1:5500";
        public string DicomServer { get; set; } = "http://172.0.0.1:9100";
        public int MaxConcurrentRefreshs { get; set; } = 8;
        public int InitialConcurrentRefreshs { get; set; } = 4;

        public static ConfigHelper GetConfig()
        {
            try
            {
                var configFilePath = AppContext.BaseDirectory + @"\Config.json";
                var json = File.ReadAllText(configFilePath);
                return JsonConvert.DeserializeObject<ConfigHelper>(json);
            }
            catch
            {
                return new ConfigHelper();
            }
        }
    }
}