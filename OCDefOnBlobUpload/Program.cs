using Azure.Core.Diagnostics;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

/** Uncomment this block for Azure SDK logging
var listener = new AzureEventSourceListener(
    (eventArgs, text) => Console.WriteLine(text),
    System.Diagnostics.Tracing.EventLevel.Informational // or EventLevel.Verbose for more detail
);
**/

builder.Build().Run();
