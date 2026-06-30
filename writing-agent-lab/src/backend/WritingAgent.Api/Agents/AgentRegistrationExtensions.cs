using WritingAgent.Api.Agents.Tools;

namespace WritingAgent.Api.Agents;
public static class AgentRegistrationExtensions
{
    public static IServiceCollection AddWritingAgents(this IServiceCollection services)
    {
        //先注册工具
        services.AddSingleton<IAgentTool, TavilySearchAgentTool>();
        services.AddSingleton<AgentToolCatalog>();

        //注册 Agent定义
        services.AddSingleton<AgentDefinition, ClarificationAgentDefinition>();


        // 注册 Agent 组装器
        services.AddSingleton<AgentAssembler>();

        return services;
    }
}