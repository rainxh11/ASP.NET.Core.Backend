using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FiftyLab.PrivateSchool.Models.Request
{
    public class StudentModel
    {
        [BsonIgnoreIfNull]
        public List<Parent>? Parents { get; set; }
        [BsonIgnoreIfNull]
        public string? Description { get; set; }
        [BsonIgnoreIfNull]
        public string? Address { get; set; }
        [BsonIgnoreIfNull]
        public string? PhoneNumber { get; set; }
        [BsonIgnoreIfNull]
        public string? Name { get; set; }
        [BsonIgnoreIfNull]
        public DateTime? DateOfBirth { get; set; }
        [BsonIgnoreIfNull]
        [JsonConverter(typeof(StringEnumConverter))]
        public Gender? Gender { get; set; }
    }

    public class StudentUpdateModel : StudentModel
    {

    }

    public class FormationModel
    {
        [BsonIgnoreIfNull]
        public string? Name { get; set; }
        [BsonIgnoreIfNull]

        public double? Price { get; set; }
        [BsonIgnoreIfNull]

        public int? DurationDays { get; set; }
    }

    public class FormationUpdateModel : FormationModel
    {
        [BsonIgnoreIfNull]
        public bool? Enabled { get; set; }

    }
    public class InvoiceModel
    {
        public One<Formation> Formation { get; set; }
        public One<Student> Student { get; set; }
        public double Paid { get; set; }
        public DateTime StartDate { get; set; } = DateTime.Now;
        public double Discount { get; set; }
    }

    public class PaymentModel
    {
        [BsonIgnoreIfNull]
        public double? Paid { get; set; }
        [BsonIgnoreIfNull]
        public double? Discount { get; set; }
    }
}
