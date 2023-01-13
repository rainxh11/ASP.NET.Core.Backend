using QuranSchool.Models;

namespace QuranSchool.Report;

public partial class StudentPasswordsDocument
{
    public StudentPasswordsDocument()
    {
        InitializeComponent();
    }

    public void FillReport(StudentPasswordReport model)
    {
        objectDataSource1.DataSource = model;
        FillDataSource();
    }
}