using SmtpServer;
using SmtpServer.Storage;

namespace QuranSchool.Email;

public class EmailBoxFilterFactory : IMailboxFilterFactory
{
    private readonly IServiceProvider _provider;

    public EmailBoxFilterFactory(IServiceProvider provider)
    {
        _provider = provider;
    }

    public IMailboxFilter CreateInstance(ISessionContext context)
    {
        return _provider.GetService<IMailboxFilter>()!;
    }
}