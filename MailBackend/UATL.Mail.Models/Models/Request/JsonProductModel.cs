using System.Text.Json.Serialization;

namespace UATL.MailSystem.Common.Request;

public class JsonProductModel
{
    [JsonInclude] public string ProductName { get; set; }

    [JsonInclude] public string Barcode { get; set; }
}