using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telemetry.Application;
using Telemetry.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole().AddDebug();

var inbox = builder.Configuration["Storage:Inbox"];
var kpis = builder.Configuration["Storage:Kpis"];

var timespanInMin = int.Parse(builder.Configuration["Utils:FlushTimeSpanMin"]!);

if ( inbox == null || !Directory.Exists(inbox) )
{
    Console.WriteLine("Inbox directory not available.");
    return;
}

builder.Services
    .AddTelemetryInfrastructure(inbox!, kpis!)
    .AddTelemetryApplication(timespanInMin);

await builder.Build().RunAsync();