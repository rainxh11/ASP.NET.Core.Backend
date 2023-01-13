using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DicomModalityBruteforcer
{
    public class SCP
    {
        public string Host { get; set; }
        public int Port { get; set; } = 104;
        public string ServerAET { get; set; }
    }
    public class ConfigHelper
    {

        public static List<SCP> GetConfig()
        {
            return new List<SCP>()
                {
                    new SCP() { Host = "172.16.1.150", Port = 104, ServerAET = "CT111781" },
                    new SCP() { Host = "172.16.1.130", Port = 104, ServerAET = "LOGIQS8" },
                };

        }
    }
}
