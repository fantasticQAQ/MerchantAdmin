namespace MerchantAdmin.Application.Dtos
{
    /// <summary>分页查询结果。</summary>
    public record PagedResult<T>(int Total, int Page, int PageSize, List<T> Items);
}
