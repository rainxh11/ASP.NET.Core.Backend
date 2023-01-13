using DicomServer.Helpers;
using DicomServerWorkList.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DicomServer.Controllers
{
    [ApiController]
    public class WorkerListController : ControllerBase
    {

        private readonly ILogger<WorkerListController> _logger;

        public WorkerListController(ILogger<WorkerListController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        [Route("dicomserver/worklist")]
        [Route("dicomserver/workerlist")]
        public async Task<ActionResult<IEnumerable<WorklistItem>>> Get()
        {
            var worklist = await StudyToWorklist.GetWorklist();
            if (worklist.Count() == 0)
            {
                return NotFound("Worklist Empty");
            }
            return Ok(new { results = worklist.Count(), data = worklist });
        }
    }
}
