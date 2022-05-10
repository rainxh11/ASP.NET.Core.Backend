namespace FiftyLab.PrivateSchool.Response;

public class ResultResponse<TData, T> : IResultResponse<TData, T>
{
    public ResultResponse(TData data, T results)
    {
        Results = results;
        Data = data;
    }

    public T Results { get; private set; }

    public TData Data { get; private set; }
}

public class StatsResultResponse<TData, TTotal>
{
    public StatsResultResponse(TData data, TTotal total, string results)
    {
        Results = results;
        Data = data;
        Total = total;
    }

    public string Results { get; private set; }
    public TData Data { get; private set; }
    public TTotal Total { get; private set; }

}