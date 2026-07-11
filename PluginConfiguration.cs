using System.Text.Json.Serialization;
using MediaBrowser.Model.Plugins;

public class PluginConfiguration : BasePluginConfiguration
{
    public bool ConfigOption { get; set; }
}