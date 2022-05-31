using System.Text.Json.Serialization;

namespace UATL.MailSystem.Common.Request;

public class CreateAccountModel : SignupModel
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AccountType Role { get; set; }
}