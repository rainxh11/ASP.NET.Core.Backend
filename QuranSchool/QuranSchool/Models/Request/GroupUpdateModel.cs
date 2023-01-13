using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Entities;

using Newtonsoft.Json;

namespace QuranSchool.Models.Request;

public class GroupUpdateModel
{
    [BsonIgnore] public string GroupId { get; set; }
    [BsonIgnore] [JsonIgnore] public Group? Group { get; set; }
    [BsonIgnoreIfNull] public string? Name { get; set; }
    [BsonIgnoreIfNull] public One<Teacher>? Teacher { get; set; }
    [BsonIgnoreIfNull] public bool? Cancelled { get; set; }
    [BsonIgnoreIfNull] public bool? OnHold { get; set; }
    [BsonIgnoreIfNull] public DateTime? HoldDate { get; set; }
    [BsonIgnore] public List<OccurrenceModel>? Occurrences { get; set; } = new();
    [BsonIgnoreIfNull] [JsonIgnore] public List<SessionModel>? SessionModels { get; set; }
    [BsonIgnoreIfNull] [JsonIgnore] public List<Session>? Sessions { get; set; }
}