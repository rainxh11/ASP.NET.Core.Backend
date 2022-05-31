using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace UATL.MailSystem.Common.Models.Request;

public class SendMailRequest : SendDraftRequest
{
    public string Subject { get; set; }
    public string Body { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public MailType Type { get; set; }

    public ISet<string> HashTags { get; set; } = new HashSet<string>();
    public string ReplyTo { get; set; }
}