using System.Reflection;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace XUnity.AutoTranslator.LlmTranslators.Config;

public class LlmConfig
{
    public string? ApiKey { get; set; }
    public bool ApiKeyRequired { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string GlossaryPrompt { get; set; } = string.Empty;
    public Dictionary<string, object> ModelParams { get; set; } = [];

    [YamlIgnore]
    public List<GlossaryLine> GlossaryLines { get; set; } = [];
}

public static class Configuration
{
    public static string CalculateConfigFolder()
    {
        string gameRoot = AppDomain.CurrentDomain.BaseDirectory;

        string reiPatcher = Path.Combine(gameRoot, "AutoTranslator");
        string bepinEx = Path.Combine(gameRoot, "BepInEx", "config");

        if (Directory.Exists(reiPatcher))
            return reiPatcher;

        if (Directory.Exists(bepinEx))
            return bepinEx;

        throw new DirectoryNotFoundException(
           "Could not find AutoTranslator or BepInEx config folder.");
    }

    public static LlmConfig GetConfiguration(string file)
    {
        if (!File.Exists(file))
            throw new Exception($"Missing Configuration File: {file}");

        var yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(new CamelCaseNamingConvention())
            .Build();
        var config = yamlDeserializer.Deserialize<LlmConfig>(File.ReadAllText(file, Encoding.UTF8));

        //Alternative Extra File loads - so we can package things easier
        var prefix = Path.GetFileNameWithoutExtension(file);
        var path = Path.GetDirectoryName(file);
        LoadSystemPrompt(config, $"{path}/{prefix}-SystemPrompt.txt");
        LoadGlossaryPrompt(config, $"{path}/{prefix}-GlossaryPrompt.txt");
        LoadApiKey(config, $"{path}/{prefix}-ApiKey.txt");

        if (string.IsNullOrEmpty(config.GlossaryPrompt))
            config.GlossaryPrompt = "### Glossary for Consistent Translations\r\nPrioritise and use the translation when an exact match with the original text is found.\r\n## Terms";

        // Load Glossary from yaml
        LoadGlossary(config, $"{path}/{prefix}-Glossary.yaml");

        return config;
    }

    public static void LoadGlossary(LlmConfig config, string file)
    {
        if (!File.Exists(file))
            return;

        var yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(new CamelCaseNamingConvention())
            .Build();
        config.GlossaryLines = yamlDeserializer.Deserialize<List<GlossaryLine>>(File.ReadAllText(file, Encoding.UTF8));
    }

    public static void LoadSystemPrompt(LlmConfig config, string file)
    {
        if (!File.Exists(file))
            return;

        config.SystemPrompt = File.ReadAllText(file, Encoding.UTF8);
    }

    public static void LoadGlossaryPrompt(LlmConfig config, string file)
    {
        if (!File.Exists(file))
            return;

        config.SystemPrompt = File.ReadAllText(file, Encoding.UTF8);
    }

    public static void LoadApiKey(LlmConfig config, string file)
    {
        // Override with file if it exists (for project specific keys)
        if (!File.Exists(file))
            return;

        config.ApiKey = File.ReadAllText(file, Encoding.UTF8);
    }
}
