using FiftyLab.PrivateSchool.Helpers;
using FiftyLab.PrivateSchool.Hubs;
using FiftyLab.PrivateSchool.Services;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace FiftyLab.PrivateSchool.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class PrinterController : ControllerBase
    {
        private readonly ILogger<PrinterController> _logger;
        private IBackgroundJobClient _backgroundJobs;
        private readonly IConfiguration _config;
        private readonly IIdentityService _identityService;

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
            try
            {
                return Ok(new { Printers = printService.GetPrinters(), Default = printService.GetDefaultPrinter() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }
    }
}