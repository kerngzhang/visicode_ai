using System.ComponentModel;
using System.Text.Json;

namespace WritingAgent.Api.Agents.Tools;

public sealed class TavilySearchAgentTool(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration) : AgentToolBase
{
    private const string DescriptionText = "快速搜索写作请求中的陌生概念、产品名、缩写或近期背景。";
    protected override string ToolName => AgentToolNames.TavilySearch;

    protected override string ToolDescription => DescriptionText;

    protected override Delegate ToolMethod => SearchAsync;

    public async Task<string> SearchAsync(
        [Description("搜索关键词，应该简洁具体。")]string query,
        CancellationToken cancellationToken = default)
    {
        var apiKey = configuration.GetValue<string>("TAVILY_API_KEY");
        if(string.IsNullOrWhiteSpace(apiKey))
        {
            return "Tavily API key 未配置。";
        }
        var client = httpClientFactory.CreateClient("tavily");

        var response = await client.PostAsJsonAsync("search", new
        {
            api_key = apiKey,
            query,
            search_depth = "basic",
            include_answer = true,
            max_results = 3
        },cancellationToken);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if(!response.IsSuccessStatusCode)
        {
            return $"Tavily 搜索失败：{json}";
        }
        
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if(root.TryGetProperty("answer",out var answer))
        {
            return answer.GetString() ?? "Tavily 搜索无结果。";
        }

        return "Tavily 搜索无结果。";
    }
}