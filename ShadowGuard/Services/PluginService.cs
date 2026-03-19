using System.IO;
using System.Text.Json;

namespace ShadowGuard;

public sealed class PluginService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public List<PluginDefinition> LoadPlugins(string pluginDirectory)
    {
        Directory.CreateDirectory(pluginDirectory);
        var plugins = new List<PluginDefinition>();

        foreach (var file in Directory.EnumerateFiles(pluginDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var plugin = JsonSerializer.Deserialize<PluginDefinition>(File.ReadAllText(file), JsonOptions);
                if (plugin is null)
                {
                    continue;
                }

                plugin.SourceFile = file;
                plugins.Add(plugin);
            }
            catch
            {
                plugins.Add(new PluginDefinition
                {
                    PluginId = Path.GetFileNameWithoutExtension(file),
                    DisplayName = Path.GetFileNameWithoutExtension(file),
                    Description = "插件文件解析失败，请检查 JSON 结构后再启用。",
                    Enabled = false,
                    SourceFile = file
                });
            }
        }

        return plugins
            .OrderByDescending(plugin => plugin.Enabled)
            .ThenBy(plugin => plugin.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
