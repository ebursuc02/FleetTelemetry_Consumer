using Microsoft.Extensions.DependencyInjection;
using System.Text;
using Telemetry.Application.Abstractions;
using Telemetry.Infrastructure.Archiving;
using Telemetry.Infrastructure.Hashing;
using Telemetry.Infrastructure.Ingest;
using Telemetry.Infrastructure.Parsing;
using Telemetry.Infrastructure.Storage;
using Telemetry.Infrastructure.Validators;

namespace Telemetry.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTelemetryInfrastructure(this IServiceCollection s, string inbox, string archive, string error)
        => s
           .AddSingleton<IIngestSource>(new FolderIngestSource(inbox))
           .AddSingleton<IFileValidator<Encoding>, FilePairValidator>()
           .AddSingleton<IFileParser, JsonlsParser>()
           .AddSingleton<IArchiver>(new LocalArchiver(archive, error))
           .AddSingleton<IHasher, Sha256Hasher>()
           .AddSingleton<IStore, RecordStore>();
}
