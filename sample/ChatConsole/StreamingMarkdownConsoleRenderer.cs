using System.Text;
using System.Text.RegularExpressions;
using Spectre.Console;

/// <summary>
/// 面向控制台的流式 Markdown 文本渲染器。
/// </summary>
/// <remarks>
/// 渲染器只在收到完整行后处理 Markdown，避免代码块、列表等语法在流式分片中被截断造成错误渲染。
/// 当前实现只处理文本 Markdown，不包含图片渲染逻辑。
/// </remarks>
internal sealed partial class StreamingMarkdownConsoleRenderer
{
    private readonly StringBuilder pendingLine = new();
    private bool isCodeBlock;

    /// <summary>
    /// 重置渲染器状态，用于开始一次新的模型回答。
    /// </summary>
    public void Reset()
    {
        pendingLine.Clear();
        isCodeBlock = false;
    }

    /// <summary>
    /// 写入一段流式文本；只有完整行会立即渲染，未完成行会暂存。
    /// </summary>
    /// <param name="text">模型流式返回的文本片段。</param>
    public void Write(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (char currentChar in text)
        {
            if (currentChar == '\r')
            {
                continue;
            }

            if (currentChar == '\n')
            {
                RenderLine(pendingLine.ToString());
                pendingLine.Clear();
                continue;
            }

            pendingLine.Append(currentChar);
        }
    }

    /// <summary>
    /// 完成当前回答的渲染，输出最后一行未换行的内容。
    /// </summary>
    public void Complete()
    {
        if (pendingLine.Length > 0)
        {
            RenderLine(pendingLine.ToString());
            pendingLine.Clear();
        }

        if (isCodeBlock)
        {
            isCodeBlock = false;
        }

        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// 根据 Markdown 行语义渲染单行文本。
    /// </summary>
    /// <param name="line">完整 Markdown 行。</param>
    private void RenderLine(string line)
    {
        string trimmedLine = line.Trim();

        if (trimmedLine.StartsWith("```", StringComparison.Ordinal))
        {
            isCodeBlock = !isCodeBlock;
            AnsiConsole.WriteLine();
            return;
        }

        if (isCodeBlock)
        {
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(line)}[/]");
            return;
        }

        if (string.IsNullOrWhiteSpace(line))
        {
            AnsiConsole.WriteLine();
            return;
        }

        if (IsHorizontalRule(trimmedLine))
        {
            AnsiConsole.Write(new Rule());
            return;
        }

        if (TryRenderHeading(trimmedLine))
        {
            return;
        }

        if (TryRenderQuote(trimmedLine))
        {
            return;
        }

        if (TryRenderUnorderedList(trimmedLine))
        {
            return;
        }

        if (TryRenderOrderedList(trimmedLine))
        {
            return;
        }

        AnsiConsole.MarkupLine(RenderInlineMarkup(line));
    }

    /// <summary>
    /// 判断当前行是否为 Markdown 分隔线。
    /// </summary>
    /// <param name="line">去除首尾空白后的 Markdown 行。</param>
    /// <returns>如果是分隔线则返回 true。</returns>
    private static bool IsHorizontalRule(string line)
    {
        return line is "---" or "***" or "___";
    }

    /// <summary>
    /// 尝试渲染 Markdown 标题。
    /// </summary>
    /// <param name="line">去除首尾空白后的 Markdown 行。</param>
    /// <returns>如果成功渲染标题则返回 true。</returns>
    private static bool TryRenderHeading(string line)
    {
        Match match = HeadingRegex().Match(line);
        if (!match.Success)
        {
            return false;
        }

        int level = match.Groups[1].Value.Length;
        string content = RenderInlineMarkup(match.Groups[2].Value.Trim());
        string color = level <= 2 ? "bold cyan" : "bold blue";
        AnsiConsole.MarkupLine($"[{color}]{content}[/]");
        return true;
    }

    /// <summary>
    /// 尝试渲染 Markdown 引用行。
    /// </summary>
    /// <param name="line">去除首尾空白后的 Markdown 行。</param>
    /// <returns>如果成功渲染引用则返回 true。</returns>
    private static bool TryRenderQuote(string line)
    {
        if (!line.StartsWith(">", StringComparison.Ordinal))
        {
            return false;
        }

        string content = line[1..].TrimStart();
        AnsiConsole.MarkupLine($"[grey]> {RenderInlineMarkup(content)}[/]");
        return true;
    }

    /// <summary>
    /// 尝试渲染 Markdown 无序列表行。
    /// </summary>
    /// <param name="line">去除首尾空白后的 Markdown 行。</param>
    /// <returns>如果成功渲染无序列表则返回 true。</returns>
    private static bool TryRenderUnorderedList(string line)
    {
        if (!line.StartsWith("- ", StringComparison.Ordinal)
            && !line.StartsWith("* ", StringComparison.Ordinal)
            && !line.StartsWith("+ ", StringComparison.Ordinal))
        {
            return false;
        }

        AnsiConsole.MarkupLine($"[green]•[/] {RenderInlineMarkup(line[2..])}");
        return true;
    }

    /// <summary>
    /// 尝试渲染 Markdown 有序列表行。
    /// </summary>
    /// <param name="line">去除首尾空白后的 Markdown 行。</param>
    /// <returns>如果成功渲染有序列表则返回 true。</returns>
    private static bool TryRenderOrderedList(string line)
    {
        Match match = OrderedListRegex().Match(line);
        if (!match.Success)
        {
            return false;
        }

        string index = Markup.Escape(match.Groups[1].Value);
        string content = RenderInlineMarkup(match.Groups[2].Value);
        AnsiConsole.MarkupLine($"[green]{index}.[/] {content}");
        return true;
    }

    /// <summary>
    /// 渲染行内 Markdown 语义，目前支持加粗和行内代码。
    /// </summary>
    /// <param name="text">原始行内 Markdown 文本。</param>
    /// <returns>可被 Spectre.Console Markup 渲染的文本。</returns>
    private static string RenderInlineMarkup(string text)
    {
        string escapedText = Markup.Escape(text);
        escapedText = BoldRegex().Replace(escapedText, "[bold]$1[/]");
        escapedText = InlineCodeRegex().Replace(escapedText, "[yellow]$1[/]");
        return escapedText;
    }

    [GeneratedRegex("^(#{1,6})\\s+(.+)$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex("^(\\d+)\\.\\s+(.+)$")]
    private static partial Regex OrderedListRegex();

    [GeneratedRegex("\\*\\*(.+?)\\*\\*")]
    private static partial Regex BoldRegex();

    [GeneratedRegex("`(.+?)`")]
    private static partial Regex InlineCodeRegex();
}
