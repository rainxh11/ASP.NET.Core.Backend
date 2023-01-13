using System;
using System.Drawing;
using DevExpress.XtraReports.UI;
using RisReport.Library.Models;

namespace RisReport.Library.Reports
{
    public partial class IncomeReport
    {
        public IncomeReport()
        {
            InitializeComponent();
        }
        public void FillReport(RisIncomeReport model)
        {
            objectDataSource1.DataSource = model;
        }
    }
}
