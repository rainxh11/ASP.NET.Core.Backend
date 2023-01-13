using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

namespace DicomServer.Helper
{
    public class RefreshOptions
    {
        public int JobsRefreshSeconds { get; set; } = 1;
        public int StorageRefreshSeconds { get; set; } = 30;
        public int PACSStudiesRefreshSeconds { get; set; } = 1;

    }
    public class Modality
    {
        public string Name { get; set; }
        public string Tag { get; set; }
    }
    public class WorklistConfig
    {
        public int ItemLifetime { get; set; } = 1;
    }
    public class ArchiveCache
    {
        public bool Enabled { get; set; } = true;
        public int ExpirationHours { get; set; } = 24;
        public int JobsExpirationHours { get; set; } = 24;
    }
    public class StudiesCache
    {
        public bool Enabled { get; set; } = true;
        public int ExpirationDays { get; set; } = 365;
    }
    public class ImageCache
    {
        public string Path { get; set; } = @".\Cache";
        public bool Enabled { get; set; } = true;
        public int JpegQuality { get; set; } = 70;
        public bool WebpEnabled { get; set; } = true;
        public int WebpQuality { get; set; } = 10;
        public int ExpirationDays { get; set; } = 365;
    }

    public class SCP
    {
        public string Host { get; set; }
        public int Port { get; set; } = 104;
        public string ServerAET { get; set; }
        public string CallingAET { get; set; }
    }

    public class OrthancApi
    {
        public string Host { get; set; } = "http://127.0.0.1:8042";
        public string User { get; set; } = "admin";
        public string Password { get; set; } = "50lab50lab";
        public string GetAuthentification()
        {
            return Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes($"{User}:{Password}"));
        }
    }

    public class ConfigHelper
    {
        public string ApiHost { get; set; } = "0.0.0.0";
        public int ApiPort { get; set; } = 9100;
        public int WorkerListPort { get; set; } = 9000;
        public string WorkerListAET { get; set; } = "WORKLIST";
        public List<SCP> PACSList { get; set; } = new List<SCP>();
        public List<SCP> SCPListModalities { get; set; } = new List<SCP>();
        public OrthancApi OrthancApi { get; set; } = new OrthancApi();
        public string MongoDBDatabase { get; set; } = "RisDb";
        public string MongoDBConnectionString { get; set; } = "mongodb://127.0.0.1:27017/RisDb?replicaSet=rs0";
        public ImageCache ImageCache { get; set; } = new ImageCache();
        public StudiesCache StudiesCache { get; set; } = new StudiesCache();
        public WorklistConfig WorklistConfig { get; set; } = new WorklistConfig();
        public List<Modality> Modalities { get; set; } = new List<Modality>();
        public string PACSFolder { get; set; }
        public string WebSocketServerUrl { get; set; }
        public ArchiveCache ArchiveCache { get; set; } = new ArchiveCache();
        public RefreshOptions RefreshOptions { get; set; } = new RefreshOptions();

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

        public string GetHost()
        {
            return $"http://{ApiHost}:{ApiPort}";
        }
    }
}