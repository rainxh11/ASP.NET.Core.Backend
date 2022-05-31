using System.Security.Principal;

namespace UATL.MailSystem.Common;

public interface ITokenService
{
    ValueTask<string> BuildToken(IConfiguration config, Account account);
    string BuildTokenFromIdentity(IIdentity? identity);
}