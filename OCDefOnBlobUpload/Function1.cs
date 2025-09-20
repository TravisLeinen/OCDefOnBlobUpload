using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace OCDefOnBlobUpload;

public class Function1
{
    private readonly ILogger<Function1> _logger;

    public Function1(ILogger<Function1> logger)
    {
        _logger = logger;
    }

    [Function(nameof(Function1))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
    {
        _logger.LogInformation("HTTP trigger function processed a request.");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync("Hello from Azure Function!");

        var options = new DefaultAzureCredentialOptions { ManagedIdentityClientId = "20238ad9-abb5-4ca6-a9ad-c468b21d0b3d" };
        var cred = new DefaultAzureCredential(options);
        var accountName = "ocdefstorage";
        
        var serviceUri = new Uri($"https://{accountName}.blob.core.windows.net");
        var helper = new StorageHelper(serviceUri, cred);
        await helper.ListContainersAsync();

        BlobServiceClient blob = new BlobServiceClient(serviceUri, cred);

        
        await foreach (var container in _service.GetBlobContainersAsync())
        {
            Console.WriteLine(container.Name);
        }

        return response;
    }

}
