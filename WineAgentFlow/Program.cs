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

var agentsIds = new[] { northBranchAgent.Id, southBranchAgent.Id, eastBranchAgent.Id, westBranchAgent.Id };

Console.WriteLine("----------------- Creating agents DONE ----------------------");

var concurrentStartExecutor = new ConcurrentStartExecutor("ConcurrentStartExecutor");

// esse cara vai receber o resultado de cada agente.
var result = new AggregationExecutor("AggregationExecutor", agentsIds);

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
    string[] agentsIds,
    ExecutorOptions? options = null,
    bool declareCrossRunShareable = false)
    : Executor<ChatMessage>(id, options, declareCrossRunShareable)
{
    private readonly List<(string agentName, ChatMessage)> _messages = [];

    public override async ValueTask HandleAsync(ChatMessage message, IWorkflowContext context,
        CancellationToken cancellationToken = new())
    {
        var agentName = GetAgentNameById(message.AuthorName);
        
        _messages.Add((agentName, message));
        
        if (_messages.Count == 4)
        {
            await context.YieldOutputAsync("" +
                                      $"{_messages[0].agentName}: {(_messages[0].Item2.Text == string.Empty ? "Not Found" : _messages[0].Item2.Text )}\n" +
                                      $"{_messages[1].agentName}: {(_messages[1].Item2.Text == string.Empty ? "Not Found" : _messages[1].Item2.Text )}\n" +
                                      $"{_messages[2].agentName}: {(_messages[2].Item2.Text == string.Empty ? "Not Found" : _messages[2].Item2.Text )}\n" +
                                      $"{_messages[3].agentName}: {(_messages[3].Item2.Text == string.Empty ? "Not Found" : _messages[3].Item2.Text )}", cancellationToken);
        }
    }
    string GetAgentNameById(string agentId)
    {
        return agentId switch
        {
            _ when agentId == agentsIds[0] => "NorthBranchAgent",
            _ when agentId == agentsIds[1] => "SouthBranchAgent",
            _ when agentId == agentsIds[2] => "EastBranchAgent",
            _ when agentId == agentsIds[3] => "WestBranchAgent",
            _ => throw new InvalidOperationException("Unknown agent ID")
        };
    }
    
}
