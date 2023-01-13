namespace QuranSchool.Models;

public class TeacherBase : ClientEntity
{
    public string Name { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string CardID { get; set; }
}