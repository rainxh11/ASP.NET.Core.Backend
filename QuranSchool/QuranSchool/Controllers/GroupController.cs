using Hangfire;

using Mapster;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Entities;

using QuranSchool.Helpers;
using QuranSchool.Hubs;
using QuranSchool.Models;
using QuranSchool.Models.Request;
using QuranSchool.Models.Response;
using QuranSchool.Services;

using System.Linq;

namespace QuranSchool.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class GroupController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IIdentityService _identityService;
    private readonly ILogger<GroupController> _logger;
    private readonly LoginInfoSaver _loginSaver;
    private readonly IHubContext<PrivateSchoolHub> _mailHub;
    private readonly AuthService _tokenService;
    private IBackgroundJobClient _backgroundJobs;
    private WebsocketNotificationService _websocketNotificationService;

    public GroupController(
        ILogger<GroupController> logger,
        IIdentityService identityService,
        AuthService tokenService,
        IHubContext<PrivateSchoolHub> mailHub,
        IBackgroundJobClient bgJobs,
        WebsocketNotificationService wsNotificationService,
        LoginInfoSaver loginSaver,
        IConfiguration config)
    {
        _logger = logger;
        _config = config;
        _tokenService = tokenService;
        _identityService = identityService;
        _mailHub = mailHub;
        _backgroundJobs = bgJobs;
        _websocketNotificationService = wsNotificationService;
        _loginSaver = loginSaver;
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles =
        $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Teacher},{AccountRole.Parent},{AccountRole.Moderator},{AccountRole.Student},")]
    [HttpGet]
    [Route("{id}")]
    public async Task<IActionResult> GetGroup(string id,
        CancellationToken ct)
    {
        var group = await DB.Find<Group>().MatchID(id).ExecuteSingleAsync(ct);

        group.Sessions = group.Sessions
                              .Distinct(new SessionEqualityComparer())
                              .GroupBy(x => x.Start.Date)
                              .Select(x => x.First())
                              .ToList();

        return Ok(group);
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles =
        $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Teacher},{AccountRole.Parent},{AccountRole.Student},{AccountRole.Moderator}")]
    [HttpGet]
    [Route("{id}/post")]
    public async Task<IActionResult> GetGroupPosts(string id,
        CancellationToken ct)
    {
        var group = await DB.Find<Group>().MatchID(id).ExecuteSingleAsync(ct);

        return Ok(group.Posts.OrderByDescending(x => x.CreatedOn));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Teacher},{AccountRole.Moderator}")]
    [HttpPost]
    [Route("{id}/post")]
    public async Task<IActionResult> PostGroupPost(string id,
        [FromBody] PostModel post,
        [FromServices] HtmlEntityService htmlService,
        CancellationToken ct)
    {
        var account = await _identityService.GetCurrentAccount(HttpContext);

        var group = await DB.Find<Group>().OneAsync(id, ct);

        group.Posts.Add(new GroupPost
        {
            CreatedBy = account.ToBaseAccount(),
            Content = htmlService.ReplaceHref(post.Body)
        });
        await group.SaveAsync(cancellation: ct);

        return Ok();
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Teacher},{AccountRole.Moderator}")]
    [HttpDelete]
    [Route("{id}/post/{postId}")]
    public async Task<IActionResult> DeleteGroupPost(string id,
        string postId,
        [FromServices] HtmlEntityService htmlService,
        CancellationToken ct)
    {
        var account = await _identityService.GetCurrentAccount(HttpContext);

        var group = await DB.Find<Group>().OneAsync(id, ct);
        var post = group.Posts.First(x => x.ID == postId);
        if (account?.ID != post.CreatedBy.ID && account?.Role != AccountType.Admin)
            return Unauthorized(new { Message = "Only the Post's Creator or an Admin Account can delete it!" });

        group.Posts.RemoveAll(x => x.ID == postId);
        await group.SaveAsync(cancellation: ct);

        return Ok();
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles =
        $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Teacher},{AccountRole.Parent},{AccountRole.Student},{AccountRole.Moderator}")]
    [HttpGet]
    [Route("")]
    public async Task<IActionResult> GetGroups([FromServices] ParentService parentService,
        CancellationToken ct,
        int page = 1,
        int limit = 10,
        string? sort = "CreatedOn",
        bool desc = true)
    {
        var account = await _identityService.GetCurrentAccount(HttpContext);
        var fluent = DB.Fluent<Group>();
        switch (account?.Role)
        {
            default:
                return Unauthorized();
            case AccountType.User:
            case AccountType.Admin:
            case AccountType.Secretary:
            case AccountType.Moderator:
                break;
            case AccountType.Student:
                fluent = fluent.Match(f => f.ElemMatch(x => x.Students,
                    new FilterDefinitionBuilder<StudentBase>().Eq(x => x.ID,
                        account.PersonalId)));
                break;
            case AccountType.Parent:
                var students = await parentService.GetParentStudents(account.PersonalId, ct);
                fluent = fluent.Match(f => f.ElemMatch(x => x.Students,
                    new FilterDefinitionBuilder<StudentBase>().In(x => x.ID,
                        students.Select(x => x.ID))));
                break;
            case AccountType.Teacher:
                fluent = fluent.Match(f => f.Eq(x => x.Teacher.ID, account.PersonalId) |
                                           f.ElemMatch(x => x.Sessions,
                                               new FilterDefinitionBuilder<Session>().Eq(x => x.Teacher.ID,
                                                   account.PersonalId)));
                break;
        }

        var groups = await DB.PagedSearch<Group>()
                             .WithFluent(fluent)
                             .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
                             .PageNumber(page)
                             .PageSize(limit)
                             .ExecuteAsync(ct);

        return Ok(new PagedResultResponse<IEnumerable<Group>>(
            groups.Results,
            groups.TotalCount,
            groups.PageCount,
            limit,
            page));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles =
        $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Teacher},{AccountRole.Parent},{AccountRole.Moderator},{AccountRole.Student}")]
    [HttpGet]
    [Route("search")]
    public async Task<IActionResult> SearchGroups([FromServices] ParentService parentService,
        CancellationToken ct,
        string search = "",
        int page = 1,
        int limit = 10,
        string? sort = "CreatedOn",
        bool desc = true,
        bool lookup = false)
    {
        search = search == "|" ? "" : search;
        search = $"/{search}/i";

        var account = await _identityService.GetCurrentAccount(HttpContext);
        var fluent = DB.Fluent<Group>();
        switch (account?.Role)
        {
            default:
                return Unauthorized();
            case AccountType.User:
            case AccountType.Admin:
            case AccountType.Secretary:
            case AccountType.Moderator:
                break;
            case AccountType.Student:
                fluent = fluent.Match(f => f.ElemMatch(x => x.Students,
                    new FilterDefinitionBuilder<StudentBase>().Eq(x => x.ID,
                        account.PersonalId)));
                break;
            case AccountType.Parent:
                var parentStudents = await parentService.GetParentStudents(account.PersonalId, ct);
                fluent = fluent.Match(f => f.ElemMatch(x => x.Students,
                    new FilterDefinitionBuilder<StudentBase>().In(x => x.ID,
                        parentStudents.Select(x => x.ID))));
                break;
            case AccountType.Teacher:
                fluent = fluent.Match(f => f.Eq(x => x.Teacher.ID, account.PersonalId) |
                                           f.ElemMatch(x => x.Sessions,
                                               new FilterDefinitionBuilder<Session>().Eq(x => x.Teacher.ID,
                                                   account.PersonalId)));
                break;
        }

        var searchQuery = fluent
           .Match(group =>
                group.Regex(x => x.Name, search) |
                group.Regex(x => x.Teacher.CardID, search) |
                group.Regex(x => x.Teacher.Name, search) |
                group.Regex(x => x.Formation.Name, search) |
                group.Regex(x => x.CreatedBy.Name, search) |
                group.Regex(x => x.CreatedBy.Description, search) |
                group.Regex(x => x.CreatedBy.UserName, search) |
                group.ElemMatch(x => x.Students,
                    new FilterDefinitionBuilder<StudentBase>().Regex(s => s.Name, search)) |
                group.ElemMatch(x => x.Students,
                    new FilterDefinitionBuilder<StudentBase>().Regex(s => s.ID, search))
            );

        var students = await DB.PagedSearch<Group>()
                               .WithFluent(searchQuery)
                               .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
                               .PageNumber(lookup ? 1 : page)
                               .PageSize(lookup ? int.MaxValue : limit)
                               .ExecuteAsync(ct);

        return !lookup
            ? Ok(new PagedResultResponse<IEnumerable<Group>>(
                students.Results,
                students.TotalCount,
                students.PageCount,
                limit,
                page))
            : (IActionResult)Ok(new ResultResponse<IEnumerable<object>, string>(students.Results.Select(x => new
            {
                x.ID,
                x.Name,
                x.Students,
                x.Teacher,
                x.Formation,
                x.Status,
                x.Start,
                x.SessionsRemained
            }), "Success"));
    }

    //--------------------------------------------------------------------------------------------------------------//
    /*[Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}")]
    [HttpPost]
    [Route("")]
    public async Task<IActionResult> CreateGroup([FromBody] GroupModel model)
    {
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);

            var group = model.Adapt<Group>();
            group.CreatedBy = account.ToBaseAccount();
            await group.InsertAsync();

            return Ok(new ResultResponse<Group, string>(group, "Group Created!"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return BadRequest();
        }
    }*/
    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}")]
    [HttpPost]
    [Route("")]
    public async Task<IActionResult> CreateGroup(CancellationToken ct,
        [FromBody] GroupCreateModel model)
    {
        var account = await _identityService.GetCurrentAccount(HttpContext);

        var formation = await model.Formation.ToEntityAsync(cancellation: ct);
        var teacher = await model.Teacher.ToEntityAsync(cancellation: ct);

        var sessions = model.Sessions.Select(x =>
        {
            var session = x.Adapt<Session>();
            session.Formation = formation.ToBase();
            session.Teacher = teacher.ToBase();
            return session;
        });

        var group = new Group
        {
            Name = model?.Name,
            Teacher = teacher.ToBase(),
            Formation = formation.ToBase(),

            Start = model.StartDate,
            Sessions = sessions.ToList(),
            CreatedBy = account?.ToBaseAccount()
        };
        if (model.Students != null)
        {
            var students = await DB.Find<Student>().Match(f => f.In(x => x.ID, model.Students))
                                   .ExecuteAsync(ct);
            group.Students = students.Select(x => x.ToBase()).ToList();
        }

        await group.InsertAsync(cancellation: ct);

        return Ok(new ResultResponse<Group, string>(group, "Group Created!"));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}")]
    [HttpPatch]
    [HttpPut]
    [Route("{id}")]
    public async Task<IActionResult> UpdateGroup(string id,
        [FromBody] GroupUpdateModel model,
        [FromServices] SessionService sessionService,
        CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            if (model.SessionModels is not null and not { Count: 0 })
                model.Sessions = model.SessionModels.Select(x =>
                {
                    var session = x.Adapt<Session>();
                    session.Formation = model.Group.Formation;
                    session.Teacher = model.Group.Teacher;
                    return session;
                }).ToList();

            var bson = model.ToBsonDocument();
            var pipeline = new EmptyPipelineDefinition<BsonDocument>()
               .AppendStage($"{{ $set : {bson} }}", BsonDocumentSerializer.Instance);


            var update = await DB.Database(_config["MongoDB.DatabaseName"])
                                 .GetCollection<BsonDocument>(nameof(Group))
                                 .UpdateOneAsync(transaction.Session,
                                      new { _id = ObjectId.Parse(id) }.ToBsonDocument(),
                                      pipeline,
                                      cancellationToken: ct);

            await sessionService.CleanDuplicateGroupSessions(id, transaction.Session, ct);


            var group = await DB.Find<Group>(transaction.Session)
                                .MatchID(id)
                                .ExecuteSingleAsync(ct);
            await transaction.CommitAsync(ct);

            return Ok(new ResultResponse<Group, string>(group, "Group Updated!"));
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
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator},{AccountRole.Teacher}")]
    [HttpPatch]
    [HttpPut]
    [Route("{id}/schedule")]
    public async Task<IActionResult> UpdateGroupSchedule(string id,
        [FromBody] ScheduleUpdateModel model,
        CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            if (model.SessionModels is null) return BadRequest();
            var group = await DB.Find<Group>(transaction.Session).MatchID(id).ExecuteSingleAsync(ct);
            var lastVersion = group.Sessions.MaxBy(x => x.Version).Version;

            var sessions = model.SessionModels.Select(x =>
            {
                var session = x.Adapt<Session>();
                session.Formation = group.Formation;
                session.Teacher = group.Teacher;
                return session;
            }).Modify(x => x.Version = lastVersion + 1).ToList();

            group.Sessions = group.Sessions
                                  .Where(x => x.End <= DateTime.Now)
                                  .Concat(sessions)
                                  .OrderBy(x => x.Start)
                                  .ToList();

            await group.SaveAsync(transaction.Session, ct);

            await transaction.CommitAsync(ct);

            return Ok(new ResultResponse<Group, string>(group, "Group Updated!"));
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
    public async Task<IActionResult> DeleteGroup(string id)
    {
        var delete = await DB.Collection<Group>()
                             .DeleteOneAsync(x => x.ID == id);
        return !delete.IsAcknowledged
            ? BadRequest(new MessageResponse<DeleteResult>(delete))
            : Ok(new MessageResponse<DeleteResult>(delete));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}")]
    [HttpPatch]
    [Route("cancel")]
    public async Task<IActionResult> CancelGroups([FromServices] SessionService sessionService,
        CancellationToken ct,
        IEnumerable<string> ids)
    {
        var transaction = DB.Transaction();
        try
        {
            var groups = await DB.Find<Group>(transaction.Session)
                                 .Match(f => f.In(x => x.ID, ids))
                                 .ExecuteAsync(ct);

            foreach (var group in groups)
            {
                var students = await DB.Find<Student>(transaction.Session)
                                       .Match(f => f.In(x => x.ID, group.Students.Select(x => x.ID)))
                                       .ExecuteAsync(ct);

                await sessionService.RemoveGroupSessionsFromStudents(students, group, ct, transaction.Session);
            }

            groups = groups
                    .Modify(group => { group.CancelRemainingSessions(); })
                    .ToList();
            await groups.SaveAsync(transaction.Session, ct);

            await transaction.CommitAsync();

            return Ok(groups);
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
    [HttpPatch]
    [Route("hold")]
    public async Task<IActionResult> SetGroupsOnHold(CancellationToken ct,
        GroupHoldModel model)
    {
        var groups = await DB.Find<Group>()
                             .Match(f => f.In(x => x.ID, model.Groups))
                             .ExecuteAsync(ct);

        groups = groups
                .Modify(group => group.SetOnHold(model.HoldDate))
                .ToList();
        await groups.SaveAsync(cancellation: ct);

        return Ok(groups);
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}")]
    [HttpPatch]
    [Route("resume")]
    public async Task<IActionResult> ResumeGroups(CancellationToken ct,
        IEnumerable<string> groupIds)
    {
        var groups = await DB.Find<Group>()
                             .Match(f => f.In(x => x.ID, groupIds))
                             .ExecuteAsync(ct);

        groups = groups
                .Modify(group => group.ResumeSessions())
                .ToList();
        await groups.SaveAsync(cancellation: ct);

        return Ok(groups);
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.Teacher}")]
    [HttpPatch]
    [Route("{id}/next")]
    public async Task<IActionResult> StartNextGroupSession([FromServices] SessionService sessionService,
        string id,
        CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);
            var group = await DB.Find<Group>()
                                .OneAsync(id, ct);
            if (group.Teacher.ID != account?.PersonalId && account?.Role != AccountType.Admin)
                return Unauthorized(new
                {
                    Message = $"Teacher {account?.Name} is not" +
                              " allowed to start this group next session!"
                });

            if (!group.CanStartNextSession)
                return Unauthorized(new
                {
                    Message =
                        "Session can only be started between" +
                        $" '{group.NextSession?.Start.Date:f}' - '{group.NextSession?.End:f}'"
                });

            group.Sessions = group.Sessions
                                  .Modify(x =>
                                   {
                                       if (x.ID == group.NextSession?.ID) x.TeacherWasPresent = true;
                                   }).ToList();

            await sessionService.AddNextGroupSessionToStudents(group, ct, transaction.Session);

            await group.SaveAsync(cancellation: ct);

            await transaction.CommitAsync(ct);
            return Ok(new ResultResponse<Session, string>(group.NextSession,
                $"Session '{group.NextSession}' Started"));
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
    [Authorize(Roles =
        $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Teacher},{AccountRole.Parent},{AccountRole.Moderator}")]
    [HttpGet]
    [Route("{id}/session/{sessionId}/presence")]
    public async Task<IActionResult> GetSessionPresence(CancellationToken ct,
        string id,
        string sessionId)
    {
        var transaction = DB.Transaction();
        try
        {
            var group = await DB.Find<Group>(transaction.Session)
                                .OneAsync(id, ct);
            var session = group.Sessions.Single(x => x.ID == sessionId);
            if (sessionId == "next") session = group.NextSession;
            var studentsPresence = new List<StudentBasePresence>();

            foreach (var baseStudent in group.Students)
            {
                var model = baseStudent.Adapt<StudentBasePresence>();
                var student = await DB.Find<Student>(transaction.Session)
                                      .OneAsync(baseStudent.ID, ct);

                var studentSession = student.Sessions
                                            .FirstOrDefault(x => x.ID == session.ID,
                                                 new StudentSession(session.ID, false, session, group,
                                                     group.Formation));
                model.Present = studentSession.WasPresent;
                model.PresentOn = studentSession.WasPresentOn;
                try
                {
                    var paiments = await DB.Find<Invoice>(transaction.Session)
                                           .Match(x => x.Enabled && x.Type != InvoiceType.NotPaid &&
                                                       x.Student.ID == baseStudent.ID &&
                                                       x.Formation.ID == group.Formation.ID)
                                           .ExecuteAsync(ct);

                    var paiment = paiments?.First(x => DateTime.Now <= x.ExpirationDate && DateTime.Now >= x.StartDate);
                    model.Paid = paiment?.Type ?? InvoiceType.NotPaid;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex.Message);
                }


                studentsPresence.Add(model);
            }

            return Ok(studentsPresence);
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
    [HttpPatch]
    [Route("{id}/session/{sessionId}")]
    public async Task<IActionResult> MoveSession([FromServices] SessionService sessionService,
        CancellationToken ct,
        string id,
        string sessionId,
        SessionCreateModel model)
    {
        var group = await DB.Find<Group>()
                            .OneAsync(id, ct);
        var session = group.Sessions.Single(x => x.ID == sessionId);
        if (sessionId == "next") session = group.NextSession;
        if (session.Status != SessionStatus.Available)
            return Unauthorized(new { Message = "Only Available SessionModels can be moved!" });

        if (model.Start.Date < group.NextSession.Start)
            return BadRequest(new
            {
                Message = "Session cannot be in a date earlier than the group's next session date!"
            });

        var teacher = group.Teacher;
        if (model.Teacher != null)
        {
            var temp = await model.Teacher.ToEntityAsync(cancellation: ct);
            teacher = temp.ToBase();
        }

        group.Sessions = group.Sessions
                              .Modify(x =>
                               {
                                   if (x.ID == sessionId) x.Cancelled = true;
                               })
                              .Append(new Session
                               {
                                   Start = model.Start,
                                   End = model.End,
                                   OnHold = model.OnHold,
                                   Teacher = teacher,
                                   Formation = group.Formation
                               }).ToList();

        await group.SaveAsync(cancellation: ct);


        return Ok(group);
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}")]
    [HttpDelete]
    [Route("{id}/session/{sessionId}")]
    public async Task<IActionResult> CancelSession(CancellationToken ct,
        string id,
        string sessionId)
    {
        var group = await DB.Find<Group>()
                            .OneAsync(id, ct);
        var session = group.Sessions.Single(x => x.ID == sessionId);
        if (sessionId == "next") session = group.NextSession;
        if (session.Status != SessionStatus.Available || session.Status != SessionStatus.OnHold)
            return Unauthorized(new { Message = "Only Available SessionModels can be moved!" });


        group.Sessions = group.Sessions
                              .Modify(x =>
                               {
                                   if (x.ID == sessionId) x.Cancelled = true;
                               })
                              .ToList();

        await group.SaveAsync(cancellation: ct);


        return Ok(group);
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator},{AccountRole.Teacher}")]
    [HttpPatch]
    [Route("{id}/student")]
    public async Task<IActionResult> DeleteStudentFromGroup(
        [FromServices] SessionService sessionService,
        CancellationToken ct,
        string id,
        [FromBody] IEnumerable<string> studentIds)
    {
        var transaction = DB.Transaction();
        try
        {
            var group = await DB.Find<Group>(transaction.Session)
                                .OneAsync(id, ct);

            group.Students.RemoveAll(x => studentIds.Contains(x.ID));

            var students = await DB.Find<Student>(transaction.Session)
                                   .Match(f => f.In(x => x.ID, studentIds))
                                   .ExecuteAsync(ct);

            await sessionService.RemoveGroupSessionsFromStudents(students, group, ct, transaction.Session);

            await group.SaveAsync(cancellation: ct, session: transaction.Session);
            await transaction.CommitAsync();

            return Ok(group);
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
    [HttpPost]
    [Route("{id}/student")]
    public async Task<IActionResult> AddStudentToGroup(
        [FromServices] SessionService sessionService,
        CancellationToken ct,
        string id,
        [FromBody] IEnumerable<string> students)
    {
        var transaction = DB.Transaction();
        try
        {
            var group = await DB.Find<Group>(transaction.Session)
                                .OneAsync(id, ct);

            students = students.Except(group.Students.Select(x => x.ID));

            var newStudents = await DB.Find<Student>(transaction.Session)
                                      .Match(f => f.In(x => x.ID, students))
                                      .ExecuteAsync(ct);

            await sessionService.AddGroupSessionsToStudents(newStudents, group, ct, transaction.Session);

            group.Students.AddRange(newStudents.Select(x => x.ToBase()));

            await group.SaveAsync(cancellation: ct, session: transaction.Session);
            await transaction.CommitAsync();

            return Ok(group);
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
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator},{AccountRole.Teacher}")]
    [HttpPatch]
    [Route("{id}/student/presence")]
    public async Task<IActionResult> MarkPresenceForStudents(
        [FromServices] IHostEnvironment env,
        CancellationToken ct,
        string id,
        [FromBody] IEnumerable<StudentPresenceModel> models)
    {
        var transaction = DB.Transaction();
        try
        {
            var group = await DB.Find<Group>(transaction.Session)
                                .OneAsync(id, ct);

            if (env.IsProduction())
                if (DateTime.Now < group.NextSession.Start.Date || DateTime.Now > group.NextSession.End)
                    return Unauthorized(new
                    {
                        Message =
                            "Cannot change presence for this session, " +
                            "Session can only be started between " +
                            $"'{group.NextSession.Start.Date:f}' - '{group.NextSession.End:f}'"
                    });

            var students = group.Students
                                .IntersectBy(models.Select(x => x.Student), x => x.ID);
            var session = group.NextSession;

            var result = await DB.Find<Student>(transaction.Session)
                                 .Match(f => f.In(x => x.ID, students.Select(x => x.ID)))
                                 .ExecuteAsync(ct);

            result = result
                    .Modify(student =>
                     {
                         var presence = models
                                       .First(s => s.Student == student.ID).Present;

                         if (!student.Sessions.Any(s => s.ID == session.ID))
                             student.AddSession(presence, session, group);

                         student.ToggleSessionPresence(session.ID, presence);
                     }).ToList();

            await result.SaveAsync(transaction.Session, ct);

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

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles =
        $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Teacher},{AccountRole.Parent},{AccountRole.Moderator}")]
    [HttpGet]
    [ResponseCache(Duration = 10)]
    [Route("teachers")]
    public async Task<IActionResult> GetGroupsTeachers(CancellationToken ct)
    {
        var teachers = await DB.Fluent<Group>()
                               .Group(x => new
                                    {
                                        ID = x.Teacher.ID,
                                        Name = x.Teacher.Name
                                    },
                                    x => new
                                    {
                                        Count = x.Count()
                                    })
                               .Project(new BsonDocument()
                                {
                                    {
                                        "teacher", new BsonDocument
                                        {
                                            { "id", "$_id.ID" },
                                            { "name", "$_id.Name" },
                                            { "count", "$Count" }
                                        }
                                    }
                                })
                               .ToListAsync(ct);

        return Ok(teachers.Select(x => x["teacher"].DeserializeAsObject()));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles =
        $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Teacher},{AccountRole.Parent},{AccountRole.Moderator}")]
    [HttpGet]
    [ResponseCache(Duration = 10)]
    [Route("formations")]
    public async Task<IActionResult> GetGroupsFormations(CancellationToken ct)
    {
        var formations = await DB.Fluent<Group>()
                                 .Group(x => new { ID = x.Formation.ID, Name = x.Formation.Name },
                                      x => new { Count = x.Count() })
                                 .Project(new BsonDocument()
                                  {
                                      {
                                          "formation", new BsonDocument
                                          {
                                              { "id", "$_id.ID" },
                                              { "name", "$_id.Name" },
                                              { "count", "$Count" }
                                          }
                                      }
                                  })
                                 .ToListAsync(ct);

        return Ok(formations.Select(x => x["formation"].DeserializeAsObject()));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles =
        $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Teacher},{AccountRole.Parent},{AccountRole.Moderator}")]
    [HttpGet]
    [ResponseCache(Duration = 3600 * 24)]
    [Route("status")]
    public IActionResult GroupStatus(CancellationToken ct)
    {
        return Ok(Enum.GetNames<GroupStatus>());
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles =
        $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Teacher},{AccountRole.Moderator}")]
    [HttpGet]
    [Route("{id}/occurrences")]
    public async Task<IActionResult> GetGroupSessionsOccurrences(string id,
        CancellationToken ct)
    {
        var group = await DB.Find<Group>().MatchID(id).ExecuteSingleAsync(ct);
        var occurrences = group.Sessions
                               .Select(session => new OccurrenceModel()
                                {
                                    StartTime = session.Start.ToString("HH:mm"),
                                    EndTime = session.End.ToString("HH:mm"),
                                    Day = session.Start.DayOfWeek
                                })
                               .Distinct(new OccurrenceModelEqualityComparer())
                               .ToList();
        return Ok(occurrences);
    }

    ////--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles =
        $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Teacher},{AccountRole.Moderator}")]
    [HttpPatch]
    [Route("{id}/extend")]
    public async Task<IActionResult> ExtendGroup(string id,
        [FromBody] ScheduleUpdateModel model,
        [FromServices] SessionService sessionsService,
        CancellationToken ct)
    {
        var group = await DB.Find<Group>().MatchID(id).ExecuteSingleAsync(ct);
        group.Sessions ??= new List<Session>();

        group.Formation.DurationDays = model switch
        {
            { Until: not null } => (model.Until?.Date - group.Start.Date).Value.Days,
            _ => (int)(group.Formation.DurationDays + model.For)
        };
        var lastVersion = group.Sessions.MaxBy(x => x.Version).Version;
        var occurrences =
            model.Occurrences
            ?? group.Sessions
                    .Where(x => x.Version == lastVersion)
                    .Select(session => new OccurrenceModel()
                     {
                         StartTime = session.Start.ToString("HH:mm"),
                         EndTime = session.End.ToString("HH:mm"),
                         Day = session.Start.DayOfWeek
                     })
                    .Distinct(new OccurrenceModelEqualityComparer())
                    .ToList();

        var sessions = sessionsService
                      .CreateSessions(occurrences, group.Start, group.Formation.DurationDays)
                      .Select(x => x.Adapt<Session>());

        var newSessions = sessions
                         .Except(
                              model.Occurrences is { Count: 0 }
                                  ? group.Sessions
                                  : group.Sessions.Where(x => x.End <= DateTime.Now), new SessionEqualityComparer())
                         .Modify(x =>
                          {
                              x.Formation = group.Formation;
                              x.Teacher = group.Teacher;
                          })
                         .ToList();

        group.Sessions.AddRange(newSessions);

        await group.SaveAsync(cancellation: ct);
        return Ok();
    }
}