using Akavache;
using Refit;
using System.Collections.Generic;
using System.Net.Http;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace DicomServer.Models;
[Headers("Authorization: Basic")]
public interface IOrthancApi
{
    [Post("/studies/{id}/anonymize")]
    Task AnonymizeStudy(string id);
    //---------------------------------//
    [Get("/studies")]
    Task<List<string>> GetStudies();
    //---------------------------------//
    [Get("/studies")]
    Task<List<OrthancStudy>> GetStudiesExpanded([AliasAs("expand")] bool expand = true);
    //---------------------------------//
    [Get("/studies/{id}")]
    Task<OrthancStudy> GetStudy(string id);
    //---------------------------------//
    [Delete("/studies/{id}")]
    Task<HttpResponseMessage> DeleteStudy(string id);
    //---------------------------------//
    [Get("/studies/{id}/instances")]
    Task<List<OrthancInstance>> GetStudyInstances(string id);
    //---------------------------------//
    [Get("/instances")]
    Task<List<string>> GetInstances();
    //---------------------------------//
    [Get("/instances")]
    Task<List<OrthancInstance>> GetInstancesExpanded([AliasAs("expand")] bool expand = true);
    //---------------------------------//
    [Get("/instances/{id}")]
    Task<OrthancInstance> GetInstance(string id);
    //---------------------------------//
    [Headers("Accept: image/jpeg")]
    [Get("/instances/{id}/rendered")]
    Task<HttpResponseMessage> GetInstanceRenderedJpeg(string id,[AliasAs("quality")] int quality = 70);
    //---------------------------------//
    [Headers("Accept: image/jpeg")]
    [Get("/instances/{id}/rendered")]
    Task<HttpResponseMessage> GetInstanceRenderedJpeg(string id, int height, int width, int quality);
    //---------------------------------//
    [Get("/instances/{id}/rendered")]
    [Headers("Accept: image/png")]

    Task<HttpResponseMessage> GetInstanceRenderedPng(string id);
    //---------------------------------//
    [Get("/instances/{id}/rendered")]
    [Headers("Accept: image/png")]

    Task<HttpResponseMessage> GetInstanceRenderedPng(string id, int height, int width);
    //---------------------------------//
    [Get("/studies/{id}/archive")]
    Task<HttpResponseMessage> GetStudyZip(string id);
    //---------------------------------//
    [Get("/jobs")]
    Task<List<string>> GetJobs();

    [Get("/jobs")]
    Task<List<OrthancJob>> GetOrthancJobs([AliasAs("expand")] bool expand = true);

    [Get("/jobs")]
    Task<HttpResponseMessage> GetOrthancJobsJson([AliasAs("expand")] bool expand = true);
    //---------------------------------//
    [Get("/jobs/{id}")]
    Task<OrthancJob> GetJob(string id);

    [Post("/jobs/{id}/cancel")]
    Task<HttpResponseMessage> CancelJob(string id);

    [Post("/jobs/{id}/resume")]
    Task<HttpResponseMessage> ResumeJob(string id);

    [Post("/jobs/{id}/pause")]
    Task<HttpResponseMessage> PauseJob(string id);

    [Post("/jobs/{id}/resubmit")]
    Task<HttpResponseMessage> ResumbitJob(string id);
    //---------------------------------//
    [Post("/studies/{id}/archive")]
    Task<OrthancArchiveAsyncResponse> CreateArchiveJob(string id, [Body] OrthancArchiveBody body);
    [Post("/studies/{id}/archive")]
    Task<HttpResponseMessage> QueueArchiveJob(string id, [Body] OrthancArchiveBody body);

    [Get("/jobs/{id}/archive")]
    Task<HttpResponseMessage> GetJobArchive(string id);
}
