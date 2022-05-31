using FiftyLab.PrivateSchool.Helpers;
using FiftyLab.PrivateSchool.Hubs;
using FiftyLab.PrivateSchool.Models;
using FiftyLab.PrivateSchool.Services;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using MongoDB.Entities;

namespace FiftyLab.PrivateSchool.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class ReportController : ControllerBase
    {
        private readonly ILogger<ReportController> _logger;
        private IBackgroundJobClient _backgroundJobs;
        private readonly IConfiguration _config;
        private readonly IIdentityService _identityService;

        public ReportController(
            ILogger<ReportController> logger,
            IIdentityService identityService,
            IBackgroundJobClient bgJobs,
            IConfiguration config)
        {
            _logger = logger;
            _config = config;
            _identityService = identityService;
            _backgroundJobs = bgJobs;
        }

        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Secretary}")]
        [HttpGet]
        [Route("invoice/{id}")]
        public async Task<IActionResult> CreateInvoiceReport(CancellationToken ct,
            [FromServices] ReportService reportService,
            string id)
        {
            var transaction = DB.Transaction();
            try
            {
                var invoice = await DB.Find<Invoice>(transaction.Session)
                    .MatchID(id)
                    .ExecuteSingleAsync(ct);
                var student = await DB.Find<Student>(transaction.Session)
                    .MatchID(invoice.Student.ID)
                    .ExecuteSingleAsync(ct);

                var schoolInfo = await DB.Find<SchoolInfo>(transaction.Session)
                    .ExecuteFirstAsync(ct);

                var model = new InvoiceReport()
                {
                    Student = student,
                    Invoice = invoice,
                    SchoolInfo = schoolInfo ?? new SchoolInfo()
                };
                var report = await reportService.CreateInvoiceReport(model);

                this.HttpContext.Response.ContentLength = report.LongLength;

                var response = new FileContentResult(report, "application/pdf")
                {
                    FileDownloadName =
                        $"Invoice_{DateTimeOffset.Now.ToString("yyyy-MM-dd_HH-mm-ss")}_{model.Student.Name}_{model.Invoice.Formation.Name}.pdf",
                    LastModified = DateTimeOffset.Now,
                    EnableRangeProcessing = false
                };
                await transaction.CommitAsync();

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                await transaction.AbortAsync();
                return BadRequest();
            }
        }

        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Secretary}")]
        [HttpGet]
        [Route("student/{id}/invoice")]
        public async Task<IActionResult> CreateLastStudentInvoice(CancellationToken ct,
            [FromServices] ReportService reportService,
            string id)
        {
            var transaction = DB.Transaction();
            try
            {
                var student = await DB.Find<Student>(transaction.Session)
                    .MatchID(id)
                    .ExecuteSingleAsync(ct);

                var invoice = await DB.Fluent<Invoice>(session: transaction.Session)
                    .Match(x => x.Eq(s => s.Student.ID, id) & x.Eq(z => z.Enabled, true))
                    .SortByDescending(x => x.CreatedOn)
                    .FirstAsync(cancellationToken: ct);

                var schoolInfo = await DB.Find<SchoolInfo>(transaction.Session)
                    .ExecuteFirstAsync(ct);

                var model = new InvoiceReport()
                {
                    Student = student,
                    Invoice = invoice,
                    SchoolInfo = schoolInfo ?? new SchoolInfo()
                };
                var report = await reportService.CreateInvoiceReport(model);

                this.HttpContext.Response.ContentLength = report.LongLength;

                var response = new FileContentResult(report, "application/pdf")
                {
                    FileDownloadName =
                        $"Invoice_{DateTimeOffset.Now.ToString("yyyy-MM-dd_HH-mm-ss")}_{model.Student.Name}_{model.Invoice.Formation.Name}.pdf",
                    LastModified = DateTimeOffset.Now,
                    EnableRangeProcessing = false
                };
                await transaction.CommitAsync();

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                await transaction.AbortAsync();
                return BadRequest();
            }
        }
    }
}