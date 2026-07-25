using System.Diagnostics;
using Jellyfin.Data.Enums;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

public class ConvertLibraryTask(IPluginManager _pluginManager,
                                IApplicationPaths _paths,
                                IItemRepository _itemRepo,
                                IMediaEncoder _encoder,
                                ILogger<ConvertLibraryTask> _logger)
    : IScheduledTask
{
    public string Name => "Convert Dolby Vision MKVs";

    public string Key => nameof(ConvertLibraryTask);

    public string Description => "Convert Dolby Vision Profile 7 media to Profile 8.1";

    public string Category => "Dolby Vision Convert Plugin";

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => [];

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var configuration = (_pluginManager.GetPlugin(Plugin.OurGuid)?.Instance as Plugin)?.Configuration
            ?? throw new Exception("Can't get plugin configuration");

        var allVideo = _itemRepo.GetItems(new InternalItemsQuery
        {
            MediaTypes = [MediaType.Video]
        })
        .Items
        .Cast<Video>();

        List<Video> toConvert = [];

        foreach (var video in allVideo)
        {
            var stream = video.GetDefaultVideoStream();
            if (stream is null || stream.DvProfile is not 7 || stream.DvLevel is not 6) continue;

            toConvert.Add(video);
        }

        if (toConvert.Count == 0) return;

        if (!File.Exists(_paths.TempDirectory))
        {
            Directory.CreateDirectory(_paths.TempDirectory);
        }

        foreach (var video in toConvert)
        {
            await ConvertOne(video, configuration, cancellationToken);
        }
    }

    private async Task ConvertOne(Video video, PluginConfiguration config, CancellationToken token)
    {
        if (config.PathToDoviTool is null) throw new Exception("dovi_tool not found");
        if (config.PathToMkvMerge is null) throw new Exception("mkvmerge not found");

        var correlationId = Guid.NewGuid().ToString()[..8];

        // Extract the raw HEVC stream and convert it to 8.1 in one step
        using var ffmpeg = new Process()
        {
            StartInfo = new ProcessStartInfo(_encoder.EncoderPath)
            {
                Arguments = string.Join(" ", [
                    "-y",
                    $"-i \"{video.Path}\"",
                    "-dn",
                    "-c:v copy",
                    "-f hevc",
                    "-"
                ]),
                RedirectStandardError = true,
                RedirectStandardOutput = true
            }
        };

        _logger.LogInformation("{Command} {Arguments}", ffmpeg.StartInfo.FileName, ffmpeg.StartInfo.Arguments);
        ffmpeg.Start();

        _ = WriteStreamToLog(
                Path.Combine(_paths.LogDirectoryPath, $"ffmpeg_hevc_{video.Id}_{correlationId}.log"),
                ffmpeg.StandardError.BaseStream,
                ffmpeg,
                token)
            .ConfigureAwait(false);

                using var doviTool = new Process()
        {
            StartInfo = new ProcessStartInfo(config.PathToDoviTool)
            {
                Arguments = string.Join(" ", [
                    "-m 2", // convert RPU to 8.1
                    "convert", // modify RPU
                    "--discard", // discard EL
                    "-",
                    "-o temp.hevc"
                ]),
                RedirectStandardInput = true,
                RedirectStandardError = true
            }
        };

        _logger.LogInformation("{Command} {Arguments}", doviTool.StartInfo.FileName, doviTool.StartInfo.Arguments);
        doviTool.Start();

        _ = WriteStreamToLog(Path.Combine(_paths.LogDirectoryPath, $"dovi_tool_{video.Id}_{correlationId}.log"),
                             doviTool.StandardError.BaseStream,
                             doviTool,
                             token)
            .ConfigureAwait(false);

        // dovitool won't quit until we close its stdin, which CopyTo won't do
        await Task.WhenAll(
            RunToExit(ffmpeg, token),
            RunToExit(doviTool, token),
            ffmpeg.StandardOutput.BaseStream.CopyToAsync(doviTool.StandardInput.BaseStream, token)
                                            .ContinueWith(_ => doviTool.StandardInput.Close(), token));

        // Merge the audio/subtitle streams from the original file and the modified HEVC stream to a new MKV
        using var mkvmerge = new Process()
        {
            StartInfo = new ProcessStartInfo(config.PathToMkvMerge)
            {
                Arguments = string.Join(" ", [
                    "-o",
                    "idk.mkv",
                    "temp.hevc",
                    "-D", // select all streams except video from next file
                    $"\"{video.Path}\""
                ]),
                RedirectStandardError = true,
                RedirectStandardOutput = true
            }
        };

        _logger.LogInformation("{Command} {Arguments}", mkvmerge.StartInfo.FileName, mkvmerge.StartInfo.Arguments);
        mkvmerge.Start();
        _ = WriteStreamToLog(
                Path.Combine(_paths.LogDirectoryPath, $"mkvmerge_{video.Id}_{correlationId}.log"),
                mkvmerge.StandardOutput.BaseStream,
                mkvmerge,
                token)
            .ConfigureAwait(false);

        await RunToExit(mkvmerge, token);
    }

    private static async Task WriteStreamToLog(string logPath, Stream logStream, Process logProcess, CancellationToken token)
    {
        await using var writer = new StreamWriter(File.Open(
            logPath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.Read));

        using var reader = new StreamReader(logStream);

        while (logStream.CanRead && !logProcess.HasExited)
        {
            var line = await reader.ReadLineAsync(token).ConfigureAwait(false);
            if (!writer.BaseStream.CanWrite)
            {
                break;
            }
            await writer.WriteLineAsync(line).ConfigureAwait(false);
            if (!writer.BaseStream.CanWrite)
            {
                break;
            }
            await writer.FlushAsync(token).ConfigureAwait(false);
        }
    }

    private static async Task RunToExit(Process p, CancellationToken token)
    {
        try
        {
            await p.WaitForExitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            p.Kill();
            throw;
        }
    }

}