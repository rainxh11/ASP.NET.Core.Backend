using Refit;
using ReniwnMailServiceAPI.Models;

namespace ReniwnMailServiceApi.Services;

public interface IMailerSendApiClient
{
    [Post("/email")]
    Task<HttpResponseMessage> SendEmail([Authorize()] string token, MailerSendRequest request);

    [Get("/templates")]
    Task<MailerSendTemplateResponse> GetTemplates([Authorize()] string token);
}