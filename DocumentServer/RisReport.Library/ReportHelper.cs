using RisReport.Library.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RisReport.Library

{
    public class ReportHelper
    {
        public static async Task<byte[]> CreateDebtReceipt(InvoiceReportModel model, bool debtReport = false, bool saveReceipt = false, string savePath = null)
        {
            var report = new Reports.DebtReceipt();
            report.FillReport(model, debtReport);

            var stream = new MemoryStream();
            await report.ExportToImageAsync(stream);

            Task.Run(() =>
            {
                try
                {
                    if (saveReceipt)
                    {
                        if (!Directory.Exists(savePath)) Directory.CreateDirectory(savePath);
                        File.WriteAllBytes(savePath + $@"\{DateTime.Now.ToFileTime()}.png", stream.ToArray());
                    }
                }
                catch
                {
                }
            });

            return stream.ToArray();
        }

        public static async Task<byte[]> CreateDetbReport(RisDebtReport model)
        {
            var report = new Reports.DebtReport();
            report.FillReport(model);

            var stream = new MemoryStream();
            await report.ExportToPdfAsync(stream);

            return stream.ToArray();
        }

        public static async Task<byte[]> CreateIncomeReport(RisIncomeReport model)
        {
            var report = new Reports.IncomeReport();
            report.FillReport(model);

            var stream = new MemoryStream();
            await report.ExportToPdfAsync(stream);

            return stream.ToArray();
        }
    }
}