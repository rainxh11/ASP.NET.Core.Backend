using QuranSchool.Models;

namespace QuranSchool.Services;

public class MailerService
{
    private readonly IConfiguration _configuration;
    private readonly IMailerSendApiClient _mailerSendApi;
    private readonly ILogger<MailerService> _logger;

    public MailerService(IConfiguration configuration, ILogger<MailerService> logger, IMailerSendApiClient client)
    {
        _logger = logger;
        _mailerSendApi = client;
        _configuration = configuration;
    }

    public async Task SendMail(MailerSendRequest request, MailType type)
    {
        try
        {
            var templates = await _mailerSendApi.GetTemplates(_configuration["MailerSend:ApiKey"]);

            request.TemplateId = templates.Templates.First(x => x.Name == type).Id;
            var result = await _mailerSendApi.SendEmail(_configuration["MailerSend:ApiKey"], request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "");
        }
    }


    public MailerSendRequest CreateMailerSendRequest(Address address, string subject,
        List<Substitution> variables)
    {
        var templateVariables = new TemplateVariable
        {
            Email = address.Email,
            Substitutions = variables
        };

        var request = new MailerSendRequest
        {
            From = new Address(_configuration["MailerSend:Email"], _configuration["EmailOptions:Sender"]),
            To = new List<Address> { address },
            Subject = subject,
            Variables = new List<TemplateVariable> { templateVariables }
        };
        return request;
    }
}