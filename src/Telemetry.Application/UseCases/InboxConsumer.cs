using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using Telemetry.Application.Abstractions;
using Telemetry.Application.DTOs;

namespace Telemetry.Application.UseCases;

public class InboxConsumer(
    IIngestSource ingest, 
    IFileParser parser, 
    IFileValidator<SidecarDto> validator, 
    IProcessingStatusWriter statusWriter, 
    BlockingCollection<RecordDto> storage) : BackgroundService
{

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // TODO: Refactoring needed
        try
        {
            await foreach (var fileRes in ingest.DiscoverAsync(ct))
            {
                var filePath = fileRes.Value!;
                var sidecarPath = filePath + ".meta.json";

                // TODO: check separatly
                var fileCheckRes = await validator.CheckAsync(filePath, ct);
            
                if (fileCheckRes.IsFailed)
                {
                    if (File.Exists(sidecarPath))
                    {
                        await statusWriter.WriteErrorAsync(new SidecarDto() { ProducerName = "V001" }, sidecarPath, string.Join("; ", fileCheckRes.Errors.Select(e => e.Message)), ct);
                        continue;
                    }
                }
         
                var sidecar = fileCheckRes.Value!;
                if (sidecar.Processed) continue;

                var archived = false;
                await foreach (var recordRes in parser.ParseAsync(filePath, ct))
                {
                    if (recordRes.IsSuccess)
                        storage.Add(recordRes.Value!, ct);
                    else
                    {
                        await statusWriter.WriteErrorAsync(sidecar, sidecarPath, string.Join("; ", recordRes.Errors.Select(e => e.Message)), ct);
                        archived = true;
                        break;
                    }
                }
                if (!archived)
                    await statusWriter.MarkProcessedAsync(sidecar, sidecarPath, ct);
            }
        } catch (OperationCanceledException) { return; }     
    }
}
