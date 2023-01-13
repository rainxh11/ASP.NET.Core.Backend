namespace QuranSchool.Models.Excel;

public class ExcelStudent
{
    public string FullName { get; set; }
    public string PlaceOfBirth { get; set; }
    public string DateOfBirth { get; set; }
    public DateTime DOB { get; set; } = new(2010, 1, 1);
    public string PhoneNumber { get; set; }
    public string Sickness { get; set; }
    public string Level { get; set; }
    public string ParentName { get; set; }
    public string ParentJob { get; set; }
    public string Address { get; set; }
    public int Group { get; set; } = 1;
    public string? TeacherName { get; set; }
    public Gender Gender { get; set; } = Gender.Male;
}