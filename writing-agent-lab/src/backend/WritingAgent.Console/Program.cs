using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

var apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
    ?? throw new InvalidOperationException("DEEPSEEK_API_KEY environment variable is not set.");

var openAiClient = new OpenAIClient(
    new ApiKeyCredential(apiKey),
    new OpenAIClientOptions
    {
        Endpoint = new Uri("https://api.deepseek.com")
    });

IChatClient chatClient = openAiClient
    .GetChatClient("deepseek-chat")
    .AsIChatClient();

AIAgent clarificationAgent = chatClient.AsAIAgent(
    name: "ClarificationAgent",
    instructions:"""
    你是写作工作流中的 Clarification Agent。

        你的唯一职责：把用户模糊、零散或过宽的写作想法澄清成一份可执行的 Writing Brief。

        你不生成候选选题，不做正式研究，不写正文。

        如果缺少会明显影响后续选题、搜索或写作结构的信息，先用自然语言一次提出 2-4 个关键问题。

        优先澄清这些维度：
        - 目标读者：写给谁看
        - 核心问题：文章要回答什么问题
        - 核心立场：希望表达什么判断
        - 内容边界：写什么，不写什么
        - 发布场景：公众号、博客、小红书、内部文档等
        - 风格倾向：理性分析、故事化、犀利评论、实操指南等

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

Console.WriteLine("ClarificationAgent 己启动，请输入 exit 退出。 \n");

var history = new List<ChatMessage>();

while (true)
{
    Console.Write("你：");
    var input = Console.ReadLine();
    if(string.IsNullOrWhiteSpace(input))
    {
        continue;
    }

    if(input.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    try
    {
        history.Add(new ChatMessage(ChatRole.User, input));

        AgentResponse response = await clarificationAgent.RunAsync(history);

        history.AddRange(response.Messages);

        Console.WriteLine($"ClarificationAgent：{response.Text}\n");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"调用失败：{ex.Message}\n");
    }
}