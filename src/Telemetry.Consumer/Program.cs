using Microsoft.Extensions.Hosting;
using Telemetry.Application;
using Telemetry.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

var inbox = builder.Configuration["Storage:Inbox"] ?? "C:\\telemetry\\inbox";
var archive = builder.Configuration["Storage:Archive"] ?? "C:\\telemetry\\archive";
var error = builder.Configuration["Storage:Error"] ?? "C:\\telemetry\\error";

builder.Services
    .AddTelemetryInfrastructure(inbox, archive, error)
    .AddTelemetryApplication();

await builder.Build().RunAsync();