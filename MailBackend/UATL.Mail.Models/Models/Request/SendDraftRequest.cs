using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UATL.MailSystem.Common.Models.Request;

public class SendDraftRequest
{
    public List<string> Recipients { get; set; } = new();

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public List<MailFlag> Flags { get; set; } = new();
}