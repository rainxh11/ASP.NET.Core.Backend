using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicomModalityBruteforcer
{
    class AetResult
    {
        [BsonId]
        public int Id { get; set; }
        public string Aet { get; set; }

    }
}
