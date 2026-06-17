
using System.ClientModel;
using System.Reflection.Metadata.Ecma335;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.AI;
using OpenAI;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddAGUI();

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

// 先注册IchatClient 再注册AIAgent
// 这样 Agent 可以从 DI 容器中获取 IChatClient 实例

builder.Services.AddAIAgent(
    name:"ClarificationAgent",
    instructions:"""
    你是写作工作流中的 Clarification Agent。

            你的唯一职责：把用户模糊、零散或过宽的写作想法澄清成一份可执行的 Writing Brief。

            你不生成候选选题，不做正式研究，不写正文。

            如果缺少会明显影响后续选题、搜索或写作结构的信息，先用自然语言一次提出 2-4 个关键问题。

            优先澄清这些维度：目标读者、核心问题、核心立场、内容边界、发布场景、风格倾向、证据要求。

            写作目标足够明确后，输出固定 Markdown 模板：

            ## Writing Brief
            - 写作目标：
            - 目标读者：
            - 核心问题：
            - 核心立场：
            - 内容边界：写……；不写……
            - 证据要求：
            - 风格倾向：
            - 成功标准：
    """
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
