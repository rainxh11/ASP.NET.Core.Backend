using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Entities;
using UATL.Mail.Helpers;
using UATL.Mail.Models.Bindings;
using UATL.MailSystem.Common;
using UATL.MailSystem.Common.Models;
using UATL.MailSystem.Common.Models.Request;
using UATL.MailSystem.Common.Response;

namespace UATL.MailSystem.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class DraftController : ControllerBase
{
    private readonly IIdentityService _identityService;
    private readonly ILogger<DraftController> _logger;
    private IConfiguration _config;
    private ITokenService _tokenService;

    public DraftController(
        ILogger<DraftController> logger,
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

    //---------------------------------------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User}")]
    [HttpGet]
    [Route("")]
    public async Task<IActionResult> GetDrafts(int page = 1, int limit = 10, string? sort = "CreatedOn",
        bool desc = true)
    {
        try
        {
            sort = FirstCharToUpper(sort.Replace("-", "").Trim());

            var account = await _identityService.GetCurrentAccount(HttpContext);

            var drafts = await DB.PagedSearch<Draft>()
                .Match(draft => draft.From.ID == account.ID)
                .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
                .PageNumber(page)
                .PageSize(limit < 0 ? int.MaxValue : limit)
                .ExecuteAsync();

            return Ok(new PagedResultResponse<IEnumerable<Draft>>(drafts.Results, drafts.TotalCount, drafts.PageCount,
                limit, page));
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    //---------------------------------------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User}")]
    [HttpGet]
    [Route("{id}")]
    public async Task<IActionResult> GetDraft(string id)
    {
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);

            var draft = await DB.Find<Draft>().Match(x => x.From.ID == account.ID).OneAsync(id);
            if (draft == null) return NotFound();

            return Ok(draft);
            ;
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
    //---------------------------------------------------------------------------------------------------------------------------------------------//

    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User}")]
    [HttpGet]
    [Route("search")]
    public async Task<IActionResult> SearchDrafts(int page = 1, int limit = 10, string? sort = "CreatedOn",
        bool desc = true, string search = "")
    {
        try
        {
            sort = FirstCharToUpper(sort.Replace("-", "").Trim());

            var account = await _identityService.GetCurrentAccount(HttpContext);

            var pipeline = DB.FluentTextSearch<Draft>(Search.Full, search).Match(draft => draft.From.ID == account.ID);

            var drafts = await DB.PagedSearch<Draft>()
                .WithFluent(pipeline)
                .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
                .PageNumber(page)
                .PageSize(limit < 0 ? int.MaxValue : limit)
                .ExecuteAsync();

            return Ok(new PagedResultResponse<IEnumerable<Draft>>(drafts.Results, drafts.TotalCount, drafts.PageCount,
                limit, page));
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    //---------------------------------------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User}")]
    [HttpPost]
    [Route("")]
    public async Task<IActionResult> AddDraftWithFiles(
        [ModelBinder(BinderType = typeof(JsonModelBinder))]
        DraftRequest? value, IList<IFormFile> files,
        CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);
            var draft = value.Adapt<Draft>();
            draft.From = account.ToBaseAccount();

            await draft.InsertAsync(transaction.Session, ct);
            draft = await DB.Find<Draft>(transaction.Session).Match(x => x.From.ID == account.ID)
                .OneAsync(draft.ID, ct);
            if (draft == null) return NotFound();


            draft.Body = ModelHelper.ReplaceHref(draft.Body);
            var attachements = new List<Attachment>();
            foreach (var file in files)
            {
                var hash = HashHelper.CalculateFileFormMd5(file);
                var query = DB.Find<Attachment>(transaction.Session)
                    .Match(x => x.MD5 == hash && x.FileSize == file.Length);
                var exist = await query.ExecuteAnyAsync(ct);
                if (exist)
                {
                    var attachement = await query.ExecuteFirstAsync(ct);
                    draft.Attachments.Add(attachement);
                }
                else
                {
                    var attachement = new Attachment
                    {
                        MD5 = hash,
                        UploadedBy = account.ToBaseAccount(),
                        ContentType = file.ContentType,
                        Name = file.FileName
                    };
                    await attachement.SaveAsync(transaction.Session);
                    using (var stream = file.OpenReadStream())
                    {
                        await attachement.Data.UploadAsync(stream, cancellation: ct, session: transaction.Session);
                    }

                    var uploaded = await DB.Find<Attachment>(transaction.Session).OneAsync(attachement.ID);
                    draft.Attachments.Add(uploaded);
                }
            }

            await draft.SaveAsync(transaction.Session);

            await transaction.CommitAsync();
            var result = await DB.Find<Draft>().OneAsync(draft.ID);

            return Ok(new ResultResponse<Draft, string>(draft,
                $"Created Draft with {files.Count} attachements uploaded."));
        }
        catch (Exception ex)
        {
            if (transaction.Session.IsInTransaction)
                await transaction.AbortAsync();
            return BadRequest(ex.Message);
        }
    }

    //---------------------------------------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User}")]
    [HttpPost]
    [Route("{id}/attachement")]
    public async Task<IActionResult> UploadAttachments(string id, [FromForm] IFormFileCollection files,
        CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);
            var draft = await DB.Find<Draft>().Match(x => x.From.ID == account.ID).OneAsync(id);
            if (draft == null) return NotFound();

            var attachements = new List<Attachment>();
            foreach (var file in files)
            {
                var hash = HashHelper.CalculateFileFormMd5(file);
                var query = DB.Find<Attachment>().Match(x => x.MD5 == hash && x.FileSize == file.Length);
                var exist = await query.ExecuteAnyAsync();
                if (exist)
                {
                    var attachement = await query.ExecuteFirstAsync();
                    draft.Attachments.Add(attachement);
                }
                else
                {
                    var attachement = new Attachment
                    {
                        MD5 = hash,
                        UploadedBy = account.ToBaseAccount(),
                        ContentType = file.ContentType,
                        Name = file.FileName
                    };
                    await attachement.SaveAsync(transaction.Session);
                    using (var stream = file.OpenReadStream())
                    {
                        await attachement.Data.UploadAsync(stream, cancellation: ct, session: transaction.Session);
                    }

                    var uploaded = await DB.Find<Attachment>(transaction.Session).OneAsync(attachement.ID);
                    draft.Attachments.Add(uploaded);
                }
            }

            await draft.SaveAsync(transaction.Session);

            await transaction.CommitAsync();
            var result = await DB.Find<Draft>().OneAsync(draft.ID);

            return Ok(new ResultResponse<string, Draft>($"Uploaded {files.Count} files.", draft));
        }
        catch (Exception ex)
        {
            if (transaction.Session.IsInTransaction)
                await transaction.AbortAsync();
            return BadRequest(ex.Message);
        }
    }

