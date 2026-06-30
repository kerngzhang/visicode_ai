using Microsoft.Extensions.AI;

namespace WritingAgent.Api.Agents.Tools;

public interface IAgentTool
{
    public AIFunction AsAIFunction();
}