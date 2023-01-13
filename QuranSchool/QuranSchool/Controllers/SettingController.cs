using Hangfire;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Entities;
using QuranSchool.Models;
using QuranSchool.Models.Request;
using QuranSchool.Services;

namespace QuranSchool.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class SettingController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IIdentityService _identityService;
    private readonly ILogger<SettingController> _logger;
    private IBackgroundJobClient _backgroundJobs;

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
        var info = await DB.Find<SchoolInfo>()
            .ExecuteSingleAsync(ct);
        return Ok(info);
    }

    [Authorize(Roles = $"{AccountRole.Admin}")]
    [HttpPut]
    [Route("schoolinfo")]
    public async Task<IActionResult> UpdateSchoolInfo(CancellationToken ct, [FromBody] SchoolInfoModel model)
    {
        var current = await DB.Find<SchoolInfo>().ExecuteFirstAsync(ct);
        var update = model.Adapt<SchoolInfo>();
        update.ID = current.ID;

        var info = await DB.UpdateAndGet<SchoolInfo>()
            .Match(x => true)
            .ModifyWith(update)
            .ExecuteAsync(ct);
        return Ok(info);
    }
}