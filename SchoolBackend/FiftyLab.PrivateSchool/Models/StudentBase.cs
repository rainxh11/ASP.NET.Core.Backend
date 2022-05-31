using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FiftyLab.PrivateSchool;

public class StudentBase : Entity
{
    public string Name { get; set; }
    public DateTime DateOfBirth { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    [BsonRepresentation(BsonType.String)]
    public Gender Gender { get; set; } = Gender.Male;
}