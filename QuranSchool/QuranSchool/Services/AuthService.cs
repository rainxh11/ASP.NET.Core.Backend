using Jetsons.JetPack;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using NetDevPack.Security.Jwt.Core.Interfaces;

using QuranSchool.Models;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Principal;

namespace QuranSchool.Services;

public class AuthService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _accessor;
    private readonly IHostEnvironment _env;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IConfiguration configuration,
        ILogger<AuthService> logger,
        IHttpContextAccessor accessor,
        IHostEnvironment env,
        IJwtService jwtService)
    {
        _logger = logger;
        _jwtService = jwtService;
        _env = env;
        _accessor = accessor;
        _configuration = configuration;
    }

    public async Task<string> GenerateToken(Account account)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var currentIssuer = $"{_accessor.HttpContext!.Request.Scheme}://{_accessor.HttpContext.Request.Host}";

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, account.ID),
                new Claim(ClaimTypes.Role, account.Role.ToString()),
                //new Claim("TenantId", account.TenantId ?? ""),
                new Claim(ClaimTypes.Email, account.UserName ?? account.Email),
            };

            var key = await _jwtService.GetCurrentSigningCredentials(); // (ECDsa or RSA) auto generated key
            var token = tokenHandler.CreateToken(new SecurityTokenDescriptor
            {
                Issuer = currentIssuer,
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.Now.AddHours(_configuration["Jwt:ExpireAfter"].ToInt(720)),
                SigningCredentials = key
            });
            return tokenHandler.WriteToken(token);
        }
        catch (Exception ex)
        {
            _logger.LogError($"{ex.Message}, {ex.InnerException?.Message}");
            return null;
        }
    }

    public async Task<string> GenerateTokenFromIdentity(IIdentity identity)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var currentIssuer =
                $"{_accessor.HttpContext!.Request.Scheme}://{_accessor.HttpContext.Request.Host}";

            var key = await _jwtService.GetCurrentSigningCredentials(); // (ECDsa or RSA) auto generated key
            var token = tokenHandler.CreateToken(new SecurityTokenDescriptor
            {
                Issuer = currentIssuer,
                Subject = new ClaimsIdentity(identity),
                Expires = DateTime.Now.AddHours(_configuration["Jwt:ExpireAfter"].ToInt(720)),
                SigningCredentials = key
            });
            return tokenHandler.WriteToken(token);
        }
        catch (Exception ex)
        {
            _logger.LogError($"{ex.Message}, {ex.InnerException.Message}");
            return null;
        }
    }

    public async Task<bool> ValidateToken(string jwt)
    {
        var handler = new JsonWebTokenHandler();
        var currentIssuer =
            $"{_accessor.HttpContext!.Request.Scheme}://{_accessor.HttpContext.Request.Host}";


        var result = await handler.ValidateTokenAsync(jwt,
            new TokenValidationParameters
            {
                ValidIssuer = currentIssuer,
                TokenDecryptionKey = await _jwtService.GetCurrentSecurityKey()
            });

        return result.IsValid;
    }

    //public async ValueTask<string> BuildToken(IConfiguration config, Account account)
    //{
    //    var tokenHandler = new JwtSecurityTokenHandler();

    //    var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]));
    //    var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha512Signature);


    //    var tokenDescriptor = new SecurityTokenDescriptor
    //    {
    //        Subject = new ClaimsIdentity(claims),
    //        Expires = DateTime.Now.AddHours(config["Jwt:ExpireAfter"].ToInt()),
    //        SigningCredentials = credentials
    //    };
    //    var token = tokenHandler.CreateToken(tokenDescriptor);

    //    var tokenString = tokenHandler.WriteToken(token);

    //    if (_env.IsProduction())
    //    {
    //        _accessor?.HttpContext?.Response.Cookies.Append("T", tokenString, new CookieOptions()
    //        {
    //            HttpOnly = true,
    //            Expires = DateTimeOffset.Now.AddHours(_configuration["Jwt:ExpireAfter"].ToInt()),
    //        });
    //    }


    //    var tokens = await BlobCache.LocalMachine.GetAllKeys();

    //    return tokenString;
    //}
}