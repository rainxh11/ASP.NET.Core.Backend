using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Entities;

namespace QuranSchool.Models.Request;

public class GroupModel
{
    [BsonIgnoreIfNull] public string? Name { get; set; }
    [BsonIgnoreIfNull] public DateTime StartDate { get; set; }
    [BsonIgnoreIfNull] public One<Teacher> Teacher { get; set; }
    [BsonIgnoreIfNull] public One<Formation> Formation { get; set; }
    [BsonIgnoreIfNull] public List<string>? Students { get; set; }
    [BsonIgnoreIfNull] public List<SessionModel> Sessions { get; set; }
}