using Jetsons.JetPack;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MongoDB.Entities;
using UATL.MailSystem.Common;
using UATL.MailSystem.Common.Models;
using UATL.MailSystem.Common.Response;

namespace UATL.Mail.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class OrderController : ControllerBase
{
    private readonly IIdentityService _identityService;
    private readonly ILogger<OrderController> _logger;
    private IConfiguration _config;
    private ITokenService _tokenService;

    public OrderController(
        ILogger<OrderController> logger,
        IIdentityService identityService,
        ITokenService tokenService,
        IConfiguration config)
    {
        _logger = logger;
        _config = config;
        _tokenService = tokenService;
        _identityService = identityService;
    }

    public static string FirstCharToUpper(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;
        return input.First().ToString().ToUpper() + input.Substring(1);
    }

    public static int Scale(int OldValue, int OldMin, int OldMax, int NewMin, int NewMax)
    {
        var OldRange = OldMax - OldMin;
        var NewRange = NewMax - NewMin;
        var NewValue = (OldValue - OldMin) * NewRange / OldRange + NewMin;

        return NewValue;
    }


    //---------------------------------------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.OrderOffice}")]
    [HttpGet]
    [Route("")]
    public async Task<IActionResult> GetMails(int page = 1, int limit = 10, string? sort = "SentOn", bool desc = true,
        bool grouped = false)
    {
        try
        {
            sort = FirstCharToUpper(sort.Replace("-", "").Trim());
            long totalCount = 0;

            if (grouped)
            {
                var skip = (page <= 1 ? 0 : page - 1) * limit;

                var sortDef = new SortDefinitionBuilder<MailModel>();
                var total = await DB.Fluent<MailModel>()
                    .Match(x => x.Type == MailType.External)
                    .Group(x => x.GroupId, z => new { GroupId = z.Key })
                    .Count()
                    .FirstOrDefaultAsync();
                totalCount = total == null ? 0 : total.Count;

                var count = await DB.Fluent<MailModel>()
                    .Match(x => x.Type == MailType.External)
                    .Sort(desc ? sortDef.Descending(sort) : sortDef.Ascending(sort))
                    .Skip(skip)
                    .Limit(limit)
                    .Group(x => x.GroupId, z => new { GroupId = z.Key })
                    //.ToListAsync();
                    .Count()
                    .FirstOrDefaultAsync();

                if (totalCount < limit)
                {
                    var resultCount = totalCount <= 0 ? 1 : totalCount.ToInt();
                    limit = Scale(limit, resultCount, limit, 0, limit / resultCount * limit);
                }
            }


            var mails = await DB.PagedSearch<MailModel>()
                .Match(x => x.Type == MailType.External)
                .ProjectExcluding(x => new { x.Body, x.Attachments })
                .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
                .PageNumber(page)
                .PageSize(limit < 0 ? int.MaxValue : limit)
                .ExecuteAsync();

            IEnumerable<object> results = !grouped
                ? mails.Results.Select(x =>
                {
                    x.Body = null;
                    x.Attachments = null;
                    return x;
                })
                : mails.Results
                    .GroupBy(x => x.GroupId ?? x.ID, x => x)
                    .Select(x =>
                    {
                        var mail = x.First();
                        mail.Recipients = x.Select(z => z.To).DefaultIfEmpty().Where(x => x != null);
                        return mail;
                    })
                    .Select(x =>
                    {
                        x.Body = null;
                        x.To = x.Recipients == null || x.Recipients.Count() <= 1 ? x.To : null;
                        x.Attachments = null;
                        return x;
                    });

            return Ok(new PagedResultResponse<IEnumerable<object>>(
                results,
                grouped ? totalCount : mails.TotalCount,
                mails.PageCount,
                limit,
                page));
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    //---------------------------------------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.OrderOffice}")]
    [HttpGet]
    [Route("search")]
    public async Task<IActionResult> SearchMails(int page = 1, int limit = 10, string? sort = "SentOn",
        bool desc = true, string search = "")
    {
        try
        {
            var searchRegex = $"/{search}/ig";
            var searchQuery = DB.Fluent<MailModel>()
                .Match(x => x.Type == MailType.External)
                .Match(acc => acc.Regex(x => x.Subject, searchRegex) |
                              acc.Regex(x => x.From.Name, searchRegex) |
                              acc.Regex(x => x.From.Description, searchRegex) |
                              acc.Regex(x => x.From.UserName, searchRegex) |
                              acc.Regex(x => x.To.UserName, searchRegex) |
                              acc.Regex(x => x.To.Name, searchRegex) |
                              acc.Regex(x => x.To.Description, searchRegex));

            var mails = await DB.PagedSearch<MailModel>()
                .WithFluent(searchQuery)
                .ProjectExcluding(x => new { x.Body, x.Attachments })
                .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
                .PageNumber(page)
                .PageSize(limit < 0 ? int.MaxValue : limit)
                .ExecuteAsync();

            return Ok(new PagedResultResponse<IEnumerable<MailModel>>(
                mails.Results.Select(x =>
                {
                    x.Body = null;
                    x.Attachments = null;
                    return x;
                }),
                mails.TotalCount,
                mails.PageCount,
                limit,
                page));
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    //------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.OrderOffice}")]
    [HttpPatch]
    [Route("{id}/approve")]
    public async Task<IActionResult> ApproveExternalMail(string id, CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);
            var update = await DB.Find<MailModel>(transaction.Session)
                .Match(x => x.Eq(mail => mail.ID, id))
                .ExecuteSingleAsync(ct);
            if (update == null)
                return NotFound();

            if (update.Approved)
                return BadRequest();

            update.Flags = update.Flags.Append(MailFlag.Approved).Append(MailFlag.Reviewed).Distinct().ToList();
            update.ApprovedBy = account.ToBaseAccount();

            await update.SaveAsync(transaction.Session, ct);
            await transaction.CommitAsync();

            return Ok();
        }
        catch (Exception ex)
        {
            if (transaction.Session.IsInTransaction)
                await transaction.AbortAsync();
            _logger.LogError(ex.Message);
            return BadRequest();
        }
    }

    //------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.OrderOffice}")]
    [HttpPatch]
    [Route("group/{groupId}/approve")]
    public async Task<IActionResult> ApproveExternalMails(string groupId, CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);
            var update = await DB.Find<MailModel>(transaction.Session)
                .Match(x => x.Eq(mail => mail.GroupId, groupId) & x.Eq(mail => mail.Type, MailType.External))
                .ExecuteAsync(ct);

            if (update == null)
                return NotFound();

            if (update.All(x => x.Approved))
                return BadRequest();

            update = update.Select(mail =>
            {
                mail.Flags = mail.Flags.Append(MailFlag.Approved).Append(MailFlag.Reviewed).Distinct().ToList();
                mail.ApprovedBy = account.ToBaseAccount();
                return mail;
            }).ToList();


            var bulkUpdate = DB.Update<MailModel>(transaction.Session);

            foreach (var mail in update) bulkUpdate.MatchID(mail.ID).ModifyWith(mail).AddToQueue();

            await bulkUpdate.ExecuteAsync(ct);

            await transaction.CommitAsync();

            return Ok();
        }
        catch (Exception ex)
        {
            if (transaction.Session.IsInTransaction)
                await transaction.AbortAsync();
            _logger.LogError(ex.Message);
            return BadRequest();
        }
    }

