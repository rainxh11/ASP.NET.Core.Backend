using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

namespace OrthancCrawler.Models
{
    public class Config
    {
        public string Url { get; set; } = "http://127.0.0.1:9100";
        public string OrthancUrl { get; set;  } = "http://127.0.0.1:8042";
        public string DestinationFolder { get; set; } = AppContext.BaseDirectory + @"\PACS_BACKUP";
        public string OrthancPostgresUrl { get; set; } = "http://127.0.0.1:8041";


        public static Config GetConfig()
        {
            try
            {
                var json = File.ReadAllText(AppContext.BaseDirectory + @"\Config.json");
                return JsonConvert.DeserializeObject<Config>(json);
            }
            catch
            {
                return new Config();
            }
        }
    }
}
