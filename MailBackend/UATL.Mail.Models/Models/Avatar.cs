using MongoDB.Entities;
using Newtonsoft.Json;
using System;

namespace UATL.MailSystem.Common.Models;

public class Avatar : FileEntity, ICreatedOn, IModifiedOn
{
    public Avatar(Account account)
    {
        Account = account.ToBaseAccount();
    }

    public Avatar()
    {
    }

    public string ContentType { get; set; } = "image/webp";

    [JsonIgnore]
    public AccountBase Account { get; }

    public DateTime CreatedOn { get; set; }
    public DateTime ModifiedOn { get; set; }
}