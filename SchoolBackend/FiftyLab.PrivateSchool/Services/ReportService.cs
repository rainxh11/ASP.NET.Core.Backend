using FiftyLab.PrivateSchool.Models;
using FiftyLab.PrivateSchool.Report;
using Hangfire;
using Jetsons.JetPack;

namespace FiftyLab.PrivateSchool.Services
{
    public class ReportService
    {
        private IConfiguration _config;
        private IBackgroundJobClient _backgroundJobs;

        public ReportService(IConfiguration config, IBackgroundJobClient backgroundJobs)
        {
            _config = config;
            _backgroundJobs = backgroundJobs;
        }

        public async Task<byte[]> CreateInvoiceReport(InvoiceReport model)
        {
            var report = new InvoiceDocument();
            report.FillReport(model);

            var stream = new MemoryStream();
            await report.ExportToPdfAsync(stream);

            if (_config["Report:SaveToFile"].ToBool())
            {
                var savePath = _config["Report:Destination"];

                if (!Directory.Exists(savePath)) Directory.CreateDirectory(savePath);

                _backgroundJobs.Enqueue(() => File.WriteAllBytes(
                    savePath +
                    $@"\{DateTime.Now.ToFileTime()}_{model.Student.Name}_{model.Invoice.Formation.Name}.pdf",
                    stream.ToArray()));
            }


            return stream.ToArray();
        }
    }
}