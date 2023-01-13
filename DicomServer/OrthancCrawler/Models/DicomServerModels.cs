using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrthancCrawler.Models
{
    public class StudyResponse
    {
        public string Id { get; set; }
    }
    public class JobResponse
    {
        public string GetFileName(string dest)
        {
            var name = Study != null ? Study.Id : Job.ID;

            return Path.Combine(dest, $"{name}.zip");
        }
        public string Link { get; set; }
        public string Name { get; set; }
        public OrthancJob? Job { get; set; }
        public RisImagingStudy? Study { get; set; }
    }
    public class RisImagingStudy : IEquatable<RisImagingStudy>
    {
        public string Id { get; set; }
        public string StudyInstanceUID { get; set; }
        public string RisId { get; set; }
        public string Description { get; set; }
        public DateTime StudyDate { get; set; }
        public string PatientName { get; set; }
        public string PatientId { get; set; }
        public string RisPatientId { get; set; }
        public List<string> Instances { get; set; } = new List<string>();

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
        public bool Equals(RisImagingStudy other)
        {
            return other.Id == this.Id;
        }

    }
}
