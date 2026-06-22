
using System.ClientModel;
using System.ComponentModel;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.AI;
using OpenAI;
using Scalar.AspNetCore;

const string ClarificationInstruction = """
        你是写作工作流中的 Clarification Agent。

        你的唯一职责：把用户模糊、零散或过宽的写作想法澄清成一份可执行的 Writing Brief。

        你不生成候选选题，不做正式研究，不写正文。

        如果用户输入里出现陌生产品名、缩写、行业术语或近期趋势，且这些信息会影响澄清判断，可以先调用 TavilySearchAsync 快速理解一次。
        搜索只用于内部判断，不做正式研究。
        如果搜索工具提示未配置 API Key，不要编造最新事实；继续基于用户已提供的信息做澄清。

        需要澄清时，优先调用名为 clarification 的前端工具。不要用纯文本 JSON 代替。

        clarification 工具参数必须符合这个最小结构：
        {
            "type": "Clarification",
            "version": "2.0",
            "questions": [
                {
                    "id": "audience",
                    "kind": "single_choice",
                    "title": "这篇文章主要写给谁看？",
                    "options": [
                        { "id": "developer", "label": "一线开发者" },
                        { "id": "manager", "label": "技术团队负责人" }
                    ]
                }
            ]
        }

        一次问 2-4 个关键问题。优先澄清这些维度：目标读者、核心问题、核心立场、内容边界、发布场景、风格倾向、证据要求。

        信息足够后，输出固定 Markdown 模板：

        ## Writing Brief
        - 写作目标：
        - 目标读者：
        - 核心问题：
        - 核心立场：
        - 内容边界：写……；不写……
        - 证据要求：
        - 风格倾向：
        - 成功标准：
        """;

static async Task<string> SearchTavilyAsync(
    HttpClient httpClient,
    string? apiKey,
    string query
    )
{
    if(string.IsNullOrWhiteSpace(apiKey))
    {
        throw new Exception("TAVILY_API_KEY environment variable is not set");
    }
    var response = await httpClient.PostAsJsonAsync("search", new
    {
        api_key = apiKey,
        query = query,
        search_depch="basic",
        include_answer=true,
        max_results=3
    });
    var json = await response.Content.ReadAsStringAsync();

    if(!response.IsSuccessStatusCode)
    {
        return $"Tavily search failed: {response.StatusCode}, {json}";
    }

    using var document = JsonDocument.Parse(json);
    var root = document.RootElement;

    if(root.TryGetProperty("answer", out var answer))
    {
        return answer.GetString() ?? "Tavily search succeeded but no answer found.";
    }
    else
    {
        return "Tavily search succeeded but no answer found.";
    }
}

#pragma warning disable MAAI001

#pragma warning enable MAAI001

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddAGUI();

builder.Services.AddHttpClient("tavily", client =>
{
    client.BaseAddress = new Uri("https://api.tavily.com/");
});

builder.Services.AddChatClient( _ =>
{
    var apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
        ?? throw new Exception("DEEPSEEK_API_KEY environment variable is not set");

    var openAIClient = new OpenAIClient(
        new ApiKeyCredential(apiKey),
        new OpenAIClientOptions
        {
            Endpoint = new Uri("https://api.deepseek.com"),
        }
    );

    return openAIClient
        .GetChatClient("deepseek-chat")
        .AsIChatClient();
});

var tavilyApiKey = builder.Configuration.GetValue<string>("TAVILY_API_KEY");
// 先注册IchatClient 再注册AIAgent
// 这样 Agent 可以从 DI 容器中获取 IChatClient 实例

builder.Services.AddAIAgent(
    name:"ClarificationAgent",
    (sp, name) =>
    {
        var chatClient = sp.GetRequiredService<IChatClient>();
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        //var tavilyApiKey = Environment.GetEnvironmentVariable("TAVILY_API_KEY");
        

        [Description("快速搜索写作请求中的陌生概念、产品名、缩写或近期背景")]
        Task<string> TavilySearchAsync(
            [Description("搜索关键词，应该简洁具体")] string query)
        {
            var httpClient = httpClientFactory.CreateClient("tavily");
            return SearchTavilyAsync(httpClient, tavilyApiKey, query);
        }

        return chatClient.AsAIAgent(
            name: name,
            instructions: ClarificationInstruction,
            tools:
            [
                AIFunctionFactory.Create(TavilySearchAsync)
            ]);
    }
);

var app = builder.Build();

app.MapOpenApi();
// http://localhost:5678/scalar/v1
app.MapScalarApiReference();

app.MapGet("/health", () => Results.Ok(new{status = "ok"}));

app.MapPost("/api/writing/chat",async (
    WritingChatRequest request, 
    [FromKeyedServices("ClarificationAgent")]AIAgent agent,
    ILogger<Program> logger) =>
{
    if(string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest(new {error = "Message cannot be empty"});
    }

    logger.LogInformation("收到澄清请求，长度：{Length}", request.Message.Length);

    AgentResponse response  = await agent.RunAsync(request.Message);

    return Results.Ok(new WritingChatResponse(response.Text));
    
});

app.MapAGUI(agentName: "ClarificationAgent",pattern:"/agui/agents/clarification-agent");

app.Run();


public sealed record WritingChatRequest(string Message);
public sealed record WritingChatResponse(string Reply);


