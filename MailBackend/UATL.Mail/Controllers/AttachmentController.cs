using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using MongoDB.Entities;
using UATL.Mail.Hubs;
using UATL.MailSystem.Common;
using UATL.MailSystem.Common.Models;
using UATL.MailSystem.Common.Response;

namespace UATL.Mail.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class AttachmentController : ControllerBase
{
    private readonly ILogger<AttachmentController> _logger;
    private readonly IHubContext<MailHub> _mailHub;
    private IConfiguration _config;
    private readonly IIdentityService _identityService;
    private ITokenService _tokenService;

    public AttachmentController(
        ILogger<AttachmentController> logger,
        IIdentityService identityService,
        ITokenService tokenService,
        IHubContext<MailHub> mailHub,
        IConfiguration config)
    {
        _logger = logger;
        _config = config;
        _tokenService = tokenService;
        _identityService = identityService;
        _mailHub = mailHub;
    }

    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User}")]
    [HttpGet]
    [Route("{id}")]
    public async Task<IActionResult> DownloadAttachment(string id, CancellationToken ct)
    {
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);
            if (account == null)
            {
                HttpContext.Response.Headers.Add("WWW-Authenticate", "Basic realm=\"\"");
                return Unauthorized();
            }


            var allowed = await HaveAccessToFile(id, account, ct, true);

            if (!allowed)
                return Unauthorized();

            var attachment = await DB.Find<Attachment>().OneAsync(id, ct);
            if (attachment == null)
                return NotFound();


            var stream = new MemoryStream();
            await attachment.Data.DownloadAsync(stream, cancellation: ct).ConfigureAwait(false);
            stream.Position = 0;
            HttpContext.Response.ContentLength = attachment.FileSize;

            var contentType = string.IsNullOrEmpty(attachment.ContentType)
                ? "application/octet-stream"
                : attachment.ContentType;

            return File(stream, contentType, attachment.Name, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return BadRequest();
        }
    }
    //----------------------------------------------------------------------------------------------------------///
    [Authorize(Roles = $"{AccountRole.Admin}")]
    [HttpGet]
    [Route("")]
    public async Task<IActionResult> GetAttachments(CancellationToken ct, int page = 1, int limit = 10, string? sort = "CreatedOn", bool desc = true)
    {
        var transaction = DB.Transaction();
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);
            if (account == null)
            {
                HttpContext.Response.Headers.Add("WWW-Authenticate", "Basic realm=\"\"");
                return Unauthorized();
            }

            var files = await DB.PagedSearch<Attachment>(transaction.Session)
                .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
                .PageNumber(page)
                .PageSize(limit)
                .ExecuteAsync(ct);

            return Ok(new PagedResultResponse<IEnumerable<Attachment>>(
                files.Results,
                files.TotalCount,
                files.PageCount,
                limit,
                page));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return BadRequest();
        }
    }
    [Authorize(Roles = $"{AccountRole.Admin}")]
    [HttpGet]
    [Route("search")]
    public async Task<IActionResult> SearchAttachments(CancellationToken ct, string? search = "", int page = 1, int limit = 10, string? sort = "CreatedOn", bool desc = true)
    {
        var transaction = DB.Transaction();
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);
            if (account == null)
            {
                HttpContext.Response.Headers.Add("WWW-Authenticate", "Basic realm=\"\"");
                return Unauthorized();
            }

            var searchQuery = DB.Fluent<Attachment>(session: transaction.Session)
                .Match(att => att.Regex(x => x.Name, search) |
                              att.Regex(x => x.UploadedBy.UserName, search) |
                              att.Regex(x => x.UploadedBy.Name, search) |
                              att.Regex(x => x.UploadedBy.Description, search) |
                              att.Regex(x => x.ID, search) |
                              att.Regex(x => x.MD5, search) |
                              att.Regex(x => x.ContentType, search));

            var files = await DB.PagedSearch<Attachment>(transaction.Session)
                .WithFluent(searchQuery)
                .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
                .PageNumber(page)
                .PageSize(limit)
                .ExecuteAsync(ct);

            return Ok(new PagedResultResponse<IEnumerable<Attachment>>(
                files.Results,
                files.TotalCount,
                files.PageCount,
                limit,
                page));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return BadRequest();
        }
    }

    ///---------------------------------------------------------------------------------------------------------///
    private async Task<bool> HaveAccessToFile(string id, Account account, CancellationToken ct, bool allowAdminFullAccess = false)
    {
        if (account.Role == AccountType.OrderOffice)
            return false;
        if (allowAdminFullAccess && account.Role == AccountType.Admin)
            return true;

        var transaction = DB.Transaction();
        try
        {
            var drafts = DB.Find<Draft>(transaction.Session)
                .ManyAsync(
                    filter => filter.ElemMatch(x => x.Attachments, x => x.ID == id) &
                              filter.Eq(x => x.From.ID, account.ID), ct);

            var mails = DB.Find<MailModel>(transaction.Session)
                .ManyAsync(
                    filter => filter.ElemMatch(x => x.Attachments, x => x.ID == id) &
                              (filter.Eq(x => x.From.ID, account.ID) | filter.Eq(x => x.To.ID, account.ID)), ct);

            var attachments = DB.Find<Attachment>(transaction.Session)
                .ManyAsync(filter => filter.Eq(x => x.UploadedBy.ID, account.ID), ct);

            await Task.WhenAll(drafts, mails, attachments);
            await transaction.CommitAsync(ct);

            return drafts.Result?.Count > 0 || mails.Result?.Count > 0 || attachments.Result?.Count > 0;
        }
        catch (Exception ex)
        {
            if (transaction.Session.IsInTransaction)
                await transaction.AbortAsync();
            _logger.LogError(ex.Message);
            return false;
        }
    }
}