using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DicomServer.Models
{
    [Collection("clients")]

    public class RisClient : Entity
    {
        public string Name { get => $"{familyName} {firstName}".Trim(); }
        public string firstName { get; set; }
        public string familyName { get; set; }
        public DateTime birthdate { get; set; }
        public string gender { get; set; }
        public DateTime createdAt { get; set; }
        public string GetAccession()
        {
            return createdAt.ToString("yyyyMMdd-HHmmss");
        }

    }
    [Collection("studies")]
    public class RisStudy : IEntity
    {
        [BsonId]
        public int _id { get; set; }

        public string statusWorklist { get; set; }
        public string worklistModality { get; set; }
        public string statusStudy { get; set; }
        public string examType { get; set; }
        public string modality { get; set; }
        public string doctor { get; set; }
        public string GetDescription()
        {
            return $"{modality} {examType}".Replace("-", "").Trim();
        }
        public DateTime createdAt { get; set; }
        public ObjectId client { get; set; }
        public async Task<RisClient> GetClient()
        {
            try
            {
                return await DB.Find<RisClient>().MatchID(client.ToString()).ExecuteSingleAsync();
            }
            catch
            {
                return null;
            }
        }
        public string ID { get; set; }
        public string GenerateNewID()
        {
            throw new NotImplementedException();
        }
    }
    public class RisStudyCombined
    {
        public int _id { get; set; }
        public ObjectId clientId { get; set; }
        public string name { get; set; }
        public string gender { get; set; }
        public DateTime birthdate { get; set; }
        public string studystatus { get; set; }
        public string exam { get; set; }
        public string doctor { get; set; }
        public DateTime date { get; set; }
        public string wlstatus { get; set; }
        public string wlmodality { get; set; }
    }

}
