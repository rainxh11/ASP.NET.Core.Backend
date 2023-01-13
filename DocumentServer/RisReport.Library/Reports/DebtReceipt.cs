using System;
using System.Threading.Tasks;
using DevExpress.XtraReports.UI;
using RisReport.Library.Models;

namespace RisReport.Library.Reports
{
    public partial class DebtReceipt
    {
        public DebtReceipt()
        {
            InitializeComponent();
        }

        public void FillReport(InvoiceReportModel model, bool debtReport = false)
        {
            ExamTable.Visible = !debtReport;

            ProductTable.Visible = model.Products.Count != 0;
            TransactionsTable.Visible = model.Transactions.Count != 0 || debtReport;
            PriceTable.Visible = !debtReport;
            TotalPriceTable.Visible = !debtReport;
            DiscountTable.Visible = !debtReport || model.Discount != 0;
            if (model.Discount == 0) DiscountTable.Visible = false;
            ConventionTable.Visible = !debtReport || model.ConventionDiscount != 0;
            if (model.ConventionDiscount == 0) ConventionTable.Visible = false;

            TotalDebtTable.Visible = !debtReport || model.TotalDebt != 0;

            if (model.TotalDebt > 0)
            {
                TotalDebtTable.Visible = true;
                TransactionsTable.Visible = true;
            }
            else
            {
                TotalDebtTable.Visible = false || debtReport;
                TransactionsTable.Visible = false || debtReport;
            }

            objectDataSource1.DataSource = model;

            try
            {
                QrCode.Text = model.QrCode;
            }
            catch
            {
            }
        }
    }
}