using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicomForwarder
{
    public class ForwardEndpoint
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string AET { get; set; }
    }

    public class Config
    {
        public int ServerPort { get; set; } = 104;
        public List<ForwardEndpoint> ForwardDestinations { get; set; } = new List<ForwardEndpoint>();
    }

    public class ConfigHandler
    {
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