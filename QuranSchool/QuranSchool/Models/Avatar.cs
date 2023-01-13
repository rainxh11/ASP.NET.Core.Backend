using MongoDB.Entities;
using Newtonsoft.Json;

namespace QuranSchool.Models;

public class Avatar : ClientFile, ICreatedOn, IModifiedOn
{
    public Avatar(Account account)
    {
        Account = account.ToBaseAccount();
    }

    public Avatar()
    {
    }

    public string ContentType { get; set; } = "image/webp";
    public string PersonalID { get; set; }

    [JsonIgnore] public AccountBase Account { get; }

    public DateTime CreatedOn { get; set; }
    public DateTime ModifiedOn { get; set; }
}