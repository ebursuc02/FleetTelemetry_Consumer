using System.Collections.Concurrent;
using Telemetry.Application.Abstractions;
using Telemetry.Application.DTOs;

namespace Telemetry.Infrastructure.Storage;

public class RecordStore : IStore
{
    private readonly ConcurrentQueue<RecordDto> records = new();

    public void Add(RecordDto record)
        => records.Enqueue(record);

    public IEnumerable<RecordDto> GetAndClearRange(DateTime fromExclusiveUtc, DateTime toInclusiveUtc)
    {
        var result = new List<RecordDto>();

        Console.WriteLine($"Records: {records.Count}");
        Console.WriteLine($"From: {fromExclusiveUtc}");
        Console.WriteLine($"To: {toInclusiveUtc}");

        while (records.TryPeek(out var record))
        {
            // consume duplicate records
            if (record.TsUtc <= fromExclusiveUtc && records.TryDequeue(out var _)) continue;

            if (record.TsUtc <= toInclusiveUtc && record.TsUtc > fromExclusiveUtc)
            {
                if (!records.TryDequeue(out var item)) break;
                result.Add(item);
            }
            else if (record.TsUtc > toInclusiveUtc) break;
        }

        return result;
    }
}