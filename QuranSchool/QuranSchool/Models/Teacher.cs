using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace QuranSchool.Models;

public class Teacher : TeacherBase, ICreatedOn, IModifiedOn
{
    public AccountBase? CreatedBy { get; set; }
    public List<string> PhoneNumbers { get; set; } = new();
    public Avatar? Avatar { get; set; }

    public string Description { get; set; }
    public string Speciality { get; set; }
    public string Numbers => string.Join(";", PhoneNumbers);


    [JsonConverter(typeof(StringEnumConverter))]
    [BsonRepresentation(BsonType.String)]
    public Gender Gender { get; set; } = Gender.Male;

    public DateTime CreatedOn { get; set; }
    public DateTime ModifiedOn { get; set; }

    public Parent ToParent()
    {
        return new Parent
        {
            PhoneNumber = Numbers,
            Name = Name,
            Job = $"{Speciality} - {Description}",
            CardID = CardID,
            CreatedOn = CreatedOn,
            DateOfBirth = DateOfBirth,
            ID = ID
        };
    }

    public TeacherBase ToBase()
    {
        return new TeacherBase
        {
            ID = ID,
            Name = Name,
            CardID = CardID,
            DateOfBirth = DateOfBirth
        };
    }
}