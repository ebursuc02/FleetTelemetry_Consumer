using Telemetry.Application.DTOs;

namespace Telemetry.Application.Abstractions;

public interface IStore
{
    void Add(RecordDto record);
    IEnumerable<RecordDto> GetAndClearRange(DateTime fromExclusiveUtc, DateTime toInclusiveUtc);
}
