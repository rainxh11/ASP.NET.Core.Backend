using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace QuranSchool.Models;

public class StudentBase : ClientEntity
{
    public List<Parent> Parents { get; set; }
    public string PhoneNumber { get; set; }
    public string Name { get; set; }
    public DateTime DateOfBirth { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    [BsonRepresentation(BsonType.String)]
    public Gender Gender { get; set; } = Gender.Male;
}