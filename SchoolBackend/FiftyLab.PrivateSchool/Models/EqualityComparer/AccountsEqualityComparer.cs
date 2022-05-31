using System.Text.Json;

namespace FiftyLab.PrivateSchool.EqualityComparer;

public class AccountsEqualityComparer : IEqualityComparer<List<Account>>
{
    public bool Equals(List<Account> x, List<Account> y)
    {
        return JsonSerializer.Serialize(x) == JsonSerializer.Serialize(y);
    }

    public int GetHashCode(List<Account> obj)
    {
        return Invio.Hashing.HashCode.From(obj);
    }
}