using Refit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using OrthancCrawler.Models;
using System.IO;

namespace OrthancCrawler
{
    public interface IDicomServerApi
    {
        [Get("/dicomserver/studies")]
        Task<List<StudyResponse>> GetStudies();

        [Get("/dicomserver/jobs")]
        Task<List<JobResponse>> GetJobs();

        [Get("/dicomserver/jobs/{id}")]
        Task<JobResponse> GetJob(string id);

        [Post("/dicomserver/studies/{id}/archive")]
        Task<HttpResponseMessage> QueueArchiveJob(string id);

        [Post("/dicomserver/jobs/{id}/cancel")]
        Task<HttpResponseMessage> CancelJob(string id);
    }
    public interface IOrthancApi
    {
        [Get("/studies")]
        Task<List<string>> GetStudies();
        //---------------------------------//
        [Get("/jobs")]
        Task<List<string>> GetJobs();
        //---------------------------------//
        [Get("/jobs/{id}")]
        Task<OrthancJob> GetJob(string id);
        //---------------------------------//
        [Post("/studies/{id}/archive")]
        Task<OrthancArchiveAsyncResponse> CreateArchiveJob(string id, [Body] OrthancArchiveBody body);

        [Post("/studies/{id}/archive")]
        Task QueueArchiveJob(string id, [Body] OrthancArchiveBody body);

        [Get("/jobs/{id}/archive")]
        Task<HttpResponseMessage> GetJobArchive(string id);

        [Post("/jobs/{id}/cancel")]
        Task<HttpResponseMessage> CancelJob(string id);

        [Multipart]
        [Post("/instances")]
        Task UploadInstance([AliasAs("file")] StreamPart stream);
    }
}
