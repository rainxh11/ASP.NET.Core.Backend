using ExcelMapper;

namespace QuranSchool.Models.Excel;

public class ExcelStudentMap : ExcelClassMap<ExcelStudent>
{
    public ExcelStudentMap()
    {
        Map(x => x.FullName)
            .WithConverter(x => x.Trim());
        Map(x => x.DateOfBirth)
            .WithConverter(x => x.Trim());
        Map(x => x.ParentJob)
            .WithConverter(x => x.Trim());
        Map(x => x.ParentName)
            .WithConverter(x => x.Trim());
        Map(x => x.Address)
            .WithConverter(x => x.Trim());
        Map(x => x.PhoneNumber)
            .WithConverter(x => x.StartsWith("0") ? x : $"0{x}");
        Map(x => x.Sickness)
            .WithConverter(x => x.Trim());
        Map(x => x.Level)
            .WithConverter(x => x.Trim());
        Map(x => x.PlaceOfBirth)
            .WithConverter(x => x.Trim());
        Map(x => x.Group);
        Map(x => x.Gender);
    }
}