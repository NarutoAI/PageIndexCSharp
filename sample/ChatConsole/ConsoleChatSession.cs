using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Spectre.Console;

/// <summary>
/// 控制台聊天会话，负责读取用户输入并以流式方式输出智能体回答。
/// </summary>
internal sealed class ConsoleChatSession
{
    private readonly AIAgent agent;
    private readonly StreamingMarkdownConsoleRenderer markdownRenderer;

    /// <summary>
    /// 初始化控制台聊天会话。
    /// </summary>
    /// <param name="agent">用于执行 PageIndex 检索问答的智能体。</param>
    /// <param name="markdownRenderer">用于渲染流式 Markdown 文本的控制台渲染器。</param>
    public ConsoleChatSession(AIAgent agent, StreamingMarkdownConsoleRenderer markdownRenderer)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(markdownRenderer);

        this.agent = agent;
        this.markdownRenderer = markdownRenderer;
    }

    /// <summary>
    /// 启动聊天循环；用户输入空行、exit 或 quit 时退出。
    /// </summary>
    /// <param name="cancellationToken">取消聊天循环和模型流式输出的令牌。</param>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        AnsiConsole.MarkupLine("[bold green]PageIndex Chat Console[/]");
        AnsiConsole.MarkupLine("输入问题开始聊天；输入 [yellow]exit[/]、[yellow]quit[/] 或空行退出。");

        while (!cancellationToken.IsCancellationRequested)
        {
            AnsiConsole.WriteLine();
            string? question = AnsiConsole.Ask<string>("[bold blue]你：[/]").Trim();

            if (string.IsNullOrWhiteSpace(question) || IsExitCommand(question))
            {
                AnsiConsole.MarkupLine("[grey]聊天已结束。[/]");
                return;
            }

            await WriteAssistantResponseAsync(question, cancellationToken);
        }
    }

    /// <summary>
    /// 调用智能体并把回答按流式 Markdown 渲染到控制台。
    /// </summary>
    /// <param name="question">用户问题。</param>
    /// <param name="cancellationToken">取消流式输出的令牌。</param>
    private async Task WriteAssistantResponseAsync(string question, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[bold green]AI：[/]");
        markdownRenderer.Reset();

        await foreach (var item in agent.RunStreamingAsync(question, cancellationToken: cancellationToken))
        {
            TextReasoningContent? reasoningContent = item.Contents?.OfType<TextReasoningContent>().FirstOrDefault();
            if (reasoningContent is not null && !string.IsNullOrWhiteSpace(reasoningContent.Text))
            {
                AnsiConsole.Write(new Markup($"[grey]{Markup.Escape(reasoningContent.Text)}[/]"));
                continue;
            }

            if (!string.IsNullOrEmpty(item.Text))
            {
                markdownRenderer.Write(item.Text);
            }
        }

        markdownRenderer.Complete();
    }

    /// <summary>
    /// 判断用户输入是否为退出命令。
    /// </summary>
    /// <param name="input">用户输入文本。</param>
    /// <returns>如果输入表示退出，则返回 true。</returns>
    private static bool IsExitCommand(string input)
    {
        return string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase)
               || string.Equals(input, "quit", StringComparison.OrdinalIgnoreCase);
    }
}
