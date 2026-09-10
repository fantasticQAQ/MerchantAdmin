using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace MerchantAdmin.AI.API.Harness;

/// <summary>
/// 用模型给会话起标题。
///
/// 之前标题就是「第一条用户消息的前 24 个字」—— 问题是第一条消息经常是
/// 「你好」「在吗」「有哪些商品？」，放在列表里完全看不出这个会话到底聊了什么。
/// 现在改成：**把对话内容喂给模型，让它概括成一个短标题**。
///
/// 三条刻意的规则：
/// - **用户手动改过名就永不覆盖**（`TitleIsAuto == false`）。自动生成的标题只是初始值，用户说了算。
/// - **开头几轮会重算几次**，之后定稿。第一轮往往是寒暄，只按它起名会起成「打招呼」；
///   聊到第三四轮主题才明确。重算不是每轮都做，超过阈值就不再花这次调用了。
/// - **失败一律不影响主流程**：起不出标题就退回原来的「第一条用户消息」。
/// </summary>
public sealed class SessionTitleService
{
    /// <summary>消息数超过这个值就定稿，不再重算（多一次调用就多一份延迟和成本）。</summary>
    private const int FreezeAfterMessages = 8;

    private const int MaxTitleChars = 16;

    private const string Prompt = """
        你是会话标题生成器。根据下面这轮对话，概括出这个会话在聊什么。

        要求：
        - 用中文，不超过 12 个字
        - 像列表里的目录一样，一眼能看出主题（例如「查询商品列表」「批量创建 60 个商品」「给可乐下单」）
        - 只输出标题本身，不要标点、引号、书名号，不要任何解释或前缀
        """;

    private readonly Kernel _kernel;
    private readonly ISessionPrefsStore _prefs;
    private readonly IConversationStore _conversations;
    private readonly bool _enabled;
    private readonly ILogger<SessionTitleService> _logger;

    public SessionTitleService(
        Kernel kernel,
        ISessionPrefsStore prefs,
        IConversationStore conversations,
        IConfiguration configuration,
        ILogger<SessionTitleService> logger)
    {
        _kernel = kernel;
        _prefs = prefs;
        _conversations = conversations;
        _enabled = configuration.GetValue("Ai:SessionTitle:Enabled", true);
        _logger = logger;
    }

    /// <summary>
    /// 有必要的话生成一个新标题，返回标题；不需要生成或生成失败时返回 null。
    /// 调用方拿它去更新前端显示的标题，失败就什么都不做。
    /// </summary>
    public async Task<string?> MaybeGenerateAsync(
        long userId, string sessionId, string userMessage, string assistantReply, CancellationToken ct = default)
    {
        if (!_enabled) return null;
        if (string.IsNullOrWhiteSpace(userMessage) || string.IsNullOrWhiteSpace(assistantReply)) return null;
        // 自动续跑那轮的输入是系统合成的，不拿它当标题素材
        if (AgentContinuation.IsContinuation(userMessage)) return null;

        try
        {
            var prefs = _prefs.Get(userId, sessionId);

            // 用户手动命名过 —— 自动标题一律让路
            if (prefs.Title is not null && !prefs.TitleIsAuto) return null;

            // 已经定稿了就不再重算
            if (prefs.TitleIsAuto)
            {
                var history = await _conversations.GetMessagesAsync(sessionId, userId, ct);
                if (history is null || history.Count > FreezeAfterMessages) return null;
            }

            var title = await AskModelAsync(userMessage, assistantReply, ct);
            if (string.IsNullOrWhiteSpace(title)) return null;

            // 再读一次：生成期间用户可能刚好重命名了，别把它盖回去
            var latest = _prefs.Get(userId, sessionId);
            if (latest.Title is not null && !latest.TitleIsAuto) return title;

            _prefs.Save(userId, sessionId, latest with { Title = title, TitleIsAuto = true });
            return title;
        }
        catch (Exception ex)
        {
            // 起标题是锦上添花，绝不能让它影响对话本身
            _logger.LogWarning(ex, "生成会话标题失败：{Session}", sessionId);
            return null;
        }
    }

    private async Task<string?> AskModelAsync(string userMessage, string assistantReply, CancellationToken ct)
    {
        var chat = _kernel.GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        history.AddSystemMessage(Prompt);
        history.AddUserMessage($"用户：{Clip(userMessage, 400)}\n\n助手：{Clip(assistantReply, 600)}");

        var settings = new OpenAIPromptExecutionSettings
        {
            MaxTokens = 48,
            Temperature = 0.2
        };

        var result = await chat.GetChatMessageContentAsync(history, settings, _kernel, ct);
        return Clean(result.Content);
    }

    /// <summary>
    /// 模型经常不听话：带上引号、书名号、结尾句号，或者干脆写成「标题：xxx」。
    /// 这里统一收拾干净 —— 列表里的标题带这些符号会很脏。
    /// </summary>
    private static string? Clean(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var text = raw.Trim();

        // 只取第一行，模型偶尔会多写一句解释
        var newline = text.IndexOfAny(['\r', '\n']);
        if (newline >= 0) text = text[..newline].Trim();

        // 去掉「标题：」这类前缀
        foreach (var prefix in new[] { "标题：", "标题:", "标题 ", "Title:", "Title：" })
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                text = text[prefix.Length..].Trim();

        text = text.Trim('"', '\'', '「', '」', '《', '》', '“', '”', '‘', '’', '。', '，', '.', ',', ':', '：', ' ', '*', '#');
        text = text.Trim();

        if (text.Length == 0) return null;
        return text.Length <= MaxTitleChars ? text : text[..MaxTitleChars];
    }

    private static string Clip(string text, int max)
        => text.Length <= max ? text : text[..max] + "…";
}
