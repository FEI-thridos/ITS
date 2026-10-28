namespace KukaFm.Middleware.Api.Data;

/// <summary>Response of the paginated history endpoints (system-definition §7).</summary>
public record PagedResult<T>(List<T> Items, int Page, int PageSize, int TotalCount);

public static class Paging
{
    public const int DefaultPage = 1;
    public const int MaxPageSize = 100;

    /// <summary>History rows per item returned with <c>includeHistory=true</c>.</summary>
    public const int IncludedHistoryRows = 100;

    /// <summary>Returns validation errors keyed by query parameter, or null when valid.</summary>
    public static Dictionary<string, string[]>? Validate(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();
        if (page < 1)
            errors["page"] = ["page must be at least 1."];
        if (pageSize is < 1 or > MaxPageSize)
            errors["pageSize"] = [$"pageSize must be between 1 and {MaxPageSize}."];
        return errors.Count == 0 ? null : errors;
    }

    /// <summary>Rows to skip for a 1-based page; long so huge page numbers cannot overflow.</summary>
    public static long Skip(int page, int pageSize) => (long)(page - 1) * pageSize;
}
