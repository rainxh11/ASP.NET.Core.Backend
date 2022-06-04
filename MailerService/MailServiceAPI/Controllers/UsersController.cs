using System.Net;
using Jetsons.JetPack;
using Microsoft.AspNetCore.Mvc;
using ReniwnMailServiceApi.Models;
using ReniwnMailServiceAPI.Models;
using ReniwnMailServiceApi.Services;
using StringRandomizer;
using StringRandomizer.Options;

namespace ReniwnMailServiceApi.Controllers;

[ApiController]
[Route("[controller]")]
public class UsersController : ControllerBase
{
    private readonly IMyReniwnApiClient _apiClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UsersController> _logger;
    private readonly IMailerSendApiClient _mailerSend;

    public UsersController(ILogger<UsersController> logger,
        RestApiClientFactory clientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _apiClient = clientFactory.CreateReniwnClient();
        _mailerSend = clientFactory.CreateMailerSendClient();
    }

    [HttpPost("")]
    public async Task<IActionResult> CreateUser([FromBody] User user, CancellationToken ct)
    {
        try
        {
            var random = new Randomizer(_configuration["GeneratedPasswordOptions:Length"].ToInt(),
                new DefaultRandomizerOptions(_configuration["GeneratedPasswordOptions:HasNumbers"].ToBool(),
                    _configuration["GeneratedPasswordOptions:HasUpperCaseAlpha"].ToBool(),
                    _configuration["GeneratedPasswordOptions:HasLowerCaseAlpha"].ToBool(),
                    _configuration["GeneratedPasswordOptions:HasSpecialCharacters"].ToBool()));

            user.Password ??= random.Next();
            user.NickName ??= user.Email.Split('@').First();

            var response = await _apiClient.CreateUser(_configuration["API:Label"],
                _configuration["API:Token"],
                user.NickName,
                user.Email,
                user.Password,
                user.Phone);


            switch (response.HttpStatusCode)
            {
                case HttpStatusCode.Created:
                    var request = CreateMailerSendRequest(user);


                    Task.Run(async () =>
                    {
                        var templates = await _mailerSend.GetTemplates(_configuration["MailerSend:Token"]);
                        request.TemplateId =
                            templates.Templates.First(x => x.Name == _configuration["MailerSend:TemplateName"]).Id;

                        await _mailerSend.SendEmail(_configuration["MailerSend:Token"], request);
                    }, ct);

                    return Created("", response);

                case HttpStatusCode.Accepted:
                    return Ok(response);

                case HttpStatusCode.BadRequest:
                    return BadRequest(response);

                case HttpStatusCode.InternalServerError:
                    return StatusCode(500, response);

                default:
                    return StatusCode((int)response.HttpStatusCode, response);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception.Message);
            return BadRequest(new { Error = exception.Message });
        }
    }

    private MailerSendRequest CreateMailerSendRequest(User user)
    {
        var substitutions = new List<Substitution>
        {
            new("label", _configuration["API:Label"].ToUpperInvariant()),
            new("email", user.Email),
            new("password", user.Password),
            new("nickname", user.NickName),
            new("phone", user.Phone)
        };
        var templateVariables = new TemplateVariable
        {
            Email = user.Email,
            Substitutions = substitutions
        };

        var request = new MailerSendRequest
        {
            From = new Address(_configuration["MailerSend:Email"], _configuration["EmailOptions:Sender"]),
            To = new List<Address> { new(user.Email, user.NickName) },
            Subject = _configuration["EmailOptions:Subject"],
            Variables = new List<TemplateVariable> { templateVariables }
        };
        return request;
    }
}