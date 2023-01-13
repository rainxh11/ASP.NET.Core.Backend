using Hangfire;
using Jetsons.JetPack;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Entities;
using QuranSchool.Helpers;
using QuranSchool.Hubs;
using QuranSchool.Models;
using QuranSchool.Models.Request;
using QuranSchool.Models.Response;
using QuranSchool.Services;
using Transaction = QuranSchool.Models.Transaction;

namespace QuranSchool.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class InvoiceController : Controller
{
    private readonly IConfiguration _config;
    private readonly IIdentityService _identityService;
    private readonly ILogger<InvoiceController> _logger;
    private readonly LoginInfoSaver _loginSaver;
    private readonly IHubContext<PrivateSchoolHub> _mailHub;
    private readonly AuthService _tokenService;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly WebsocketNotificationService _websocketNotificationService;

    public InvoiceController(
        ILogger<InvoiceController> logger,
        IIdentityService identityService,
        AuthService tokenService,
        IHubContext<PrivateSchoolHub> mailHub,
        IBackgroundJobClient bgJobs,
        WebsocketNotificationService nservice,
        LoginInfoSaver loginSaver,
        IConfiguration config)
    {
        _logger = logger;
        _config = config;
        _tokenService = tokenService;
        _identityService = identityService;
        _mailHub = mailHub;
        _backgroundJobs = bgJobs;
        _websocketNotificationService = nservice;
        _loginSaver = loginSaver;
    }
    //--------------------------------------------------------------------------------------------------------------//


    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}")]
    [HttpGet]
    [ResponseCache(Duration = 10,
        VaryByQueryKeys = new[] { "limit", "sort", "page", "desc", "type" })]
    [Route("")]
    public async Task<IActionResult> GetInvoices(InvoiceType? type, CancellationToken ct, int page = 1,
        int limit = -1, string? sort = "CreatedOn", bool desc = true)
    {
        var invoices = await DB.PagedSearch<Invoice>()
            .Match(f => type is null ? f.Empty : f.Eq(x => x.Type, type))
            .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
            .PageNumber(page)
            .PageSize(limit < 0 ? int.MaxValue : limit)
            .ExecuteAsync(ct).ConfigureAwait(false);

        return Ok(new PagedResultResponse<IEnumerable<Invoice>>(
            invoices.Results,
            invoices.TotalCount,
            invoices.PageCount,
            limit,
            page));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}")]
    [HttpGet]
    [ResponseCache(Duration = 10,
        VaryByQueryKeys = new[] { "limit", "sort", "page", "desc", "type", "search" })]
    [Route("search")]
    public async Task<IActionResult> SearchInvoices(InvoiceType? type, CancellationToken ct, string search = "",
        int page = 1, int limit = -1, string? sort = "CreatedOn", bool desc = true)
    {
        search = $"/{search}/ig";
        var searchQuery = DB.Fluent<Invoice>()
            .Match(invoice => (invoice.Regex(x => x.Student.ID, search) |
                               invoice.Regex(x => x.InvoiceID, search) |
                               invoice.Regex(x => x.Student.Name, search) |
                               invoice.Regex(x => x.Formation.Name, search) |
                               invoice.Regex(x => x.Formation.ID, search) |
                               invoice.Regex(x => x.Formation.Price, search) |
                               invoice.Regex(x => x.CreatedBy.Description, search) |
                               invoice.Regex(x => x.CreatedBy.Name, search) |
                               invoice.Regex(x => x.CreatedBy.UserName, search) |
                               invoice.Regex(x => x.CreatedBy.ID, search)) &
                              (type is null ? invoice.Empty : invoice.Eq(x => x.Type, type))
            );


        var invoices = await DB.PagedSearch<Invoice>()
            .WithFluent(searchQuery)
            .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
            .PageNumber(page)
            .PageSize(limit < 0 ? int.MaxValue : limit)
            .ExecuteAsync(ct).ConfigureAwait(false);

        return Ok(new PagedResultResponse<IEnumerable<Invoice>>(
            invoices.Results,
            invoices.TotalCount,
            invoices.PageCount,
            limit,
            page));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator},{AccountRole.Teacher}")]
    [HttpPost]
    [Route("")]
    public async Task<IActionResult> CreateInvoice([FromBody] IEnumerable<InvoiceModel> models,
        CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext).ConfigureAwait(false);

            var invoices = new List<Invoice>();
            foreach (var model in models)
            {
                var formation = await model.Formation.ToEntityAsync(transaction.Session, ct).ConfigureAwait(false);
                var student = await model.Student.ToEntityAsync(transaction.Session, ct).ConfigureAwait(false);
                var left = formation.Price - model.Discount - model.Paid;

                var transactions = Enumerable
                    .Empty<Transaction>()
                    .Append(new Transaction(TransactionType.Payment, model.Paid, account.ToBaseAccount()))
                    .Append(new Transaction(TransactionType.Debt, left, account.ToBaseAccount()))
                    .Append(new Transaction(TransactionType.Discount, model.Discount, account.ToBaseAccount()))
                    .Where(x => x.Amount > 0);

                var seqNumber = await DB.NextSequentialNumberAsync<Invoice>(ct).ConfigureAwait(false);

                var invoice = new Invoice
                {
                    InvoiceID = $"{seqNumber}",
                    CreatedBy = account.ToBaseAccount(),
                    StartDate = model.StartDate,
                    Formation = formation.ToBase(),
                    Student = student.ToBase(),
                    Enabled = true,
                    Parents = student.Parents,
                    Type = left > 0 ? InvoiceType.Debt : InvoiceType.Paid
                };

                await transactions.SaveAsync(transaction.Session, ct).ConfigureAwait(false);
                await invoice.InsertAsync(transaction.Session, ct).ConfigureAwait(false);
                await invoice.Transactions.AddAsync(transactions, transaction.Session, ct).ConfigureAwait(false);
                invoices.Add(invoice);
            }

            await transaction.CommitAsync().ConfigureAwait(false);

            return Ok(new ResultResponse<IEnumerable<Invoice>, string>(invoices,
                $" '{invoices.Count}' Invoice Created"));
        }
        catch (Exception ex)
        {
            if (transaction.Session.IsInTransaction)
                await transaction.AbortAsync().ConfigureAwait(false);
            _logger.LogError(ex.Message);
            return BadRequest();
        }
    }

    //--------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin}, {AccountRole.User}")]
    [HttpPost]
    [Route("{id}")]
    public async Task<IActionResult> PayInvoice(string id, [FromBody] PaymentModel model, CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);
            var invoice = await DB.Find<Invoice>(transaction.Session)
                .MatchID(id)
                .ExecuteSingleAsync(ct);

            model.Paid = model.Paid.ToDouble() > invoice.LeftUnpaid
                ? invoice.LeftUnpaid
                : model.Paid.ToDouble();

            model.Discount = model.Discount.ToDouble() > invoice.LeftUnpaid - model.Paid.ToDouble()
                ? invoice.LeftUnpaid - model.Paid.ToDouble()
                : model.Discount.ToDouble();

            var transactions = Enumerable
                .Empty<Transaction>()
                .Append(new Transaction(TransactionType.Payment, model.Paid.ToDouble(), account.ToBaseAccount()))
                .Append(new Transaction(TransactionType.Discount, model.Discount.ToDouble(),
                    account.ToBaseAccount()))
                .Where(x => x.Amount > 0);

            var leftUnpaid = invoice.LeftUnpaid - transactions
                .Where(x => x.Enabled && x.Type == TransactionType.Payment)
                .Sum(x => x.Amount);

            if (transactions.Count() == 0)
            {
                invoice.Type = invoice.LeftUnpaid > 0 ? InvoiceType.Debt : InvoiceType.Paid;
                await invoice.SaveAsync(transaction.Session, ct);
                await transaction.CommitAsync();

                return BadRequest(new MessageResponse<string>($"Invoice '{invoice.ID}' is already fully paid!"));
            }

            await transactions.SaveAsync(transaction.Session, ct);
            await invoice.Transactions.AddAsync(transactions, transaction.Session, ct);


            invoice.Type = leftUnpaid > 0 ? InvoiceType.Debt : InvoiceType.Paid;

            await invoice.SaveAsync(transaction.Session, ct);


            await transaction.CommitAsync();
            return Ok(new ResultResponse<Invoice, string>(invoice,
                @$"Updated Invoice '{invoice.ID}' with {model.Paid.ToDouble()} 
                                                                            payment amount & {model.Discount.ToDouble()} discount amount!"));
        }
        catch (Exception ex)
        {
            if (transaction.Session.IsInTransaction)
                await transaction.AbortAsync();
            _logger.LogError(ex.Message);
            return BadRequest();
        }
    }

    //--------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin}")]
    [HttpPatch]
    [Route("{id}/cancel")]
    public async Task<IActionResult> CancelInvoice(string id, CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);
            var alreadyCancelled = await DB.Find<Invoice>(transaction.Session)
                .Match(f => f.Eq(x => x.ID, id) & f.Eq(x => x.Enabled, false))
                .ExecuteAnyAsync(ct).ConfigureAwait(false);
            if (alreadyCancelled)
                return BadRequest(new MessageResponse<string>($"Invoice '{id}' is already cancelled!"));

            var update = await DB.UpdateAndGet<Invoice>(transaction.Session)
                .MatchID(id)
                .Modify(x => x.CancelledBy, account.ToBaseAccount())
                .Modify(x => x.CancelledOn, DateTime.Now)
                .Modify(x => x.Enabled, false)
                .ExecuteAsync(ct).ConfigureAwait(false);

            var invoiceTransactions = await DB.Find<Invoice>(transaction.Session)
                .MatchID(id)
                .ExecuteSingleAsync(ct);

            var cancelledTransactions = await DB.Update<Transaction>(transaction.Session)
                .Match(f => f.In(x => x.ID, invoiceTransactions.Transactions.Select(z => z.ID)) &
                            f.Eq(x => x.Enabled, true))
                .Modify(x => x.Enabled, false)
                .Modify(x => x.CancelledBy, account.ToBaseAccount())
                .Modify(x => x.CancelledOn, DateTime.Now)
                .ExecuteAsync(ct);

            await transaction.CommitAsync();

            return Ok(new ResultResponse<Invoice, string>(update,
                $"Invoice '{update.ID}', with '{cancelledTransactions.ModifiedCount}' Transactions cancelled!"));
        }
        catch (Exception ex)
        {
            if (transaction.Session.IsInTransaction)
                await transaction.AbortAsync()
                    .ConfigureAwait(false);
            ;
            _logger.LogError(ex.Message);
            return BadRequest();
        }
    }
}