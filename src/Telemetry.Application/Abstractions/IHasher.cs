namespace Telemetry.Application.Abstractions;

public interface IHasher
{
    public string ComputeHex(string filePath);
}
