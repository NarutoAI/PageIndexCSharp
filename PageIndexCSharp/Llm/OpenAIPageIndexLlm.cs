using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using PageIndexCSharp.Interfaces;

namespace PageIndexCSharp.Llm;

/// <summary>
/// 基于 Microsoft Agent Framework 的 LLM 适配器。
/// </summary>
public sealed class OpenAIPageIndexLlm : IPageIndexLlm, IPageIndexVisionLlm
{
    private readonly IChatClient _chatClient;
    private readonly AIAgent _indexAgent;
    private readonly AgentSession _session;

    /// <summary>
    /// 使用现有 Maf Agent 创建 LLM 适配器。
    /// </summary>
    public OpenAIPageIndexLlm(IChatClient chatClient)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _indexAgent = _chatClient.AsAIAgent(
            new ChatClientAgentOptions()
            {
                Name = "PageIndex",
                ChatOptions = new ChatOptions()
                {
                    Instructions = PageIndexPrompts.SystemInstructions,
                },
#pragma warning disable MEAI001
                //消息裁剪，保留最新的几条记录，以便模型根据上下文理解
                ChatHistoryProvider =
                    new InMemoryChatHistoryProvider(new() {ChatReducer = new MessageCountingChatReducer(5)})
#pragma warning restore MEAI001
                
            });
        //创建会话历史
        _session = _indexAgent.CreateSessionAsync().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
    {
        AgentResponse response =
            await _indexAgent.RunAsync(prompt, cancellationToken: cancellationToken, session: _session)
                .ConfigureAwait(false);
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

        var response = await _indexAgent.RunAsync(message, cancellationToken: cancellationToken, session: _session);
        return response.Text;
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
    public static OpenAIPageIndexLlm FromOpenAI(string url, string apiKey, string model,ILoggerFactory? loggerFactory = null)
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
            .GetChatClient(model).AsIChatClient()
            .AsBuilder()
            .UseChatLogger(loggerFactory)
            .Build();

        return new OpenAIPageIndexLlm(chatClient);
    }

    /// <summary>
    /// 创建response消息协议的LLM Agent
    /// </summary>
    public static OpenAIPageIndexLlm FromResponseOpenAI(string url, string apiKey, string model,ILoggerFactory? loggerFactory = null)
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
#pragma warning disable MAAI001
        var chatClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions()
            {
                Endpoint = new Uri(url),
                NetworkTimeout = TimeSpan.FromMinutes(15),
            })

           /**
            * https://developers.openai.com/api/docs/guides/migrate-to-responses?update-item-definitions=responses&update-multiturn=responses
            * ["reasoning.encrypted_content"] 返回加密的推理上下文 用户后续对话的传递
            * 禁用有状态存储，但是任然利用推理 设置store=false 同时设置 includeReasoningEncryptedContent=true 返回加密的推理上下文
            API 将返回推理令牌的加密版本；在未来的请求中，您可以像传递常规推理项一样，将这些加密令牌回传给 API
            
            当请求中包含 `encrypted_content` 时，openai会在内存中对其进行解密（绝不写入磁盘），
            利用其生成后续响应，随后便会安全地将其销毁。任何新生成的推理令牌都会被即刻加密并返回给您，
            从而确保不会有任何中间状态被持久化存储。
            */
            .GetResponsesClient().AsIChatClientWithStoredOutputDisabled(model)
            .AsBuilder()
            .UseChatLogger(loggerFactory)
            .ConfigureOptions(configure: options =>
            {
                //设置推理的信息
                options.Reasoning = new ReasoningOptions
                {
                    Effort = ReasoningEffort.Medium,
                    Output = ReasoningOutput.Summary//返回推理的摘要信息 ，因为推理上下文是加密的，无法解密，只能输出摘要
                };
            })
            .Build();
#pragma warning restore MAAI001
#pragma warning restore OPENAI001

        return new OpenAIPageIndexLlm(chatClient);
    }

}

internal static class ChatClientBuilderExtension
{
    extension(ChatClientBuilder chatClientBuilder)
    {
        public ChatClientBuilder UseChatLogger(ILoggerFactory? loggerFactory = null)
        {
            return loggerFactory==null ? chatClientBuilder : chatClientBuilder.UseLogging(loggerFactory);
        }
    }
}