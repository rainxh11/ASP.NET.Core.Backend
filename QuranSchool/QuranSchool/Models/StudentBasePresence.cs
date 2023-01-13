namespace QuranSchool.Models;

public class StudentBasePresence : StudentBase
{
    public bool Present { get; set; }
    public DateTime? PresentOn { get; set; }

    public InvoiceType Paid { get; set; } = InvoiceType.NotPaid;
}