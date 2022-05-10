using FiftyLab.PrivateSchool.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Entities;

namespace FiftyLab.PrivateSchool.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class TransactionController : Controller
    {

        private readonly ILogger<TransactionController> _logger;
        private readonly IConfiguration _config;
        private readonly IIdentityService _identityService;

        public TransactionController(
            ILogger<TransactionController> logger,
            IIdentityService identityService,
            ITokenService tokenService,
            IConfiguration config)
        {
            _logger = logger;
            _config = config;
            _identityService = identityService;
        }
        //--------------------------------------------------------------------------------------------------//
        [Authorize(Roles = $"{AccountRole.Admin}")]
        [HttpPatch]
        [Route("{id}/cancel")]
        public async Task<IActionResult> CancelTransaction(string id, CancellationToken ct)
        {
            try
            {
                var account = await _identityService.GetCurrentAccount(HttpContext);
                var alreadyCancelled = await DB.Find<Transaction>()
                    .Match(f => f.Eq(x => x.ID, id) & f.Eq(x => x.Enabled, false))
                    .ExecuteAnyAsync(ct);
                if (alreadyCancelled)
                    return BadRequest(new MessageResponse<string>($"Transaction '{id}' is already cancelled!"));

                var update = await DB.UpdateAndGet<Transaction>()
                    .MatchID(id)
                    .Modify(x => x.CancelledBy, account.ToBaseAccount())
                    .Modify(x => x.CancelledOn, DateTime.Now)
                    .Modify(x => x.Enabled, false)
                    .ExecuteAsync(ct);

                return Ok(new ResultResponse<Transaction, string>(update, $"Transaction '{update.ID}' cancelled!"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }
    }
}
