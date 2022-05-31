using UATL.MailSystem.Common;

namespace UATL.Mail.Models.Models.Response;

public class Recipient
{
    public Recipient(Account account)
    {
        ID = account.ID;
        UserName = account.UserName;
        Name = account.Name;
        Description = account.Description;
        Avatar = $"/account/{account.ID}/avatar";
    }

    public string ID { get; }
    public string UserName { get; }
    public string Name { get; }
    public string Description { get; }
    public string Avatar { get; }
}