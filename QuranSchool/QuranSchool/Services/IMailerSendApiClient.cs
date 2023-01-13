using QuranSchool.Models;
using Refit;

namespace QuranSchool.Services;

public interface IMailerSendApiClient
{
    [Post("/email")]
    Task<HttpResponseMessage> SendEmail([Authorize()] string token, MailerSendRequest request);

    [Get("/templates")]
    Task<MailerSendTemplateResponse> GetTemplates([Authorize()] string token);
}