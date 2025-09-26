using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System.ClientModel;
using System.Net;


namespace OCDefOnBlobUpload;

// NOTE: Although search embedding can be done here, Azure AI search does this automatically.
// Without that enabled all this function does is connect to gpt-4o and feed the results of the vector search into it.
public class SubmitChat
{
    private readonly ILogger<SubmitChat> _logger;
    private string openAiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY")!;
    private string openAiEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!;
    private readonly string searchEndpoint = Environment.GetEnvironmentVariable("AZURE_SEARCH_ENDPOINT")!;
    private readonly string searchIndex = Environment.GetEnvironmentVariable("AZURE_SEARCH_INDEX")!;
    private readonly string searchKey = Environment.GetEnvironmentVariable("AZURE_SEARCH_KEY")!;


    public SubmitChat(ILogger<SubmitChat> logger)
    {
        _logger = logger;
    }

    [Microsoft.Azure.Functions.Worker.Function(nameof(SubmitChat))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
    {
        _logger.LogInformation("Sending user query to AI Search...");
        string userQuery = await new StreamReader(req.Body).ReadToEndAsync();
        var client = new AzureOpenAIClient(new Uri(openAiEndpoint), new AzureKeyCredential(openAiKey));
        // Send embeddings to AI search
        /**
        var embedding = await client.GetEmbeddingClient("text-embedding-3-large").GenerateEmbeddingAsync(userQuery);
        ReadOnlyMemory<float> vector = embedding.Value.ToFloats();
        var vectorQuery = new VectorizedQuery(vector)
        {
            Fields = { "text_vector" }
        };

        _logger.LogInformation("Successfully retrieved embeddings, searching with Azure AI Search...");
        **/

        var searchOptions = new SearchOptions
        {
            Size = 5,
            Select = { "chunk" }
        };
        var searchClient = new SearchClient(new Uri(searchEndpoint), searchIndex, new AzureKeyCredential(searchKey));
        var searchResults = await searchClient.SearchAsync<SearchDocument>(userQuery, searchOptions);

        var relevantChunks = searchResults.Value.GetResults()
            .Select(r => r.Document["chunk"]?.ToString())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        // Put relevant chunks together and send to OpenAI chat
        _logger.LogInformation("Successfully searched and found relevant chunks, plugging context into OpenAI chat...");
        var systemPrompt = "You are a law proceedings assistant that pores over court proceeding documents to answer law-related queries and search for potential appeals.";
        var userPrompt = $"Question: {userQuery}";

        // Return the answer to the frontend
        var messages = new ChatMessage[]
        {
            new SystemChatMessage(systemPrompt)
        };
        foreach (var relevantChunk in relevantChunks)
        {
            messages = messages.Append(new SystemChatMessage($"Context: {relevantChunk}")).ToArray();
        }

        string answer;
        try
        {
            ChatClient chatClient = client.GetChatClient("gpt-4o");
            var i = 1;
            foreach (var msg in messages)
            {
                _logger.LogInformation($"Adding context chunk {i}...");
                await chatClient.CompleteChatAsync(msg);
                await Task.Delay(5000); // To avoid rate limiting
                i++;
            }
            ClientResult<ChatCompletion> chatResult = chatClient.CompleteChatAsync(new UserChatMessage(userPrompt)).Result;
            answer = chatResult.Value.Content.FirstOrDefault()?.Text ?? "I'm sorry, I couldn't find an answer to your question.";
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError($"OpenAI request failed: {ex.Message}");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Error communicating with OpenAI service.");
            return errorResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Unexpected error: {ex.Message}");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An unexpected error occurred.");
            return errorResponse;
        }
        _logger.LogInformation("Completed and returned response.");

        // Return the answer to the frontend
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync(answer); // Replace with answer
        return response;
    }
}
