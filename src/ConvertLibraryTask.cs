using MediaBrowser.Model.Tasks;

public class ConvertLibraryTask : IScheduledTask
{
    public string Name => "Convert Dolby Vision MKVs";

    public string Key => nameof(ConvertLibraryTask);

    public string Description => "Convert Dolby Vision Profile 7 media to Profile 8.1";

    public string Category => "Dolby Vision Convert Plugin";

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => [];

    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}