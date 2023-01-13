using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace QuranSchool.Models.Request;

public class TeacherModel
{
    [BsonIgnoreIfNull] public string? Speciality { get; set; }
    [BsonIgnoreIfNull] public List<string>? PhoneNumbers { get; set; }
    [BsonIgnoreIfNull] public string? Name { get; set; }
    [BsonIgnoreIfNull] public string? CardID { get; set; }
    [BsonIgnoreIfNull] public DateTime? DateOfBirth { get; set; }

    [BsonIgnoreIfNull]
    [JsonConverter(typeof(StringEnumConverter))]
    public Gender? Gender { get; set; }
}