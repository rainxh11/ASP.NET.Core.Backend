namespace FiftyLab.PrivateSchool;
public class Parent : IEquatable<Parent>
{
    public string Name { get; set; }
    public string CardID { get; set; }
    public string Address { get; set; }
    public string PhoneNumber { get; set; }
    public DateTime DateOfBirth { get; set; }

    public bool Equals(Parent? other)
    {
        return this.Name == other.Name && this.PhoneNumber == other.PhoneNumber;
    }

    public override int GetHashCode()
    {
        return Invio.Hashing.HashCode.From(Name, PhoneNumber);
    }
}