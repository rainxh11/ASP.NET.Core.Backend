using Dicom;
using Dicom.Network;
using Dicom.Network.Client;
using DicomServer.Helper;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DicomClient = Dicom.Network.Client.DicomClient;

namespace DicomServer.Helpers;
public class ModalitiesRetrieveWorker
{
    private static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private static DicomClient _client;
    public static void StartWorker()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    var config = ConfigHelper.GetConfig();
                    var images = new List<DicomDataset>();

                    foreach (var scp in config.SCPListModalities)
                    {
                        _client = new DicomClient(scp.Host, scp.Port, false, scp.CallingAET, scp.ServerAET);
                        _client.NegotiateAsyncOps();

                        var serieUids = new List<string>();
                        var request = CreateSeriesRequestByStudyUID("*");
                        request.OnResponseReceived += (req, response) =>
                        {
                            serieUids.Add(response.Dataset?.GetSingleValue<string>(DicomTag.SeriesInstanceUID));
                        };
                        await _client.AddRequestAsync(request);
                        await _client.SendAsync();                       
                     
                        foreach( var seriesUID in serieUids)
                        {
                            var cGetRequest = new DicomCGetRequest(seriesUID);
                            _client.OnCStoreRequest += (DicomCStoreRequest req) =>
                            {
                                images.Add(req.Dataset);
                                return Task.FromResult(new DicomCStoreResponse(req, DicomStatus.Success));
                            };

                            var pcs = DicomPresentationContext.GetScpRolePresentationContextsFromStorageUids(
                                DicomStorageCategory.Image,
                                DicomTransferSyntax.ExplicitVRLittleEndian,
                                DicomTransferSyntax.ImplicitVRLittleEndian,
                                DicomTransferSyntax.ImplicitVRBigEndian,
                                DicomTransferSyntax.DeflatedExplicitVRLittleEndian,
                                DicomTransferSyntax.ExplicitVRLittleEndian
                                );
                            _client.AdditionalPresentationContexts.AddRange(pcs);
                            await _client.AddRequestAsync(cGetRequest);
                            await _client.SendAsync();
                        }
                        
                    }

                    foreach(var pacs in config.PACSList)
                    {
                        var client = new DicomClient(pacs.Host, pacs.Port, false, pacs.CallingAET, pacs.ServerAET);
                        images.ForEach(async image =>
                        {
                            try
                            {
                                await client.AddRequestAsync(new DicomCStoreRequest(image));
                                await client.SendAsync();
                            }
                            catch
                            {

                            }
                        });                  
                    }
                    
                }
                catch
                {

                }
               Thread.Sleep(TimeSpan.FromSeconds(30));
            }
        }, _cancellationTokenSource.Token);
    }
    public static DicomCFindRequest CreateStudyRequestByPatientName(string patientName)
    {
        // there is a built in function to create a Study-level CFind request very easily:
        // return DicomCFindRequest.CreateStudyQuery(patientName: patientName);

        // but consider to create your own request that contains exactly those DicomTags that
        // you realy need pro process your data and not to cause unneccessary traffic and IO load:

        var request = new DicomCFindRequest(DicomQueryRetrieveLevel.Study);

        // always add the encoding
        request.Dataset.AddOrUpdate(new DicomTag(0x8, 0x5), "ISO_IR 100");

        // add the dicom tags with empty values that should be included in the result of the QR Server
        request.Dataset.AddOrUpdate(DicomTag.PatientName, "");
        request.Dataset.AddOrUpdate(DicomTag.PatientID, "");
        request.Dataset.AddOrUpdate(DicomTag.ModalitiesInStudy, "");
        request.Dataset.AddOrUpdate(DicomTag.StudyDate, "");
        request.Dataset.AddOrUpdate(DicomTag.StudyInstanceUID, "");
        request.Dataset.AddOrUpdate(DicomTag.StudyDescription, "");

        // add the dicom tags that contain the filter criterias
        request.Dataset.AddOrUpdate(DicomTag.PatientName, patientName);

        return request;
    }
    public static DicomCFindRequest CreateSeriesRequestByStudyUID(string studyInstanceUID)
    {
        // there is a built in function to create a Study-level CFind request very easily:
        // return DicomCFindRequest.CreateSeriesQuery(studyInstanceUID);

        // but consider to create your own request that contains exactly those DicomTags that
        // you realy need pro process your data and not to cause unneccessary traffic and IO load:
        var request = new DicomCFindRequest(DicomQueryRetrieveLevel.Series);

        request.Dataset.AddOrUpdate(new DicomTag(0x8, 0x5), "ISO_IR 100");

        // add the dicom tags with empty values that should be included in the result
        request.Dataset.AddOrUpdate(DicomTag.SeriesInstanceUID, "");
        request.Dataset.AddOrUpdate(DicomTag.SeriesDescription, "");
        request.Dataset.AddOrUpdate(DicomTag.Modality, "");
        request.Dataset.AddOrUpdate(DicomTag.NumberOfSeriesRelatedInstances, "");

        // add the dicom tags that contain the filter criterias
        request.Dataset.AddOrUpdate(DicomTag.StudyInstanceUID, studyInstanceUID);

        return request;
    }
    public static DicomCGetRequest CreateCGetBySeriesUID(string studyUID, string seriesUID)
    {
        var request = new DicomCGetRequest(studyUID, seriesUID);
        // no more dicomtags have to be set
        return request;
    }
    public static void StopWorker()
    {
        _cancellationTokenSource.Cancel();
    }
}
