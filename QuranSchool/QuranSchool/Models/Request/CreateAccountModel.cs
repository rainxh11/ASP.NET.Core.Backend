using System.Text.Json.Serialization;

namespace QuranSchool.Models.Request;

public class CreateAccountModel : SignupModel
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AccountType Role { get; set; }

    public bool? EmailConfirmed { get; set; } = true;
    public bool? Enabled { get; set; } = true;
    public string? Email { get; set; }
}