using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace MerchantAdmin.AI.API.Harness;

/// <summary>
/// 会话历史的压缩。
///
/// 为什么需要它：批量任务（「建 1000 个商品」）每一轮都会产生「1 条带 tool_calls 的助手消息 + N 条工具结果」。
/// 60 次调用就是 62 条消息，几轮下来历史里全是工具脚手架：
/// - 把上下文撑爆（token 全花在已经没用的工具返回值上）
/// - 把用户消息挤出去（实测把会话裁到只剩 1 条，标题都变成了「（空会话）」）
/// - 之前那段「裁到 40 条再删开头的孤儿 tool 消息」的逻辑更糟：裁剪点落在一批工具结果中间时，
///   它会把剩下的几十条**全部删光**，历史直接归零，模型彻底丢失进度
///
/// 正确做法：先丢掉工具脚手架（助手随后会用自己的话总结结果，那些原始返回值留一份在审计轨迹里就够了），
/// 再按条数裁剪 —— 此时历史里只剩「用户消息 / 助手文本」，怎么裁都不会产生孤儿工具结果。
/// </summary>
public static class ChatHistoryCompactor
{
    public static void Compact(ChatHistory history, int maxMessages, bool dropToolScaffolding = true)
    {
        if (dropToolScaffolding) DropToolScaffolding(history);
        TrimTo(history, maxMessages);
    }

    /// <summary>
    /// 丢掉工具调用与工具结果。
    /// 留下的助手消息如果本来只装了工具调用、没有文本，就整条丢掉（空消息会被 OpenAI 拒绝）。
    /// </summary>
    private static void DropToolScaffolding(ChatHistory history)
    {
        for (var i = history.Count - 1; i >= 0; i--)
        {
            var message = history[i];

            if (message.Role == AuthorRole.Tool)
            {
                history.RemoveAt(i);
                continue;
            }

            var calls = message.Items.OfType<FunctionCallContent>().ToList();
            foreach (var call in calls) message.Items.Remove(call);

            if (message.Items.Count == 0 && string.IsNullOrWhiteSpace(message.Content))
                history.RemoveAt(i);
        }
    }

    /// <summary>
    /// 按条数裁剪，永远保留最后一条（正在生成的这条通常是最终答复）。
    /// 赶在这里做是安全的：工具脚手架已经清掉，不可能再裁出孤儿工具结果。
    /// </summary>
    private static void TrimTo(ChatHistory history, int maxMessages)
    {
        var limit = Math.Max(2, maxMessages);
        while (history.Count > limit)
            history.RemoveAt(0);
    }
}
