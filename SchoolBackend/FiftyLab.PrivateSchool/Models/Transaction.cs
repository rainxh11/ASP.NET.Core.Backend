using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FiftyLab.PrivateSchool;

public class Transaction : Entity, ICreatedOn, IModifiedOn
{
    public Transaction()
    {
        this.InitOneToMany(() => Invoices);
    }

    [JsonIgnore] [InverseSide] public Many<Invoice> Invoices { get; set; } = new Many<Invoice>();

    public Transaction(TransactionType type, double amount, AccountBase account)
    {
        this.Amount = amount;
        this.CreatedBy = account;
        this.Type = type;
    }

    [JsonConverter(typeof(StringEnumConverter))]
    [BsonRepresentation(BsonType.String)]
    public TransactionType Type { get; set; }

    [JsonIgnore]
    [BsonIgnore]
    public string TypeLabel
    {
        get
        {
            if (!Enabled) return "<color='black'>Annulé</color>".ToUpperInvariant();
            switch (Type)
            {
                case TransactionType.Debt:
                    return "<color='red'>Dette</color>".ToUpperInvariant();
                default:
                    return "";
                case TransactionType.Payment:
                    return "<color='green'>Paiment</color>".ToUpperInvariant();
                case TransactionType.Discount:
                    return "<color='purple'>Remise</color>".ToUpperInvariant();
            }
        }
    }

    public double Amount { get; set; } = 0;
    public bool Enabled { get; set; } = true;
    public DateTime CreatedOn { get; set; } = DateTime.Now;
    public DateTime ModifiedOn { get; set; }
    public AccountBase CreatedBy { get; set; }
    [IgnoreDefault] public AccountBase? CancelledBy { get; set; } = null;
    [IgnoreDefault] public DateTime? CancelledOn { get; set; } = null;
}