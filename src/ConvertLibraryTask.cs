using Jellyfin.Data.Enums;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Tasks;

public class ConvertLibraryTask(IPluginManager _pluginManager,
                                ILibraryManager _libraryManager,
                                IItemRepository _itemRepo)
    : IScheduledTask
{
    public string Name => "Convert Dolby Vision MKVs";

    public string Key => nameof(ConvertLibraryTask);

    public string Description => "Convert Dolby Vision Profile 7 media to Profile 8.1";

    public string Category => "Dolby Vision Convert Plugin";

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => [];

    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var configuration = (_pluginManager.GetPlugin(Plugin.OurGuid)?.Instance as Plugin)?.Configuration
            ?? throw new Exception("Can't get plugin configuration");

        var allVideo = _itemRepo.GetItems(new InternalItemsQuery
        {
            MediaTypes = [MediaType.Video]
        })
        .Items
        .Cast<Video>();

        return Task.CompletedTask;
    }
}