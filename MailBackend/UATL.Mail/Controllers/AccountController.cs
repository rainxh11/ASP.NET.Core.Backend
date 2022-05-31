using Akavache;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Entities;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using UATL.Mail.Helpers;
using UATL.Mail.Hubs;
using UATL.Mail.Models;
using UATL.Mail.Models.Models.Response;
using UATL.Mail.Services;
using UATL.MailSystem.Common;
using UATL.MailSystem.Common.Models;
using UATL.MailSystem.Common.Request;
using UATL.MailSystem.Common.Response;

namespace UATL.MailSystem.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class AccountController : ControllerBase
{
    private readonly ILogger<AccountController> _logger;
    private readonly IHubContext<MailHub> _mailHub;
    private IBackgroundJobClient _backgroundJobs;
    private readonly IConfiguration _config;
    private readonly IIdentityService _identityService;
    private readonly LoginInfoSaver _loginSaver;
    private NotificationService _notificationSerivce;
    private readonly ITokenService _tokenService;

    public AccountController(
        ILogger<AccountController> logger,
        IIdentityService identityService,
        ITokenService tokenService,
        IHubContext<MailHub> mailHub,
        IBackgroundJobClient bgJobs,
        NotificationService nservice,
        LoginInfoSaver loginSaver,
        IConfiguration config)
    {
        _logger = logger;
        _config = config;
        _tokenService = tokenService;
        _identityService = identityService;
        _mailHub = mailHub;
        _backgroundJobs = bgJobs;
        _notificationSerivce = nservice;
        _loginSaver = loginSaver;
    }

