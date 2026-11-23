using A2A;
using System.Text.Json;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: Pmcro.A2A.Client <base-http-url>");
    return 2;
}
var baseUri = new Uri(args[0].TrimEnd('/') + "/");
var resolver = new A2ACardResolver(baseUri);
var card = await resolver.GetAgentCardAsync();
var skillNames = card.Skills.Select(s => s.Id).ToArray();
if (card.Name != "PMCR-O Echo Worker" || !skillNames.Contains("pmcro-echo"))
    throw new InvalidDataException("The discovered card did not identify the expected PMCR-O worker");
var endpoint = card.SupportedInterfaces.Single(i => i.ProtocolBinding == "JSONRPC").Url;
var client = new A2AClient(new Uri(endpoint));
const string input = "seed-a2a-proof-001";
var request = new SendMessageRequest
{
    Message = new Message
    {
        Role = Role.User,
        MessageId = Guid.NewGuid().ToString("N"),
        Parts = [Part.FromText(input)]
    }
};
var response = await client.SendMessageAsync(request);
var task = response.Task ?? throw new InvalidDataException("A2A returned no AgentTask");
var retrieved = await client.GetTaskAsync(new GetTaskRequest { Id = task.Id });
var output = retrieved.Artifacts?.FirstOrDefault()?.Parts?.FirstOrDefault()?.Text ?? "";
var expected = "PMCRO-WORKER|pmcro-echo-worker|" + input;
var verified = task.Status.State == TaskState.Completed
    && retrieved.Status.State == TaskState.Completed
    && string.Equals(output, expected, StringComparison.Ordinal)
    && !string.IsNullOrWhiteSpace(task.Id);
Console.WriteLine(JsonSerializer.Serialize(new
{
    discovery = "a2a-card-resolver",
    card_name = card.Name,
    card_skill_ids = skillNames,
    card_protocol_binding = "JSONRPC",
    card_endpoint = endpoint,
    requested_agent_id = "pmcro-echo-worker",
    request_input = input,
    task_id = task.Id,
    task_state = task.Status.State.ToString(),
    retrieved_task_state = retrieved.Status.State.ToString(),
    artifact_text = output,
    verified
}, new JsonSerializerOptions { WriteIndented = true }));
return verified ? 0 : 3;
