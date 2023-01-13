namespace QuranSchool.Models.Response;

public class DebtPagedResultResponse<TData> : PagedResultResponse<TData>
{
    public DebtPagedResultResponse(TData data, long total, long pageCount, long pageSize, long page, double debt)
        : base(data, total, pageCount, pageSize, page)
    {
        TotalDebt = debt;
    }

    public double TotalDebt { get; set; }
}