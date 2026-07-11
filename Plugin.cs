using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public static Guid OurGuid = Guid.Parse("");
    public override string Name => "";
    public override Guid Id => OurGuid;

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths, xmlSerializer)
    {
    }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return [
            new()
            {
                Name = Name,
                EmbeddedResourcePath = "config.html"
            }
        ];
    }
}