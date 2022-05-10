using System.Text.Json;

namespace FiftyLab.PrivateSchool.EqualityComparer;

public class ListEqualityComparer<T> : IEqualityComparer<T>
{
    public bool Equals(T x, T y)
    {
        return JsonSerializer.Serialize(x) == JsonSerializer.Serialize(y);
    }

    public int GetHashCode(T obj)
    {
        return Invio.Hashing.HashCode.From(obj);
    }
}