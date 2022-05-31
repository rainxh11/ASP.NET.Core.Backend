using System;
using DevExpress.XtraReports.UI;
using FiftyLab.PrivateSchool.Models;

namespace FiftyLab.PrivateSchool.Report
{
    public partial class InvoiceDocument
    {
        public InvoiceDocument()
        {
            InitializeComponent();
        }

        public void FillReport(InvoiceReport model)
        {
            objectDataSource1.DataSource = model;
            this.FillDataSource();
        }
    }
}