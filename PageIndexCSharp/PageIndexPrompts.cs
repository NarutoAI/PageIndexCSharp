namespace PageIndexCSharp;

/// <summary>
/// 集中维护 PageIndex 调用 LLM 时使用的提示词，方便和 Python 版 prompt 对齐。
/// </summary>
public static class PageIndexPrompts
{
    /// <summary>
    /// 用于 Maf Agent 的系统指令，要求模型只输出可解析 JSON。
    /// </summary>
    public const string SystemInstructions = """
        You are a document structure extraction assistant.
        You must follow the user's requested JSON schema exactly.
        Return valid JSON only. Do not wrap the JSON in markdown fences. Do not output explanations outside JSON.
        """;

    /// <summary>
    /// 无目录场景：从带 physical_index 标签的 PDF 页面文本中抽取层级目录。
    /// </summary>
    public static string BuildGenerateTocPrompt(string pagesText) => $$"""
        You are an expert in extracting hierarchical tree structure, your task is to generate the tree structure of the document.

        The structure variable is the numeric system which represents the index of the hierarchy section in the table of contents.
        For example, the first section has structure index 1, the first subsection has structure index 1.1, the second subsection has structure index 1.2, etc.

        For the title, you need to extract the original title from the text, only fix the space inconsistency.

        The provided text contains tags like <physical_index_X> and <physical_index_X> to indicate the start and end of page X.

        For the physical_index, you need to extract the physical index of the start of the section from the text. Keep the <physical_index_X> format.

        The response should be in the following JSON format:
        [
          {
            "structure": "1",
            "title": "title of the section, keep the original title",
            "physical_index": "<physical_index_X>"
          }
        ]

        Directly return the final JSON structure. Do not output anything else.

        Given text:
        {{pagesText}}
        """;

    /// <summary>
    /// 目录页场景：把原始目录文本转换成包含层级编号和页码的 JSON。
    /// </summary>
    public static string BuildTransformTocPrompt(string tocText) => $$"""
        You are given a table of contents, Your job is to transform the whole table of content into a JSON format included table_of_contents.

        structure is the numeric system which represents the index of the hierarchy section in the table of contents.
        For example, the first section has structure index 1, the first subsection has structure index 1.1, the second subsection has structure index 1.2, etc.

        The response should be in the following JSON format:
        {
          "table_of_contents": [
            {
              "structure": "x.x.x or null",
              "title": "title of the section",
              "page": 1
            }
          ]
        }

        You should transform the full table of contents in one go.
        Directly return the final JSON structure, do not output anything else.

        Given table of contents:
        {{tocText}}
        """;

    /// <summary>
    /// 根据节点正文生成节点摘要。
    /// </summary>
    public static string BuildNodeSummaryPrompt(string nodeTitle, string nodeText) => $$"""
        You are given a part of a document, your task is to generate a concise description for this section.
        Focus on what this section is about and what useful information it contains.
        Return plain text only.

        Section title: {{nodeTitle}}

        Partial Document Text:
        {{nodeText}}
        """;

    /// <summary>
    /// 根据结构树生成文档整体描述。
    /// </summary>
    public static string BuildDocumentDescriptionPrompt(string structureJson) => $$"""
        You are given the hierarchical structure of a document with summaries.
        Generate a concise document description in one paragraph.
        Return plain text only.

        Document structure:
        {{structureJson}}
        """;

    /// <summary>
    /// 单文档查询：根据用户问题和文档结构选择最相关的页码范围。
    /// </summary>
    public static string BuildSelectPagesPrompt(string question, string documentJson, string structureJson) => $$"""
        You are a document retrieval planner.
        Your task is to choose the smallest set of pages that are likely to contain evidence for answering the user's question.

        Return valid JSON only in the following format:
        {
          "pages": "5-7 or 3,8 or 12",
          "reason": "brief reason"
        }

        Rules:
        - Use page numbers from start_index and end_index in the structure.
        - Prefer narrow page ranges.
        - Do not answer the question yet.
        - If multiple sections are relevant, use comma separated page ranges.

        User question:
        {{question}}

        Document metadata:
        {{documentJson}}

        Document structure:
        {{structureJson}}
        """;

    /// <summary>
    /// 多文档查询：根据用户问题、文档元信息和结构选择文档与页码范围。
    /// </summary>
    public static string BuildSelectDocumentAndPagesPrompt(string question, string knowledgeBaseJson) => $$"""
        You are a knowledge base retrieval planner.
        Your task is to choose the most relevant document and the smallest set of pages for answering the user's question.

        Return valid JSON only in the following format:
        {
          "doc_id": "document id",
          "pages": "5-7",
          "reason": "brief reason"
        }

        Rules:
        - Select exactly one doc_id from the provided knowledge base.
        - Use page numbers from start_index and end_index in the selected document structure.
        - Prefer narrow page ranges.
        - Do not answer the question yet.

        User question:
        {{question}}

        Knowledge base:
        {{knowledgeBaseJson}}
        """;

    /// <summary>
    /// 根据命中页面正文回答用户问题。
    /// </summary>
    public static string BuildAnswerQuestionPrompt(string question, string sourcePagesJson) => $$"""
        You are a document question answering assistant.
        Answer the user's question using only the provided source pages.
        If the source pages do not contain enough information, say that the information is not available in the provided pages.
        Keep the answer concise and cite page numbers when useful.

        User question:
        {{question}}

        Source pages:
        {{sourcePagesJson}}
        """;
}
