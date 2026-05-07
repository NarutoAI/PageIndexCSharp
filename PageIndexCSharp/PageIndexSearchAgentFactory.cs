using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using PageIndexCSharp.Interfaces;
using PageIndexCSharp.Llm;
using PageIndexCSharp.Store;

namespace PageIndexCSharp;

/// <summary>
/// 创建用于 PageIndex 文档检索的智能体工厂。
/// </summary>
public static class PageIndexSearchAgentFactory
{
    /// <summary>
    /// 创建一个带有 PageIndex 检索工具的智能体。
    /// </summary>
    /// <param name="llm">LLM 适配器实例。</param>
    /// <param name="documentStore">文档存储实例。</param>
    /// <returns>已注册检索工具的智能体。</returns>
    public static AIAgent CreateSearchAgent(
        MafPageIndexLlm llm,
        IPageIndexDocumentStore documentStore)
    {
        ArgumentNullException.ThrowIfNull(llm);
        ArgumentNullException.ThrowIfNull(documentStore);

        PageIndexTools tools = new(documentStore);

        return llm.CreateAgent(
            instructions: PageIndexTools.Instructions,
            name: "PageIndexSearchAgent",
            tools:
            [
                AIFunctionFactory.Create(tools.GetAllDocumentAsync),
                AIFunctionFactory.Create(tools.GetDocumentStructureAsync),
                AIFunctionFactory.Create(tools.GetPageContentAsync)
            ]);
    }
}
