using MongoDB.Entities;
using System;
using System.Collections.Generic;
using BsonRequired = MongoDB.Bson.Serialization.Attributes.BsonRequiredAttribute;

namespace UATL.MailSystem.Common.Models;

public class Draft : Entity, ICreatedOn, IModifiedOn
{
    /*public Draft()
    {
        this.InitOneToMany(() => Attachments);
    }*/
    [BsonRequired] public AccountBase From { get; set; }

    [IgnoreDefault] public string Subject { get; set; }

    [IgnoreDefault] public string Body { get; set; } = string.Empty;

    [IgnoreDefault] public List<Attachment> Attachments { get; set; } = new List<Attachment>();

    [IgnoreDefault] public ISet<string> HashTags { get; set; } = new HashSet<string>();

    public DateTime CreatedOn { get; set; }
    public DateTime ModifiedOn { get; set; }
}