using System.Text.Json.Serialization;

namespace UATL.MailSystem.Common.Request;

public class AccountUpdateModel
{
#nullable enable
    public string? Id { get; set; }
    public string? Name { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AccountType? Role { get; set; }

    public bool? Enabled { get; set; }
    public string? Description { get; set; }
#nullable disable
}