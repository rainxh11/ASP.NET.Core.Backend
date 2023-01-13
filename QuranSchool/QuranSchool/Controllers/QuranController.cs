using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Entities;
using QuranApi;

namespace QuranSchool.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class QuranController : ControllerBase
{
    private readonly ILogger<QuranController> _logger;

    public QuranController(
        ILogger<QuranController> logger)
    {
        _logger = logger;
    }

    //-------------------------------------------------------------------------//
    [HttpGet]
    [Route("")]
    public async Task<IActionResult> GetQuran(CancellationToken ct)
    {
        var quran = await DB.Find<Surah>().ExecuteAsync(ct);
        return Ok(quran);
    }

    //-------------------------------------------------------------------------//
    [HttpGet]
    [Route("surah")]
    public async Task<IActionResult> GetSurahs(CancellationToken ct)
    {
        var surahs = await DB.Fluent<Surah>()
            .Project(new BsonDocument
            {
                { "Name", "$Name" },
                { "Number", "$Number" },
                { "EnglishName", "$EnglishName" },
                { "AyahCount", new BsonDocument("$size", "$Ayahs") }
            })
            .ToListAsync(ct);

        return Ok(surahs.Select(x => BsonSerializer.Deserialize<object>(x)));
    }

    //-------------------------------------------------------------------------//
    [HttpGet]
    [Route("surah/{id:int}/ayah")]
    public async Task<IActionResult> GetSurahAyahs(int id, CancellationToken ct)
    {
        var surah = await DB.Find<Surah>()
            .Match(f => f.Eq(x => x.Number, id))
            .ExecuteFirstAsync(ct);

        return Ok(surah.Ayahs);
    }
}