using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using WineAgentFlow;

var endpoint = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_PROJECT_ENDPOINT") ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
const string deploymentName = "gpt-4.1-mini";

PersistentAgentsClient persistentAgentsClient = new(endpoint, new AzureCliCredential());

var openApiSpec = File.ReadAllBytes("spec.json");

var spec = BinaryData.FromBytes(openApiSpec);

var openApiTool = new OpenApiToolDefinition(
    name: "GetWine",
    description: "Endpoint to search for wines from different store branches.",
    spec: spec,
    openApiAuthentication: new OpenApiAnonymousAuthDetails()
);
Console.WriteLine("------------------- Creating agents START -------------------------");

// criando 4 agentes dentro do foundry, graças ao persistentAgent.adminstration
AIAgent northBranchAgent = await CreateNorthBranchAgent(persistentAgentsClient, openApiTool);
AIAgent southBranchAgent = await CreateSouthBranchAgent(persistentAgentsClient, openApiTool);
AIAgent eastBranchAgent = await CreateEastBranchAgent(persistentAgentsClient, openApiTool);
AIAgent westBranchAgent = await CreateWestBranchAgent(persistentAgentsClient, openApiTool);

Console.WriteLine("----------------- Creating agents DONE ----------------------");

var concurrentStartExecutor = new ConcurrentStartExecutor("ConcurrentStartExecutor");

// esse cara vai receber o resultado de cada agente.
var result = new AggregationExecutor("AggregationExecutor");

var workflow = new WorkflowBuilder(concurrentStartExecutor)
    // são 4 targets, a mensagem é broadcasted para os 4 agentes
    .AddFanOutEdge(concurrentStartExecutor, targets: [northBranchAgent, southBranchAgent, eastBranchAgent, westBranchAgent])
    .AddFanInEdge(result, sources: [northBranchAgent, southBranchAgent, eastBranchAgent, westBranchAgent])
    .WithOutputFrom(result).Build();

await using StreamingRun run = await InProcessExecution.StreamAsync(workflow, input: "Where can I buy 'Chateau Margaux' wine ?");

await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    // só vai entrar nesse if após o context.YieldOutputAsync. - isso gera um evento WorkflowOutputEvent 
    if (evt is WorkflowOutputEvent output)
    {
         Console.WriteLine($"Workflow completed with results:\n{output.Data}");
    }
}


Console.WriteLine("------------------- Deleting agents -------------------------");

await persistentAgentsClient.Administration.DeleteAgentAsync(northBranchAgent.Id);
await persistentAgentsClient.Administration.DeleteAgentAsync(southBranchAgent.Id);
await persistentAgentsClient.Administration.DeleteAgentAsync(eastBranchAgent.Id);
await persistentAgentsClient.Administration.DeleteAgentAsync(westBranchAgent.Id);

Console.WriteLine("------------------- Deleting agents DONE -------------------------");

async Task<AIAgent> CreateNorthBranchAgent(PersistentAgentsClient chatClient1, OpenApiToolDefinition openApiTool1)
{
    var agentAsync = await chatClient1.Administration.CreateAgentAsync(
        instructions:
        "You are an assistant the searches wines calling an endpoint already configured, the branch parameter is 'North' and the user must provide the wineName. ",
        name: "NorthBranchAgent",
        model: deploymentName,
        tools: [openApiTool1]
    );

    return await chatClient1.GetAIAgentAsync(agentAsync.Value.Id);

}

async Task<AIAgent> CreateSouthBranchAgent(PersistentAgentsClient chatClient1, OpenApiToolDefinition openApiTool1)
{
    var agentAsync = await chatClient1.Administration.CreateAgentAsync(
        instructions:
        "You are an assistant the searches wines calling an endpoint already configured, the branch parameter is 'South' and the user must provide the wineName. ",
        name: "SouthBranchAgent",
        model: deploymentName,
        tools: [openApiTool1]
    );
    
    return await chatClient1.GetAIAgentAsync(agentAsync.Value.Id);
}

async Task<AIAgent> CreateWestBranchAgent(PersistentAgentsClient chatClient1, OpenApiToolDefinition openApiTool1)
{
    var agentAsync = await chatClient1.Administration.CreateAgentAsync(
        instructions:
        "You are an assistant the searches wines calling an endpoint already configured, the branch parameter is 'West' and the user must provide the wineName. ",
        name: "WestBranchAgent",
        model: deploymentName,
        tools: [openApiTool1]
    );
    
    return await chatClient1.GetAIAgentAsync(agentAsync.Value.Id);
}

async Task<AIAgent> CreateEastBranchAgent(PersistentAgentsClient chatClient1, OpenApiToolDefinition openApiTool1)
{
    var agentAsync = await chatClient1.Administration.CreateAgentAsync(
        instructions:
        "You are an assistant the searches wines calling an endpoint already configured, the branch parameter is 'East' and the user must provide the wineName. ",
        name: "EastBranchAgent",
        model: deploymentName,
        tools: [openApiTool1]
    );
    
    return await chatClient1.GetAIAgentAsync(agentAsync.Value.Id);
}

internal class ConcurrentStartExecutor(
    string id,
    ExecutorOptions? options = null,
    bool declareCrossRunShareable = false)
    : Executor<string>(id, options, declareCrossRunShareable)
{
    public override async ValueTask HandleAsync(string message, IWorkflowContext context,
        CancellationToken cancellationToken = new CancellationToken())
    {
        await context.SendMessageAsync(new ChatMessage(ChatRole.User, message), cancellationToken);
        await context.SendMessageAsync(new TurnToken(emitEvents: true), cancellationToken: cancellationToken);
    }
}

internal class AggregationExecutor(
    string id,
    ExecutorOptions? options = null,
    bool declareCrossRunShareable = false)
    : Executor<ChatMessage>(id, options, declareCrossRunShareable)
{
    private readonly List<ChatMessage> _messages = [];

    public override async ValueTask HandleAsync(ChatMessage message, IWorkflowContext context,
        CancellationToken cancellationToken = new())
    {
      
        _messages.Add(message);

        if (_messages.Count == 4)
        {
            await context.YieldOutputAsync("" +
                                      $"South Branch Agent: {(_messages[0].Text == string.Empty ? "Not Found" : _messages[0].Text )}\n" +
                                      $"North Branch Agent: {(_messages[1].Text == string.Empty ? "Not Found" : _messages[1].Text )}\n" +
                                      $"East Branch Agent: {(_messages[2].Text == string.Empty ? "Not Found" : _messages[2].Text )}\n" +
                                      $"West Branch Agent: {(_messages[3].Text == string.Empty ? "Not Found" : _messages[3].Text )}", cancellationToken);
        }
    }
}