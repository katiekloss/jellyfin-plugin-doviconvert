using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public static Guid OurGuid = Guid.Parse("adc5ba9b-de9c-4f24-914c-2992f21e825e");
    public override string Name => "DoVi Convert";
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