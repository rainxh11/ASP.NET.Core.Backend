using QuranSchool.Models;

namespace QuranSchool.Services;

public class ResetPasswordVariables : EmailRequestVariables
{
    public string Name { get; init; }
    public string UserName { get; init; }

    public override List<Substitution> GetSubstitutions()
    {
        return new List<Substitution>
        {
            new Substitution("name", Name),
            new Substitution("userName", UserName)
        };
    }
}