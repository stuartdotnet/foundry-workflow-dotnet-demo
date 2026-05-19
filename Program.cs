// NuGet: Azure.Identity

using Azure.Core;
using Azure.Identity;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

var projectEndpoint = "";
var workflowName = "ContosoPay-Customer-Support-Triage";
var openAiBase = projectEndpoint.TrimEnd('/') + "/openai/v1";

// Get bearer token (scope used by Python SDK: https://ai.azure.com/.default)
var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions());
var token = await credential.GetTokenAsync(new TokenRequestContext(["https://ai.azure.com/.default"]));

using var http = new HttpClient();
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

// Create conversation  (mirrors: openai_client.conversations.create())
var createConvResp = await http.PostAsync(
    $"{openAiBase}/conversations",
    new StringContent("{}", Encoding.UTF8, "application/json"));
if (!createConvResp.IsSuccessStatusCode)
{
    var errorBody = await createConvResp.Content.ReadAsStringAsync();
    throw new Exception($"POST /conversations failed {(int)createConvResp.StatusCode} ({createConvResp.ReasonPhrase}): {errorBody}");
}
createConvResp.EnsureSuccessStatusCode();

var convJson = await createConvResp.Content.ReadAsStringAsync();
string conversationId;
using (var convDoc = JsonDocument.Parse(convJson))
    conversationId = convDoc.RootElement.GetProperty("id").GetString()!;
Console.WriteLine($"Created conversation (id: {conversationId})");

// Stream the workflow  (mirrors: openai_client.responses.create(..., stream=True))
var requestBody = JsonSerializer.Serialize(new
{
    conversation = conversationId,
    agent_reference = new { name = workflowName, type = "agent_reference" },
    input = "Process tickets",
    stream = true
});

var streamRequest = new HttpRequestMessage(HttpMethod.Post, $"{openAiBase}/responses")
{
    Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
};

using var streamResp = await http.SendAsync(streamRequest, HttpCompletionOption.ResponseHeadersRead);
streamResp.EnsureSuccessStatusCode();

using var responseStream = await streamResp.Content.ReadAsStreamAsync();
using var reader = new StreamReader(responseStream);

// Process events from the workflow run  (mirrors: for event in stream)
string? line;
while ((line = await reader.ReadLineAsync()) != null)
{
    if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

    var data = line["data: ".Length..];
    if (data == "[DONE]") break;

    try
    {
        using var eventDoc = JsonDocument.Parse(data);
        if (!eventDoc.RootElement.TryGetProperty("type", out var typeProp)) continue;
        if (typeProp.GetString() != "response.completed") continue;

        var responseId = eventDoc.RootElement
            .GetProperty("response")
            .GetProperty("id")
            .GetString()!;

        // Retrieve full response  (mirrors: openai_client.responses.retrieve(event.response.id))
        var retrieveResp = await http.GetAsync($"{openAiBase}/responses/{responseId}");
        retrieveResp.EnsureSuccessStatusCode();

        using var retrieveDoc = JsonDocument.Parse(await retrieveResp.Content.ReadAsStringAsync());
        Console.WriteLine("\nResponse completed:");
        if (retrieveDoc.RootElement.TryGetProperty("output", out var output))
        {
            foreach (var item in output.EnumerateArray())
            {
                if (item.TryGetProperty("type", out var itemType) && itemType.GetString() == "message"
                    && item.TryGetProperty("content", out var content))
                {
                    foreach (var contentItem in content.EnumerateArray())
                    {
                        if (contentItem.TryGetProperty("text", out var text))
                            Console.WriteLine(text.GetString());
                    }
                }
            }
        }
    }
    catch (JsonException) { }
}

// Clean up resources  (mirrors: openai_client.conversations.delete(conversation_id=...))
await http.DeleteAsync($"{openAiBase}/conversations/{conversationId}");
Console.WriteLine("\nConversation deleted");