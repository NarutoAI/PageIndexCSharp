using System.Text.RegularExpressions;
using PageIndexCSharp.Model;

namespace PageIndexCSharp.Parsing;

/// <summary>
/// Markdown 文档 PageIndex 解析器，按标题层级构建结构树。
/// </summary>
public static partial class MarkdownPageIndexParser
{
    private sealed class MarkdownSection
    {
        public required string Title { get; init; }

        public required int Level { get; init; }

        public required int LineNumber { get; init; }

        public required int SegmentIndex { get; init; }

        public required string Text { get; init; }
    }

    private sealed record MarkdownParseResult(List<PageIndexNode> Structure, List<DocumentPageContent> Pages);

    /// <summary>
    /// 读取 Markdown 文件，并根据 # 标题层级生成 PageIndex 文档结构和逻辑分段内容。
    /// </summary>
    public static (List<PageIndexNode> Structure, List<DocumentPageContent> Pages) Parse(string markdownPath)
    {
        MarkdownParseResult result = ParseInternal(markdownPath);
        return (result.Structure, result.Pages);
    }

    /// <summary>
    /// 读取 Markdown 文件，并根据标题分段返回逻辑页内容。
    /// </summary>
    public static List<DocumentPageContent> ExtractPages(string markdownPath)
    {
        return ParseInternal(markdownPath).Pages;
    }

    /// <summary>
    /// 读取 Markdown 文件，并根据标题层级返回结构树。
    /// </summary>
    public static List<PageIndexNode> BuildStructure(string markdownPath)
    {
        return ParseInternal(markdownPath).Structure;
    }

    private static MarkdownParseResult ParseInternal(string markdownPath)
    {
        if (string.IsNullOrWhiteSpace(markdownPath))
        {
            throw new ArgumentException("Markdown path cannot be empty.", nameof(markdownPath));
        }

        if (!File.Exists(markdownPath))
        {
            throw new FileNotFoundException("Markdown file not found.", markdownPath);
        }

        var markdownContent = File.ReadAllText(markdownPath);
        var lines = markdownContent.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        //查找所有标题所在的行
        var headingLineIndexes = FindHeadingLineIndexes(lines);

        if (headingLineIndexes.Count == 0)
        {
            List<DocumentPageContent> singlePage =
            [
                new DocumentPageContent
                {
                    Page = 1,
                    Content = markdownContent
                }
            ];

            List<PageIndexNode> singleNode =
            [
                new PageIndexNode
                {
                    Title = Path.GetFileNameWithoutExtension(markdownPath),
                    NodeId = "0000",
                    StartIndex = 1,
                    EndIndex = 1,
                    Text = markdownContent
                }
            ];

            return new MarkdownParseResult(singleNode, singlePage);
        }
        //找到每个标题对应的节点数据
        var sections = BuildSections(lines, headingLineIndexes);
        var pages = sections
            .Select(section => new DocumentPageContent
            {
                Page = section.SegmentIndex,
                Content = section.Text
            })
            .ToList();

        var structure = BuildTree(sections);
        return new MarkdownParseResult(structure, pages);
    }

    private static List<int> FindHeadingLineIndexes(IReadOnlyList<string> lines)
    {
        List<int> headingLineIndexes = [];
        var inCodeBlock = false;

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index].Trim();
            if (CodeBlockRegex().IsMatch(line))
            {
                inCodeBlock = !inCodeBlock;
                continue;
            }

            if (inCodeBlock || string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (HeadingRegex().IsMatch(line))
            {
                headingLineIndexes.Add(index);
            }
        }

        return headingLineIndexes;
    }

    private static List<MarkdownSection> BuildSections(IReadOnlyList<string> lines, IReadOnlyList<int> headingLineIndexes)
    {
        List<MarkdownSection> sections = [];
        for (var index = 0; index < headingLineIndexes.Count; index++)
        {
            var startLineIndex = headingLineIndexes[index];
            //找到下一个标题的行 作为结束的行记录
            var endLineIndex = index + 1 < headingLineIndexes.Count ? headingLineIndexes[index + 1] : lines.Count;
            var headingLine = lines[startLineIndex].Trim();
            var headingMatch = HeadingRegex().Match(headingLine);
            //读取中间的内容
            var sectionText = string.Join('\n', lines.Skip(startLineIndex).Take(endLineIndex - startLineIndex)).Trim();
            sections.Add(new MarkdownSection
            {
                Title = headingMatch.Groups[2].Value.Trim(),
                Level = headingMatch.Groups[1].Value.Length,
                LineNumber = startLineIndex + 1,
                SegmentIndex = index + 1,
                Text = sectionText
            });
        }

        return sections;
    }

    private static List<PageIndexNode> BuildTree(IReadOnlyList<MarkdownSection> sections)
    {
        List<PageIndexNode> roots = [];
        Stack<(PageIndexNode Node, int Level)> stack = [];

        for (int index = 0; index < sections.Count; index++)
        {
            MarkdownSection section = sections[index];
            PageIndexNode node = new()
            {
                Title = section.Title,
                NodeId = index.ToString("D4"),
                StartIndex = section.SegmentIndex,
                EndIndex = FindEndIndex(sections, index),
                Text = section.Text
            };

            while (stack.Count > 0 && stack.Peek().Level >= section.Level)
            {
                stack.Pop();
            }

            if (stack.Count == 0)
            {
                roots.Add(node);
            }
            else
            {
                PageIndexNode parent = stack.Peek().Node;
                parent.Nodes ??= [];
                parent.Nodes.Add(node);
            }

            stack.Push((node, section.Level));
        }

        return roots;
    }

    private static int FindEndIndex(IReadOnlyList<MarkdownSection> sections, int currentIndex)
    {
        MarkdownSection current = sections[currentIndex];
        for (int index = currentIndex + 1; index < sections.Count; index++)
        {
            if (sections[index].Level <= current.Level)
            {
                return sections[index].SegmentIndex - 1;
            }
        }

        return sections[^1].SegmentIndex;
    }

    /// <summary>
    /// 正则获取代码块的记录
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex("^(?:(?:[-*+]+|\\d+[.)])\\s*)*(?:```|~~~)")]
    private static partial Regex CodeBlockRegex();

    /// <summary>
    /// 正则获取标题记录
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex("^(#{1,6})\\s+(.+)$")]
    private static partial Regex HeadingRegex();
}
