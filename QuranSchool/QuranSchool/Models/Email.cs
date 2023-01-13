using MongoDB.Entities;
using SmtpServer;
using SmtpServer.Mail;

namespace QuranSchool.Models;

public class Email : ClientFile, ICreatedOn, IModifiedOn
{
    public DateTime CreatedOn { get; set; }
    public DateTime ModifiedOn { get; set; }

    public Email()
    {
    }

    public Email(IMessageTransaction mailTransaction)
    {
        From = mailTransaction.From;
        To = mailTransaction.To.ToList();
        Parameters = mailTransaction.Parameters.ToDictionary(x => x.Key, x => x.Value);
    }

    public IMailbox From { get; set; }
    public List<IMailbox> To { get; set; } = new();
    public Dictionary<string, string> Parameters { get; set; } = new();
}