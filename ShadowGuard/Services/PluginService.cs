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
                using var stream = File.OpenRead(file);
                var plugin = JsonSerializer.Deserialize<PluginDefinition>(stream, JsonOptions);
                if (plugin is null)
                {
                    plugins.Add(CreateInvalidPlugin(file, "Plugin file is empty."));
                    continue;
                }

                plugin.SourceFile = file;
                NormalizePlugin(plugin, file);
                ValidatePlugin(plugin);
                plugins.Add(plugin);
            }
            catch (Exception exception)
            {
                plugins.Add(CreateInvalidPlugin(file, "Plugin file parse failed: " + exception.Message));
            }
        }

        return plugins
            .OrderByDescending(plugin => plugin.Enabled)
            .ThenBy(plugin => plugin.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void NormalizePlugin(PluginDefinition plugin, string sourceFile)
    {
        if (string.IsNullOrWhiteSpace(plugin.PluginId))
        {
            plugin.PluginId = Path.GetFileNameWithoutExtension(sourceFile);
        }

        if (string.IsNullOrWhiteSpace(plugin.DisplayName))
        {
            plugin.DisplayName = plugin.PluginId;
        }

        plugin.Rules ??= new List<PluginRule>();
        plugin.ValidationMessages ??= new List<string>();
    }

    private static void ValidatePlugin(PluginDefinition plugin)
    {
        plugin.ValidationMessages.Clear();

        if (plugin.Rules.Count == 0)
        {
            plugin.ValidationMessages.Add("Plugin has no rules and was disabled.");
            plugin.Enabled = false;
            return;
        }

        var validRules = new List<PluginRule>();
        foreach (var rule in plugin.Rules)
        {
            if (rule.TryValidate(out var error))
            {
                validRules.Add(rule);
            }
            else
            {
                plugin.ValidationMessages.Add(error);
            }
        }

        plugin.Rules = validRules;

        if (plugin.Rules.Count == 0)
        {
            plugin.ValidationMessages.Add("Plugin has no valid rules and was disabled.");
            plugin.Enabled = false;
        }
    }

    private static PluginDefinition CreateInvalidPlugin(string file, string message)
    {
        return new PluginDefinition
        {
            PluginId = Path.GetFileNameWithoutExtension(file),
            DisplayName = Path.GetFileNameWithoutExtension(file),
            Description = message,
            Enabled = false,
            SourceFile = file,
            ValidationMessages = new List<string> { message }
        };
    }
}
