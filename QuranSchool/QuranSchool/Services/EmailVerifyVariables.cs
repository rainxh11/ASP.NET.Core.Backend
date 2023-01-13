using QuranSchool.Models;

namespace QuranSchool.Services;

public class EmailVerifyVariables : EmailRequestVariables
{
    public string EmailToken { get; init; }

    public override List<Substitution> GetSubstitutions()
    {
        return new List<Substitution>
        {
            new Substitution("emailToken", EmailToken)
        };
    }
}