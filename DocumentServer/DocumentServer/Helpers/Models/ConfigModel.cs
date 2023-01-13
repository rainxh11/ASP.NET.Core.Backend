using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace RisDocumentServer.Helpers.Models
{
    public class StringReplacement
    {
        public string Replace { get; set; }
        public string With { get; set; }
        public bool IsReplaceRegex { get; set; }

        public string ReplaceString(string input)
        {
            if (IsReplaceRegex)
            {
                return new Regex(Replace, RegexOptions.None).Replace(input, With);
            }
            else
            {
                return input.Replace(Replace, With);
            }
        }
    }

    public class ConfigModel
    {
        public string ServerHost { get; set; } = "0.0.0.0";
        public int ServerPort { get; set; } = 5000;
        public string PrintServerHost { get; set; } = "127.0.0.1";
        public int PrintServerPort { get; set; } = 104;
        public string PrintServerAet { get; set; } = "CRFUSION";
        public string PrintServerAec { get; set; } = "WORD2DICOM";
        public string MongoDBConnectionString { get; set; } = "mongodb://127.0.0.1:27017/RisDb?replicaSet=rs0";
        public string MongoDBDatabase { get; set; } = "RisDb";
        public bool SaveReceipt { get; set; } = false;
        public string SaveReceiptPath { get; set; } = null;
        public string QrCodeHost { get; set; } = "https://radiolaghoauti.web.app";
        public List<StringReplacement> ClientNameReplacements { get; set; } = new List<StringReplacement>();

        public static ConfigModel GetConfig()
        {
            try
            {
                var json = File.ReadAllText(AppContext.BaseDirectory + @"\Config.json");
                return JsonConvert.DeserializeObject<ConfigModel>(json);
            }
            catch (Exception ex)
            {
                Program.Logger.Error(ex.Message);
                Console.WriteLine(ex.Message);
                return new ConfigModel();
            }
        }

        public string GetHost()
        {
            return $"http://{ServerHost}:{ServerPort}";
        }
    }
}