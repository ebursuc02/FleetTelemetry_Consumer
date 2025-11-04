using Microsoft.Extensions.Hosting;
using Telemetry.Application;
using Telemetry.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

var inbox = builder.Configuration["Storage:Inbox"];
var archive = builder.Configuration["Storage:Archive"];
var error = builder.Configuration["Storage:Error"];

if ( inbox == null || !Directory.Exists(inbox) )
{
    Console.WriteLine("Inbox directory not available.");
    return;
}

builder.Services
    .AddTelemetryInfrastructure(inbox!, archive!, error!)
    .AddTelemetryApplication();

await builder.Build().RunAsync();