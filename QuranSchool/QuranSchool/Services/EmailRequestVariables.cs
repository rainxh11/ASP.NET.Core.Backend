using QuranSchool.Models;

namespace QuranSchool.Services;

public abstract class EmailRequestVariables
{
    public abstract List<Substitution> GetSubstitutions();
}