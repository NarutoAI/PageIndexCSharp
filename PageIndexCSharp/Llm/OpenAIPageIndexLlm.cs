using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using PageIndexCSharp.Interfaces;

namespace PageIndexCSharp.Llm;

/// <summary>
/// 基于 Microsoft Agent Framework 的 LLM 适配器。
/// </summary>
public sealed class OpenAIPageIndexLlm : IPageIndexLlm, IPageIndexVisionLlm
{
    private readonly IChatClient _chatClient;
    private readonly AIAgent _indexAgent;
    
    /// <summary>
    /// 使用现有 Maf Agent 创建 LLM 适配器。
    /// </summary>
    public OpenAIPageIndexLlm(IChatClient chatClient)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _indexAgent = _chatClient.AsAIAgent(
            instructions: PageIndexPrompts.SystemInstructions,
            name: "PageIndex");
    }

    /// <inheritdoc />
    public async Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
    {
        AgentResponse response =
            await _indexAgent.RunAsync(prompt, cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.Text;
    }

    /// <inheritdoc />
    public async Task<string> CompleteWithImageAsync(
        string prompt,
        byte[] imageBytes,
        string mediaType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(imageBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);

        var message = new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, new List<AIContent>
        {
            new TextContent(prompt),
            new DataContent(imageBytes, mediaType)
        });

        var response = await _chatClient
            .GetResponseAsync(message, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return response.Text ?? string.Empty;
    }

    /// <summary>
    /// 基于当前聊天客户端创建一个带指定指令和工具的 Agent。
    /// </summary>
    public AIAgent CreateAgent(string instructions, string name, IList<AITool> tools)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instructions);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(tools);

        return _chatClient.AsAIAgent(
            instructions: instructions,
            name: name,
            tools: tools);
    }

    /// <summary>
    /// 通过 OpenAI API Key 和模型名称快速创建 MafPageIndexLlm。
    /// </summary>
    public static OpenAIPageIndexLlm FromOpenAI(string url, string apiKey, string model)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("OpenAI API key cannot be empty.", nameof(apiKey));
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("OpenAI model cannot be empty.", nameof(model));
        }

        var chatClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions()
            {
                Endpoint = new Uri(url),
                NetworkTimeout = TimeSpan.FromMinutes(15)
            })
            .GetChatClient(model).AsIChatClient();

        return new OpenAIPageIndexLlm(chatClient);
    }
    /// <summary>
    /// 创建response消息协议的LLM Agent
    /// </summary>
    public static OpenAIPageIndexLlm FromResponseOpenAI(string url, string apiKey, string model)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("OpenAI API key cannot be empty.", nameof(apiKey));
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("OpenAI model cannot be empty.", nameof(model));
        }

#pragma warning disable OPENAI001
        var chatClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions()
            {
                Endpoint = new Uri(url),
                NetworkTimeout = TimeSpan.FromMinutes(15)
            })
            .GetResponsesClient().AsIChatClient(model);
#pragma warning restore OPENAI001

        return new OpenAIPageIndexLlm(chatClient);
    }
}