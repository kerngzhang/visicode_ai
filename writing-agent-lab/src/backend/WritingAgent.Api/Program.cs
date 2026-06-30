using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using Scalar.AspNetCore;
using WritingAgent.Api.Agents;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddAGUI();

builder.Services.AddHttpClient("tavily", client =>
{
    client.BaseAddress = new Uri("https://api.tavily.com/");
});

//注册工具、Agent定义、Agent组装
builder.Services.AddWritingAgents();

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


var app = builder.Build();

app.MapOpenApi();
// http://localhost:5678/scalar/v1
app.MapScalarApiReference();

app.MapWritingAgents();

// 健康检查接口，用来确认 API 服务是否启动。
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// 普通 JSON 接口：方便 curl、Scalar 或普通前端 fetch 调用。
app.MapPost("/api/writing/chat", async (
    WritingChatRequest request,
    AgentAssembler assembler,
    IEnumerable<AgentDefinition> definitions,
    ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest(new { error = "message 不能为空" });
    }

    logger.LogInformation("收到澄清请求，长度：{Length}", request.Message.Length);

    var definition = definitions.Single(definition => definition.Id == AgentIds.ClarificationAgent);
    var agent = assembler.Assemble(definition);
    AgentResponse response = await agent.RunAsync(request.Message);

    return Results.Ok(new WritingChatResponse(response.Text));
});

app.Run();


public sealed record WritingChatRequest(string Message);
public sealed record WritingChatResponse(string Reply);


