using System.Text.Json.Serialization;

namespace QuranSchool.Models;

public class MailerSendRequest
{
    public Address From { get; set; }
    public List<Address> To { get; set; }
    public string? Subject { get; set; }
    public string? Text { get; set; }
    public string? Html { get; set; }
    [JsonPropertyName("template_id")] public string TemplateId { get; set; }
    public List<TemplateVariable> Variables { get; set; } = new();
}