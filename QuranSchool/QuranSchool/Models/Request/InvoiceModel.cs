using MongoDB.Entities;

namespace QuranSchool.Models.Request;

public class InvoiceModel
{
    public One<Formation> Formation { get; set; }
    public One<Student> Student { get; set; }
    public double Paid { get; set; }
    public DateTime StartDate { get; set; } = DateTime.Now;
    public double Discount { get; set; }
}