using Hangfire;
using HashidsNet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Entities;
using QuranSchool.Helpers;
using QuranSchool.Hubs;
using QuranSchool.Models;
using QuranSchool.Services;
using HashCode = Invio.Hashing.HashCode;

namespace QuranSchool.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class EventController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IIdentityService _identityService;
    private readonly ILogger<EventController> _logger;
    private readonly LoginInfoSaver _loginSaver;
    private readonly IHubContext<PrivateSchoolHub> _mailHub;
    private readonly ParentService _parentService;
    private readonly AuthService _tokenService;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly WebsocketNotificationService _websocketNotificationService;

    public EventController(
        ILogger<EventController> logger,
        IIdentityService identityService,
        AuthService tokenService,
        IHubContext<PrivateSchoolHub> mailHub,
        IBackgroundJobClient bgJobs,
        WebsocketNotificationService nservice,
        LoginInfoSaver loginSaver,
        ParentService parentService,
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
        _parentService = parentService;
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize]
    [HttpGet]
    [Route("")]
    public async Task<IActionResult> GetTimeTable([FromServices] SessionService sessionService, CancellationToken ct,
        DateTime? start = null,
        DateTime? end = null)
    {
        var account = await _identityService.GetCurrentAccount(HttpContext);

        Func<FilterDefinitionBuilder<Group>, FilterDefinition<Group>> filter = f => f.Ne(x => x.Cancelled, true);

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
                filter = f => f.Ne(x => x.Cancelled, true) &
                              f.ElemMatch(x => x.Students,
                                  new FilterDefinitionBuilder<StudentBase>()
                                      .Eq(s => s.ID, account.PersonalId));
                break;
            case AccountType.Parent:
                var students = await _parentService.GetParentStudents(account.PersonalId, ct);
                filter = f => f.Ne(x => x.Cancelled, true) &
                              (f.Eq(x => x.Teacher.ID, account.PersonalId) |
                               f.ElemMatch(x => x.Students,
                                   new FilterDefinitionBuilder<StudentBase>()
                                       .In(s => s.ID, students.Select(student => student.ID))));
                break;
            case AccountType.Teacher:
                var teacherStudents = await _parentService.GetParentStudents(account.PersonalId, ct);
                filter = f => f.Ne(x => x.Cancelled, true) &
                              (f.Eq(x => x.Teacher.ID, account.PersonalId) |
                               f.ElemMatch(x => x.Students,
                                   new FilterDefinitionBuilder<StudentBase>()
                                       .In(s => s.ID, teacherStudents.Select(student => student.ID))) |
                               f.ElemMatch(x => x.Sessions,
                                   new FilterDefinitionBuilder<Session>()
                                       .Eq(s => s.Teacher.ID, account.PersonalId)));
                break;
        }

        var fluent = await sessionService.RetrieveSessions(x => x.Match(filter), start, end, ct);

        var result = fluent
            .Select(x => BsonSerializer.Deserialize<dynamic>(x))
            .Modify(x =>
            {
                var id = HashCode.From(x._id, x.Start);
                x.ID = new Hashids().Encode(Math.Abs(id));
            })
            .ToList();

        return Ok(result);
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize]
    [HttpGet]
    [Route("account/{id}")]
    public async Task<IActionResult> GetTimeTable([FromServices] SessionService sessionService, string id,
        CancellationToken ct,
        DateTime? start = null,
        DateTime? end = null)
    {
        var account = await DB.Find<Account>().OneAsync(id, ct);

        Func<FilterDefinitionBuilder<Group>, FilterDefinition<Group>> filter = f => f.Ne(x => x.Cancelled, true);

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
                filter = f => f.Ne(x => x.Cancelled, true) &
                              f.ElemMatch(x => x.Students,
                                  new FilterDefinitionBuilder<StudentBase>()
                                      .Eq(s => s.ID, account.PersonalId));
                break;
            case AccountType.Parent:
                var students = await _parentService.GetParentStudents(account.PersonalId, ct);
                filter = f => f.Ne(x => x.Cancelled, true) &
                              (f.Eq(x => x.Teacher.ID, account.PersonalId) |
                               f.ElemMatch(x => x.Students,
                                   new FilterDefinitionBuilder<StudentBase>()
                                       .In(s => s.ID, students.Select(student => student.ID))));
                break;
            case AccountType.Teacher:
                var teacherStudents = await _parentService.GetParentStudents(account.PersonalId, ct);
                filter = f => f.Ne(x => x.Cancelled, true) &
                              (f.Eq(x => x.Teacher.ID, account.PersonalId) |
                               f.ElemMatch(x => x.Students,
                                   new FilterDefinitionBuilder<StudentBase>()
                                       .In(s => s.ID, teacherStudents.Select(student => student.ID))) |
                               f.ElemMatch(x => x.Sessions,
                                   new FilterDefinitionBuilder<Session>()
                                       .Eq(s => s.Teacher.ID, account.PersonalId)));
                break;
        }

        var fluent = await sessionService.RetrieveSessions(x => x.Match(filter), start, end, ct);

        var result = fluent
            .Select(x => BsonSerializer.Deserialize<dynamic>(x))
            .Modify(x =>
            {
                var id = HashCode.From(x._id, x.Start);
                x.ID = new Hashids().Encode(Math.Abs(id));
            })
            .ToList();


        return Ok(result);
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles =
        $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator},{AccountRole.Student},{AccountRole.Teacher},{AccountRole.Parent}")]
    [HttpGet]
    [Route("group/{id}")]
    public async Task<IActionResult> GetGroupTimeTable([FromServices] SessionService sessionService, string id,
        CancellationToken ct,
        DateTime? start = null,
        DateTime? end = null)
    {
        var fluent = await sessionService
            .RetrieveSessions(x => x
                    .Match(f => f.Ne(g => g.Cancelled, true) & f.Eq(g => g.ID, id)),
                start,
                end,
                ct);

        var result = fluent
            .Select(x => BsonSerializer.Deserialize<dynamic>(x))
            .Modify(x =>
            {
                var id = HashCode.From(x._id, x.Start);
                x.ID = new Hashids().Encode(Math.Abs(id));
            })
            .ToList();


        return Ok(result);
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles =
        $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator},{AccountRole.Student},{AccountRole.Teacher},{AccountRole.Parent}")]
    [HttpGet]
    [Route("student/{id}")]
    public async Task<IActionResult> GetStudentTimeTable([FromServices] SessionService sessionService, string id,
        CancellationToken ct,
        DateTime? start = null,
        DateTime? end = null)
    {
        var fluent = await sessionService
            .RetrieveSessions(
                x => x.Match(f => f.Ne(x => x.Cancelled, true) & f.ElemMatch(x => x.Students,
                    new FilterDefinitionBuilder<StudentBase>().Eq(s => s.ID, id))),
                start,
                end,
                ct);

        var result = fluent
            .Select(x => BsonSerializer.Deserialize<dynamic>(x))
            .Modify(x =>
            {
                var id = HashCode.From(x._id, x.Start);
                x.ID = new Hashids().Encode(Math.Abs(id));
            })
            .ToList();


        return Ok(result);
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles =
        $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator},{AccountRole.Teacher}")]
    [HttpGet]
    [Route("teacher/{id}")]
    public async Task<IActionResult> GetTeacherTimeTable([FromServices] SessionService sessionService, string id,
        CancellationToken ct,
        DateTime? start = null,
        DateTime? end = null)
    {
        var fluent = await sessionService
            .RetrieveSessions(x => x.Match(f => f.Ne(x => x.Cancelled, true) &
                                                (f.Eq(x => x.Teacher.ID, id) |
                                                 f.ElemMatch(x => x.Sessions,
                                                     new FilterDefinitionBuilder<Session>()
                                                         .Eq(s => s.Teacher.ID, id)))),
                start,
                end,
                ct);

        var result = fluent
            .Select(x => BsonSerializer.Deserialize<dynamic>(x))
            .Modify(x =>
            {
                var id = HashCode.From(x._id, x.Start);
                x.ID = new Hashids().Encode(Math.Abs(id));
            })
            .ToList();


        return Ok(result);
    }
}