using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Entities;
using UATL.MailSystem.Common;
using UATL.MailSystem.Common.Models;
using UATL.MailSystem.Common.Models.Request;

namespace UATL.Mail.Helpers;

public class MailRequestHelper
{
    public static async Task<List<MailModel>> GetMails(SendMailRequest? request, Account account,
        CancellationToken ct = default, IClientSessionHandle session = null)
    {
        var mails = new List<MailModel>();
        var groupId = ObjectId.GenerateNewId().ToString();
        var body = ModelHelper.ReplaceHref(request.Body);

        if (request.Recipients?.Count > 0)
        {
            foreach (var recipient in request.Recipients.Where(x => x != account.ID))
            {
                var destinationAccount = await DB.Find<Account>(session).OneAsync(recipient, ct);
                if (destinationAccount == null)
                    throw new Exception($"Recipient with id:'{recipient}' not found!");
                if (destinationAccount.Role == AccountType.OrderOffice)
                    break;

                var mail = new MailModel
                {
                    ID = ObjectId.GenerateNewId().ToString(),
                    Body = body,
                    Subject = request.Subject,
                    From = account.ToBaseAccount(),
                    To = destinationAccount.ToBaseAccount(),
                    SentOn = DateTime.Now,
                    Type = request.Type,
                    Flags = request.Flags,
                    GroupId = groupId,
                    HashTags = request.HashTags,
                    CreatedOn = DateTime.Now
                };
                if (!string.IsNullOrEmpty(request.ReplyTo))
                {
                    var replyMail = await DB.Find<MailModel>(session).MatchID(request.ReplyTo).ExecuteSingleAsync(ct);
                    mail.ReplyTo = replyMail;
                }

                mails.Add(mail);
            }
        }
        else
        {
            if (request.Type == MailType.External)
            {
                var mail = new MailModel
                {
                    ID = ObjectId.GenerateNewId().ToString(),
                    Body = body,
                    Subject = request.Subject,
                    From = account.ToBaseAccount(),
                    SentOn = DateTime.Now,
                    Type = request.Type,
                    Flags = request.Flags,
                    GroupId = groupId,
                    HashTags = request.HashTags,
                    CreatedOn = DateTime.Now
                };
                if (!string.IsNullOrEmpty(request.ReplyTo))
                {
                    var replyMail = await DB.Find<MailModel>(session).MatchID(request.ReplyTo).ExecuteSingleAsync(ct);
                    mail.ReplyTo = replyMail;
                }

                mails.Add(mail);
            }
            else
            {
                throw new Exception("Internal Mail require at least one recipient");
            }
        }

        return mails;
    }
}