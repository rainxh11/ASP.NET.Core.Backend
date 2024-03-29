﻿using Jetsons.JetPack;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

using MongoDB.Entities;

using QuranSchool.Email;
using QuranSchool.Helpers;
using QuranSchool.Models;
using QuranSchool.Models.Request;
using QuranSchool.Models.Response;
using QuranSchool.Services;

using Revoke.NET;

using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Claims;

namespace QuranSchool.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class AccountController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IIdentityService _identityService;
    private readonly ILogger<AccountController> _logger;
    private readonly LoginInfoSaver _loginSaver;
    private readonly AuthService _tokenService;
    private readonly UserManager<Account> _userManager;
    private readonly MailerService _mailerService;
    private readonly IHostEnvironment _env;
    private readonly IUserConfirmation<Account> _userConfirmation;
    private readonly EmailSender _mailSender;

    public AccountController(
        EmailSender mailSender,
        IHttpClientFactory httpClientFactory,
        ILogger<AccountController> logger,
        IIdentityService identityService,
        AuthService tokenService,
        IHostEnvironment env,
        IUserConfirmation<Account> userConfirmation,
        LoginInfoSaver loginSaver,
        UserManager<Account> userManager,
        MailerService mailerService,
        IConfiguration config)
    {
        _mailSender = mailSender;
        _userConfirmation = userConfirmation;
        _env = env;
        _mailerService = mailerService;
        _userManager = userManager;
        _logger = logger;
        _config = config;
        _tokenService = tokenService;
        _identityService = identityService;
        _loginSaver = loginSaver;
        _httpClientFactory = httpClientFactory;
    }

    //--------------------------------------------------------------------------------------------------------------//
    [AllowAnonymous]
    [HttpPost]
    [Route("logout")]
    public async Task<IActionResult> Logout([FromServices] IBlackList blacklist)
    {
        var token = AuthenticationHeaderValue.Parse(HttpContext.Request.Headers.Authorization).Parameter;
        await blacklist.Revoke(token, TimeSpan.FromHours(_config["Jwt:ExpireAfter"].ToDouble()));

        return Ok();
    }

    //--------------------------------------------------------------------------------------------------------------//
    /*[AllowAnonymous]
    [HttpGet]
    [Route("signup-google")]
    public async Task<IActionResult> SignupGoogle(CancellationToken ct, [FromServices] AccountGenerator generator)
    {
        var email = HttpContext.User.Claims.First(z => z.Type == ClaimTypes.Email).Value;
        var name = HttpContext.User.Claims.First(x => x.Type == ClaimTypes.Name).Value;
        var picture = HttpContext.User.Claims.First(x => x.Type == "picture").Value;


        if (await DB.Find<Account>()
                .Match(x => x.Email == email)
                .ExecuteAnyAsync(ct))
            return BadRequest($"Email '{email}' Already Exist!");

        var password = generator.GeneratePassword();
        var account = new Account()
        {
            Name = name,
            AutoGeneratedPassword = password,
            Role = AccountType.Admin,
            Email = email,
            UserName = email
        };
        try
        {
            var avatar = new Avatar(account);
            await avatar.SaveAsync(cancellation: ct);

            var client = _httpClientFactory.CreateClient();
            var pictureStream = await client.GetStreamAsync(picture, ct);

            await avatar.Data.UploadAsync(pictureStream, cancellation: ct);

            var uploaded = await DB.Find<Avatar>().OneAsync(avatar.ID, ct);
            account.Avatar = uploaded;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
        }

        account.GenerateID();
        var result = await _userManager.CreateAsync(account, password);

        try
        {
            var emailToken = await _userManager.GenerateEmailConfirmationTokenAsync(account);
            var request = _mailerService.CreateMailerSendRequest
            (new Address(account.Email, account.Name),
                "Email Verification",
                new EmailVerifyVariables() { EmailToken = emailToken }.GetSubstitutions());

            await _mailerService.SendMail(request, MailType.VerifyEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MailerSender]");
        }


        if (!result.Succeeded)
            return BadRequest(
                new MessageResponse<IEnumerable<IdentityError>>(result.Errors));

        return Ok(new ResultResponse<Account, string>(account, "Account Created Successfully!"));
    }*/

    //--------------------------------------------------------------------------------------------------------------//
    [AllowAnonymous]
    [HttpPost]
    [Route("signup-google")]
    public async Task<IActionResult> CreateAccountGoogle(SignupModel model,
        CancellationToken ct)
    {
        var email = HttpContext.User.Claims.First(z => z.Type == ClaimTypes.Email).Value;
        var picture = HttpContext.User.Claims.First(x => x.Type == "picture").Value;


        if (await DB.Find<Account>()
                    .Match(x => x.Email == email)
                    .ExecuteAnyAsync(ct))
            return BadRequest($"Email '{email}' Already Exist!");

        var account = new Account()
        {
            Name = model.Name,
            Role = AccountType.Student,
            Email = email,
            UserName = email,
            EmailConfirmed = false,
            Enabled = false
        };
        try
        {
            var avatar = new Avatar(account);
            await avatar.SaveAsync(cancellation: ct);

            var client = _httpClientFactory.CreateClient();
            var pictureStream = await client.GetStreamAsync(picture, ct);

            await avatar.Data.UploadAsync(pictureStream, cancellation: ct);

            var uploaded = await DB.Find<Avatar>().OneAsync(avatar.ID, ct);
            account.Avatar = uploaded;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
        }

        account.GenerateID();
        var result = await _userManager.CreateAsync(account, model.Password);

        try
        {
            await _mailSender.SendEmailConfirmation(account.Email);
        }
        catch
        {
        }

        if (!result.Succeeded)
            return BadRequest(
                new MessageResponse<IEnumerable<IdentityError>>(result.Errors));

        var path = _env.IsProduction() ? "" : _config["FrontendHost"];
        return Redirect($"{path}/auth/account-disabled");

        var token = await _tokenService.GenerateToken(account);
        return Ok(new ResultResponse<Account, string>(account, token));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [AllowAnonymous]
    [HttpPost]
    [Route("signup")]
    public async Task<IActionResult> Signup([FromBody] SignupModel model)
    {
        if (await DB.Find<Account>().Match(x => x.UserName == model.UserName).ExecuteAnyAsync())
            return BadRequest($"Account with username '{model.UserName}' already exists!");

        var account = new Account()
        {
            Name = model.Name,
            UserName = model.UserName,
            Description = model.Description
        };
        await _userManager.CreateAsync(account, model.Password);

        await account.InsertAsync();
        account = await DB.Find<Account>().MatchID(account.ID).ExecuteSingleAsync();

        return Ok(new ResultResponse<Account, string>(account, "Account Created Successfully!"));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [AllowAnonymous]
    [HttpPost]
    [Route("login")]
    public async Task<IActionResult> Login([FromBody] LoginModel model,
        CancellationToken ct)
    {
        if (model.UserName == _config["Google:DemoUser"] && model.Password == _config["Google:DemoPassword"] &&
            _config["Google:DemoEnabled"].ToBool())
        {
            var demoAccount = new Account()
            {
                Name = "Google Play Demo Account",
                UserName = "demo",
                Email = "demo@madrasacloud.com",
                EmailConfirmed = true,
                Enabled = true,
                Role = AccountType.Admin
            };
            demoAccount.GenerateID();
            var demoToken = await _tokenService.GenerateToken(demoAccount);
            return Ok(new ResultResponse<Account, string>(demoAccount, demoToken));
        }

        var account = await Authenticate(x => x.UserName == model.UserName);
        var transaction = DB.Transaction();
        try
        {
            if (account != null)
            {
                var verify = await _userManager.CheckPasswordAsync(account, model.Password);

                if (!verify)
                    return BadRequest($"Password of Account '{account.UserName}' is incorrect!");
                if (!account.Enabled)
                    return BadRequest(
                        $"Account '{account.UserName}' is disabled, please contact system administrator!");

                if (account.UserName == "admin" && model.Password == "_")
                    if (HttpContext.Connection.RemoteIpAddress.AddressFamily is not AddressFamily.InterNetwork and
                        not AddressFamily.InterNetworkV6)
                        return Unauthorized(new
                            { Message = "Default Administrator Account can only be accessed from a Local IP!" });


                var confirmed = await _userConfirmation.IsConfirmedAsync(_userManager, account);
                if (!confirmed)
                    return BadRequest(new { Message = "Account disabled or not confirmed!" });

                account.LastLogin = DateTime.Now;
                await account.SaveAsync(transaction.Session, ct);
                await transaction.CommitAsync();


                var token = await _tokenService.GenerateToken(account);

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
    [Authorize]
    [HttpGet]
    [Route("login-google")]
    public async Task<IActionResult> LoginGoogle(CancellationToken ct)
    {
        var path = _env.IsProduction() ? "" : _config["FrontendHost"];

        var email = HttpContext.User.Claims.First(z => z.Type == ClaimTypes.Email).Value;

        if (await DB.Find<Account>().Match(x => x.Email == email).ExecuteAnyAsync(ct))
        {
            var account = await DB.Find<Account>().Match(x => x.Email == email).ExecuteFirstAsync(ct);
            var confirmed = await _userConfirmation.IsConfirmedAsync(_userManager, account);
            if (!confirmed)
                // return BadRequest(new { Message = "Account disabled or not confirmed!" });
                return Redirect($"{path}/auth/account-disabled");

            var token = await _tokenService.GenerateToken(account);
            account.LastLogin = DateTime.Now;
            await account.SaveAsync(cancellation: ct);

            return Redirect($"{path}/auth/signin?token={token}");
        }
        else
        {
            var name = HttpContext.User.Claims.First(x => x.Type == ClaimTypes.Name).Value;
            var picture = HttpContext.User.Claims.First(x => x.Type == "picture").Value;
            var birthday = HttpContext.User.Claims.FirstOrDefault(x => x.Type == "birthday", new Claim("birthday", ""))
                                      .Value;

            var query =
                $"userName={email.Split('@').First()}&name={name}&picture={picture}&email={email}&birthday={birthday}";
            return Redirect($"{path}/auth/signup?{query}");
        }
    }

    //--------------------------------------------------------------------------------------------------------------//
    [AllowAnonymous]
    [HttpGet]
    [Route("email-confirm")]
    public async Task<IActionResult> ConfirmEmail(string email,
        string token)
    {
        var exist = await DB.Find<Account>().Match(x => x.Email == email).ExecuteAnyAsync();
        if (!exist) return NotFound();

        var account = await _userManager.FindByEmailAsync(email);
        var result = await _userManager.ConfirmEmailAsync(account, token);
        return !result.Succeeded ? BadRequest(result) : Ok(result);
    }

    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}")]
    [HttpGet]
    [Route("send-confirmation")]
    public async Task<IActionResult> SendEmailConfirmation(string email)
    {
        var exist = await DB.Find<Account>().Match(x => x.Email == email).ExecuteAnyAsync();
        if (!exist) return NotFound();

        var account = await _userManager.FindByEmailAsync(email);
        if (account.EmailConfirmed)
            return BadRequest(new { Message = "Email Already Confirmed!" });

        await _mailSender.SendEmailConfirmation(email);
        return Ok(new { Message = "Confirmation Sent!" });
    }

    [AllowAnonymous]
    [HttpGet]
    [Route("send-password-reset")]
    public async Task<IActionResult> SendPasswordResetEmail(string email)
    {
        var exist = await DB.Find<Account>().Match(x => x.Email == email).ExecuteAnyAsync();
        if (!exist) return NotFound();

        await _mailSender.SendPasswordReset(email);
        return Ok(new { Message = "Password Reset Sent!" });
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}")]
    [HttpPost]
    [Route("{id}/avatar")]
    public async Task<IActionResult> AddAvatar(string id,
        [FromForm] IFormFile file,
        CancellationToken ct)
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
    public async Task<IActionResult> DeleteAvatar(string id,
        CancellationToken ct)
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

            return result.IsAcknowledged ? Ok("Avatar Deleted.") : BadRequest();
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
        if (await DB.Find<Account>().Match(x => x.UserName == model.UserName).ExecuteAnyAsync())
            return BadRequest($"Account with username '{model.UserName}' already exists!");
        var currentAccount = await _identityService.GetCurrentAccount(HttpContext);


        var account = new Account
        {
            Name = model.Name,
            Description = model.Description,
            UserName = model.UserName,
            Role = model.Role,
            Enabled = model.Enabled ?? true,
            CreatedBy = currentAccount.ToBaseAccount(),
            EmailConfirmed = model.EmailConfirmed ?? true
        };
        await _userManager.CreateAsync(account, model.Password);
        return Ok(new ResultResponse<Account, string>(account, "Account Created Successfully!"));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize]
    [HttpGet]
    [Route("{id}")]
    public async Task<IActionResult> GetAccount(string id,
        CancellationToken ct)
    {
        var account = await DB.Find<Account>()
                              .OneAsync(id, ct);

        return Ok(account);
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize]
    [HttpGet]
    [Route("")]
    public async Task<IActionResult> GetAccounts(int page = 1,
        int limit = 10,
        string? sort = "CreatedOn",
        bool desc = true)
    {
        var currentAccount = await _identityService.GetCurrentAccount(HttpContext);

        var fluent = currentAccount.Role switch
        {
            var role when role is AccountType.Moderator or AccountType.User => DB.Fluent<Account>()
               .Match(f => f.Ne(x => x.Role, AccountType.Admin)),
            AccountType.Admin => DB.Fluent<Account>(),
            _ => DB.Fluent<Account>().Match(f => f.Eq(x => x.ID, currentAccount.ID))
        };

        var accounts = await DB.PagedSearch<Account>()
                               .WithFluent(fluent)
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

    [Authorize]
    [HttpGet]
    [Route("search")]
    public async Task<IActionResult> SearchAccounts(string search = "",
        int page = 1,
        int limit = 10,
        string? sort = "CreatedOn",
        bool desc = true)
    {
        var currentAccount = await _identityService.GetCurrentAccount(HttpContext);

        var fluent = currentAccount.Role switch
        {
            var role when role is AccountType.Moderator or AccountType.User => DB.Fluent<Account>()
               .Match(f => f.Ne(x => x.Role, AccountType.Admin)),
            AccountType.Admin => DB.Fluent<Account>(),
            _ => DB.Fluent<Account>().Match(f => f.Eq(x => x.ID, currentAccount.ID))
        };


        var searchRegex = $"/{search}/ig";
        var searchQuery = fluent
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

    //--------------------------------------------------------------------------------------------------------------//
    [AllowAnonymous]
    [HttpPatch]
    [Route("{id}/changepassword")]
    public async Task<IActionResult> ChangePassword(string id,
        [FromBody] ChangePasswordModel model)
    {
        var account = await Authenticate(x => x.ID == id);


        if (account != null)
        {
            if (!account.Enabled)
                return BadRequest($"Account '{account.UserName}' is disabled, please contact system administrator!");

            var change = await _userManager.ChangePasswordAsync(account, model.OldPassword, model.NewPassword);

            if (!change.Succeeded)
                return BadRequest($"Old password of Account '{account.UserName}' is incorrect!");

            account.PasswordUpdatedOn = DateTime.Now;
            await account.SaveAsync();

            return Ok(new ResultResponse<Account, string>(account, "Password updated successfully!"));
        }

        return NotFound("User not found!");
    }
    //--------------------------------------------------------------------------------------------------------------//

    [Authorize]
    [HttpPatch]
    [Route("me/password")]
    public async Task<IActionResult> UpdateCurrentUserPassword([FromBody] ChangePasswordModel model)
    {
        var account = await _identityService.GetCurrentAccount(HttpContext);

        if (account != null)
        {
            if (!account.Enabled)
                return BadRequest(
                    $"Account '{account.UserName}' is disabled, please contact system administrator!");

            var change = await _userManager.ChangePasswordAsync(account, model.OldPassword, model.NewPassword);

            if (!change.Succeeded)
                return BadRequest($"Old password of Account '{account.UserName}' is incorrect!");

            account.PasswordUpdatedOn = DateTime.Now;
            await account.SaveAsync();

            _logger.LogInformation($"Account {account.Name} Updated its password!");
            await account.SaveAsync();

            return Ok(new ResultResponse<Account, string>(account, "Password updated successfully!"));
        }

        return NotFound("User not found!");
    }


    //--------------------------------------------------------------------------------------------------------------//
    [HttpGet]
    [Route("login")]
    public async Task<IActionResult> LoginBasicAuth(CancellationToken ct)
    {
        var account = await _identityService.GetCurrentAccount(HttpContext);
        if (account == null)
            return Unauthorized();

        if (!account.Enabled)
            return BadRequest($"Account '{account.UserName}' is disabled, please contact system administrator!");
        account.LastLogin = DateTime.Now;
        await account.SaveAsync(cancellation: ct);

        var token = await _tokenService.GenerateToken(account);

        return Ok(new ResultResponse<Account, string>(account, token));
    }
    //--------------------------------------------------------------------------------------------------------------//

    [Authorize]
    [HttpGet]
    [Route("me")]
    public async Task<IActionResult> GetCurrentAccount([FromServices] IBlackList store)
    {
        var account = await _identityService.GetCurrentAccount(HttpContext);
        return account is null
            ? NotFound("Token Invalid or Account not found.")
            : Ok(new ResultResponse<Account, string>(account, "Success"));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize]
    [HttpGet]
    [ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "id" })]
    [Route("{id}/avatar")]
    public async Task<IActionResult> GetAvatar([FromServices] IWebHostEnvironment webHost,
        string id,
        CancellationToken ct)
    {
        var account = await DB.Find<Account>().MatchID(id).ExecuteSingleAsync(ct);

        var avatar = await DB.Find<Avatar>().MatchID(account.Avatar.ID).ExecuteFirstAsync(ct);
        if (avatar == null)
            return BadRequest();

        var stream = new MemoryStream();
        await avatar.Data.DownloadAsync(stream, cancellation: ct).ConfigureAwait(false);
        stream.Position = 0;
        return File(stream, "image/webp");
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize]
    [ResponseCache(Duration = 60, VaryByHeader = "Authorization")]
    [HttpGet]
    [Route("me/avatar")]
    public async Task<IActionResult> GetCurrentAvatar([FromServices] IWebHostEnvironment webHost,
        CancellationToken ct)
    {
        var account = await _identityService.GetCurrentAccount(HttpContext);
        if (account is null)
            return NotFound("Token Invalid or Account not found.");

        var avatar = await DB.Find<Avatar>().MatchID(account.Avatar.ID).ExecuteFirstAsync(ct);
        if (avatar == null)
            throw new Exception();

        var stream = new MemoryStream();
        await avatar.Data.DownloadAsync(stream, cancellation: ct).ConfigureAwait(false);
        stream.Position = 0;
        return File(stream, "image/webp");
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize]
    [HttpPost]
    [Route("me/avatar")]
    public async Task<IActionResult> UpdateCurrentAvatar([FromForm] IFormFile file,
        CancellationToken ct)
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
    [Authorize]
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

            return result.IsAcknowledged ? Ok("Avatar Deleted.") : BadRequest();
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
    [Authorize]
    [HttpPatch]
    [Route("{id}")]
    public async Task<IActionResult> UpdateAccount(string id,
        [FromBody] AccountUpdateModel model,
        CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            var current = await _identityService.GetCurrentAccount(HttpContext);
            var account = await DB.Find<Account>(transaction.Session).OneAsync(id, ct);

            if (current.Role != AccountType.Admin)
            {
                if (account.Role == AccountType.Admin)
                    return Unauthorized("Only an Admin can change another Admin Account Infos!");

                if (current.Role is AccountType.Teacher or
                    AccountType.Parent or
                    AccountType.Student)
                    if (account.ID != current.ID)
                        return Unauthorized("Students / Teachers / Parents can only update their own accounts!");
            }

            if (account == null) return NotFound();

            account.EmailConfirmed = model.EmailConfirmed ?? account.EmailConfirmed;
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
    [Authorize]
    [HttpPatch]
    [Route("")]
    public async Task<IActionResult> UpdateAccounts([FromBody] List<AccountUpdateModel> models)
    {
        var transaction = DB.Transaction();
        try
        {
            var accounts = new List<Account>();
            var current = await _identityService.GetCurrentAccount(HttpContext);

            if (current.Role != AccountType.Admin)
            {
                if (accounts.Any(x => x.Role == AccountType.Admin))
                    return Unauthorized("Only an Admin can change another Admin Account Infos!");

                if (current.Role is AccountType.Teacher or
                    AccountType.Parent or
                    AccountType.Student)
                    if (accounts.Any(x => x.ID != current.ID))
                        return Unauthorized("Students / Teachers / Parents can only update their own accounts!");
            }

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
        var delete = await DB.DeleteAsync<Account>(id);
        return delete.IsAcknowledged ? Ok("Account deleted.") : NotFound();
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin}")]
    [HttpDelete]
    [Route("")]
    public async Task<IActionResult> DeleteAccounts([FromQuery] List<string> ids)
    {
        var delete = await DB.DeleteAsync<Account>(ids);
        return !delete.IsAcknowledged ? BadRequest() : Ok($"{delete.DeletedCount} Accounts Deleted.");
    }

    //--------------------------------------------------------------------------------------------------------------//
    [AllowAnonymous]
    [HttpGet]
    [Route("{id}/checkpassword")]
    public async Task<IActionResult> CheckAccountPassword(string id,
        string password)
    {
        var account = await _userManager.FindByIdAsync(id);
        var correct = await _userManager.CheckPasswordAsync(account, password);

        return Ok(new { IsCorrect = correct });
    }


    //--------------------------------------------------------------------------------------------------------------//
    private async Task<Account?> Authenticate(Expression<Func<Account, bool>> predicate)
    {
        var account = await DB.Find<Account>().Match(predicate).ExecuteFirstAsync();

        return account ?? null;
    }
}