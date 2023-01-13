using Refit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DocumentWatcher
{
    public interface DicomServerApi
    {
        [Get("/dicomserver/studies/{id}/archive")]
        Task<HttpResponseMessage> DownloadStudyArchive(string id);
    }
    public interface DocumentServerApi
    {
        [Multipart]
        [Post("/documentserver/documenteditor/import")]
        Task<string> UploadDocument([AliasAs("files")] FileInfo file);
    }
}