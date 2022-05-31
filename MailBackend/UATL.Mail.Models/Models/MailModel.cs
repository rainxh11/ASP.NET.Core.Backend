using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;

namespace UATL.MailSystem.Common.Models;

[Collection("Mail")]
public class MailModel : Draft
{
    /*public MailModel()
    {
        this.InitOneToMany(() => Attachments);

    }*/
    [IgnoreDefault][AsObjectId] public string GroupId { get; set; } = null;

    [IgnoreDefault] public One<MailModel> ReplyTo { get; set; }

    [IgnoreDefault] public DateTime SentOn { get; set; }

    public AccountBase To { get; set; }
    private bool IsEncrypted { get; set; } = false;

    [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
    [IgnoreDefault]
    public List<MailFlag> Flags { get; set; } = new();

    [JsonIgnore]
    [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
    public MailType Type { get; set; } = MailType.Internal;

    [JsonProperty("Type")] public string TypeName => Type.ToString();

    [IgnoreDefault] public DateTime ViewedOn { get; set; }

    public bool Viewed { get; set; }

    public bool Approved
    {
        get
        {
            if (Type == MailType.External)
                return Flags.Contains(MailFlag.Approved);
            return false;
        }
    }

    [IgnoreDefault] public AccountBase ApprovedBy { get; set; } = null;


    public bool Acknowledged => Flags.Contains(MailFlag.Acknowledged);

    public bool RequireTask => Flags.Contains(MailFlag.RequireTask);

    public bool Important => Flags.Contains(MailFlag.Important);

    public bool Reviewed => Flags.Contains(MailFlag.Reviewed);

    [BsonIgnore] public IEnumerable<AccountBase>? Recipients { get; set; }

    public void SetViewed()
    {
        Viewed = true;
        ViewedOn = DateTime.Now;
    }
}