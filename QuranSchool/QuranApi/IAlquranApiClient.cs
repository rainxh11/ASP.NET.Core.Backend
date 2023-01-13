using System.Threading.Tasks;
using Refit;

namespace QuranApi;

public interface IAlquranApiClient
{
    [Get("/v1/quran/{edition}")]
    Task<QuranResponse> GetQuran(string edition = "quran-simple");
}