namespace QuranSchool.Models;

public class TemplateVariable
{
    public string Email { get; set; }
    public List<Substitution> Substitutions { get; set; } = new();
}