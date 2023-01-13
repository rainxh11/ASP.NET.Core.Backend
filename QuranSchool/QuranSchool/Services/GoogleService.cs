using Google.Apis.Auth;
using QuranSchool.Models.Request;

namespace QuranSchool.Services;

public class GoogleService
{
    private readonly IConfiguration _configuration;
    private readonly IConfigurationSection _jwtConfig;
    private readonly IConfigurationSection _googleConfig;
    private readonly ILogger<GoogleService> _logger;

    public GoogleService(IConfiguration configuration, ILogger<GoogleService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _googleConfig = configuration.GetSection("Google");
        _jwtConfig = configuration.GetSection("Jwt");
    }

    public async Task<GoogleJsonWebSignature.Payload> AccountFromToken(string token)
    {
        var payload = await GoogleJsonWebSignature.ValidateAsync(token);
        return payload;
    }


    public async Task<GoogleJsonWebSignature.Payload> VerifyGoogleToken(ExternalAuthModel externalAuth)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings()
            {
                Audience = new List<string>() { _googleConfig.GetSection("TenantId").Value }
            };
            var payload = await GoogleJsonWebSignature.ValidateAsync(externalAuth.IdToken, settings);

            return payload;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "");
            return null;
        }
    }
}