using Akavache;
using DicomServer.Helper;
using DicomServer.Models;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Reactive.Linq;
using WebP.Net;
using System.Drawing;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Linq.Expressions;
using System.ComponentModel;
using System.Linq.Dynamic.Core;
using System.Linq.Dynamic;
using DicomServer.Helpers;
using System.Text;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net;

namespace DicomServer.Controllers;

public class PACSController : Controller
{
    public static string FirstCharToUpper(string input)
    {
        if (String.IsNullOrEmpty(input))
            return String.Empty;
        return input.First().ToString().ToUpper() + input.Substring(1);
    }
    private readonly ILogger<PACSController> _logger;
    private readonly IOrthancApi _orthanc;

    public PACSController(ILogger<PACSController> logger, IOrthancApi orthanc)
    {
        _logger = logger;
        _orthanc = orthanc;
    }
    [HttpGet]
    [Route("dicomserver/studies/search")]
    public async Task<IActionResult> SearchStudies(string search = "", int page = 1, int limit = 10, string sort = "StudyDate", bool desc = true)
    {
        try
        {
            page = page <= 0 ? 1 : page - 1;
            sort = FirstCharToUpper(sort.Replace("-","").Trim());
            var orderBy = desc ? $"{sort} desc" : sort;


            var studies = await CacheHelper.GetStudies();

            var filtered = studies
                .Where(x =>
                string.IsNullOrEmpty(search) ||
                x.Description.Contains(search, StringComparison.InvariantCultureIgnoreCase) ||
                x.PatientName.Contains(search, StringComparison.InvariantCultureIgnoreCase) ||
                x.StudyInstanceUID.Contains(search, StringComparison.InvariantCultureIgnoreCase));

            var count = filtered.Count();


            studies = filtered
                .AsQueryable()
                .OrderBy(orderBy)
                .ToList()
                .Skip(page * limit)
                .Take(limit)
                .ToList();
                

            return Ok(new { results = count, data = studies });
        }
        catch (Exception ex)
        {
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }
    [HttpGet]
    [Route("dicomserver/studies")]
    public async Task<ActionResult<IEnumerable<RisImagingStudy>>> GetStudies()
    {
        try
        {

            var studies = await CacheHelper.GetStudies();

            return Ok(studies);
        }
        catch(Exception ex)
        {
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet]
    [Route("dicomserver/studies/{id}")]
    public async Task<ActionResult<RisImagingStudy>> GetStudy(string id)
    {
        try
        {
            var config = ConfigHelper.GetConfig();

            var orthancStudy = await _orthanc.GetStudy(id);
            var instances = await _orthanc.GetStudyInstances(id);

            var result = new RisImagingStudy()
            {
                Id = id,
                Description = orthancStudy.MainDicomTags.StudyDescription?.Trim().ToUpper(),
                StudyDate = DateTime.ParseExact(orthancStudy.MainDicomTags.StudyDate, "yyyyMMdd", CultureInfo.InvariantCulture),
                Instances = instances.Select(x => x.ID).ToList(),
                PatientId = orthancStudy.PatientMainDicomTags.PatientID,
                PatientName = orthancStudy.PatientMainDicomTags.PatientName.Replace("^", " ").Trim().ToUpper(),
                StudyInstanceUID = orthancStudy.MainDicomTags.StudyInstanceUID
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }

    [HttpDelete]
    [Route("dicomserver/studies/{id}")]
    public async Task<ActionResult> DeleteStudy(string id)
    {
        try
        {
            var response = await _orthanc.DeleteStudy(id);

            if (response.IsSuccessStatusCode)
            {
                await Task.WhenAll(CacheHelper.RemoveStudyFromImageCache(id),
                    CacheHelper.RemoveStudyFromCache(id));

                return Ok($"Study: {id} Deleted Successfully.");
            }
            else
            {
                return new StatusCodeResult(((int)response.StatusCode));
            }

        }
        catch
        {
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet]
    [Route("dicomserver/instances/{id}/image")]
    public async Task<ActionResult> GetInstanceImage(string id)
    {
        try
        {
            var config = ConfigHelper.GetConfig();
            var content = config.ImageCache.WebpEnabled && config.ImageCache.Enabled ? "image/webp" : "image/jpeg";
            var cacheid = config.ImageCache.WebpEnabled ? $"{id}_webp" : $"{id}";

            var image = new byte[] { };
            if (config.ImageCache.Enabled)
            {
                try
                {
                    image = await BlobCache.LocalMachine.GetObject<byte[]>(cacheid);
                }
                catch
                {
                    if (config.ImageCache.WebpEnabled)
                    {
                        var instanceImage = await _orthanc.GetInstanceRenderedPng(id);
                        var result = await instanceImage.Content.ReadAsByteArrayAsync();
                        var stream = new MemoryStream(result);
                        var bitmap = new Bitmap(stream);

                        var webp = new WebPObject(bitmap);
                        image = webp.GetWebPLossy(config.ImageCache.WebpQuality, true);

                        bitmap.Dispose();
                        await stream.DisposeAsync();
                    }
                    else
                    {
                        var instanceImage = await _orthanc.GetInstanceRenderedJpeg(id, config.ImageCache.JpegQuality);
                        image = await instanceImage.Content.ReadAsByteArrayAsync();
                    }         
                   
                    await BlobCache.LocalMachine.InsertObject<byte[]>(cacheid, image, new TimeSpan(config.ImageCache.ExpirationDays,0,0,0));
                }
            }
            else
            {
                var instanceImage = await _orthanc.GetInstanceRenderedJpeg(id, config.ImageCache.JpegQuality);
                image = await instanceImage.Content.ReadAsByteArrayAsync();
            }

            return File(image, content);
        }
        catch (Exception ex)
        {
            return File(new byte[] { }, "image/jpeg");
        }
    }

    [HttpGet]
    [Route("dicomserver/studies/{id}/archive")]
    public async Task<ActionResult> GetStudyZipSync(string id)
    {
        try
        {
            var study = await _orthanc.GetStudy(id);

            /*var archive =  await BlobCache.LocalMachine.DownloadUrl()

            var response = await _orthanc.GetStudyZip(id);
            var archive = await response.Content.ReadAsByteArrayAsync();*/

            var archive = await CacheHelper.DownloadFile(id);
            
            return File(archive, "application/zip", $"{study.PatientMainDicomTags.PatientName.Trim().Replace("^","").Replace(" ", "-")}_{study.MainDicomTags.StudyID}.zip");
        }
        catch (Exception ex)
        {
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }
    
    [HttpGet]
    [Route("dicomserver/pacs/status")]
    public async Task<ActionResult> GetStatus()
    {
        var status = await PACSStatusHelper.GetStorageStatus();
        if(status == null)
        {
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
        else
        {
            return Ok(status);
        }
    }

    [HttpGet]
    [Route("dicomserver/studies/cached/{id}/archive")]
    public async Task<ActionResult> GetCachedStudyArchive(string id)
    {
        try
        {
            var study = await _orthanc.GetStudy(id);
            var archive = await BlobCache.LocalMachine.GetObject<byte[]>($"{id}_zip");
            return File(archive, "application/zip", $"{study.PatientMainDicomTags.PatientName.ToUpper()} [{study.MainDicomTags.StudyDescription.ToUpper()} - {DateTime.ParseExact(study.MainDicomTags.StudyDate + " " + study.MainDicomTags.StudyTime.Split('.')[0], "yyyyMMdd HHmmss", CultureInfo.InvariantCulture).ToString("yyyy-MM-dd")}].zip", true);
        }
        catch
        {
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost]
    [Route("dicomserver/studies/{id}/archive")]
    public async Task<ActionResult> QueueStudyArchiveJob(string id)
    {
        try
        {
            var config = ConfigHelper.GetConfig();
            var response = await _orthanc.QueueArchiveJob(id, new OrthancArchiveBody());

            var data = await response.Content.ReadAsByteArrayAsync();
            if(data.Length >0)
            {
                var json = Encoding.UTF8.GetString(data);

                try
                {
                    var archiveResponse = JsonConvert.DeserializeObject<OrthancArchiveAsyncResponse>(json);
                    archiveResponse.StudyId = id;
                    await BlobCache.LocalMachine.InsertObject<OrthancArchiveAsyncResponse>($"{id}_job",archiveResponse, TimeSpan.FromHours(config.ArchiveCache.JobsExpirationHours));
                    return Ok();
                }
                catch
                {
                    await BlobCache.LocalMachine.InsertObject<byte[]>($"{id}_zip", data, TimeSpan.FromHours(config.ArchiveCache.ExpirationHours));
                    return Ok(new { ArchiveLink = $"/studies/cached/{id}/archive" });
                }
            }
            else
            {
                return Ok();
            }
        }
        catch
        {
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet]
    [Route("dicomserver/jobs/{id}/archive")]
    public async Task<IActionResult> DownloadJobArchiveAsync(string id, string fileName)
    {
        try
        {
            var config = ConfigHelper.GetConfig();
            HttpClient client = new HttpClient();
            var url = $"{config.OrthancApi.Host}/jobs/{id}/archive";


            HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

            var jobs = await OrthancHelper.GetJobs(_orthanc);

            var job = jobs.FirstOrDefault(x => x.Job.ID == id);
            var archiveSize = job.Job.Content.ArchiveSize;

            fileName ??= $"{job.Study.PatientName}_{job.Study.Description}_{job.Study.StudyDate.ToString("dd-MM-yyyy")}.zip";

            this.HttpContext.Response.ContentLength = Convert.ToInt64(archiveSize);

            Stream streamToReadFrom = await response.Content.ReadAsStreamAsync();

            return File(streamToReadFrom, "application/octet-stream", fileName, true);

        }
        catch
        {
            return NotFound();
        }
    }

    private (long,long) ReadHttpRange()
    {
        var rangeHeaderValue = this.HttpContext.Request.Headers
            .FirstOrDefault(x => x.Key == "Range")
            .Value
            .FirstOrDefault();

        var ranges = rangeHeaderValue
            .Replace("bytes=", "")
            .Split('-')
            .Select(x => Convert.ToInt64(x))
            .Take(2)
            .ToArray();

        return (ranges[0], ranges[1]);
    }

    [HttpGet]
    [Route("dicomserver/jobs")]
    public async Task<ActionResult> GetJobs()
    {
        try
        {
            var jobs = await OrthancHelper.GetJobs(_orthanc);

            return Ok(jobs);
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.Message);
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }
    [HttpGet]
    [Route("dicomserver/jobs/{id}")]
    public async Task<ActionResult> GetJob(string id)
    {
        try
        {
            var jobs = await OrthancHelper.GetJobs(_orthanc);

            return Ok(jobs.FirstOrDefault(x => x.Job.ID == id));
        }
        catch
        {
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }
    [HttpPost]
    [Route("dicomserver/jobs/{id}/pause")]
    public async Task<ActionResult> PauseJob(string id)
    {
        try
        {
            var respone = await _orthanc.PauseJob(id);
            return new StatusCodeResult((int)respone.StatusCode);
        }
        catch
        {
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }
    [HttpPost]
    [Route("dicomserver/jobs/{id}/resume")]
    public async Task<ActionResult> ResumeJob(string id)
    {
        try
        {
            var respone = await _orthanc.ResumeJob(id);
            return new StatusCodeResult((int)respone.StatusCode);
        }
        catch
        {
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }
    [HttpPost]
    [Route("dicomserver/jobs/{id}/resubmit")]
    public async Task<ActionResult> ResubmitJob(string id)
    {
        try
        {
            var respone = await _orthanc.ResumbitJob(id);
            return new StatusCodeResult((int)respone.StatusCode); ;
        }
        catch
        {
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }
    [HttpPost]
    [Route("dicomserver/jobs/{id}/cancel")]
    public async Task<ActionResult> CancelJob(string id)
    {
        try
        {
            var respone = await _orthanc.CancelJob(id);
            return new StatusCodeResult((int)respone.StatusCode);
        }
        catch
        {
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }
}
