using Microsoft.Extensions.Hosting;
using System.Text;
using System.Text.RegularExpressions;
using Telemetry.Application.Abstractions;
using Telemetry.Application.Results;

namespace Telemetry.Application.UseCases;

public class InboxConsumer(IIngestSource ingest, IFileParser parser, IFileValidator<Encoding> validator, IArchiver archiver, IStore storage) : BackgroundService
{
    private readonly IIngestSource _ingest = ingest;
    private readonly IFileParser _parser = parser;
    private readonly IFileValidator<Encoding> _validator = validator;
    private readonly IArchiver _archiver = archiver;
    private readonly IStore _storage = storage;

    private static readonly Regex TelemetryFileRegex = new(
        @"^telemetry_(?<date>\d{8})_(?<time>\d{6})_(?<vehicleId>V[0-9]+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var fileRes in _ingest.DiscoverAsync(ct))
            {
                var filePath = fileRes.Value!;
                var sidecarPath = filePath + ".meta.json";

                var fileCheckRes = await _validator.CheckAsync(filePath, ct);
                var parseRes = ParseFileNameInfo(filePath);
            
                if (!fileCheckRes.Success || !parseRes.Success)
                {
                    await _archiver.ErrorAsync(filePath, sidecarPath, fileCheckRes.Error ?? parseRes.Error!, ct);
                    continue;
                }
         
                var dateTime = parseRes.Value!;
                var encoding = fileCheckRes.Value!;

                try
                {
                    var archived = false;
                    await foreach (var recordRes in _parser.ParseAsync(filePath, encoding, ct))
                    {
                        if (recordRes.Success)
                            _storage.Add(recordRes.Value!);
                        else
                        {
                            await _archiver.ErrorAsync(filePath, sidecarPath, recordRes.Error!, ct);
                            archived = true;
                            break;
                        }
                    }
                    if (!archived)
                        await _archiver.ArchiveAsync(filePath, sidecarPath, dateTime.ToString("yyyy/MM/dd"), ct);
                }
                catch (Exception ex)
                {
                    await _archiver.ErrorAsync(filePath, sidecarPath, ex.Message, ct);
                }
            }
        } catch (OperationCanceledException) { return; }     
    }

    private static Result<DateTime> ParseFileNameInfo(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);

        var m = TelemetryFileRegex.Match(name);
        if (!m.Success)
            return Result<DateTime>.Fail($"Filename '{name}' does not match 'telemetry_yyyyMMdd_hhmmss_vehId'.");

        return CheckDateAndTime(m.Groups["date"].Value, m.Groups["time"].Value);
    }

    private static Result<DateTime> CheckDateAndTime(string date, string time)
    {
        var year = int.Parse(date[..4]);
        var month = int.Parse(date[4..6]);
        var day = int.Parse(date[6..]);

        if ((month < 1 || month > 12) || (day < 1 || day > 31))
            return Result<DateTime>.Fail($"The filename date format is not correct: {year}/{month}/{day}.");

        var hour = int.Parse(time[..2]);
        var minute = int.Parse(time[2..4]);
        var second = int.Parse(time[4..]);

        if (hour > 23 || minute > 59 || second > 59)
            return Result<DateTime>.Fail($"The filename time format is not correct: {hour}:{minute}:{second}.");

        return Result<DateTime>.Ok(new DateTime(year, month, day, hour, minute, second));
    }

}
