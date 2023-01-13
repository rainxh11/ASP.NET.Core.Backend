namespace QuranSchool.Models.Response;

public class StatsResultResponse<TData, TTotal>
{
    public StatsResultResponse(TData data, TTotal total, string results)
    {
        Results = results;
        Data = data;
        Total = total;
    }

    public string Results { get; }
    public TData Data { get; }
    public TTotal Total { get; }
}