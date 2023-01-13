
using System;
using System.Collections.Generic;

namespace DicomServer.Models;
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
    public List<string> Instances { get; set; } = new List< string>();

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
    public bool Equals(RisImagingStudy other)
    {
        return other.Id == this.Id;
    }

}
