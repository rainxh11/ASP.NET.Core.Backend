using QuranSchool.Models;

namespace QuranSchool.Report;

public partial class InvoiceDocument
{
    public InvoiceDocument()
    {
        InitializeComponent();
    }

    public void FillReport(InvoiceReport model)
    {
        objectDataSource1.DataSource = model;
        FillDataSource();
    }
}