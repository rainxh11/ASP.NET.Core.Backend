using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Dicom;
using Dicom.Log;
using Dicom.Network;
using System.Linq;

namespace DicomForwarder
{
    public class DicomCStoreSCP : DicomService, IDicomServiceProvider, IDicomCStoreProvider, IDicomCEchoProvider
    {
        private Queue<DicomAssociation> _assosciationQueue = new Queue<DicomAssociation>();

        private static readonly DicomTransferSyntax[] AcceptedTransferSyntaxes = new DicomTransferSyntax[]
        {
               DicomTransferSyntax.ExplicitVRLittleEndian,
               DicomTransferSyntax.ExplicitVRBigEndian,
               DicomTransferSyntax.ImplicitVRLittleEndian
        };

        private static readonly DicomTransferSyntax[] AcceptedImageTransferSyntaxes = new DicomTransferSyntax[]
        {
               // Lossless
               DicomTransferSyntax.JPEGLSLossless,
               DicomTransferSyntax.JPEG2000Lossless,
               DicomTransferSyntax.JPEGProcess14SV1,
               DicomTransferSyntax.JPEGProcess14,
               DicomTransferSyntax.RLELossless,
               // Lossy
               DicomTransferSyntax.JPEGLSNearLossless,
               DicomTransferSyntax.JPEG2000Lossy,
               DicomTransferSyntax.JPEGProcess1,
               DicomTransferSyntax.JPEGProcess2_4,
               // Uncompressed
               DicomTransferSyntax.ExplicitVRLittleEndian,
               DicomTransferSyntax.ExplicitVRBigEndian,
               DicomTransferSyntax.ImplicitVRLittleEndian,
        };

        public DicomCStoreSCP(INetworkStream stream, Encoding fallbackEncoding, Logger log)
            : base(stream, fallbackEncoding, log)
        {
        }

        public Task OnReceiveAssociationRequestAsync(DicomAssociation association)
        {
            /*if (association.CalledAE != "LIVRET")
            {
                return SendAssociationRejectAsync(
                    DicomRejectResult.Permanent,
                    DicomRejectSource.ServiceUser,
                    DicomRejectReason.CalledAENotRecognized);
            }*/

            foreach (var pc in association.PresentationContexts)
            {
                if (pc.AbstractSyntax == DicomUID.Verification) pc.AcceptTransferSyntaxes(AcceptedTransferSyntaxes);
                else if (pc.AbstractSyntax.StorageCategory != DicomStorageCategory.None) pc.AcceptTransferSyntaxes(AcceptedImageTransferSyntaxes);
            }
            _assosciationQueue.Enqueue(association);

            return SendAssociationAcceptAsync(association);
        }

        public Task OnReceiveAssociationReleaseRequestAsync()
        {
            return SendAssociationReleaseResponseAsync();
        }

        public void OnReceiveAbort(DicomAbortSource source, DicomAbortReason reason)
        {
        }

        public void OnConnectionClosed(Exception exception)
        {
        }

        public DicomCStoreResponse OnCStoreRequest(DicomCStoreRequest request)
        {
            var association = _assosciationQueue.Dequeue();

            var config = ConfigHandler.GetConfig();
            config.ForwardDestinations.AsParallel().ForAll(async x =>
            {
                try
                {
                    var calledAet = string.IsNullOrEmpty(x.AET) ? association.CalledAE : x.AET;

                    var client = new Dicom.Network.Client.DicomClient(x.Host, x.Port, false, association.CallingAE, association.CalledAE); ;
                    client.NegotiateAsyncOps();
                    await client.AddRequestAsync(request);
                    await client.SendAsync();
                }
                catch
                {
                }
            });

            /*var studyUid = request.Dataset.GetSingleValue<string>(DicomTag.StudyInstanceUID);
            var instUid = request.SOPInstanceUID.UID;

            var path = Path.GetFullPath(Program.StoragePath);
            path = Path.Combine(path, studyUid);

            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            path = Path.Combine(path, instUid) + ".dcm";

            request.File.Save(path);*/

            return new DicomCStoreResponse(request, DicomStatus.Success);
        }

        public void OnCStoreRequestException(string tempFileName, Exception e)
        {
            // let library handle logging and error response
        }

        public DicomCEchoResponse OnCEchoRequest(DicomCEchoRequest request)
        {
            return new DicomCEchoResponse(request, DicomStatus.Success);
        }
    }
}