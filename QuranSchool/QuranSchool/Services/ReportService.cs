using Hangfire;
using Jetsons.JetPack;
using QuranSchool.Models;
using QuranSchool.Report;

namespace QuranSchool.Services;

public class ReportService
{
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly IConfiguration _config;

    public ReportService(IConfiguration config, IBackgroundJobClient backgroundJobs)
    {
        _config = config;
        _backgroundJobs = backgroundJobs;
    }

    public async Task<byte[]> CreatePasswordReport(StudentPasswordReport model)
    {
        var report = new StudentPasswordsDocument();
        report.FillReport(model);

        var stream = new MemoryStream();
        await report.ExportToPdfAsync(stream);

        return stream.ToArray();
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