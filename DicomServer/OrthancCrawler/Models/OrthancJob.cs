using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrthancCrawler.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    public class OrthancArchiveBody
    {
        [JsonProperty("Asynchronous")]
        public bool Asynchronous { get; set; } = true;
    }
    public class OrthancArchiveAsyncResponse
    {
        public string ID { get; set; }
        public string Path { get; set; }
    }

    public enum OrthancJobStatus
    {
        Running,
        Pending,
        Success,
        Failure,
        Paused,
        Retry,
        Unknown
    }
    public class OrthancJob
    {

        public string CompletionTime { get; set; }

        public Content Content { get; set; }
        public string CreationTime { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? CompletedOn { get; set; }
        public double EffectiveRuntime { get; set; }
        public int ErrorCode { get; set; }
        public string ErrorDescription { get; set; }
        public string ErrorDetails { get; set; }
        public string ID { get; set; }
        public int Priority { get; set; }
        public int Progress { get; set; }
        public string State { get; set; }
        public string Timestamp { get; set; }
        public string Type { get; set; }
        public OrthancJobStatus Status { get; set; }
        public string GetFileName()
        {
            return $"{this.ID}.zip";
        }
        public bool IsFinished()
        {
            return this.Progress == 100 && this.Status == OrthancJobStatus.Success;
        }
        public long GetCompressedSize()
        {
            try
            {
                return Convert.ToInt64(this.Content.ArchiveSize);
            }
            catch
            {
                return -1;
            }
        }
        public long GetUncompressedSIze()
        {
            try
            {
                return Convert.ToInt64(this.Content.UncompressedSize);
            }
            catch
            {
                return -1;
            }
        }
        public TimeSpan TimeSpan { get; set; }
    }

    public class Content
    {
        public string ArchiveSize { get; set; }
        public int ArchiveSizeMB { get; set; }
        public string Description { get; set; }
        public int InstancesCount { get; set; }
        public string UncompressedSize { get; set; }
        public int UncompressedSizeMB { get; set; }
    }

}