    //--------------------------------------------------------------------------------------------------------------//
    [AllowAnonymous]
    [HttpPost]
    [Route("signup")]
    public async Task<IActionResult> Signup([FromBody] SignupModel model)
    {
        try
        {
            if (await DB.Find<Account>().Match(x => x.UserName == model.UserName).ExecuteAnyAsync())
                return BadRequest($"Account with username '{model.UserName}' already exists!");

            var account = new Account(model.Name, model.UserName, model.Password, model.Description);
            await account.InsertAsync();
            account = await DB.Find<Account>().MatchID(account.ID).ExecuteSingleAsync();

            return Ok(new ResultResponse<Account, string>(account, "Account Created Successfully!"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);

            return BadRequest(ex.Message);
        }
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.OrderOffice}")]
    [HttpPost]
    [Route("{id}/avatar")]
    public async Task<IActionResult> AddAvatar(string id, [FromForm] IFormFile file, CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            if (!file.ContentType.Contains("image"))
                return BadRequest(new MessageResponse<string>(
                    $"Content of type: '{file.ContentType}' not allowed! Only image type is allowed!"));

            var account = await _identityService.GetCurrentAccount(HttpContext);

            if (account.ID != id && account.Role != AccountType.Admin)
                return Unauthorized(
                    new MessageResponse<string>(
                        "Only the owner of the account or an Admin account can modify Avatar!"));

            if (account.ID != id)
                account = await DB.Find<Account>(transaction.Session).OneAsync(id, ct);

            if (account == null)
                return NotFound();

            if (account.Avatar != null)
                await account.Avatar.DeleteAsync(transaction.Session, ct);
            var avatar = new Avatar(account);

            await avatar.SaveAsync(transaction.Session, ct);
            using (var stream = await ImageHelper.EncodeWebp(file, ct))
            {
                await avatar.Data.UploadAsync(stream, cancellation: ct, session: transaction.Session);
            }

            var uploaded = await DB.Find<Avatar>(transaction.Session).OneAsync(avatar.ID);
            account.Avatar = uploaded;
            await account.SaveAsync(transaction.Session, ct);
            await transaction.CommitAsync(ct);

            return Ok(new ResultResponse<Account, string>(account, "Avatar updated!"));
        }
        catch (Exception ex)
        {
            if (transaction.Session.IsInTransaction)
                await transaction.AbortAsync();
            _logger.LogError(ex.Message);

            return BadRequest(ex.Message);
        }
    }



    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin}")]
    [HttpDelete]
    [Route("{id}/avatar")]
    public async Task<IActionResult> DeleteAvatar(string id, CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            var account = await DB.Find<Account>(transaction.Session).OneAsync(id, ct);
            if (account == null || account.Avatar == null)
                return NotFound();

            account.Avatar = null;
            await account.SaveAsync(transaction.Session, ct);
            await transaction.CommitAsync(ct);
            var result = await DB.DeleteAsync<Avatar>(x => x.Account.ID == id, transaction.Session, ct);

            if (result.IsAcknowledged)
                return Ok("Avatar Deleted.");
            return BadRequest();
        }
        catch (Exception ex)
        {
            await transaction.AbortAsync();
            _logger.LogError(ex.Message);
            return BadRequest();
        }
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin}")]
    [HttpPost]
    [Route("")]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountModel model)
    {
        try
        {
            if (await DB.Find<Account>().Match(x => x.UserName == model.UserName).ExecuteAnyAsync())
                return BadRequest($"Account with username '{model.UserName}' already exists!");
            var currentAccount = await _identityService.GetCurrentAccount(HttpContext);


            var account = new Account(model.Name, model.UserName, model.Password, model.Description);
            account.Role = model.Role;
            account.CreatedBy = currentAccount.ToBaseAccount();

            await account.InsertAsync();
            account = await DB.Find<Account>().MatchID(account.ID).ExecuteSingleAsync();

            return Ok(new ResultResponse<Account, string>(account, "Account Created Successfully!"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);

            return BadRequest(ex.Message);
        }
    }

    //--------------------------------------------------------------------------------------------------------------//
    [AllowAnonymous]
    [HttpGet]
    [Route("")]
    public async Task<IActionResult> GetAccounts(int page = 1, int limit = 10, string? sort = "CreatedOn",
        bool desc = true)
    {
        try
        {
            var accounts = await DB.PagedSearch<Account>()
                .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
                .PageNumber(page)
                .PageSize(limit < 0 ? int.MaxValue : limit)
                .ExecuteAsync();


            return Ok(new PagedResultResponse<IEnumerable<Account>>(
                accounts.Results,
                accounts.TotalCount,
                accounts.PageCount,
                limit,
                page));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);

            return BadRequest(ex.Message);
        }
    }

    [AllowAnonymous]
    [HttpGet]
    [Route("search")]
    public async Task<IActionResult> SearchAccounts(string search = "", int page = 1, int limit = 10, string? sort = "CreatedOn",
        bool desc = true)
    {
        try
        {
            var searchRegex = $"/{search}/ig";
            var searchQuery = DB.Fluent<Account>()
                .Match(acc => acc.Regex(x => x.UserName, searchRegex) |
                              acc.Regex(x => x.Name, searchRegex) |
                              acc.Regex(x => x.Description, searchRegex) |
                              acc.Regex(x => x.CreatedBy.Name, searchRegex) |
                              acc.Regex(x => x.CreatedBy.UserName, searchRegex) |
                              acc.Regex(x => x.CreatedBy.Description, searchRegex));

            var accounts = await DB.PagedSearch<Account>()
                .WithFluent(searchQuery)
                .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
                .PageNumber(page)
                .PageSize(limit < 0 ? int.MaxValue : limit)
                .ExecuteAsync();


            return Ok(new PagedResultResponse<IEnumerable<Account>>(
                accounts.Results,
                accounts.TotalCount,
                accounts.PageCount,
                limit,
                page));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);

            return BadRequest(ex.Message);
        }
    }
    //--------------------------------------------------------------------------------------------------------------//
    [AllowAnonymous]
    [HttpPatch]
    [Route("{id}/changepassword")]
    public async Task<IActionResult> ChangePassword(string id, [FromBody] ChangePasswordModel model)
    {
        var account = await Authenticate(x => x.ID == id);

        if (account != null)
        {
            if (!account.Enabled)
                return BadRequest($"Account '{account.UserName}' is disabled, please contact system administrator!");
            if (!account.Replace(model.OldPassword, model.NewPassword))
                return BadRequest($"Old password of Account '{account.UserName}' is incorrect!");

            await account.SaveAsync();

            return Ok(new ResultResponse<Account, string>(account, "Password updated successfully!"));
        }

        return NotFound("User not found!");
    }

    //--------------------------------------------------------------------------------------------------------------//
    private bool IPIsLocal(IPAddress host)
    {
        try
        {
            IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());
            if (IPAddress.IsLoopback(host)) return true;
            return localIPs.Any(ip => host.Equals(ip));
        }
        catch { }
        return false;
    }
    [AllowAnonymous]
    [HttpPost]
    [Route("login")]
    public async Task<IActionResult> Login([FromBody] LoginModel model, CancellationToken ct)
    {
        var account = await Authenticate(x => x.UserName == model.UserName);
        var transaction = DB.Transaction();
        try
        {
            if (account != null)
            {
                if (!VerifyPassword(account, model.Password))
                    return BadRequest($"Password of Account '{account.UserName}' is incorrect!");
                if (!account.Enabled)
                    return BadRequest(
                        $"Account '{account.UserName}' is disabled, please contact system administrator!");

                if (account.UserName == "admin" && model.Password == "adminadmin")
                {
                    if (HttpContext.Connection.RemoteIpAddress.AddressFamily != AddressFamily.InterNetwork &&
                        HttpContext.Connection.RemoteIpAddress.AddressFamily != AddressFamily.InterNetworkV6)
                        return Unauthorized(new
                        { Message = "Default Administrator Account can only be accessed from a Local IP!" });
                }


                account.LastLogin = DateTime.Now;
                await account.SaveAsync(transaction.Session, ct);
                await transaction.CommitAsync();

                var token = await _tokenService.BuildToken(_config, account);
                //_backgroundJobs.Enqueue(() => _notificationSerivce.SendEmail("rainxh11@gmail.com", "UATL MAIL Test",$"Account {account.Name} Logged in.", ct));

                await _loginSaver.AddLogin(HttpContext, account).ConfigureAwait(false);

                return Ok(new ResultResponse<Account, string>(account, token));
            }

            return NotFound("User not found!");
        }
        catch (Exception ex)
        {
            if (transaction.Session.IsInTransaction)
                await transaction.AbortAsync();
            _logger.LogError(ex.Message);
            return BadRequest();
        }
    }

    //--------------------------------------------------------------------------------------------------------------//
    [HttpGet]
    [Route("login")]
    public async Task<IActionResult> LoginBasicAuth(CancellationToken ct)
    {
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);
            if (account == null)
                return Unauthorized();

            if (!account.Enabled)
                return BadRequest($"Account '{account.UserName}' is disabled, please contact system administrator!");
            account.LastLogin = DateTime.Now;
            await account.SaveAsync(cancellation: ct);

            var token = await _tokenService.BuildToken(_config, account);

            return Ok(new ResultResponse<Account, string>(account, token));
        }
        catch
        {
            return BadRequest();
        }
    }
    //--------------------------------------------------------------------------------------------------------------//

    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.OrderOffice}")]
    [HttpGet]
    [Route("me")]
    public async Task<IActionResult> GetCurrentAccount()
    {
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);
            if (account is null)
                return NotFound("Token Invalid or Account not found.");
            return Ok(new ResultResponse<Account, string>(account, "Success"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);

            return BadRequest(ex.Message);
        }
    }
    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.OrderOffice}")]
    [HttpGet]
    [Route("{id}/avatar")]
    public async Task<IActionResult> GetAvatar([FromServices] IWebHostEnvironment webHost, string id, CancellationToken ct)
    {
        try
        {
            var account = await DB.Find<Account>().MatchID(id).ExecuteSingleAsync(ct);
            if (account.Avatar == null)
            {
                var origin = webHost.IsProduction() ? "/" : HttpContext.Request.Headers.Referer.First();
                return Redirect(origin + "images/avatars/generic.jpg");
            }

            var avatar = await DB.Find<Avatar>().MatchID(account.Avatar.ID).ExecuteFirstAsync(ct);
            if (avatar == null)
                return Redirect(HttpContext.Request.Headers.Referer.First() + "images/avatars/generic.jpg");

            var stream = new MemoryStream();
            await avatar.Data.DownloadAsync(stream, cancellation: ct).ConfigureAwait(false);
            stream.Position = 0;
            return File(stream, "image/webp");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return BadRequest();
        }
    }
    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.OrderOffice}")]
    [HttpGet]
    [Route("me/avatar")]
    public async Task<IActionResult> GetCurrentAvatar(CancellationToken ct)
    {
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);
            if (account is null)
                return NotFound("Token Invalid or Account not found.");

            var avatar = await DB.Find<Avatar>().MatchID(account.Avatar.ID).ExecuteFirstAsync(ct);
            if (avatar == null)
                return NotFound();

            var stream = new MemoryStream();
            await avatar.Data.DownloadAsync(stream, cancellation: ct).ConfigureAwait(false);
            stream.Position = 0;
            return File(stream, "image/webp");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return BadRequest();
        }
    }
    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.OrderOffice}")]
    [HttpPost]
    [Route("me/avatar")]
    public async Task<IActionResult> UpdateCurrentAvatar([FromForm] IFormFile file, CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            if (!file.ContentType.Contains("image"))
                return BadRequest(new MessageResponse<string>(
                    $"Content of type: '{file.ContentType}' not allowed! Only image type is allowed!"));

            var account = await _identityService.GetCurrentAccount(HttpContext);

            if (account == null)
                return NotFound();

            if (account.Avatar != null)
                await account.Avatar.DeleteAsync(transaction.Session, ct);
            var avatar = new Avatar(account);

            await avatar.SaveAsync(transaction.Session, ct);
            await using (var stream = await ImageHelper.EncodeWebp(file, ct))
            {
                await avatar.Data.UploadAsync(stream, cancellation: ct, session: transaction.Session);
            }

            var uploaded = await DB.Find<Avatar>(transaction.Session).OneAsync(avatar.ID);
            account.Avatar = uploaded;
            await account.SaveAsync(transaction.Session, ct);
            await transaction.CommitAsync(ct);

            return Ok(new ResultResponse<Account, string>(account, "Avatar updated!"));
        }
        catch (Exception ex)
        {
            if (transaction.Session.IsInTransaction)
                await transaction.AbortAsync();
            _logger.LogError(ex.Message);

            return BadRequest(ex.Message);
        }
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.OrderOffice}")]
    [HttpDelete]
    [Route("me/avatar")]
    public async Task<IActionResult> DeleteCurrentAvatar(CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);
            if (account is null)
                return NotFound("Token Invalid or Account not found.");

            if (account == null || account.Avatar == null)
                return NotFound();

            account.Avatar = null;
            await account.SaveAsync(transaction.Session, ct);
            await transaction.CommitAsync(ct);
            var result = await DB.DeleteAsync<Avatar>(x => x.Account.ID == account.ID, transaction.Session, ct);

            if (result.IsAcknowledged)
                return Ok("Avatar Deleted.");
            return BadRequest();
        }
        catch (Exception ex)
        {
            if (transaction.Session.IsInTransaction)
                await transaction.AbortAsync();
            _logger.LogError(ex.Message);
            return BadRequest();
        }
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin}")]
    [HttpPatch]
    [Route("{id}")]
    public async Task<IActionResult> UpdateAccount(string id, [FromBody] AccountUpdateModel model, CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            var account = await DB.Find<Account>(transaction.Session).OneAsync(id, ct);
            if (account == null) return NotFound();

            account.Enabled = model.Enabled ?? account.Enabled;
            account.Name = model.Name ?? account.Name;
            account.Role = model.Role ?? account.Role;
            account.Description = model.Description ?? account.Description;
            account.ModifiedOn = DateTime.Now;

            var update = await DB.Update<Account>(transaction.Session).MatchID(account.ID).ModifyWith(account)
                .ExecuteAsync(ct);
            await transaction.CommitAsync(ct);

            if (!update.IsAcknowledged) return BadRequest();
            var updated = await DB.Find<Account>().OneAsync(id, ct);

            return Ok(new ResultResponse<Account, string>(updated, "Account Updated"));
        }
        catch (Exception ex)
        {
            if (transaction.Session.IsInTransaction)
                await transaction.AbortAsync();
            _logger.LogError(ex.Message);

            return BadRequest(ex.Message);
        }
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin}")]
    [HttpPatch]
    [Route("")]
    public async Task<IActionResult> UpdateAccounts([FromBody] List<AccountUpdateModel> models)
    {
        var transaction = DB.Transaction();
        try
        {
            var accounts = new List<Account>();

            foreach (var model in models)
            {
                var update = DB.UpdateAndGet<Account>(transaction.Session)
                    .Match(x => x.ID == model.Id);

                if (model.Name != null) update.Modify(x => x.Name, model.Name);
                if (model.Role != null) update.Modify(x => x.Role, model.Role);
                if (model.Enabled != null) update.Modify(x => x.Enabled, model.Enabled);

                var account = await update.ExecuteAsync();
                if (account == null) return NotFound($"Account with Id: {model.Id} not found");
                accounts.Add(account);
            }

            await transaction.CommitAsync();
            return Ok(new ResultResponse<List<Account>, string>(accounts, "Accounts Updated."));
        }
        catch (Exception ex)
        {
            if (transaction.Session.IsInTransaction)
                await transaction.AbortAsync();
            _logger.LogError(ex.Message);

            return BadRequest(ex.Message);
        }
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin}")]
    [HttpDelete]
    [Route("{id}")]
    public async Task<IActionResult> DeleteAccount(string id)
    {
        try
        {
            var delete = await DB.DeleteAsync<Account>(id);
            if (delete.IsAcknowledged)
                return Ok("Account deleted.");
            return NotFound();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin}")]
    [HttpDelete]
    [Route("")]
    public async Task<IActionResult> DeleteAccounts([FromBody] List<string> ids)
    {
        try
        {
            var delete = await DB.DeleteAsync<Account>(ids);
            if (!delete.IsAcknowledged) return BadRequest();
            return Ok($"{delete.DeletedCount} Accounts Deleted.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);

            return BadRequest(ex.Message);
        }
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.OrderOffice}")]
    [HttpGet]
    [Route("recipients")]
    public async Task<IActionResult> GetRecipients(string? search = "")
    {
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);
            var query = new List<Account>();
            search = search == "null" ? "" : search;

            if (string.IsNullOrEmpty(search))
                query = await DB.Find<Account>()
                    .Match(x => x.Ne(account => account.ID, account.ID) &
                                x.Ne(account => account.Role, AccountType.OrderOffice))
                    .ExecuteAsync();
            else
                query = await DB.Find<Account>()
                    .Match(x => x.Ne(account => account.ID, account.ID) &
                                x.Ne(account => account.Role, AccountType.OrderOffice))
                    .ManyAsync(f => f.Regex(x => x.Name, new BsonRegularExpression($"/{search}/i")) |
                                    f.Regex(x => x.UserName, new BsonRegularExpression($"/{search}/i")) |
                                    f.Regex(x => x.ID, new BsonRegularExpression($"/{search}/i")) |
                                    f.Regex(x => x.Description, new BsonRegularExpression($"/{search}/i"))
                    );

            var recipients = query.Select(x => new Recipient(x));

            return Ok(new ResultResponse<IEnumerable<Recipient>, int>(recipients, recipients.Count()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return BadRequest();
        }
    }
    //--------------------------------------------------------------------------------------------------------------//
    //--------------------------------------------------------------------------------------------------------------//
    //--------------------------------------------------------------------------------------------------------------//

    [Authorize(Roles = $"{AccountRole.Admin}")]
    [HttpGet]
    [Route("stats/login")]
    public async Task<IActionResult> GetLoginStats()
    {
        try
        {
            var logins = await BlobCache.LocalMachine.GetAllObjects<AccountLogin>();
            //var accounts = logins.Select(x => x.Account.ID).Distinct();

            return Ok(logins);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return BadRequest();
        }
    }

    [AllowAnonymous]
    [HttpGet]
    [Route("{id}/checkpassword")]
    public async Task<IActionResult> CheckAccountPassword(string id)
    {
        try
        {
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return BadRequest();
        }
    }

    //--------------------------------------------------------------------------------------------------------------//
    private bool VerifyPassword(Account account, string password)
    {
        return account.Verify(password);
    }

    //--------------------------------------------------------------------------------------------------------------//
    private async Task<Account?> Authenticate(Expression<Func<Account, bool>> predicate)
    {
        var account = await DB.Find<Account>().Match(predicate).ExecuteFirstAsync();

        if (account != null) return account;
        return null;
    }
}