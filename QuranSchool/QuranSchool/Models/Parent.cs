using MongoDB.Bson;
using HashCode = Invio.Hashing.HashCode;

namespace QuranSchool.Models;

public class Parent : IEquatable<Parent>
{
    public string ID { get; set; } = ObjectId.GenerateNewId().ToString();
    public DateTime CreatedOn { get; set; } = DateTime.Now;
    public DateTime ModifiedOn { get; set; }
    public string Name { get; set; }
    public string CardID { get; set; }
    public string Address { get; set; }
    public string PhoneNumber { get; set; }
    public string? Job { get; set; } = "";
    public DateTime DateOfBirth { get; set; }

    public bool Equals(Parent? other)
    {
        return Name == other.Name && PhoneNumber == other.PhoneNumber;
    }

    public override int GetHashCode()
    {
        return HashCode.From(Name, PhoneNumber);
    }
}