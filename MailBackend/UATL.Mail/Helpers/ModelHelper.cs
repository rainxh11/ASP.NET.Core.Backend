using System.Text.RegularExpressions;
using System.Web;

namespace UATL.Mail.Helpers;

public class ModelHelper
{
    public static string ReplaceHref(string content, string replaceRoute = "/externallink?link=")
    {
        var decoded = HttpUtility.HtmlDecode(content);
        var regex = new Regex(@"href=""([^""]+)""", RegexOptions.Multiline);

        var matches = regex
            .Matches(decoded)
            .Select(x => x.Groups[1].Value)
            .Select(x => new {Replace = $"{replaceRoute}{Uri.EscapeDataString(x)}", Match = x});

        foreach (var match in matches) decoded = decoded.Replace(match.Match, match.Replace);

        return HttpUtility.HtmlEncode(decoded);
    }
}