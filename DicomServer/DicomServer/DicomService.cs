using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DicomServer
{
public class DicomService : BackgroundService
{
    public DicomService(ILoggerFactory loggerFactory)
    {
        Logger = loggerFactory.CreateLogger<DicomService>();
    }

    public ILogger Logger { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("50LAB Dicom Server is starting.");

        stoppingToken.Register(() => Logger.LogInformation("50LAB Dicom Server is stopping."));

        while (!stoppingToken.IsCancellationRequested)
        {
            Logger.LogInformation("50LAB Dicom Server is doing background work.");

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        Logger.LogInformation("50LAB Dicom Server has stopped.");
    }
}
}
