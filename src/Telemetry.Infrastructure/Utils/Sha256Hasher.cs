using System.Security.Cryptography;
using Telemetry.Application.Abstractions;

namespace Telemetry.Infrastructure.Utils;

public class Sha256Hasher  : IHasher
{
    public string ComputeHex(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var fileStream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(fileStream);
        return Convert.ToHexString(hash).ToLower();
    }
}
