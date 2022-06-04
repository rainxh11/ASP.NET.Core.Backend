using System.Net;
using System.Text.Json.Serialization;

namespace ReniwnMailServiceApi.Models;

public class ReniwnResponse
{
    [JsonPropertyName("STATUS")] public string Status { get; set; }

    [JsonPropertyName("MSG")] public string Message { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    [JsonPropertyName("CODE")]
    public HttpStatusCode HttpStatusCode { get; set; }
}