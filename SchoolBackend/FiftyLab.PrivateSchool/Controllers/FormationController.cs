using FiftyLab.PrivateSchool.Helpers;
using FiftyLab.PrivateSchool.Hubs;
using FiftyLab.PrivateSchool.Models.Request;
using FiftyLab.PrivateSchool.Response;
using FiftyLab.PrivateSchool.Services;
using Hangfire;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Entities;

namespace FiftyLab.PrivateSchool.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class FormationController : Controller
    {
        private readonly ILogger<FormationController> _logger;
        private readonly IHubContext<PrivateSchoolHub> _mailHub;
        private IBackgroundJobClient _backgroundJobs;
        private readonly IConfiguration _config;
        private readonly IIdentityService _identityService;
        private readonly LoginInfoSaver _loginSaver;
        private NotificationService _notificationService;
        private readonly ITokenService _tokenService;

        public FormationController(
            ILogger<FormationController> logger,
            IIdentityService identityService,
            ITokenService tokenService,
            IHubContext<PrivateSchoolHub> mailHub,
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
            _notificationService = nservice;
            _loginSaver = loginSaver;
        }

        //--------------------------------------------------------------------------------------------------------------//
        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Secretary}")]
        [HttpGet]
        [Route("")]
        public async Task<IActionResult> GetFormations(CancellationToken ct, int page = 1, int limit = -1,
            string? sort = "CreatedOn", bool desc = true)
        {
            try
            {
                var students = await DB.PagedSearch<Formation>()
                    .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
                    .PageNumber(page)
                    .PageSize(limit < 0 ? Int32.MaxValue : limit)
                    .ExecuteAsync(ct);

                return Ok(new PagedResultResponse<IEnumerable<Formation>>(
                    students.Results,
                    students.TotalCount,
                    students.PageCount,
                    limit,
                    page));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }

        //--------------------------------------------------------------------------------------------------------------//
        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Secretary}")]
        [HttpGet]
        [Route("search")]
        public async Task<IActionResult> SearchFormations(CancellationToken ct, string search = "", int page = 1,
            int limit = -1, string? sort = "CreatedOn", bool desc = true)
        {
            try
            {
                search = $"/{search}/ig";
                var searchQuery = DB.Fluent<Formation>().Match(formation => formation.Regex(x => x.Name, search) |
                                                                            formation.Regex(x => x.Price, search) |
                                                                            formation.Regex(x => x.ID, search));

                var students = await DB.PagedSearch<Formation>()
                    .WithFluent(searchQuery)
                    .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
                    .PageNumber(page)
                    .PageSize(limit < 0 ? Int32.MaxValue : limit)
                    .ExecuteAsync(ct);

                return Ok(new PagedResultResponse<IEnumerable<Formation>>(
                    students.Results,
                    students.TotalCount,
                    students.PageCount,
                    limit,
                    page));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }

        //--------------------------------------------------------------------------------------------------------------//
        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Secretary}")]
        [HttpPost]
        [Route("")]
        public async Task<IActionResult> CreateFormation([FromBody] FormationModel model, CancellationToken ct)
        {
            try
            {
                var account = await _identityService.GetCurrentAccount(HttpContext);

                var formation = model.Adapt<Formation>();
                formation.CreatedBy = account.ToBaseAccount();
                await formation.InsertAsync(cancellation: ct);

                return Ok(new ResultResponse<Formation, string>(formation, "Formation Created!"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }

        //--------------------------------------------------------------------------------------------------------------//
        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Secretary}")]
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

                var update = await DB.Database("CrecheDb")
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
        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Secretary}")]
        [HttpDelete]
        [Route("{id}")]
        public async Task<IActionResult> DeleteFormation(string id, CancellationToken ct)
        {
            try
            {
                var delete = await DB.Collection<Formation>()
                    .DeleteOneAsync(x => x.ID == id, cancellationToken: ct);
                if (!delete.IsAcknowledged)
                    return BadRequest(new MessageResponse<DeleteResult>(delete));

                return Ok(new MessageResponse<DeleteResult>(delete));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }
    }
}