using BCrypt.Net;
using Mapster;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Entities;
using Newtonsoft.Json;
using System;
using UATL.MailSystem.Common.Models;

namespace UATL.MailSystem.Common;

public class Account : AccountBase, ICreatedOn, IModifiedOn
{
    public Account()
    {
        this.InitOneToMany(() => Starred);
    }

    public Account(string name, string userName, string password, string description = "")
    {
        PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(password, 13, HashType.SHA384);
        PasswordUpdatedOn = DateTime.Now;
        Name = name;
        UserName = userName;
        Description = description;
    }

    [BsonRequired][JsonIgnore] public string PasswordHash { get; set; }

    public DateTime LastLogin { get; set; }
    public DateTime PasswordUpdatedOn { get; set; }

    [IgnoreDefault] public AccountBase CreatedBy { get; set; }

    //[JsonConverter(typeof(StringEnumConverter))]
    //[JsonIgnore]
    [JsonProperty("RolePriority")] public AccountType Role { get; set; } = AccountType.User;

    [JsonProperty("Role")] public string RoleName => Role.ToString();

    public bool Enabled { get; set; } = true;

    [IgnoreDefault]
    public Avatar Avatar { get; set; }

    [JsonIgnore] public Many<MailModel> Starred { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.Now;
    public DateTime ModifiedOn { get; set; }

    public AccountBase ToBaseAccount()
    {
        return this.Adapt<AccountBase>();
    }

    public static string SetPassword(string password)
    {
        return BCrypt.Net.BCrypt.EnhancedHashPassword(password, 13, HashType.SHA384);
    }

    public void CreatePassword(string password)
    {
        PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(password, 13, HashType.SHA384);
        PasswordUpdatedOn = DateTime.Now;
    }

    public static bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.EnhancedVerify(password, hash);
    }

    public bool Verify(string password)
    {
        return BCrypt.Net.BCrypt.EnhancedVerify(password, PasswordHash);
    }

    public bool Replace(string password, string newPassword)
    {
        if (BCrypt.Net.BCrypt.EnhancedVerify(password, PasswordHash))
        {
            PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(newPassword, 13, HashType.SHA384);
            PasswordUpdatedOn = DateTime.Now;
            ModifiedOn = DateTime.Now;
            return true;
        }

        return false;
    }
}