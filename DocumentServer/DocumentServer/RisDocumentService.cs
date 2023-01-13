using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RisDocumentServer
{
public class RisDocumentService : BackgroundService
{
    public RisDocumentService(ILoggerFactory loggerFactory)
    {
        Logger = loggerFactory.CreateLogger<RisDocumentService>();
    }

    public ILogger Logger { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("50LAB RIS Document Server is starting.");

        stoppingToken.Register(() => Logger.LogInformation("50LAB RIS Document Server is stopping."));

        while (!stoppingToken.IsCancellationRequested)
        {
            Logger.LogInformation("50LAB RIS Document Server is doing background work.");

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        Logger.LogInformation("50LAB RIS Document Server has stopped.");
    }
}
}
