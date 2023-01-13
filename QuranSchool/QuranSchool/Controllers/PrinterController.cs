using Hangfire;
using Microsoft.AspNetCore.Mvc;
using QuranSchool.Services;

namespace QuranSchool.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class PrinterController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IIdentityService _identityService;
    private readonly ILogger<PrinterController> _logger;
    private IBackgroundJobClient _backgroundJobs;

    public PrinterController(
        ILogger<PrinterController> logger,
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
    [Route("")]
    public IActionResult GetPrinters([FromServices] PrintService printService)
    {
        return Ok(new { Printers = printService.GetPrinters(), Default = printService.GetDefaultPrinter() });
    }
}