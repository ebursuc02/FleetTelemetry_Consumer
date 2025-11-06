using Microsoft.Extensions.DependencyInjection;
using System.Text;
using Telemetry.Application.Abstractions;
using Telemetry.Application.DTOs;
using Telemetry.Domain.Accumulators;
using Telemetry.Domain.Repositories;
using Telemetry.Domain.Abstractions;
using Telemetry.Infrastructure.Ingest;
using Telemetry.Infrastructure.Parsing;
using Telemetry.Infrastructure.Repositories;
using Telemetry.Infrastructure.Validators;
using System.Threading.Channels;
using Telemetry.Infrastructure.Utils;

namespace Telemetry.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTelemetryInfrastructure(this IServiceCollection s, string inbox, string kpi)
        => s
           .AddSingleton<IIngestSource>(new FolderIngestSource(inbox))
           .AddSingleton<IFileValidator<SidecarDto>, FilePairValidator>()
           .AddSingleton<IFileParser, JsonlsParser>()
           .AddSingleton<IProcessingStatusWriter, FileProcessingStatusWriter>()
           .AddSingleton<IHasher, Sha256Hasher>()
           .AddSingleton<IKpiRepository>(new FileKpiRepository(kpi))
           .AddSingleton<ISidecarRepository>(new FileSidecarRepository(kpi))
           .AddSingleton<IFuelAccumulator, FuelAccumulator>()
           .AddSingleton<IStopAccumulator, StopAccumulator>();
}
