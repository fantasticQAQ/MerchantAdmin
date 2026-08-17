using System.Collections.Concurrent;

namespace Payment.API.Payments;

/// <summary>
/// 支付会话存储：记录在途支付是否已被订单服务取消。
/// 真实系统中对应支付网关侧的支付单状态（如支付宝 trade_status），这里用内存模拟。
/// </summary>
public interface IPaymentSessionStore
{
    /// <summary>登记一笔支付会话。</summary>
    void Start(int orderId);

    /// <summary>标记该订单的支付已取消（订单服务发出取消支付事件）。</summary>
    void MarkCancelled(int orderId);

    /// <summary>该订单的支付是否已被取消。</summary>
    bool IsCancelled(int orderId);

    /// <summary>移除会话（支付流程结束）。</summary>
    void Remove(int orderId);
}

public class InMemoryPaymentSessionStore : IPaymentSessionStore
{
    private readonly ConcurrentDictionary<int, bool> _sessions = new();

    public void Start(int orderId) => _sessions[orderId] = false;

    public void MarkCancelled(int orderId) => _sessions[orderId] = true;

    public bool IsCancelled(int orderId) => _sessions.TryGetValue(orderId, out var cancelled) && cancelled;

    public void Remove(int orderId) => _sessions.TryRemove(orderId, out _);
}
