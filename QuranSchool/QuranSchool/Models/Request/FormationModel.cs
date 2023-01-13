using MongoDB.Bson.Serialization.Attributes;

namespace QuranSchool.Models.Request;

public class FormationModel
{
    [BsonIgnoreIfNull] public string? Name { get; set; }
    [BsonIgnoreIfNull] public double? Price { get; set; }
    [BsonIgnoreIfNull] public int? DurationDays { get; set; }
    [BsonIgnoreIfNull] public double? Hours { get; set; }
}