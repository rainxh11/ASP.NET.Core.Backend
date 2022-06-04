using System.Text.Json.Serialization;

namespace ReniwnMailServiceAPI.Models;

public class MailerSendTemplateResponse
{
    [JsonPropertyName("data")] public List<MailerSendTemplate> Templates { get; set; }
}

public class MailerSendTemplate
{
    public string Id { get; set; }
    public string Name { get; set; }
}

public class MailerSendRequest
{
    public Address From { get; set; }
    public List<Address> To { get; set; }
    public string? Subject { get; set; }
    public string? Text { get; set; }
    public string? Html { get; set; }
    [JsonPropertyName("template_id")] public string TemplateId { get; set; }
    public List<TemplateVariable>? Variables { get; set; }
}

public record Address(string Email, string Name);

public class TemplateVariable
{
    public string Email { get; set; }
    public List<Substitution>? Substitutions { get; set; }
}

public record Substitution(string Var, object Value);