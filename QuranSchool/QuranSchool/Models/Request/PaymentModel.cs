using MongoDB.Bson.Serialization.Attributes;

namespace QuranSchool.Models.Request;

public class PaymentModel
{
    [BsonIgnoreIfNull] public double? Paid { get; set; }
    [BsonIgnoreIfNull] public double? Discount { get; set; }
}