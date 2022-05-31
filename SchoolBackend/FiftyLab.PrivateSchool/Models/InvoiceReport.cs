namespace FiftyLab.PrivateSchool.Models
{
    public class InvoiceReport
    {
        public SchoolInfo SchoolInfo { get; set; }
        public Invoice Invoice { get; set; }
        public Student Student { get; set; }
        public string Parent => string.Join("; ", Student.Parents.Select(x => x.Name));
        public List<Transaction> Transactions => Invoice.Transactions.OrderBy(x => x.CreatedOn).ToList();
    }
}