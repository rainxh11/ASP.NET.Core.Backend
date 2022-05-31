namespace UATL.MailSystem.Common.Response;

public class PagedResultResponse<TData>
{
    public PagedResultResponse(TData data, long total, long pageCount, long pageSize, long page)
    {
        Data = data;
        Total = total;
        PageSize = pageSize;
        PageCount = pageCount;
        Page = page;
    }

    public long Total { get; }
    public long Page { get; }
    public long PageCount { get; }
    public long PageSize { get; }
    public TData Data { get; }
}