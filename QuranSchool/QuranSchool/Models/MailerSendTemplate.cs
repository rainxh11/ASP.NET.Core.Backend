using Newtonsoft.Json.Converters;
using QuranSchool.Services;

namespace QuranSchool.Models;

public class MailerSendTemplate
{
    public string Id { get; set; }

    [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
    public MailType Name { get; set; }
}