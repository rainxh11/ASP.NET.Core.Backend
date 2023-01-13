using System.Text.Json.Serialization;

namespace QuranSchool.Models;

public class MailerSendTemplateResponse
{
    [JsonPropertyName("data")] public List<MailerSendTemplate> Templates { get; set; }
}