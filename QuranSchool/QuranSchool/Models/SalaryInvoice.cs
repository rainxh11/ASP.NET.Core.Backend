using MongoDB.Entities;

namespace QuranSchool.Models;

public class SalaryInvoice : ClientEntity, ICreatedOn, IModifiedOn
{
    public SalaryInvoice()
    {
        this.InitOneToMany(() => Transactions);
    }

    public AccountBase? CreatedBy { get; set; }

    public List<Session> Sessions { get; set; } = new();
    public TeacherBase Teacher { get; set; }
    public double SchoolPercentage { get; set; }

    public double SessionsTotal => Sessions
        .Where(x => x.Teacher.ID == Teacher.ID && x.TeacherWasPresent && !x.Cancelled).Sum(x => x.Price);

    public double TeacherCut => SessionsTotal - SessionsTotal * SchoolPercentage / 100;
    public double SchoolCut => SessionsTotal - TeacherCut;

    public double TeacherDue => TeacherCut -
                                Transactions.Where(x => x.Type == TransactionType.Salary && x.Enabled)
                                    .Sum(x => x.Amount);

    public Many<Transaction> Transactions { get; set; }

    public bool Enabled { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime ModifiedOn { get; set; }
}