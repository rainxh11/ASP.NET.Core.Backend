using System.Collections.Generic;

namespace UATL.MailSystem.Common.Models.Request;

public class DraftRequest
{
    public string Subject { get; set; }
    public string Body { get; set; }
    public ISet<string> HashTags { get; set; } = new HashSet<string>();
}