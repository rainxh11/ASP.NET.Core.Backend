namespace QuranSchool.Models.Request;

public class ExternalAuthModel
{
    public string? Provider { get; set; }
    public string? IdToken { get; set; }
}