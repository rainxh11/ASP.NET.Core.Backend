using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace QuranSchool.Models.Request;

public class StudentModel
{
    [BsonIgnoreIfNull] public List<Parent>? Parents { get; set; }
    [BsonIgnoreIfNull] public string? Description { get; set; }
    [BsonIgnoreIfNull] public string? Address { get; set; }
    [BsonIgnoreIfNull] public string? PhoneNumber { get; set; }
    [BsonIgnoreIfNull] public string? Name { get; set; }
    [BsonIgnoreIfNull] public string? PlaceOfBirth { get; set; }
    [BsonIgnoreIfNull] public string? StudyLevel { get; set; }
    [BsonIgnoreIfNull] public DateTime? DateOfBirth { get; set; }
    [BsonIgnore] public IEnumerable<string>? Groups { get; set; } = new List<string>();

    [BsonIgnoreIfNull]
    [JsonConverter(typeof(StringEnumConverter))]
    public Gender? Gender { get; set; }
}