    //------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.OrderOffice}")]
    [HttpPatch]
    [Route("{id}/review")]
    public async Task<IActionResult> ReviewOrder(string id, CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);
            var update = await DB.Find<MailModel>(transaction.Session)
                .Match(x => x.Eq(mail => mail.ID, id))
                .ExecuteSingleAsync(ct);

            if (update == null)
                return NotFound();

            update.Flags = update.Flags.Append(MailFlag.Reviewed).Distinct().ToList();

            await update.SaveAsync(transaction.Session, ct);
            await transaction.CommitAsync();

            return Ok();
        }
        catch (Exception ex)
        {
            if (transaction.Session.IsInTransaction)
                await transaction.AbortAsync();
            _logger.LogError(ex.Message);
            return BadRequest();
        }
    }

    //------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.OrderOffice}")]
    [HttpPatch]
    [Route("group/{groupId}/review")]
    public async Task<IActionResult> ReviewOrders(string groupId, CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);
            var update = await DB.Find<MailModel>(transaction.Session)
                .Match(x => x.Eq(mail => mail.GroupId, groupId) & x.Eq(mail => mail.Type, MailType.External))
                .ExecuteAsync(ct);

            if (update == null)
                return NotFound();

            update = update.Select(mail =>
            {
                mail.Flags = mail.Flags.Append(MailFlag.Reviewed).Distinct().ToList();
                mail.ApprovedBy = account.ToBaseAccount();
                return mail;
            }).ToList();


            var bulkUpdate = DB.Update<MailModel>(transaction.Session);

            foreach (var mail in update) bulkUpdate.MatchID(mail.ID).ModifyWith(mail).AddToQueue();

            await bulkUpdate.ExecuteAsync(ct);
            await transaction.CommitAsync();

            return Ok();
        }
        catch (Exception ex)
        {
            if (transaction.Session.IsInTransaction)
                await transaction.AbortAsync();
            _logger.LogError(ex.Message);
            return BadRequest();
        }
    }
}