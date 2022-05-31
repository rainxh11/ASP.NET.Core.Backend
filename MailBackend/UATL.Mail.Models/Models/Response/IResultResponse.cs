namespace UATL.MailSystem.Common.Response;

public interface IResultResponse<TData, T>
{
    public T Results { get; }
    public TData Data { get; }
}