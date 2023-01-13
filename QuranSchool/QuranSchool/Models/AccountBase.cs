using MongoDB.Bson.Serialization.Attributes;

namespace QuranSchool.Models;

public class AccountBase : ClientEntity
{
    [BsonRequired] public string Name { get; set; }

    [BsonRequired] public string UserName { get; set; }

    [BsonIgnoreIfNull] public string? Email { get; set; }
    public string Description { get; set; }
}