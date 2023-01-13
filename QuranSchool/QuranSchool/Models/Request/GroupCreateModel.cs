using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Entities;
using Newtonsoft.Json;

namespace QuranSchool.Models.Request;

public class GroupCreateModel
{
    [BsonIgnoreIfNull] public string? Name { get; set; }
    [BsonIgnoreIfNull] public One<Teacher>? Teacher { get; set; }
    [BsonIgnoreIfNull] public One<Formation>? Formation { get; set; }
    [BsonIgnoreIfNull] public List<string>? Students { get; set; }
    [BsonIgnoreIfNull] public List<OccurrenceModel>? Occurrences { get; set; }
    [BsonIgnoreIfNull][JsonIgnore] public List<SessionModel>? Sessions { get; set; } = new List<SessionModel>();
    [BsonIgnoreIfNull] public DateTime StartDate { get; set; }
}