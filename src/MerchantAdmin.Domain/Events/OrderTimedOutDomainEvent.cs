using MerchantAdmin.Domain.Entities.AggregatesModel.OrderAggregate;

namespace MerchantAdmin.Domain.Events;

/// <summary>超时关闭领域事件：订单超时未支付被系统自动关闭（区别于用户主动取消）。</summary>
public record OrderTimedOutDomainEvent(Order Order) : INotification;
