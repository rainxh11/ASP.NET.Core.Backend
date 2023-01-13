
using Newtonsoft.Json;
using System;
using System.Globalization;

namespace DicomServer.Models;
public class OrthancInstance
{
    public int FileSize { get; set; }
    public string FileUuid { get; set; }
    public string ID { get; set; }
    public int? IndexInSeries { get; set; }
    public InstanceMaindicomtags MainDicomTags { get; set; }
    public string ParentSeries { get; set; }
    public string Type { get; set; }
}

public class InstanceMaindicomtags
{
    public string AcquisitionNumber { get; set; }
    public string InstanceCreationDate { get; set; }
    public string InstanceCreationTime { get; set; }
    public string ImageComments { get; set; }
    public string ImageOrientationPatient { get; set; }
    public string ImagePositionPatient { get; set; }
    public string InstanceNumber { get; set; }
    public string SOPInstanceUID { get; set; }
}

public class OrthancStudy
{
    public string ID { get; set; }
    public bool IsStable { get; set; }
    public string LastUpdate { get; set; }
    public StudyMaindicomtags MainDicomTags { get; set; }
    public string ParentPatient { get; set; }
    public Patientmaindicomtags PatientMainDicomTags { get; set; }
    public string[] Series { get; set; }
    public string Type { get; set; }
}

public class StudyMaindicomtags
{
    public string AccessionNumber { get; set; }
    public string InstitutionName { get; set; }
    public string ReferringPhysicianName { get; set; }
    public string StudyDate { get; set; }
    public string StudyDescription { get; set; }
    public string StudyID { get; set; }
    public string StudyInstanceUID { get; set; }
    public string StudyTime { get; set; }
}

public class Patientmaindicomtags
{
    public string PatientBirthDate { get; set; }
    public string PatientID { get; set; }
    public string PatientName { get; set; }
    public string PatientSex { get; set; }
}

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
    public string StudyId { get; set; }
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
    public DateTime? CreatedOn
    {
        get
        {
            try
            {
                var date = CreationTime.Split('.')[0];
                return DateTime.ParseExact(date, "yyyyMMddTHHmmss", CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }
    }
    public DateTime? CompletedOn
    {
        get
        {
            try
            {
                var date = CompletionTime.Split('.')[0];

                return DateTime.ParseExact(date, "yyyyMMddTHHmmss", CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }
    }
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
    public OrthancJobStatus Status
    {
        get
        {
            try
            {
                return (OrthancJobStatus)Enum.Parse(typeof(OrthancJobStatus), this.State);
            }
            catch
            {
                return OrthancJobStatus.Unknown;
            }
        }
    }
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
    public TimeSpan TimeSpan { get => TimeSpan.FromSeconds(this.EffectiveRuntime); }
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
