using System.Text.Json;
using HashCode = Invio.Hashing.HashCode;

namespace QuranSchool.Models.EqualityComparer;

public class ListEqualityComparer<T> : IEqualityComparer<T>
{
    public bool Equals(T x, T y)
    {
        return JsonSerializer.Serialize(x) == JsonSerializer.Serialize(y);
    }

    public int GetHashCode(T obj)
    {
        return HashCode.From(obj);
    }
}