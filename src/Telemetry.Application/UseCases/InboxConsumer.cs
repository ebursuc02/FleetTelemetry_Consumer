using AutoMapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Telemetry.Application.Abstractions;
using Telemetry.Application.DTOs;
using Telemetry.Domain.Entities;

namespace Telemetry.Application.UseCases;

public class InboxConsumer(
    IIngestSource ingest, 
    IFileParser parser, 
    IFileValidator<SidecarDto> validator, 
    IProcessingStatusWriter statusWriter, 
    BlockingCollection<Record> storage,
    IMapper mapper,
    ILogger<InboxConsumer> logger) : BackgroundService
{

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("InboxConsumer started");
        int processedFiles = 0, failedFiles = 0, producedRecords = 0;

        try
        {
            await foreach (var filePath in ingest.DiscoverAsync(ct))
            {
                if (ct.IsCancellationRequested) break;

                var sidecarPath = filePath + ".meta.json";

                var fileCheckRes = await validator.CheckAsync(filePath, ct);
            
                if (fileCheckRes.IsFailed)
                {
                    var producer = GetProducerFromFileName(filePath);
                    var reason = string.Join("; ", fileCheckRes.Errors.Select(e => e.Message));
                    logger.LogWarning($"Validation failed for {filePath}: {reason}");

                    await statusWriter.WriteErrorAsync(
                        new SidecarDto { ProducerName = producer },
                        sidecarPath,
                        reason,
                        ct);

                    failedFiles++;
                    continue;
                }
         
                var sidecar = fileCheckRes.Value!;
                if (sidecar.Processed)
                {
                    logger.LogDebug($"Already processed, skipping: {filePath}");
                    continue;
                }

                logger.LogDebug($"Parsing: {filePath}");
                var added = 0;
                var marked = false;

                await foreach (var recordRes in parser.ParseAsync(filePath, ct))
                {
                    if (recordRes.IsSuccess)
                    {
                        storage.Add(mapper.Map<Record>(recordRes.Value!), ct);
                        added++;
                        producedRecords++;
                    }
                    else
                    {
                        var reason = string.Join("; ", recordRes.Errors.Select(e => e.Message));
                        logger.LogWarning($"Parsing error for {filePath}: {reason}");

                        await statusWriter.WriteErrorAsync(sidecar, sidecarPath, reason, ct);
                        marked = true;
                        failedFiles++;
                        break;
                    }
                }
                if (!marked)
                    await statusWriter.MarkProcessedAsync(sidecar, sidecarPath, ct);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("InboxConsumer stopping (cancellation requested)");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "InboxConsumer crashed with an unhandled exception");
        }
        finally
        {
            logger.LogInformation(
                $"InboxConsumer stopped. Files processed: {processedFiles}, failed: {failedFiles}, records produced: {producedRecords}");
        }
    }

    private static string GetProducerFromFileName(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        return name.Length >= 4 ? name.Substring(name.Length - 4, 4) : name;
    }
}
