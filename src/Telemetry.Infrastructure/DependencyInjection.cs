using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telemetry.Application.Abstractions;
using Telemetry.Application.DTOs;
using Telemetry.Domain.Abstractions;
using Telemetry.Domain.Accumulators;
using Telemetry.Domain.Repositories;
using Telemetry.Infrastructure.Ingest;
using Telemetry.Infrastructure.Parsing;
using Telemetry.Infrastructure.Repositories;
using Telemetry.Infrastructure.Utils;
using Telemetry.Infrastructure.Validators;

namespace Telemetry.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTelemetryInfrastructure(this IServiceCollection s, string inbox, string kpi)
        => s
           .AddSingleton<IIngestSource>(new FolderIngestSource(inbox))
           .AddSingleton<IFileValidator<SidecarDto>, FileAndSidecarValidator>()
           .AddSingleton<IFileParser, JsonlsParser>()
           .AddSingleton<IProcessingStatusWriter, FileProcessingStatusWriter>()
           .AddSingleton<IHasher, Sha256Hasher>()
           .AddSingleton<IKpiRepository>(sp =>
               new FileKpiRepository(
                   logger: sp.GetRequiredService<ILogger<FileKpiRepository>>(),
                   folderPath: kpi))
           .AddSingleton<ISidecarRepository>(sp =>
               new FileSidecarRepository(
                   logger: sp.GetRequiredService<ILogger<FileSidecarRepository>>(),
                   sidecarFolder: kpi));
}
