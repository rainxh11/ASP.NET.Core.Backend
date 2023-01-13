using System.Linq;
using DicomServer.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using Akavache;
using System.Reactive.Linq;
using System.Reactive.Concurrency;

namespace DicomServer.Helpers;
public class OrthancHelper
{
    public static async Task<List<ResponseJob>> GetJobs(IOrthancApi orthanc)
    {
        var studies = await CacheHelper.GetStudies();
        var cachedResponses = await BlobCache.LocalMachine.GetAllObjects<OrthancArchiveAsyncResponse>();
        var orthanJobs = await orthanc.GetOrthancJobs();

        var jobs = orthanJobs.Select(job =>
        {
            try
            {
                if(cachedResponses.Any(x => x.ID == job.ID))
                {
                    var studyId = cachedResponses?.FirstOrDefault(x => x.ID == job.ID).StudyId;
                    var study = studies.FirstOrDefault(x => x.Id == studyId);

                    return new ResponseJob(job, study);
                }
                else
                {
                    return new ResponseJob(job);
                }            
            }
            catch
            {
                return new ResponseJob(job);
            }
        }).OrderByDescending(x => x.Job.CreatedOn).ToList();
        return jobs;
    }
}
