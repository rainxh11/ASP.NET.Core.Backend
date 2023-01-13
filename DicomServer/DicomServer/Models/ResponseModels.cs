using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Invio.Hashing;

namespace DicomServer.Models
{
    public class ResponseJobsEqualityComparer : IEqualityComparer<List<ResponseJob>>
    {
        public bool Equals(List<ResponseJob> x, List<ResponseJob> y)
        {
            if(x.Count != y.Count)
            {
                return false;
            }
            else
            {
                return x.Sum(x => x.GetHashCode()) == y.Sum(y => y.GetHashCode());
            }
        }

        public int GetHashCode([DisallowNull] List<ResponseJob> obj)
        {
            unchecked
            {
                return obj.Sum(x => x.GetHashCode());
            }
        }
    }
    public class ResponseJobEqualityComparer : IEqualityComparer<ResponseJob>
    {
        public bool Equals(ResponseJob x, ResponseJob y)
        {
            if(x.Study != null && y.Study != null)
            {
                return x.Study.Id == y.Study.Id && x.Job.ID == y.Job.ID && x.Job.Progress == y.Job.Progress && x.Job.State == y.Job.State && x.Job.GetUncompressedSIze() == y.Job.GetUncompressedSIze();
            }
            else
            {
                return x.Job.ID == y.Job.ID;
            }
        }

        public int GetHashCode([DisallowNull] ResponseJob obj)
        {
            unchecked
            {
                if (obj.Study != null)
                {
                    return Invio.Hashing.HashCode.From(obj.Study.Id, obj.Job.ID, obj.Job.State, obj.Job.State, obj.Job.GetUncompressedSIze());
                }
                else
                {
                    return Invio.Hashing.HashCode.From(obj.Job.ID, obj.Job.State, obj.Job.State, obj.Job.GetUncompressedSIze());
                }
            }
        }
    }
    public class ResponseJob : IEquatable<ResponseJob>
    {
        public string Link { get; set; }
        public string Name { get; set; }
        public RisImagingStudy Study { get; set; }
        public OrthancJob Job { get; set; }

        public ResponseJob(OrthancJob job, RisImagingStudy study = null)
        {
            if(job != null)
            {
                this.Job = job;
                this.Link = $"/jobs/{job.ID}/{job.Type}";
                if (study == null)
                {
                    Name = job.ID;
                }
                else
                {
                    Name = $"{study.PatientName} [{study.Description} - {study.StudyDate.ToString("yyyy-MM-dd")}]";
                    Study=study;
                }
            }
            
        }

        public bool Equals(ResponseJob other)
        {
            if(this.Job != null && other.Job != null)
            {
                if (Study != null && other.Study != null)
                {
                    return this.Study.Id == other.Study.Id && this.Job.ID == other.Job.ID && this.Job.Progress == other.Job.Progress && this.Job.State == other.Job.State && this.Job.GetUncompressedSIze() == other.Job.GetUncompressedSIze();
                }
                else
                {
                    return this.Job.ID == other.Job.ID && this.Job.Progress == other.Job.Progress && this.Job.State == other.Job.State && this.Job.GetUncompressedSIze() == other.Job.GetUncompressedSIze();
                }
            }
            else
            {
                return false;
            }
            
        }
        public override int GetHashCode()
        {
            if(Job != null)
            {
                unchecked
                {
                    if (this.Study != null)
                    {
                        return Invio.Hashing.HashCode.From(this.Study.Id, this.Job.ID, this.Job.State, this.Job.State, this.Job.GetUncompressedSIze());
                    }
                    else
                    {
                        return Invio.Hashing.HashCode.From(this.Job.ID, this.Job.State, this.Job.State, this.Job.GetUncompressedSIze());
                    }
                }
            }
            else
            {
                return 0;
            }
           
        }
    }
}
