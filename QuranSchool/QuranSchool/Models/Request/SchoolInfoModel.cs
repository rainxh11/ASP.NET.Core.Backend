using MongoDB.Bson.Serialization.Attributes;

namespace QuranSchool.Models.Request;

public class SchoolInfoModel
{
    [BsonIgnoreIfDefault] public string? Name { get; set; }
    [BsonIgnoreIfDefault] public string? Address { get; set; }
    [BsonIgnoreIfDefault] public string? Website { get; set; }
    [BsonIgnoreIfDefault] public string? PhoneNumber { get; set; }
}