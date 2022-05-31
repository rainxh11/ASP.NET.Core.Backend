namespace UATL.MailSystem.Common.Response;

public class ResultResponse<TData, T> : IResultResponse<TData, T>
{
    public ResultResponse(TData data, T results)
    {
        Results = results;
        Data = data;
    }

    public T Results { get; }

    public TData Data { get; }
}