using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MongoDB.Entities;
using QuranSchool.Models;
using QuranSchool.Services;

namespace QuranSchool.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class ReportController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IIdentityService _identityService;
    private readonly ILogger<ReportController> _logger;
    private IBackgroundJobClient _backgroundJobs;

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

    [Authorize]
    [HttpGet]
    [Route("group/{id}/password")]
    public async Task<IActionResult> CreateGroupStudentsPasswordReport(CancellationToken ct,
        [FromServices] ReportService reportService,
        string id)
    {
        var group = await DB.Find<Group>().OneAsync(id, ct);
        var groupStudents = group.Students.Select(x => x.ID);
        var students = await DB.Find<Student>().Match(f => f.In(x => x.ID, groupStudents)).ExecuteAsync(ct);
        var accounts = await DB.Find<Account>().Match(f => f.In(x => x.PersonalId, groupStudents)).ExecuteAsync(ct);

        var intersection = students.Select(x => x.ID).Intersect(accounts.Select(x => x.PersonalId));

        var models = students
            .Where(x => intersection.Contains(x.ID))
            .Select(student =>
                new StudentPassword
                {
                    Account = accounts.First(x => x.PersonalId == student.ID),
                    Student = student
                })
            .ToList();

        var model = new StudentPasswordReport
        {
            Passwords = models
        };
        var report = await reportService.CreatePasswordReport(model);

        HttpContext.Response.ContentLength = report.LongLength;

        var response = new FileContentResult(report, "application/pdf")
        {
            FileDownloadName =
                $"Passwords_{DateTime.Now:yyyy-MM-dd_HH-mm}_{group.Name}_{group.Teacher.Name}.pdf",
            LastModified = DateTime.Now,
            EnableRangeProcessing = false
        };

        return response;
    }

    [Authorize]
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

            var model = new InvoiceReport
            {
                Student = student,
                Invoice = invoice,
                SchoolInfo = schoolInfo ?? new SchoolInfo()
            };
            var report = await reportService.CreateInvoiceReport(model);

            HttpContext.Response.ContentLength = report.LongLength;

            var response = new FileContentResult(report, "application/pdf")
            {
                FileDownloadName =
                    $"Invoice_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{model.Student.Name}_{model.Invoice.Formation.Name}.pdf",
                LastModified = DateTime.Now,
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

    [Authorize]
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
                .FirstAsync(ct);

            var schoolInfo = await DB.Find<SchoolInfo>(transaction.Session)
                .ExecuteFirstAsync(ct);

            var model = new InvoiceReport
            {
                Student = student,
                Invoice = invoice,
                SchoolInfo = schoolInfo ?? new SchoolInfo()
            };
            var report = await reportService.CreateInvoiceReport(model);

            HttpContext.Response.ContentLength = report.LongLength;

            var response = new FileContentResult(report, "application/pdf")
            {
                FileDownloadName =
                    $"Invoice_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{model.Student.Name}_{model.Invoice.Formation.Name}.pdf",
                LastModified = DateTime.Now,
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