    //---------------------------------------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User}")]
    [HttpPost]
    [Route("{id}/send")]
    public async Task<IActionResult> SendDraft(string id, [FromBody] SendDraftRequest sendModel, CancellationToken ct,
        bool keepDraft = false)
    {
        var transaction = DB.Transaction();
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);
            var draft = await DB.Find<Draft>(transaction.Session).Match(x => x.From.ID == account.ID).OneAsync(id);
            if (draft == null) return NotFound();

            var groupId = sendModel.Recipients.Count() == 1 ? ObjectId.GenerateNewId().ToString() : null;
            var mails = new List<MailModel>();

            foreach (var recipient in sendModel.Recipients.Where(x => x != account.ID))
            {
                var mail = draft.Adapt<MailModel>();
                var to = await DB.Find<Account>(transaction.Session).OneAsync(recipient);
                mail.To = to.ToBaseAccount();
                mail.Flags = sendModel.Flags;
                mails.Add(mail);
            }

            var result = await DB.InsertAsync(mails, transaction.Session, ct);

            if (!result.IsAcknowledged) return BadRequest();
            await transaction.CommitAsync(ct);

            return Ok(new MessageResponse<string>($"Sent {result.InsertedCount} Mail."));
            ;
        }
        catch (Exception ex)
        {
            if (transaction.Session.IsInTransaction)
                await transaction.AbortAsync();
            return BadRequest(ex.Message);
        }
    }

    //---------------------------------------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User}")]
    [HttpPatch]
    [Route("{id}")]
    public async Task<IActionResult> UpdateDraft(string id, [FromBody] DraftRequest draftRequest)
    {
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);
            var draft = await DB.Find<Draft>().OneAsync(id);
            if (draft == null) return NotFound();

            draft.Subject = draftRequest.Subject;
            draft.Body = ModelHelper.ReplaceHref(draftRequest.Body);

            await draft.SaveAsync();

            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    //---------------------------------------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User}")]
    [HttpDelete]
    [Route("{id}")]
    public async Task<IActionResult> DeleteDraft(string id)
    {
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);

            var result = await DB.DeleteAsync<Draft>(x => x.From.ID == account.ID && x.ID == id);

            if (!result.IsAcknowledged)
                return BadRequest();
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    //---------------------------------------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User}")]
    [HttpDelete]
    [Route("")]
    public async Task<IActionResult> DeleteManyDraft([FromBody] IEnumerable<string> ids)
    {
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);

            var result = await DB.DeleteAsync<Draft>(x => x.From.ID == account.ID && ids.Contains(x.ID));

            if (!result.IsAcknowledged)
                return BadRequest();
            return Ok($"Deleted {result.DeletedCount} Items.");
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
    //---------------------------------------------------------------------------------------------------------------------------------------------//
}