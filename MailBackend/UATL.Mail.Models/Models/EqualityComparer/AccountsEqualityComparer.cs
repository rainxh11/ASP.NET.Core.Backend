using System.Collections.Generic;
using System.Text.Json;
using Invio.Hashing;

namespace UATL.MailSystem.Common.EqualityComparer;

public class AccountsEqualityComparer : IEqualityComparer<List<Account>>
{
    public bool Equals(List<Account> x, List<Account> y)
    {
        return JsonSerializer.Serialize(x) == JsonSerializer.Serialize(y);
    }

    public int GetHashCode(List<Account> obj)
    {
        return HashCode.From(obj);
    }
}