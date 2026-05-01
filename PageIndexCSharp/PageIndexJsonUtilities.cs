using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PageIndexCSharp.Model;

namespace PageIndexCSharp;

/// <summary>
/// PageIndex 内部 JSON 解析和结构处理工具。
/// </summary>
public static partial class PageIndexJsonUtilities
{
    /// <summary>
    /// 项目统一 JSON 序列化配置，保持 snake_case 字段由 JsonPropertyName 控制。
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// 从 LLM 响应中提取第一个完整 JSON 对象或数组。
    /// </summary>
    public static string ExtractJsonText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("LLM returned empty content.");
        }

        string cleaned = text.Trim();
        if (cleaned.StartsWith("```", StringComparison.Ordinal))
        {
            cleaned = CodeFenceRegex().Replace(cleaned, string.Empty).Trim();
        }

        int objectStart = cleaned.IndexOf('{');
        int arrayStart = cleaned.IndexOf('[');
        int start = objectStart >= 0 && arrayStart >= 0 ? Math.Min(objectStart, arrayStart) : Math.Max(objectStart, arrayStart);
        if (start < 0)
        {
            throw new InvalidOperationException("No JSON object or array was found in LLM response.");
        }

        char open = cleaned[start];
        char close = open == '{' ? '}' : ']';
        int depth = 0;
        bool inString = false;
        bool escape = false;

        for (int i = start; i < cleaned.Length; i++)
        {
            char ch = cleaned[i];
            if (inString)
            {
                if (escape)
                {
                    escape = false;
                }
                else if (ch == '\\')
                {
                    escape = true;
                }
                else if (ch == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (ch == '"')
            {
                inString = true;
            }
            else if (ch == open)
            {
                depth++;
            }
            else if (ch == close)
            {
                depth--;
                if (depth == 0)
                {
                    return cleaned[start..(i + 1)];
                }
            }
        }

        throw new InvalidOperationException("No complete JSON object or array was found in LLM response.");
    }

    /// <summary>
    /// 解析 LLM 返回的目录 JSON，兼容 physical_index 字符串标签和 table_of_contents 包装对象。
    /// </summary>
    public static List<PageIndexFlatItem> ParseFlatItems(string llmResponse)
    {
        string json = ExtractJsonText(llmResponse);
        JsonNode? node = JsonNode.Parse(json);
        JsonArray? array = node as JsonArray;

        if (node is JsonObject obj && obj["table_of_contents"] is JsonArray tocArray)
        {
            array = tocArray;
        }

        if (array is null)
        {
            throw new InvalidOperationException("TOC JSON must be an array or an object containing table_of_contents.");
        }

        List<PageIndexFlatItem> items = [];
        foreach (JsonNode? itemNode in array)
        {
            if (itemNode is not JsonObject itemObject)
            {
                continue;
            }

            string? title = itemObject["title"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            int? physicalIndex = ReadPhysicalIndex(itemObject);
            if (physicalIndex is null)
            {
                continue;
            }

            items.Add(new PageIndexFlatItem
            {
                Structure = itemObject["structure"]?.GetValue<string>(),
                Title = title.Trim(),
                PhysicalIndex = physicalIndex
            });
        }

        return items;
    }
    

    /// <summary>
    /// 根据扁平目录项生成树结构，并推导 start_index/end_index。
    /// </summary>
    public static List<PageIndexNode> BuildTree(IReadOnlyList<PageIndexFlatItem> flatItems, int pageCount)
    {
        if (pageCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageCount), "Page count must be positive.");
        }

        List<PageIndexFlatItem> validItems = flatItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Title) && item.PhysicalIndex is >= 1)
            .OrderBy(item => item.PhysicalIndex)
            .ToList();

        for (int i = 0; i < validItems.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(validItems[i].Structure))
            {
                validItems[i].Structure = (i + 1).ToString();
            }
        }

        Dictionary<string, PageIndexNode> byStructure = [];
        List<PageIndexNode> roots = [];
        int nodeIndex = 0;

        for (int i = 0; i < validItems.Count; i++)
        {
            PageIndexFlatItem item = validItems[i];
            int start = Math.Clamp(item.PhysicalIndex!.Value, 1, pageCount);
            int nextStart = i + 1 < validItems.Count ? Math.Clamp(validItems[i + 1].PhysicalIndex!.Value, 1, pageCount) : pageCount + 1;
            int end = Math.Max(start, Math.Min(pageCount, nextStart - 1));

            PageIndexNode node = new()
            {
                Title = item.Title,
                NodeId = nodeIndex.ToString("D4"),
                StartIndex = start,
                EndIndex = end
            };
            nodeIndex++;

            string structure = item.Structure!;
            byStructure[structure] = node;

            string? parentStructure = GetParentStructure(structure);
            if (parentStructure is not null && byStructure.TryGetValue(parentStructure, out PageIndexNode? parent))
            {
                parent.Nodes ??= [];
                parent.Nodes.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        return roots;
    }

    /// <summary>
    /// 递归移除节点 text 字段，用于 get_document_structure。
    /// </summary>
    public static List<PageIndexNode> CloneWithoutText(IEnumerable<PageIndexNode> nodes)
    {
        return nodes.Select(node => new PageIndexNode
        {
            Title = node.Title,
            NodeId = node.NodeId,
            StartIndex = node.StartIndex,
            EndIndex = node.EndIndex,
            Summary = node.Summary,
            Text = null,
            Nodes = node.Nodes is null ? null : CloneWithoutText(node.Nodes)
        }).ToList();
    }

    private static int? ReadPhysicalIndex(JsonObject itemObject)
    {
        JsonNode? physicalNode = itemObject["physical_index"] ?? itemObject["start_index"] ?? itemObject["page"];
        if (physicalNode is null)
        {
            return null;
        }

        if (physicalNode.GetValueKind() == JsonValueKind.Number)
        {
            return physicalNode.GetValue<int>();
        }

        if (physicalNode.GetValueKind() == JsonValueKind.String)
        {
            string? text = physicalNode.GetValue<string>();
            Match match = PhysicalIndexRegex().Match(text ?? string.Empty);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int parsed))
            {
                return parsed;
            }

            if (int.TryParse(text, out parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static string? GetParentStructure(string structure)
    {
        int lastDot = structure.LastIndexOf('.');
        return lastDot <= 0 ? null : structure[..lastDot];
    }

    [GeneratedRegex("^```(?:json)?|```$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex CodeFenceRegex();

    [GeneratedRegex("physical_index_(\\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex PhysicalIndexRegex();
}
