using SmtpServer;
using SmtpServer.Authentication;

namespace QuranSchool.Email;

public class EmailUserAuthenticatorFactory : IUserAuthenticatorFactory
{
    private readonly IServiceProvider _provider;

    public EmailUserAuthenticatorFactory(IServiceProvider provider)
    {
        _provider = provider;
    }

    public IUserAuthenticator CreateInstance(ISessionContext context)
    {
        return _provider.GetService<IUserAuthenticator>()!;
    }
}