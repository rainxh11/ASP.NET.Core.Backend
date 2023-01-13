using Hangfire;
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
public class FormationController : Controller
{
    private readonly IConfiguration _config;
    private readonly IIdentityService _identityService;
    private readonly ILogger<FormationController> _logger;
    private readonly LoginInfoSaver _loginSaver;
    private readonly IHubContext<PrivateSchoolHub> _mailHub;
    private readonly AuthService _tokenService;
    private IBackgroundJobClient _backgroundJobs;
    private WebsocketNotificationService _websocketNotificationService;

    public FormationController(
        ILogger<FormationController> logger,
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
    [ResponseCache(Duration = 60,
        VaryByQueryKeys = new[] { "sort", "page", "desc", "limit" })]
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Teacher},{AccountRole.Moderator}")]
    [HttpGet]
    [Route("")]
    public async Task<IActionResult> GetFormations(CancellationToken ct, int page = 1, int limit = -1,
        string? sort = "CreatedOn", bool desc = true)
    {
        var students = await DB.PagedSearch<Formation>()
            .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
            .PageNumber(page)
            .PageSize(limit < 0 ? int.MaxValue : limit)
            .ExecuteAsync(ct);

        return Ok(new PagedResultResponse<IEnumerable<Formation>>(
            students.Results,
            students.TotalCount,
            students.PageCount,
            limit,
            page));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Teacher},{AccountRole.Moderator}")]
    [HttpGet]
    [Route("search")]
    public async Task<IActionResult> SearchFormations(CancellationToken ct, string search = "", int page = 1,
        int limit = -1, string? sort = "CreatedOn", bool desc = true)
    {
        search = $"/{search}/ig";
        var searchQuery = DB.Fluent<Formation>()
            .Match(formation => formation.Regex(x => x.Name, search) |
                                formation.Regex(x => x.Price, search) |
                                formation.Regex(x => x.ID, search));

        var students = await DB.PagedSearch<Formation>()
            .WithFluent(searchQuery)
            .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
            .PageNumber(page)
            .PageSize(limit < 0 ? int.MaxValue : limit)
            .ExecuteAsync(ct);

        return Ok(new PagedResultResponse<IEnumerable<Formation>>(
            students.Results,
            students.TotalCount,
            students.PageCount,
            limit,
            page));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}")]
    [HttpPost]
    [Route("")]
    public async Task<IActionResult> CreateFormation([FromBody] FormationModel model, CancellationToken ct)
    {
        var account = await _identityService.GetCurrentAccount(HttpContext);

        var formation = model.Adapt<Formation>();
        formation.CreatedBy = account.ToBaseAccount();
        await formation.InsertAsync(cancellation: ct);

        return Ok(new ResultResponse<Formation, string>(formation, "Formation Created!"));
    }

    //--------------------------------------------------------------------------------------------------------------//

    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}")]
    [HttpPatch]
    [Route("{id}")]
    public async Task<IActionResult> UpdateFormation(string id, [FromBody] FormationUpdateModel model,
        CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            var bson = model.ToBsonDocument();
            var pipeline = new EmptyPipelineDefinition<BsonDocument>()
                .AppendStage($"{{ $set : {bson} }}", BsonDocumentSerializer.Instance);

            var update = await DB.Database(_config["MongoDB.DatabaseName"])
                .GetCollection<BsonDocument>(nameof(Formation))
                .UpdateOneAsync(transaction.Session,
                    new { _id = ObjectId.Parse(id) }.ToBsonDocument(),
                    pipeline,
                    cancellationToken: ct);

            var formation = await DB.Find<Formation>(transaction.Session)
                .MatchID(id)
                .ExecuteSingleAsync(ct);
            await transaction.CommitAsync();

            return Ok(new ResultResponse<Formation, string>(formation, "Formation Updated!"));
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
    public async Task<IActionResult> DeleteFormation(string id, CancellationToken ct)
    {
        var delete = await DB.Collection<Formation>()
            .DeleteOneAsync(x => x.ID == id, ct);
        return !delete.IsAcknowledged
            ? BadRequest(new MessageResponse<DeleteResult>(delete))
            : Ok(new MessageResponse<DeleteResult>(delete));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}")]
    [HttpGet]
    [Route("{id}/student")]
    public async Task<IActionResult> GetSubscribedStudents(string id, CancellationToken ct)
    {
        var formation = await DB.Find<Formation>().OneAsync(id, ct);

        var query = await DB.Fluent<Invoice>()
            .Match(f => f.Eq(x => x.Formation.ID, id) &
                        f.Eq(x => x.Enabled, true))
            .ToListAsync(ct);
        var students = query
            .Where(x => !x.Expired)
            .Select(x => x.Student)
            .DistinctBy(x => x.ID)
            .OrderBy(x => x.Name);

        return Ok(new ResultResponse<Formation, IEnumerable<StudentBase>>(formation, students));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}")]
    [HttpGet]
    [Route("{id}/student/search")]
    public async Task<IActionResult> SearchSubscribedStudents(string id, CancellationToken ct, string? search = "")
    {
        search = search == "|" ? "" : search;
        search = $"/{search}/i";

        var formation = await DB.Find<Formation>().OneAsync(id, ct);

        var query = await DB.Fluent<Invoice>()
            .Match(f => f.Eq(x => x.Formation.ID, id) &
                        f.Eq(x => x.Enabled, true) &
                        (f.Regex(x => x.Student.Name, search) |
                         f.Regex(x => x.Student.PhoneNumber, search) |
                         f.ElemMatch(x => x.Student.Parents,
                             new FilterDefinitionBuilder<Parent>().Regex(
                                 p => p.Name, search)) |
                         f.ElemMatch(x => x.Student.Parents,
                             new FilterDefinitionBuilder<Parent>().Regex(
                                 p => p.PhoneNumber, search)) |
                         f.ElemMatch(x => x.Student.Parents,
                             new FilterDefinitionBuilder<Parent>().Regex(
                                 p => p.CardID, search)))
            )
            .ToListAsync(ct);
        var students = query
            .Where(x => !x.Expired)
            .Select(x => x.Student)
            .DistinctBy(x => x.ID)
            .OrderBy(x => x.Name);

        return Ok(new ResultResponse<Formation, IEnumerable<StudentBase>>(formation, students));
    }
}