using System.Text.Json.Serialization;

namespace ReniwnMailServiceApi.Models;

public class User
{
    [JsonPropertyName("nickname")] public string? NickName { get; set; }
    public string Email { get; set; }
    public string? Password { get; set; }
    public string? Phone { get; set; } = "";
}