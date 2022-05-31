using System.Text.Json.Serialization;

namespace FiftyLab.PrivateSchool.Request;

public class CreateAccountModel : SignupModel
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AccountType Role { get; set; }
}