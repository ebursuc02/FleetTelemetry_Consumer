using FluentResults;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Telemetry.Application.Abstractions;

namespace Telemetry.Infrastructure.Ingest;

public class FolderIngestSource : IIngestSource, IDisposable
{
    private readonly string _inbox;
    private readonly FileSystemWatcher _fileWatcher;
    private readonly BlockingCollection<string> _filesToProcess = [];

    public FolderIngestSource(string inbox)
    {
        _inbox = inbox;

        _fileWatcher = new FileSystemWatcher(_inbox)
        {
            Filter = "*.meta.json",
            EnableRaisingEvents = true,
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName
        };

        _fileWatcher.Renamed += OnSidecarEvent;

        AddMissedFiles();
    }

    public void OnSidecarEvent(object? s, FileSystemEventArgs args)
    {
        if (!args.Name!.EndsWith(".meta.json", StringComparison.OrdinalIgnoreCase)) return;
        var dataPath = args.FullPath[..^(".meta.json".Length)];
        if (Path.GetFileName(dataPath).EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) return;

        _filesToProcess.Add(dataPath);
    }

    public async IAsyncEnumerable<Result<string>> DiscoverAsync(
    [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var dataPath in _filesToProcess.GetConsumingEnumerable(ct))
        {
            var sidecar = dataPath + ".meta.json";
            if (!File.Exists(dataPath) || !File.Exists(sidecar))
                continue;

            yield return dataPath;

            await Task.Yield();
        }
    }

    private void AddMissedFiles()
    {
        foreach (var data in Directory.EnumerateFiles(_inbox, "*.jsonl", SearchOption.TopDirectoryOnly))
        {
            if (Path.GetFileName(data).EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
            if (File.Exists(data + ".meta.json")) 
                _filesToProcess.Add(data);
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        _fileWatcher.EnableRaisingEvents = false;

        _fileWatcher.Created -= OnSidecarEvent;
        _fileWatcher.Changed -= OnSidecarEvent;
        _fileWatcher.Renamed -= OnSidecarEvent;

        _fileWatcher.Dispose();
    }
}
