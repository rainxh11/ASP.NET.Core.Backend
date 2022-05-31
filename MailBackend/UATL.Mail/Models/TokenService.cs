using System.IdentityModel.Tokens.Jwt;
using System.Reactive.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using Akavache;
using Jetsons.JetPack;
using Microsoft.IdentityModel.Tokens;

namespace UATL.MailSystem.Common;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async ValueTask<string> BuildToken(IConfiguration config, Account account)
    {
        var tokenHandler = new JwtSecurityTokenHandler();

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha512Signature);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, account.ID),
            new Claim(ClaimTypes.Role, account.Role.ToString()),
            new Claim(ClaimTypes.Email, account.UserName),
            new Claim(ClaimTypes.Hash, account.PasswordHash)
        };
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.Now.AddHours(config["Jwt:ExpireAfter"].ToInt()),
            SigningCredentials = credentials
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);

        var tokenString = tokenHandler.WriteToken(token);
        await BlobCache.LocalMachine.InsertObject(tokenString, account,
            DateTime.Now.AddHours(config["Jwt:ExpireAfter"].ToInt()));

        var tokens = await BlobCache.LocalMachine.GetAllKeys();

        return tokenString;
    }

    public string BuildTokenFromIdentity(IIdentity? identity)
    {
        var tokenHandler = new JwtSecurityTokenHandler();

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha512Signature);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(identity),
            //Expires = DateTime.Now.AddHours(config["Jwt:ExpireAfter"].ToInt()),
            Expires = DateTime.Now.AddHours(_configuration["Jwt:ExpireAfter"].ToInt()),
            SigningCredentials = credentials
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);

        var tokenString = tokenHandler.WriteToken(token);

        return tokenString;
    }
}