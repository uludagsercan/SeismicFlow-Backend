namespace SeismicFlow.Shared.Pagination
{
    /// <summary>
    /// Wraps a list of items with pagination metadata.
    /// Returned by query handlers that support paging.
    /// </summary>
    public sealed class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; }
        public int TotalCount { get; }
        public int Page { get; }
        public int PageSize { get; }

        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasNextPage => Page < TotalPages;
        public bool HasPreviousPage => Page > 1;

        public PagedResult(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
        {
            Items = items;
            TotalCount = totalCount;
            Page = page;
            PageSize = pageSize;
        }
    }

    /// <summary>
    /// Standard paging request parameters.
    /// </summary>
    public sealed record PagedQuery(int Page = 1, int PageSize = 20)
    {
        public int Skip => (Page - 1) * PageSize;
    }







}
