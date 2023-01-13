using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace QuranSchool.Models;

public class Invoice : ClientEntity, ICreatedOn, IModifiedOn
{
    public Invoice()
    {
        this.InitManyToMany(() => Transactions, x => x.Invoices);
    }

    [IgnoreDefault] public string? GroupId { get; set; }

    public string InvoiceID { get; set; }
    [BsonRequired] public DateTime StartDate { get; set; }

    public DateTime ExpirationDate => StartDate.AddDays(Formation.DurationDays);

    public bool Expired => DateTime.Now > ExpirationDate;

    public bool Enabled { get; set; } = true;
    [BsonRequired] public FormationBase Formation { get; set; }
    [BsonRequired] public StudentBase Student { get; set; }

    public List<Parent> Parents { get; set; }
    [BsonRequired] public AccountBase CreatedBy { get; set; }

    [IgnoreDefault] public AccountBase? CancelledBy { get; set; } = null;
    [IgnoreDefault] public DateTime? CancelledOn { get; set; } = null;
    [OwnerSide] public Many<Transaction> Transactions { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    [BsonRepresentation(BsonType.String)]
    public InvoiceType Type { get; set; } = InvoiceType.NotPaid;

    [JsonConverter(typeof(StringEnumConverter))]
    public InvoiceStatus Status =>
        Expired && Enabled
            ? InvoiceStatus.Expired
            : !Enabled
                ? InvoiceStatus.Cancelled
                : !Expired && Enabled
                    ? InvoiceStatus.Enabled
                    : InvoiceStatus.Enabled;

    public AccountBase? LastPaidBy
    {
        get
        {
            return !Transactions.Any(x => x.Enabled && x.Type == TransactionType.Payment)
                ? null
                : Transactions
                    .Where(x => x.Enabled && x.Type == TransactionType.Payment)
                    .OrderByDescending(x => x.CreatedOn)
                    .First()
                    .CreatedBy;
        }
    }

    public DateTime? LastPaidOn
    {
        get
        {
            return !Transactions.Any(x => x.Enabled && x.Type == TransactionType.Payment)
                ? null
                : Transactions
                    .Where(x => x.Enabled && x.Type == TransactionType.Payment)
                    .OrderByDescending(x => x.CreatedOn)
                    .First()
                    .CreatedOn;
        }
    }

    public double PriceAfterDiscount
    {
        get
        {
            var total = Formation.Price - Discount;
            return total >= 0 ? total : 0;
        }
    }

    public double Discount
    {
        get
        {
            return Transactions
                .Where(x => x.Type == TransactionType.Discount && x.Enabled)
                .Sum(x => x.Amount);
        }
    }

    public double Paid
    {
        get
        {
            return Transactions
                .Where(x => x.Type == TransactionType.Payment && x.Enabled)
                .Sum(x => x.Amount);
        }
    }

    public double LeftUnpaid
    {
        get
        {
            var left = PriceAfterDiscount - Paid;
            return left >= 0 ? left : 0;
        }
    }

    public DateTime CreatedOn { get; set; } = DateTime.Now;
    public DateTime ModifiedOn { get; set; }
}