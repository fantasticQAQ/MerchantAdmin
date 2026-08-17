using MediatR;

namespace MerchantAdmin.Application.Commands
{
    /// <summary>更新商品：支持编辑名称/价格、调整库存、上下架。字段为 null 表示不修改该项。</summary>
    public record UpdateProductCommand(
        int ProductId,
        string? Name = null,
        decimal? Price = null,
        decimal? StockDelta = null,
        bool? IsActive = null
    ) : IRequest<bool>;
}
