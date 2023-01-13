using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Entities;

namespace QuranApi;

public class QuranService : BackgroundService
{
    private readonly IAlquranApiClient _client;
    private readonly ILogger<QuranService> _logger;

    public QuranService(IAlquranApiClient client, ILogger<QuranService> logger)
    {
        _client = client;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var exist = await DB.Find<Surah>().ExecuteAnyAsync(stoppingToken);
            if (!exist)
            {
                var quran = await _client.GetQuran();
                await quran.Data.Surahs.InsertAsync(cancellation: stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
        }
    }
}