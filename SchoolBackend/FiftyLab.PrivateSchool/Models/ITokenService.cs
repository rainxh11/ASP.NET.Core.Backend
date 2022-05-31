using System.Security.Principal;

namespace FiftyLab.PrivateSchool;

public interface ITokenService
{
    ValueTask<string> BuildToken(IConfiguration config, Account account);
    string BuildTokenFromIdentity(IIdentity? identity);
}