using Hangfire;
using Jetsons.JetPack;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Entities;
using QuranSchool.Helpers;
using QuranSchool.Hubs;
using QuranSchool.Models;
using QuranSchool.Models.Request;
using QuranSchool.Models.Response;
using QuranSchool.Services;

namespace QuranSchool.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class TeacherController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IIdentityService _identityService;
    private readonly ILogger<TeacherController> _logger;
    private readonly LoginInfoSaver _loginSaver;
    private readonly IHubContext<PrivateSchoolHub> _mailHub;
    private readonly AuthService _tokenService;
    private IBackgroundJobClient _backgroundJobs;
    private WebsocketNotificationService _websocketNotificationService;

    public TeacherController(
        ILogger<TeacherController> logger,
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
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator},{AccountRole.Teacher}")]
    [HttpGet]
    [Route("{id}/group")]
    public async Task<IActionResult> GetTeacherGroups(string id, CancellationToken ct)
    {
        var account = await _identityService.GetCurrentAccount(HttpContext);
        if (account.Role == AccountType.Teacher && id != account.PersonalId)
            return Unauthorized();

        var groups = await DB.Find<Group>()
            .Match(f => f.Eq(x => x.Cancelled, false) &
                        (f.ElemMatch(x => x.Sessions,
                             new FilterDefinitionBuilder<Session>().Eq(x => x.Teacher.ID, id)) |
                         f.Eq(x => x.Teacher.ID, id)))
            .ExecuteAsync(ct);

        return Ok(groups.OrderByDescending(x => x.CreatedOn));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}, {AccountRole.Teacher}")]
    [HttpGet]
    [Route("{id}")]
    public async Task<IActionResult> GetTeacher(string id, CancellationToken ct)
    {
        var account = await _identityService.GetCurrentAccount(HttpContext);
        if (account.Role == AccountType.Teacher && id != account.PersonalId)
            return Unauthorized();

        var student = await DB.Find<Teacher>().OneAsync(id, ct);

        return Ok(student);
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}, {AccountRole.Teacher}")]
    [HttpGet]
    [Route("")]
    public async Task<IActionResult> GetTeachers(int page = 1, int limit = 10, string? sort = "CreatedOn",
        bool desc = true)
    {
        var account = await _identityService.GetCurrentAccount(HttpContext);
        var fluent = account.Role switch
        {
            AccountType.Teacher => DB.Fluent<Teacher>().Match(f => f.Eq(x => x.ID, account.PersonalId)),
            _ => DB.Fluent<Teacher>()
        };

        var students = await DB.PagedSearch<Teacher>()
            .WithFluent(fluent)
            .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
            .PageNumber(page)
            .PageSize(limit)
            .ExecuteAsync();

        return Ok(new PagedResultResponse<IEnumerable<Teacher>>(
            students.Results,
            students.TotalCount,
            students.PageCount,
            limit,
            page));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}, {AccountRole.Teacher}")]
    [HttpGet]
    [Route("search")]
    public async Task<IActionResult> SearchTeachers(string search = "", int page = 1, int limit = 10,
        string? sort = "CreatedOn", bool desc = true, bool lookup = false)
    {
        var account = await _identityService.GetCurrentAccount(HttpContext);
        var fluent = account.Role switch
        {
            AccountType.Teacher => DB.Fluent<Teacher>().Match(f => f.Eq(x => x.ID, account.PersonalId)),
            _ => DB.Fluent<Teacher>()
        };

        search = search == "|" ? "" : search;
        search = $"/{search}/i";
        var searchQuery = fluent.Match(teacher => teacher.Regex(x => x.Name, search) |
                                                  teacher.Regex(x => x.Description, search) |
                                                  teacher.Regex(x => x.Speciality, search) |
                                                  teacher.Regex(x => x.CardID, search) |
                                                  teacher.Regex(x => x.CreatedBy.Name, search) |
                                                  teacher.Regex(x => x.CreatedBy.Description,
                                                      search) |
                                                  teacher.Regex(x => x.CreatedBy.UserName, search) |
                                                  teacher.ElemMatch(x => x.PhoneNumbers,
                                                      new FilterDefinitionBuilder<string>().Regex(
                                                          p => p, search))
        );

        var students = await DB.PagedSearch<Teacher>()
            .WithFluent(searchQuery)
            .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
            .PageNumber(lookup ? 1 : page)
            .PageSize(lookup ? int.MaxValue : limit)
            .ExecuteAsync();

        return !lookup
            ? Ok(new PagedResultResponse<IEnumerable<Teacher>>(
                students.Results,
                students.TotalCount,
                students.PageCount,
                limit,
                page))
            : (IActionResult)Ok(new ResultResponse<IEnumerable<object>, string>(students.Results.Select(x => new
            {
                x.ID,
                x.Name,
                PhoneNumber = string.Join(";", x.PhoneNumbers),
                x.Speciality,
                x.DateOfBirth
            }), "Success"));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}")]
    [HttpPost]
    [Route("")]
    public async Task<IActionResult> CreateTeacher([FromBody] TeacherModel model)
    {
        var account = await _identityService.GetCurrentAccount(HttpContext);

        var teacher = model.Adapt<Teacher>();
        teacher.CreatedBy = account.ToBaseAccount();
        await teacher.InsertAsync();

        return Ok(new ResultResponse<Teacher, string>(teacher, "Teacher Created!"));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}, {AccountRole.Teacher}")]
    [HttpPatch]
    [Route("{id}")]
    public async Task<IActionResult> UpdateTeacher(string id, [FromBody] TeacherUpdateModel model,
        CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);
            if (account.Role == AccountType.Teacher && id != account.PersonalId)
                return Unauthorized();

            var bson = model.ToBsonDocument();
            var pipeline = new EmptyPipelineDefinition<BsonDocument>()
                .AppendStage($"{{ $set : {bson} }}", BsonDocumentSerializer.Instance);

            var update = await DB.Database(_config["MongoDB.DatabaseName"])
                .GetCollection<BsonDocument>(nameof(Teacher))
                .UpdateOneAsync(transaction.Session,
                    new { _id = ObjectId.Parse(id) }.ToBsonDocument(),
                    pipeline,
                    cancellationToken: ct);

            var teacher = await DB.Find<Teacher>(transaction.Session)
                .MatchID(id)
                .ExecuteSingleAsync(ct);
            await transaction.CommitAsync();

            return Ok(new ResultResponse<Teacher, string>(teacher, "Teacher Updated!"));
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
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}")]
    [HttpDelete]
    [Route("{id}")]
    public async Task<IActionResult> DeleteTeacher(string id)
    {
        var delete = await DB.Collection<Teacher>()
            .DeleteOneAsync(x => x.ID == id);
        return !delete.IsAcknowledged
            ? BadRequest(new MessageResponse<DeleteResult>(delete))
            : Ok(new MessageResponse<DeleteResult>(delete));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}, {AccountRole.Teacher}")]
    [HttpPost]
    [Route("{id}/avatar")]
    public async Task<IActionResult> UpdateTeacherAvatar(string id, [FromForm] IFormFile file, CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            if (!file.ContentType.Contains("image"))
                return BadRequest(new MessageResponse<string>(
                    $"Content of type: '{file.ContentType}' not allowed! Only image type is allowed!"));

            var account = await _identityService.GetCurrentAccount(HttpContext);
            if (account.Role == AccountType.Teacher && id != account.PersonalId)
                return Unauthorized();

            var teacher = await DB.Find<Teacher>(transaction.Session)
                .OneAsync(id, ct);

            if (teacher.Avatar != null)
                await teacher.Avatar.DeleteAsync(transaction.Session, ct);
            var avatar = new Avatar
            {
                PersonalID = teacher.ID
            };

            await avatar.SaveAsync(transaction.Session, ct);
            await using (var stream = await ImageHelper.EncodeWebp(file, ct))
            {
                await avatar.Data.UploadAsync(stream, cancellation: ct, session: transaction.Session);
            }

            var uploaded = await DB.Find<Avatar>(transaction.Session).OneAsync(avatar.ID);
            teacher.Avatar = uploaded;
            await teacher.SaveAsync(transaction.Session, ct);
            await transaction.CommitAsync(ct);

            return Ok(new ResultResponse<Teacher, string>(teacher, "Avatar updated!"));
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
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}, {AccountRole.Teacher}")]
    [HttpDelete]
    [Route("{id}/avatar")]
    public async Task<IActionResult> DeleteTeacherAvatar(string id, CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);
            if (account.Role == AccountType.Teacher && id != account.PersonalId)
                return Unauthorized();

            var teacher = await DB.Find<Teacher>(transaction.Session)
                .OneAsync(id, ct);

            if (teacher == null || teacher.Avatar == null)
                return NotFound();

            teacher.Avatar = null;
            await teacher.SaveAsync(transaction.Session, ct);
            await transaction.CommitAsync(ct);
            var result = await DB.DeleteAsync<Avatar>(x => x.PersonalID == teacher.ID, transaction.Session, ct);

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
    [ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "id" })]
    [HttpGet]
    [Route("{id}/avatar")]
    public async Task<IActionResult> GetTeacherAvatar([FromServices] IWebHostEnvironment webHost, CancellationToken ct,
        string id)
    {
        var teacher = await DB.Find<Teacher>()
            .OneAsync(id, ct);

        var avatar = await DB.Find<Avatar>().MatchID(teacher.Avatar.ID).ExecuteFirstAsync(ct);

        var stream = new MemoryStream();
        await avatar.Data.DownloadAsync(stream, cancellation: ct).ConfigureAwait(false);
        stream.Position = 0;
        return File(stream, "image/webp");
    }
}