namespace MerchantAdmin.API.Infrastructure.Caching
{
    public interface IDelayJobService
    {
        Task ScheduleCancelOrderAsync(int orderId, TimeSpan delay);
        Task CancelCancelOrderAsync(int orderId);
    }
}
