using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RisReport.Library.Models
{
    public class RisDebtReport
    {
        public List<RisDebtClient> Clients { get; set; } = new List<RisDebtClient>();
        public decimal TotalDebt => Clients.Sum(x => x.debtAmount);
        public int ClientCount => Clients.Count;

        public string Date => DateTime.Now.ToString("dddd, dd MMMM yyyy HH:mm", CultureInfo.CreateSpecificCulture("fr-Fr")).ToUpperInvariant();
    }
    public class RisDebtClient
    {
        public string Name { get => $"{familyName} {firstName}".Trim(); }
        public string firstName { get; set; }
        public string familyName { get; set; }

        public string gender { get; set; }

        public string phoneNumber { get; set; }

        public DateTime lastDebtAt { get; set; }

        [BsonIgnore]
        public string LastDebt => lastDebtAt.ToString("dddd, dd MMMM yyyy | HH:mm", CultureInfo.CreateSpecificCulture("fr-Fr")).ToUpperInvariant();

        public DateTime createdAt { get; set; }

        [BsonIgnore]
        public string CreationDate => createdAt.ToString("dddd, dd MMMM yyyy | HH:mm", CultureInfo.CreateSpecificCulture("fr-Fr")).ToUpperInvariant();

        public List<DebtModel> debtPaymentInfo { get; set; }

        [BsonIgnore]
        public List<DebtModel> Debts => debtPaymentInfo.Where(x => x.amount > 0).OrderBy(x => x.date).ToList();

        public decimal debtAmount { get; set; }
    }
    public class DebtModel
    {
        public ObjectId _id { get; set; }
        public double amount { get; set; }
        public string status { get; set; }
        public DateTime date { get; set; }

        [BsonIgnore]
        public string DebtDate => date.ToString("dddd, dd MMMM yyyy | HH:mm", CultureInfo.CreateSpecificCulture("fr-Fr")).ToUpperInvariant();

        [BsonIgnore]
        public string Type
        {
            get
            {
                switch (status)
                {
                    default:
                    case "out":
                        return "<color='red'>Dette Sortie</color>".ToUpperInvariant();
                    case "in":
                        return "<color='green'>Dette Payée</color>".ToUpperInvariant();
                    case "discount":
                        return "<color='orange'>Remise de Dette</color>".ToUpperInvariant();
                }
            }
        }

        public RisUser createdBy { get; set; }

        [BsonIgnore]
        public string User => createdBy != null ? createdBy.name  : string.Empty;
    }

    [Collection("users")]
    public class RisUser : Entity
    {
        public string name { get; set; }
        public string email { get; set; }
    }

    public class ClientDebt
    {
        public ObjectId _id { get; set; }
        public double amount { get; set; }
        public string status { get; set; }
        public ObjectId createdBy { get; set; }
        public DateTime date { get; set; }

        [BsonIgnore]
        public string Type
        {
            get
            {
                switch (status)
                {
                    default:
                    case "out":
                        return "<color='red'>Dette</color>".ToUpperInvariant();
                    case "in":
                        return "<color='green'>Payée</color>".ToUpperInvariant();
                    case "discount":
                        return "<color='orange'>Remise de Dette</color>".ToUpperInvariant();
                }
            }
        }
    }

    [Collection("clients")]
    public class RisClient : Entity
    {
        public string Name { get => $"{familyName} {firstName}".Trim(); }
        public string NameWithPhone { get => $"<b>{familyName} {firstName}</b>\n<i>{phoneNumber}</i>"; }
         public string firstName { get; set; }
        public string familyName { get; set; }

        //public DateTime birthdate { get; set; }
        public string gender { get; set; }

        public string phoneNumber { get; set; }

        public DateTime createdAt { get; set; }
        public List<ClientDebt> debtPaymentInfo { get; set; }

        public string GetAccession()
        {
            return createdAt.ToString("yyyyMMdd-HHmmss");
        }
    }

    [Collection("studies")]
    public class RisStudy : IEntity
    {
        [BsonId]
        public int _id { get; set; }

        public string statusPayment { get; set; }
        public string examType { get; set; }
        public string modality { get; set; }
        public double discount { get; set; }
        public double price { get; set; }
        public string group { get; set; }
        public string conv { get; set; }
        public double convPrice { get; set; }

        public string GetDescription()
        {
            return $"{modality} {examType}".ToUpperInvariant().Replace("-", "").Trim();
        }

        public DateTime paidAt { get; set; }
        public ObjectId client { get; set; }

        public string ID { get; set; }

        public string GenerateNewID()
        {
            throw new NotImplementedException();
        }
    }

    public class InvoiceReportModel
    {
        public RisClient ClientModel { get; set; }
        public List<RisReceipt> Exams { get; set; } = new List<RisReceipt>();
        public string PaidBy { get; set; } = string.Empty;
        public double PaidAmount { get; set; }

        public double Paid
        {
            get
            {
                try
                {
                    return Exams.Sum(x => x.price) - Exams.Sum(x => Math.Abs(x.discount));
                }
                catch
                {
                    return 0;
                }
            }
        }

        public double Discount
        {
            get
            {
                try
                {
                    return Exams.Sum(x => Math.Abs(x.discount));
                }
                catch
                {
                    return 0;
                }
            }
        }

        public double TotalPrice
        {
            get
            {
                try
                {
                    return Exams.Sum(x => x.price);
                }
                catch
                {
                    return 0;
                }
            }
        }

        public string Status
        {
            get
            {
                try
                {
                    return Exams.FirstOrDefault().status;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        public double ConventionDiscount { get => Exams.Sum(x => x.conventionPrice); }

        public string Client
        {
            get
            {
                if (ClientModel == null)
                {
                    return Exams.First().client.Name;
                }
                else
                {
                    return ClientModel.Name;
                }
            }
        }

        public string InvoiceDate
        {
            get
            {
                return PaidAt.ToString("dddd, d MMMM, yyyy HH:mm", CultureInfo.CreateSpecificCulture("fr-FR")).ToUpperInvariant();
            }
        }

        public DateTime PaidAt
        {
            get
            {
                if (ClientModel == null)
                {
                    return Exams.First().date.ToLocalTime();
                }
                else
                {
                    return ClientModel.debtPaymentInfo.Where(x => x.status == "in").OrderByDescending(x => x.date).FirstOrDefault().date.ToLocalTime();
                }
            }
        }

        public List<ClientDebt> Transactions
        {
            get
            {
                try
                {
                    return Exams.FirstOrDefault().client.debtPaymentInfo.Where(x => x.amount > 0).ToList();
                }
                catch
                {
                    return ClientModel.debtPaymentInfo.Where(x => x.amount > 0).ToList();
                }
            }
        }
        public InvoiceReportModel(string host)
        {
            _host = host;
        }
        private string _host;

        public string QrCode
        {
            get
            {
                var clientId = ClientModel == null ? Exams.FirstOrDefault().client.ID : ClientModel.ID;
                return $"URL:{_host}/studies/table?id={clientId}";
            }
        }

        public string Barcode
        {
            get
            {
                try
                {
                    return Exams.First()._id.ToString();
                }
                catch
                {
                    return null;
                }
            }
        }

        public double ExamDebt
        {
            get
            {
                if (Transactions.Count != 0)
                {
                    return Transactions.Where(x => x.status == "out").OrderByDescending(x => x.date).First().amount;
                }
                else
                {
                    return 0;
                }
            }
        }

        public double TotalDebt
        {
            get
            {
                if (ClientModel == null)
                {
                    return Transactions.Where(x => x.status == "out").Sum(x => x.amount) - Transactions.Where(x => x.status == "in").Sum(x => x.amount);
                }
                else
                {
                    return ClientModel.debtPaymentInfo.Where(x => x.status == "out").Sum(x => x.amount) - ClientModel.debtPaymentInfo.Where(x => x.status == "in").Sum(x => x.amount);
                }
            }
        }

        public List<StudyProduct> Products
        {
            get
            {
                try
                {
                    return Exams.SelectMany(x => x.products).ToList();
                }
                catch
                {
                    return new List<StudyProduct>();
                }
            }
        }
    }

    public class StudyProduct
    {
        public string name { get; set; }
        public double price { get; set; }
        public int quantityP { get; set; }

        public double SubTotal
        {
            get
            {
                return quantityP * price;
            }
        }
    }
    public class RisIncomeReport
    {
        public List<RisReceipt> Studies { get; set; } = new List<RisReceipt>();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Period {
            get
            {
                if (AllTime)
                {
                    return "<b>Tous les temps.</b>";
                }
                else
                {
                   if(EndDate.ToString("yyyy-MM-dd") == StartDate.ToString("yyyy-MM-dd"))
                    {
                        return $"<b>{StartDate.ToString("dddd, dd MMMM yyyy", CultureInfo.CreateSpecificCulture("fr-FR"))}</b>".ToUpperInvariant();
                    }
                    else
                    {
                        return $"<b>{StartDate.ToString("dddd, dd MMMM yyyy", CultureInfo.CreateSpecificCulture("fr-FR"))}</b> <i>j'usqua</i> <b>{EndDate.ToString("dddd, dd MMMM yyyy", CultureInfo.CreateSpecificCulture("fr-FR"))}</b>".ToUpperInvariant();
                    }
                }
            }
        }
        public double TotalPaid { get => Studies.Sum(x => x.TotalPaid); }
        public int TotalCount { get => Studies.Count; }
        public string TotalDetails { get => $"<b>{TotalPaid.ToString("N2")} DA</b> ({TotalCount.ToString("N0")} Examens)";  }

        public bool AllTime { get; set; } = false;
    }
    
    public class ExamReport : Entity
    {
        public string paidBy { get; set; }
        public string exam { get; set; }
        public int examCount { get; set; }
        public double price { get; set; }
        public double discount { get; set; }
        public double convPrice { get; set; }
        public string status { get; set; }
        public double productSum { get; set; }
        public double TotalPaid
        {
            get
            {
                if (status == "debt")
                {
                    return 0;
                }
                else
                {
                    return (productSum + price) - discount;
                }
            }
        }
    }
    public class RisExamsReport 
    {
        public List<ExamReport> Exams { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool AllTime { get; set; } = false;
        public string Period
        {
            get
            {
                if (AllTime)
                {
                    return "<b>Tous les temps.</b>";
                }
                else
                {
                    if (EndDate.ToString("yyyy-MM-dd") == StartDate.ToString("yyyy-MM-dd"))
                    {
                        return $"<b>{StartDate.ToString("dddd, dd MMMM yyyy", CultureInfo.CreateSpecificCulture("fr-FR"))}</b>".ToUpperInvariant();
                    }
                    else
                    {
                        return $"<b>{StartDate.ToString("dddd, dd MMMM yyyy", CultureInfo.CreateSpecificCulture("fr-FR"))}</b> <i>j'usqua</i> <b>{EndDate.ToString("dddd, dd MMMM yyyy", CultureInfo.CreateSpecificCulture("fr-FR"))}</b>".ToUpperInvariant();
                    }
                }
            }
        }
        public double TotalPaid { get => Exams.Sum(x => x.TotalPaid); }
        public int TotalCount { get => Exams.Sum(x => x.examCount); }
        public string TotalDetails { get => $"<b>{TotalPaid.ToString("N2")} DA</b> ({TotalCount.ToString("N0")} Examens)"; }

    }

    public class RisReceipt : IEntity
    {
        [BsonId]
        public long _id { get; set; }

        public string status { get; set; }
        public string StatusDetails
        {
            get
            {
                switch (status)
                {
                    case "debt":
                        return "<color='orange'>Dette</color>".ToUpperInvariant();
                    case "paid":
                        return "<color='green'>Payée</color>".ToUpperInvariant();
                    default:
                        return status.ToUpperInvariant();
                }
            }
        }
        public string exam { get; set; }
        public double discount { get; set; }
        public double paid { get; set; }
        public double productsSum { get => products.Sum(x => x.SubTotal); }
        public double TotalPaid { 
            get
            {
                if(status == "debt")
                {
                    return 0;
                }
                else
                {
                    return productsSum + paid;
                }
            }
        }
        public double price { get; set; }
        public string convention { get; set; }
        public double conventionPrice { get; set; }
        public string ConventionDetails
        {
            get
            {
                try
                {
                    if(convention == "NORMAL")
                    {
                        return "0.00 DA";
                    }
                    else
                    {
                        return $"{conventionPrice.ToString("N2")} DA\n<b><i>{convention.ToUpperInvariant()}</i></b>";
                    }
                }
                catch
                {
                    return string.Empty;
                }
            }
        }
        public DateTime createdAt { get; set; }
        public DateTime date { get; set; }
        public string Day { get => date.ToString("dd/MM/yyyy"); }
        public RisClient client { get; set; }
        public string paidBy { get; set; }
        public List<StudyProduct> products { get; set; } = new List<StudyProduct>();
        public string TotalPrice
        {
            get
            {
                var productSum = products.Sum(x => x.SubTotal);
                if(productSum > 0)
                {
                    return $"{price.ToString("N2")} DA \n+<i>{productSum.ToString("N2")} DA de Produits)</i>";

                }
                else
                {
                    return $"{price.ToString("N2")} DA";
                }
            }
        }
        public string ID { get; set; }

        public string GenerateNewID()
        {
            throw new NotImplementedException();
        }
    }
}