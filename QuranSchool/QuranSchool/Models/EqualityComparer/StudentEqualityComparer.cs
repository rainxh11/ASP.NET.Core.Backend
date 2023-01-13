namespace QuranSchool.Models.EqualityComparer;

public class StudentEqualityComparer : IEqualityComparer<Student>
{
    public bool Equals(Student? x, Student? y)
    {
        return x.DateOfBirth == y.DateOfBirth && x.Name == y.Name;
    }

    public int GetHashCode(Student obj)
    {
        return Invio.Hashing.HashCode.From(obj.Name, obj.DateOfBirth.ToString("dd/MM/yyy"));
    }
}