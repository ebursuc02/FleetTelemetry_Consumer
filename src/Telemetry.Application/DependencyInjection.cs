using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using Telemetry.Application.Abstractions;
using Telemetry.Application.Policies;
using Telemetry.Application.UseCases;
using Telemetry.Application.Utils;
using Telemetry.Domain.Abstractions;
using Telemetry.Domain.Accumulators;
using Telemetry.Domain.Entities;

namespace Telemetry.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTelemetryApplication(this IServiceCollection s, int flushTimeIntervalInMin)
        => s.AddHostedService<InboxConsumer>()
            .AddHostedService<KPIOrchestratorHandler>()
            .AddSingleton<IFlushPolicy>(new IntervalFlushPolicy(TimeSpan.FromMinutes(flushTimeIntervalInMin)))
            .AddSingleton<IClock, SystemClock>()
            .AddSingleton<BlockingCollection<Record>>()
            .AddAutoMapper(_ => { }, typeof(MappingProfile).Assembly)
            .AddSingleton<IFuelAccumulator, FuelAccumulator>()
            .AddSingleton<IStopAccumulator, StopAccumulator>();
}
