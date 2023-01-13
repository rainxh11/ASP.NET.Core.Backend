using ExcelMapper;
using QuranSchool.Models.Excel;
using System.Text;

namespace QuranSchool.Services;

public class ExcelConverter
{
    public ExcelConverter()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public List<ExcelStudent> GetStudents(IList<IFormFile> files)
    {
        List<ExcelStudent> students = new();
        foreach (var file in files)
            using (var stream = file.OpenReadStream())
            using (var importer = new ExcelImporter(stream))
            {
                importer.Configuration.RegisterClassMap<ExcelStudentMap>();

                var sheet = importer.ReadSheet();
                var groupStudents = sheet.ReadRows<ExcelStudent>().Select(x =>
                {
                    x.TeacherName = file.FileName.Replace(".xlsx", "");
                    if (DateTime.TryParse(x.DateOfBirth, out var date)) x.DOB = date;

                    return x;
                });
                students.AddRange(groupStudents);
            }

        return students;
    }
}