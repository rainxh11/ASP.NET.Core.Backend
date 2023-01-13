using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Entities;
using QuranSchool.Models;
using QuranSchool.Models.Response;
using QuranSchool.Services;
using Transaction = QuranSchool.Models.Transaction;

namespace QuranSchool.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class TransactionController : Controller
{
    private readonly IConfiguration _config;
    private readonly IIdentityService _identityService;
    private readonly ILogger<TransactionController> _logger;

    public TransactionController(
        ILogger<TransactionController> logger,
        IIdentityService identityService,
        AuthService tokenService,
        IConfiguration config)
    {
        _logger = logger;
        _config = config;
        _identityService = identityService;
    }

    //--------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin}")]
    [HttpPatch]
    [Route("{id}/cancel")]
    public async Task<IActionResult> CancelTransaction(string id, CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);
            var alreadyCancelled = await DB.Find<Transaction>(transaction.Session)
                .Match(f => f.Eq(x => x.ID, id) & f.Eq(x => x.Enabled, false))
                .ExecuteAnyAsync(ct);
            if (alreadyCancelled)
                return BadRequest(new MessageResponse<string>($"Transaction '{id}' is already cancelled!"));

            var update = await DB.UpdateAndGet<Transaction>()
                .MatchID(id)
                .Modify(x => x.CancelledBy, account.ToBaseAccount())
                .Modify(x => x.CancelledOn, DateTime.Now)
                .Modify(x => x.Enabled, false)
                .ExecuteAsync(ct);

            var invoices = await DB.Find<Invoice>(transaction.Session).ExecuteAsync(ct);
            var invoice = invoices.First(x => x.Transactions.Any(t => t.ID == id));

            invoice.Enabled = !invoice.Transactions.All(x => !x.Enabled);

            invoice.Type = invoice switch
            {
                var x when x.LeftUnpaid == x.PriceAfterDiscount => InvoiceType.Debt,
                var x when x.PriceAfterDiscount == 0 || x.LeftUnpaid == 0 => InvoiceType.Paid,
                var x when x.LeftUnpaid > 0 && x.LeftUnpaid < x.PriceAfterDiscount => InvoiceType.Debt,
                _ => invoice.Type
            };

            await invoice.SaveAsync(transaction.Session, ct);

            await transaction.CommitAsync();
            return Ok(new ResultResponse<Transaction, string>(update, $"Transaction '{update.ID}' cancelled!"));
        }
        catch (Exception ex)
        {
            if (transaction.Session.IsInTransaction) await transaction.AbortAsync();
            _logger.LogError(ex.Message);
            return BadRequest();
        }
    }
}