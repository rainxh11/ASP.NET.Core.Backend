using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DocumentWatcher.Models
{
    public class StudyFile
    {
        public RisStudy Study { get; set; }
        public RisClient Client { get; set; }
        public FileInfo File { get; set; }
    }

    [Collection("clients")]
    public class RisClient : Entity
    {
        public string Name { get => $"{familyName} {firstName}".Trim(); }

        public string Age
        {
            get
            {
                var age = (DateTime.Now - birthdate).Days;
                var ageString = age >= 365
                    ? $"{Math.Round(Convert.ToDecimal(age / 365), 0).ToString("N0")} ANS"
                    : $"{Math.Round(Convert.ToDecimal(age / 12), 0).ToString("N0")} MOIS";

                return ageString;
            }
        }

        public string NameWithAge
        {
            get
            {
                var age = (DateTime.Now - birthdate).Days;
                var ageString = age >= 365
                    ? $"{Math.Round(Convert.ToDecimal(age / 365), 0).ToString("N0")} ANS"
                    : $"{Math.Round(Convert.ToDecimal(age / 12), 0).ToString("N0")} MOIS";

                return $"{Name} {ageString}".Trim();
            }
        }

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

    public class Report
    {
        public string text { get; set; }
        public string htmlTags { get; set; }
    }

    public class ReportSync
    {
        public string familyName { get; set; }
        public string date { get; set; }
        public string title { get; set; }
        public string doctor { get; set; }
        public string age { get; set; }
        public BsonArray block { get; set; }
    }

    [Collection("studies")]
    public class RisStudy : IEntity
    {
        [BsonIgnore]
        public string PatientName { get; set; }

        [BsonIgnore]
        public string Age { get; set; }

        [BsonIgnore]
        public string RefereeDoctor { get; set; }

        [BsonId]
        public int _id { get; set; }

        public Report report { get; set; }
        public ReportSync reportSync { get; set; }
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
        public DateTime updatedAt { get; set; }

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
}