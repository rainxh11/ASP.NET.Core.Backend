using System.Text.Json.Serialization;

namespace QuranSchool.Models.Request;

public class AccountUpdateModel
{
    public string? Id { get; set; }
    public string? Name { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AccountType? Role { get; set; }

    public string? Description { get; set; }
    public bool? EmailConfirmed { get; set; } = true;
    public bool? Enabled { get; set; } = true;
#nullable enable
#nullable disable
}