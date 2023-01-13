using SmtpServer;
using SmtpServer.Mail;
using SmtpServer.Storage;

namespace QuranSchool.Email;

public class EmailBoxFilter : IMailboxFilter
{
    private readonly ILogger<EmailBoxFilter> _logger;

    public EmailBoxFilter(ILogger<EmailBoxFilter> logger)
    {
        _logger = logger;
    }

    public Task<MailboxFilterResult> CanAcceptFromAsync(ISessionContext context, IMailbox from, int size,
        CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Receiving Email from: {0}", from.AsAddress());
        return Task.FromResult(MailboxFilterResult.Yes);
    }

    public Task<MailboxFilterResult> CanDeliverToAsync(ISessionContext context, IMailbox to, IMailbox from,
        CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Sending From: {0}, to: {1}", from.AsAddress(), to.AsAddress());
        return Task.FromResult(MailboxFilterResult.Yes);
    }
}