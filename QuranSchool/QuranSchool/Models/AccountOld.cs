﻿namespace QuranSchool.Models;

//public class AccountOld : AccountBase, ICreatedOn, IModifiedOn
//{
//    public AccountOld()
//    {
//    }


//    public AccountOld(string name, string userName, string password, string description = "")
//    {
//        PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(password, 13);
//        PasswordUpdatedOn = DateTime.Now;
//        Name = name;
//        UserName = userName;
//        Description = description;
//    }

//    [BsonIgnoreIfNull] public string? PersonalID { get; set; }

//    [BsonRequired] [JsonIgnore] public string PasswordHash { get; set; }

//    public DateTime LastLogin { get; set; }
//    public DateTime PasswordUpdatedOn { get; set; }

//    [IgnoreDefault] public AccountBase CreatedBy { get; set; }
//    [BsonIgnoreIfNull] public string? AutoGeneratedPassword { get; set; }

//    //[JsonIgnore]
//    [BsonRepresentation(BsonType.String)]
//    [JsonProperty("RolePriority")]
//    public AccountType Role { get; set; } = AccountType.User;

//    [JsonProperty("Role")] public string RoleName => Role.ToString();

//    public bool Enabled { get; set; } = true;

//    [IgnoreDefault] public Avatar Avatar { get; set; }
//    public bool RequiredToChangePassword { get; set; }
//    public DateTime CreatedOn { get; set; } = DateTime.Now;
//    public DateTime ModifiedOn { get; set; }

//    public AccountBase ToBaseAccount()
//    {
//        return this.Adapt<AccountBase>();
//    }

//    public static string SetPassword(string password)
//    {
//        return BCrypt.Net.BCrypt.EnhancedHashPassword(password, 13);
//    }

//    public void CreatePassword(string password)
//    {
//        if (AutoGeneratedPassword is not null)
//        {
//            AutoGeneratedPassword = null;
//            RequiredToChangePassword = false;
//        }

//        PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(password, 13);
//        PasswordUpdatedOn = DateTime.Now;
//    }

//    public static bool VerifyPassword(string password, string hash)
//    {
//        return BCrypt.Net.BCrypt.EnhancedVerify(password, hash);
//    }

//    public bool Verify(string password)
//    {
//        return BCrypt.Net.BCrypt.EnhancedVerify(password, PasswordHash);
//    }

//    public bool Replace(string password, string newPassword)
//    {
//        if (BCrypt.Net.BCrypt.EnhancedVerify(password, PasswordHash))
//        {
//            PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(newPassword, 13);
//            PasswordUpdatedOn = DateTime.Now;
//            ModifiedOn = DateTime.Now;

//            AutoGeneratedPassword = null;
//            RequiredToChangePassword = false;

//            return true;
//        }

//        return false;
//    }
//}