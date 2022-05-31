using System.ComponentModel;

namespace FiftyLab.PrivateSchool;

public enum TransactionType
{
    [Description("Paiment")] Payment,
    [Description("Dette")] Debt,
    [Description("Remise")] Discount,
    [Description("Dépense")] Expense,
    [Description("Remborsement")] Refund
}