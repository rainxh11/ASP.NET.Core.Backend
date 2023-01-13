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
using System.Linq.Dynamic.Core;
using Transaction = QuranSchool.Models.Transaction;

namespace QuranSchool.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class StudentController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IIdentityService _identityService;
    private readonly ILogger<StudentController> _logger;
    private readonly LoginInfoSaver _loginSaver;
    private readonly IHubContext<PrivateSchoolHub> _mailHub;
    private readonly AuthService _tokenService;
    private IBackgroundJobClient _backgroundJobs;
    private WebsocketNotificationService _websocketNotificationService;

    public StudentController(
        ILogger<StudentController> logger,
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
    [Authorize(Roles =
        $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator},{AccountRole.Teacher},{AccountRole.Student},{AccountRole.Parent}")]
    [HttpGet]
    [Route("{id}")]
    public async Task<IActionResult> GetStudent(string id, CancellationToken ct)
    {
        var account = await _identityService.GetCurrentAccount(HttpContext);

        var student = await DB.Find<Student>().OneAsync(id, ct);

        switch (account.Role)
        {
            case AccountType.Parent:
            case AccountType.Teacher:
                if (!student.Parents.Any(x => x.ID == account.PersonalId))
                    return Unauthorized();
                break;
            case AccountType.Student:
                if (student.ID != account.PersonalId)
                    return Unauthorized();
                break;
        }

        return Ok(student);
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles =
        $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator},{AccountRole.Teacher},{AccountRole.Student},{AccountRole.Parent}")]
    [HttpGet]
    [Route("{id}/group")]
    public async Task<IActionResult> GetStudentGroups(string id, CancellationToken ct)
    {
        var groups = await DB.Find<Group>()
            .Match(f => f.Eq(x => x.Cancelled, false) &
                        f.ElemMatch(x => x.Students,
                            new FilterDefinitionBuilder<StudentBase>().Eq(x => x.ID, id)))
            .ExecuteAsync(ct);

        return Ok(groups.OrderByDescending(x => x.CreatedOn));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles =
        $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator},{AccountRole.Teacher},{AccountRole.Student},{AccountRole.Parent}")]
    [HttpGet]
    [Route("{id}/group/{groupId}/session")]
    public async Task<IActionResult> GetStudentGroupSessions(string id, string groupId, CancellationToken ct)
    {
        var student = await DB.Find<Student>()
            .Match(f => f.Eq(x => x.ID, id) &
                        f.ElemMatch(x => x.Sessions,
                            new FilterDefinitionBuilder<StudentSession>().Eq(x => x.Group.ID, groupId)))
            .ExecuteFirstAsync(ct);

        return Ok(student.Sessions.Where(x => x.Group.ID == groupId).OrderBy(x => x.Start));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles =
        $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator},{AccountRole.Teacher},{AccountRole.Student},{AccountRole.Parent}")]
    [HttpGet]
    [Route("{id}/session")]
    public async Task<IActionResult> GetStudentGroupSessions(string id, CancellationToken ct)
    {
        var student = await DB.Find<Student>()
            .Match(f => f.Eq(x => x.ID, id))
            .ExecuteFirstAsync(ct);

        return Ok(student.Sessions.GroupBy(x => x.Group.ID, x => x));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles =
        $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator},{AccountRole.Teacher},{AccountRole.Student},{AccountRole.Parent}")]
    [HttpGet]
    [Route("")]
    public async Task<IActionResult> GetStudents(int page = 1, int limit = 10, string? sort = "CreatedOn",
        bool desc = true)
    {
        var account = await _identityService.GetCurrentAccount(HttpContext);

        var fluent = account.Role switch
        {
            var role when role == AccountType.Parent || role == AccountType.Teacher => DB.Fluent<Student>()
                .Match(f => f.ElemMatch(x => x.Parents,
                    new FilterDefinitionBuilder<Parent>().Eq(x => x.ID, account.PersonalId))),
            AccountType.Student => DB.Fluent<Student>().Match(f => f.Eq(x => x.ID, account.PersonalId)),
            _ => DB.Fluent<Student>()
        };

        var students = await DB.PagedSearch<Student>()
            .WithFluent(fluent)
            .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
            .PageNumber(page)
            .PageSize(limit)
            .ExecuteAsync();

        return Ok(new PagedResultResponse<IEnumerable<Student>>(
            students.Results,
            students.TotalCount,
            students.PageCount,
            limit,
            page));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}")]
    [HttpGet]
    [Route("debt")]
    public async Task<IActionResult> GetDebtStudents(CancellationToken ct, int page = 1,
        int limit = -1, string? sort = "Debt", bool desc = true)
    {
        var fluent = await DB.Fluent<Invoice>()
            .Match(f => f.Eq(x => x.Type, InvoiceType.Debt))
            //.Group(x => x.Student.ID, x => x)
            .ToListAsync(ct);

        var grouped = fluent.GroupBy(x => x.Student.ID, x => x).ToList();

        var debtStudents = grouped
            .Select(x => new StudentDebt(x.First().Student, x.Sum(z => z.LeftUnpaid)))
            .AsQueryable()
            .OrderBy(sort, desc ? "DESC" : "ASC")
            .Skip(page <= 0 ? 0 : page - 1)
            .Take(limit <= 0 ? int.MaxValue : limit)
            .ToList();
        var total = grouped.Sum(x => x.Sum(x => x.LeftUnpaid));

        return Ok(new DebtPagedResultResponse<IEnumerable<object>>(
            debtStudents,
            grouped.Count,
            page,
            limit,
            page,
            total
        ));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles =
        $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator},{AccountRole.Teacher},{AccountRole.Student},{AccountRole.Parent}")]
    [HttpGet]
    [Route("search")]
    public async Task<IActionResult> SearchStudents(string search = "", int page = 1, int limit = 10,
        string? sort = "CreatedOn", bool desc = true, bool lookup = false)
    {
        var account = await _identityService.GetCurrentAccount(HttpContext);

        var fluent = account.Role switch
        {
            var role when role == AccountType.Parent || role == AccountType.Teacher => DB.Fluent<Student>()
                .Match(f => f.ElemMatch(x => x.Parents,
                    new FilterDefinitionBuilder<Parent>().Eq(x => x.ID, account.PersonalId))),
            AccountType.Student => DB.Fluent<Student>().Match(f => f.Eq(x => x.ID, account.PersonalId)),
            _ => DB.Fluent<Student>()
        };


        search = search == "|" ? "" : search;
        search = $"/{search}/i";
        var searchQuery = fluent.Match(student => student.Regex(x => x.Name, search) |
                                                  student.Regex(x => x.Description, search) |
                                                  student.Regex(x => x.CreatedBy.Name, search) |
                                                  student.Regex(x => x.CreatedBy.Description,
                                                      search) |
                                                  student.Regex(x => x.CreatedBy.UserName, search) |
                                                  student.Regex(x => x.PhoneNumber, search) |
                                                  student.Regex(x => x.Address, search) |
                                                  student.ElemMatch(x => x.Parents,
                                                      new FilterDefinitionBuilder<Parent>().Regex(
                                                          p => p.Name, search)) |
                                                  student.ElemMatch(x => x.Parents,
                                                      new FilterDefinitionBuilder<Parent>().Regex(
                                                          p => p.Address, search)) |
                                                  student.ElemMatch(x => x.Parents,
                                                      new FilterDefinitionBuilder<Parent>().Regex(
                                                          p => p.CardID, search)) |
                                                  student.ElemMatch(x => x.Parents,
                                                      new FilterDefinitionBuilder<Parent>().Regex(
                                                          p => p.PhoneNumber, search))
        );

        var students = await DB.PagedSearch<Student>()
            .WithFluent(searchQuery)
            .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
            .PageNumber(lookup ? 1 : page)
            .PageSize(lookup ? int.MaxValue : limit)
            .ExecuteAsync();

        return !lookup
            ? Ok(new PagedResultResponse<IEnumerable<Student>>(
                students.Results,
                students.TotalCount,
                students.PageCount,
                limit,
                page))
            : (IActionResult)Ok(new ResultResponse<IEnumerable<object>, string>(students.Results.Select(x => new
            {
                x.ID,
                x.Name,
                x.PhoneNumber,
                x.DateOfBirth
            }), "Success"));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}")]
    [HttpGet]
    [Route("debt/search")]
    [ResponseCache(VaryByQueryKeys = new[] { "page", "limit", "sort", "desc", "search" }, Duration = 240)]
    public async Task<IActionResult> SearchDebtStudents(CancellationToken ct, string? search = "", int page = 1,
        int limit = -1, string? sort = "Debt", bool desc = true)
    {
        search = search == "|" ? "" : search;
        search = $"/{search}/i";
        var fluent = await DB.Fluent<Invoice>()
            .Match(f => f.Eq(x => x.Type, InvoiceType.Debt) &
                        (f.Regex(x => x.Student.Name, search) | f.Regex(x => x.Student.ID, search)))
            //.Group(x => x.Student.ID, x => x)
            .ToListAsync(ct);

        var grouped = fluent.GroupBy(x => x.Student.ID, x => x).ToList();


        var debtStudents = grouped
            .Select(x => new StudentDebt(x.First().Student, x.Sum(z => z.LeftUnpaid)))
            .AsQueryable()
            .OrderBy(sort, desc ? "DESC" : "ASC")
            .Skip(page <= 0 ? 0 : page - 1)
            .Take(limit <= 0 ? int.MaxValue : limit)
            .ToList();

        var total = grouped.Sum(x => x.Sum(x => x.LeftUnpaid));

        return Ok(new DebtPagedResultResponse<IEnumerable<object>>(
            debtStudents,
            grouped.Count,
            page,
            limit,
            page,
            total
        ));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}")]
    [HttpPost]
    [Route("")]
    public async Task<IActionResult> CreateStudent(CancellationToken ct, [FromBody] StudentModel model)
    {
        var transaction = DB.Transaction();
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);

            var student = model.Adapt<Student>();
            student.CreatedBy = account.ToBaseAccount();
            await student.InsertAsync(cancellation: ct, session: transaction.Session);
            if (model.Groups.Any())
            {
                var groups = await DB.Find<Group>(transaction.Session)
                    .ManyAsync(x => x.In(g => g.ID, model.Groups), ct);

                groups = groups
                    .Modify(x => x.Students.Add(student.ToBase()))
                    .ToList();
                await groups.SaveAsync(transaction.Session, ct);
            }

            await transaction.CommitAsync(ct);
            return Ok(new ResultResponse<Student, string>(student, "Student Created!"));
        }
        catch (Exception ex)
        {
            await transaction.AbortAsync();
            _logger.LogError(ex.Message);
            return BadRequest();
        }
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}")]
    [HttpPatch]
    [Route("{id}")]
    public async Task<IActionResult> UpdateStudent(string id, [FromBody] StudentModel model,
        CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            var bson = model.ToBsonDocument();
            var pipeline = new EmptyPipelineDefinition<BsonDocument>()
                .AppendStage($"{{ $set : {bson} }}", BsonDocumentSerializer.Instance);

            var update = await DB.Database(_config[""])
                .GetCollection<BsonDocument>(nameof(Student))
                .UpdateOneAsync(transaction.Session,
                    new { _id = ObjectId.Parse(id) }.ToBsonDocument(),
                    pipeline,
                    cancellationToken: ct);

            var student = await DB.Find<Student>(transaction.Session)
                .MatchID(id)
                .ExecuteSingleAsync(ct);
            await transaction.CommitAsync();

            return Ok(new ResultResponse<Student, string>(student, "Student Updated!"));
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
    public async Task<IActionResult> DeleteStudent(string id)
    {
        var delete = await DB.Collection<Student>()
            .DeleteOneAsync(x => x.ID == id);
        return !delete.IsAcknowledged
            ? BadRequest(new MessageResponse<DeleteResult>(delete))
            : Ok(new MessageResponse<DeleteResult>(delete));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles =
        $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator},{AccountRole.Teacher},{AccountRole.Student},{AccountRole.Parent}")]
    [HttpGet]
    [Route("{id}/invoice")]
    public async Task<IActionResult> GetStudentInvoices(string id, CancellationToken ct, int page = 1,
        int limit = -1, string? sort = "CreatedOn", bool desc = true)
    {
        var invoices = await DB.PagedSearch<Invoice>()
            .Match(x => x.Student.ID == id)
            .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
            .PageNumber(page)
            .PageSize(limit < 0 ? int.MaxValue : limit)
            .ExecuteAsync(ct);

        return Ok(new PagedResultResponse<IEnumerable<Invoice>>(
            invoices.Results,
            invoices.TotalCount,
            invoices.PageCount,
            limit,
            page));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles =
        $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator},{AccountRole.Teacher},{AccountRole.Student},{AccountRole.Parent}")]
    [HttpGet]
    [Route("{id}/debt")]
    public async Task<IActionResult> GetStudentDebt(string id, CancellationToken ct)
    {
        var invoices = await DB.Find<Invoice>()
            .Match(x => x.Student.ID == id && x.Enabled)
            .ExecuteAsync(ct);

        var debt = invoices.Sum(x => x.LeftUnpaid);
        var unpaidInvoices = invoices.Where(x => x.LeftUnpaid > 0).ToList();

        var from = unpaidInvoices
            .OrderBy(x => x.Transactions
                .Where(z => z.Type == TransactionType.Debt)
                .OrderBy(z => z.CreatedOn)
                .First()
                .CreatedOn
            );
        var to = unpaidInvoices
            .OrderByDescending(x => x.Transactions
                .Where(z => z.Type == TransactionType.Debt)
                .OrderBy(z => z.CreatedOn)
                .First()
                .CreatedOn
            );


        return Ok(new { TotalDebt = debt, From = from, To = to });
    }

    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}")]
    [HttpPost]
    [Route("{id}/debt")]
    public async Task<IActionResult> PayStudentDebt(string id, [FromBody] PaymentModel model, CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);
            var invoices = await DB.Find<Invoice>(transaction.Session)
                .Match(x => x.Student.ID == id && x.Enabled)
                .ExecuteAsync(ct);

            var totalDebt = invoices.Sum(x => x.LeftUnpaid);
            if (totalDebt <= 0)
                return BadRequest(new { Message = "Student has 0 debt!" });

            model.Paid = model.Paid.ToDouble() > totalDebt
                ? totalDebt
                : model.Paid.ToDouble();

            model.Discount = model.Discount.ToDouble() > totalDebt - model.Paid.ToDouble()
                ? totalDebt - model.Paid.ToDouble()
                : model.Discount.ToDouble();

            var invoiceCount = invoices.Count(x => x.LeftUnpaid > 0);


            foreach (var invoice in invoices.Where(x => x.LeftUnpaid > 0))
            {
                var discount = model.Discount >= invoice.LeftUnpaid ? invoice.LeftUnpaid : model.Discount;
                var paid = model.Paid >= invoice.LeftUnpaid - discount
                    ? invoice.LeftUnpaid - discount
                    : model.Paid;

                var transactions = Enumerable
                    .Empty<Transaction>()
                    .Append(new Transaction(TransactionType.Payment, paid.ToDouble(),
                        account.ToBaseAccount()))
                    .Append(new Transaction(TransactionType.Discount, discount.ToDouble(),
                        account.ToBaseAccount()))
                    .Where(x => x.Amount > 0)
                    .ToList();

                var leftUnpaid = invoice.LeftUnpaid - transactions
                    .Where(x => x.Enabled && x.Type == TransactionType.Payment)
                    .Sum(x => x.Amount);

                if (transactions.Count == 0)
                {
                    invoice.Type = invoice.LeftUnpaid > 0 ? InvoiceType.Debt : InvoiceType.Paid;
                    await invoice.SaveAsync(transaction.Session, ct);
                }
                else
                {
                    await transactions.SaveAsync(transaction.Session, ct);
                    await invoice.Transactions.AddAsync(transactions, transaction.Session, ct);


                    invoice.Type = leftUnpaid > 0 ? InvoiceType.Debt : InvoiceType.Paid;

                    await invoice.SaveAsync(transaction.Session, ct);
                }
            }

            await transaction.CommitAsync(ct);
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
        $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator},{AccountRole.Teacher},{AccountRole.Student},{AccountRole.Parent}")]
    [HttpGet]
    [Route("{id}/invoice/search")]
    public async Task<IActionResult> SearchStudentInvoices(string id, CancellationToken ct, string search = "",
        int page = 1, int limit = -1, string? sort = "CreatedOn", bool desc = true)
    {
        search = $"/{search}/ig";
        var searchQuery = DB.Fluent<Invoice>()
            .Match(x => x.Student.ID == id)
            .Match(formation => formation.Regex(x => x.Student.ID, search) |
                                formation.Regex(x => x.Student.Name, search) |
                                formation.Regex(x => x.Formation.Name, search) |
                                formation.Regex(x => x.Formation.ID, search) |
                                formation.Regex(x => x.Formation.Price, search) |
                                formation.Regex(x => x.CreatedBy.Description, search) |
                                formation.Regex(x => x.CreatedBy.Name, search) |
                                formation.Regex(x => x.CreatedBy.UserName, search) |
                                formation.Regex(x => x.CreatedBy.ID, search));


        var invoices = await DB.PagedSearch<Invoice>()
            .WithFluent(searchQuery)
            .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
            .PageNumber(page)
            .PageSize(limit < 0 ? int.MaxValue : limit)
            .ExecuteAsync(ct);

        return Ok(new PagedResultResponse<IEnumerable<Invoice>>(
            invoices.Results,
            invoices.TotalCount,
            invoices.PageCount,
            limit,
            page));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Moderator}")]
    [HttpGet]
    [Route("parent")]
    public async Task<IActionResult> SearchParents(CancellationToken ct, string? search)
    {
        var searchRegex = search is null ? "|" : $"/{search}/ig";
        var parents = await DB.Fluent<Student>()
            .Match(student =>
                student.ElemMatch(x => x.Parents,
                    new FilterDefinitionBuilder<Parent>().Regex(p => p.Name, searchRegex)) |
                student.ElemMatch(x => x.Parents,
                    new FilterDefinitionBuilder<Parent>().Regex(p => p.Address, searchRegex)) |
                student.ElemMatch(x => x.Parents,
                    new FilterDefinitionBuilder<Parent>().Regex(p => p.CardID, searchRegex)) |
                student.ElemMatch(x => x.Parents,
                    new FilterDefinitionBuilder<Parent>().Regex(p => p.PhoneNumber, searchRegex)))
            .ToListAsync(ct);

        var result = parents
            .SelectMany(x => x.Parents)
            .Distinct();

        var teachers = await DB.Find<Teacher>().ExecuteAsync(ct);

        result = teachers.Select(x => x.ToParent()).Concat(result).Distinct();

        return Ok(new ResultResponse<IEnumerable<Parent>, int>(
            result,
            result.Count()
        ));
    }

    //--------------------------------------------------------------------------------------------------------------//
    [Authorize]
    [HttpPost]
    [Route("{id}/avatar")]
    public async Task<IActionResult> UpdateStudentAvatar(string id, [FromForm] IFormFile file, CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            if (!file.ContentType.Contains("image"))
                return BadRequest(new MessageResponse<string>(
                    $"Content of type: '{file.ContentType}' not allowed! Only image type is allowed!"));


            var student = await DB.Find<Student>(transaction.Session)
                .OneAsync(id, ct);

            var account = await _identityService.GetCurrentAccount(HttpContext);

            switch (account.Role)
            {
                case AccountType.Teacher:
                case AccountType.Parent:
                    if (!student.Parents.Any(x => x.ID == account.PersonalId))
                        return Unauthorized();
                    break;
                case AccountType.Student:
                    if (student.ID != account.PersonalId)
                        return Unauthorized();
                    break;
            }

            if (student.Avatar != null)
                await student.Avatar.DeleteAsync(transaction.Session, ct);
            var avatar = new Avatar
            {
                PersonalID = student.ID
            };

            await avatar.SaveAsync(transaction.Session, ct);
            await using (var stream = await ImageHelper.EncodeWebp(file, ct))
            {
                await avatar.Data.UploadAsync(stream, cancellation: ct, session: transaction.Session);
            }

            var uploaded = await DB.Find<Avatar>(transaction.Session).OneAsync(avatar.ID);
            student.Avatar = uploaded;
            await student.SaveAsync(transaction.Session, ct);
            await transaction.CommitAsync(ct);

            return Ok(new ResultResponse<Student, string>(student, "Avatar updated!"));
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
    [Route("{id}/avatar")]
    public async Task<IActionResult> DeleteStudentAvatar(string id, CancellationToken ct)
    {
        var transaction = DB.Transaction();
        try
        {
            var student = await DB.Find<Student>(transaction.Session)
                .OneAsync(id, ct);

            var account = await _identityService.GetCurrentAccount(HttpContext);

            switch (account.Role)
            {
                case AccountType.Teacher:
                case AccountType.Parent:
                    if (!student.Parents.Any(x => x.ID == account.PersonalId))
                        return Unauthorized();
                    break;
                case AccountType.Student:
                    if (student.ID != account.PersonalId)
                        return Unauthorized();
                    break;
            }

            if (student == null || student.Avatar == null)
                return NotFound();

            student.Avatar = null;
            await student.SaveAsync(transaction.Session, ct);
            await transaction.CommitAsync(ct);
            var result = await DB.DeleteAsync<Avatar>(x => x.PersonalID == student.ID, transaction.Session, ct);

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
    public async Task<IActionResult> GetStudentAvatar([FromServices] IWebHostEnvironment webHost, CancellationToken ct,
        string id)
    {
        try
        {
            var student = await DB.Find<Student>()
                .OneAsync(id, ct);
            var account = await _identityService.GetCurrentAccount(HttpContext);

            switch (account.Role)
            {
                case AccountType.Teacher:
                case AccountType.Parent:
                    if (!student.Parents.Any(x => x.ID == account.PersonalId))
                        return Unauthorized();
                    break;
                case AccountType.Student:
                    if (student.ID != account.PersonalId)
                        return Unauthorized();
                    break;
            }

            var avatar = await DB.Find<Avatar>().MatchID(student.Avatar.ID).ExecuteFirstAsync(ct);
            if (avatar is null)
                return File(new byte[] { }, "");

            var stream = new MemoryStream();
            await avatar.Data.DownloadAsync(stream, cancellation: ct).ConfigureAwait(false);
            stream.Position = 0;
            return File(stream, "image/webp");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            var origin = webHost.IsProduction() ? "/" : HttpContext.Request.Headers.Referer.First();
            return Redirect("/images/avatars/generic.jpg");
        }
    }
}