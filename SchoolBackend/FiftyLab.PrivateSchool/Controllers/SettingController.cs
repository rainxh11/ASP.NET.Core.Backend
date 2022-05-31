using FiftyLab.PrivateSchool.Models;
using FiftyLab.PrivateSchool.Models.Request;
using FiftyLab.PrivateSchool.Services;
using Hangfire;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Entities;

namespace FiftyLab.PrivateSchool.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class SettingController : ControllerBase
{
    private readonly ILogger<SettingController> _logger;
    private IBackgroundJobClient _backgroundJobs;
    private readonly IConfiguration _config;
    private readonly IIdentityService _identityService;

    public SettingController(
        ILogger<SettingController> logger,
        IIdentityService identityService,
        IBackgroundJobClient bgJobs,
        IConfiguration config)
    {
        _logger = logger;
        _config = config;
        _identityService = identityService;
        _backgroundJobs = bgJobs;
    }

    [HttpGet]
    [Route("schoolinfo")]
    public async Task<IActionResult> GetSchoolInfo(CancellationToken ct)
    {
        try
        {
            var info = await DB.Find<SchoolInfo>()
                .ExecuteSingleAsync(ct);
            return Ok(info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return BadRequest();
        }
    }

    [Authorize(Roles = $"{AccountRole.Admin}")]
    [HttpPut]
    [Route("schoolinfo")]
    public async Task<IActionResult> UpdateSchoolInfo(CancellationToken ct, [FromBody] SchoolInfoModel model)
    {
        try
        {
            var info = await DB.UpdateAndGet<SchoolInfo>()
                .Match(x => true)
                .ModifyWith(model.Adapt<SchoolInfo>())
                .ExecuteAsync(ct);
            return Ok(info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return BadRequest();
        }
    }
}