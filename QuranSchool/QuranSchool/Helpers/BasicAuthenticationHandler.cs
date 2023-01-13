using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;

namespace QuranSchool.Helpers;

public class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly BasicAuthenticationHelper _basicAuthHelper;

    public BasicAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        BasicAuthenticationHelper basicAuthHelper
    ) : base(options, logger, encoder, clock)
    {
        _basicAuthHelper = basicAuthHelper;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        try
        {
            var context = Context.Request;

            var authHeader = AuthenticationHeaderValue.Parse(Request.Headers["Authorization"]);
            var credentialBytes = Convert.FromBase64String(authHeader.Parameter);
            var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':', 2);
            var username = credentials[0];
            var password = credentials[1];

            var claims = await _basicAuthHelper.Login(username, password).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(claims);

            Context.User = claims;
            //var token = _tokenService.BuildTokenFromIdentity(claims.Identity);
            //Context.Request.Headers["Authorization"] = $"Bearer {token}";
            //Context.Response.Cookies.Append("T", token);
            return AuthenticateResult.Success(new AuthenticationTicket(claims, Scheme.Name));
        }
        catch (Exception ex)
        {
            return AuthenticateResult.Fail($"Authentication failed: {ex.Message}");
        }
    }
}