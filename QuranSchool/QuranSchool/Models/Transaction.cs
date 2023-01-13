using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace QuranSchool.Models;

public class Transaction : ClientEntity, ICreatedOn, IModifiedOn
{
    public Transaction()
    {
        this.InitOneToMany(() => Invoices);
    }

    public Transaction(TransactionType type, double amount, AccountBase account)
    {
        Amount = amount;
        CreatedBy = account;
        Type = type;
    }

    [JsonIgnore] [InverseSide] public Many<Invoice> Invoices { get; set; } = new();

    [JsonConverter(typeof(StringEnumConverter))]
    [BsonRepresentation(BsonType.String)]
    public TransactionType Type { get; set; }

    [JsonIgnore]
    [BsonIgnore]
    public string TypeLabel
    {
        get
        {
            return !Enabled
                ? "<color='black'>Annulé</color>".ToUpperInvariant()
                : Type switch
                {
                    TransactionType.Debt => "<color='red'>Dette</color>".ToUpperInvariant(),
                    TransactionType.Payment => "<color='green'>Paiment</color>".ToUpperInvariant(),
                    TransactionType.Discount => "<color='purple'>Remise</color>".ToUpperInvariant(),
                    _ => ""
                };
        }
    }

    public double Amount { get; set; }
    public bool Enabled { get; set; } = true;
    public AccountBase CreatedBy { get; set; }
    [IgnoreDefault] public AccountBase? CancelledBy { get; set; } = null;
    [IgnoreDefault] public DateTime? CancelledOn { get; set; } = null;
    public DateTime CreatedOn { get; set; } = DateTime.Now;
    public DateTime ModifiedOn { get; set; }
}