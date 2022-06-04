using Refit;

namespace ReniwnMailServiceApi.Services;

public class RestApiClientFactory
{
    private readonly IConfiguration _configuration;

    public RestApiClientFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IMyReniwnApiClient CreateReniwnClient()
    {
        return RestService.For<IMyReniwnApiClient>(_configuration["API:BasePath"]);
    }

    public IMailerSendApiClient CreateMailerSendClient()
    {
        return RestService.For<IMailerSendApiClient>(_configuration["MailerSend:BasePath"]);
    }
}