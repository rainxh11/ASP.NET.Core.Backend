using System;
using System.Drawing;
using DevExpress.XtraReports.UI;
using RisReport.Library.Models;

namespace RisReport.Library.Reports
{
    public partial class DebtReport
    {
        public DebtReport()
        {
            InitializeComponent();
        }

        public void FillReport(RisDebtReport model)
        {
            try
            {
                this.Watermark.ImageSource = new DevExpress.XtraPrinting.Drawing.ImageSource(Image.FromFile(AppContext.BaseDirectory + @"\Assets\watermark.png"));
            }
            catch
            {
            }
            objectDataSource1.DataSource = model;
        }
    }
}