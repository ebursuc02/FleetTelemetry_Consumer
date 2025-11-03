using Microsoft.Extensions.DependencyInjection;
using Telemetry.Application.UseCases;

namespace Telemetry.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTelemetryApplication(this IServiceCollection s)
    => s.AddHostedService<InboxConsumer>()
        .AddHostedService<Orchestrator>();
}
