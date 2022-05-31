using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Entities;

namespace UATL.MailSystem.Common;

public class AccountBase : Entity
{
    [BsonRequired] public string Name { get; set; }

    [BsonRequired] public string UserName { get; set; }

    public string Description { get; set; }
}