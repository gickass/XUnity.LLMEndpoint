using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace XUnity.AutoTranslator.LlmTranslators.Config;

public class LlmConfig
{
    public string? ApiKey { get; set; }
    public bool ApiKeyRequired { get; set; } = false;
    public List<string> Urls { get; set; } = new List<string>();
    public string Model { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string GlossaryPrompt { get; set; } = string.Empty;
    public Dictionary<string, object> ModelParams { get; set; } = new Dictionary<string, object>();
    public List<GlossaryLine> GlossaryLines { get; set; } = new List<GlossaryLine>();
}

public static class Configuration
{
    public static LlmConfig GetConfiguration(string file)
    {
        string gameRoot = AppDomain.CurrentDomain.BaseDirectory;
        string[] foldersToCheck = {
        Path.Combine(gameRoot, "AutoTranslator"),
        Path.Combine(gameRoot, "BepInEx", "config")
        };

        foreach (var folder in foldersToCheck)
        {
            string filepath = Path.Combine(folder, file);
            if (!File.Exists(filepath)) continue;
            var yamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var config = yamlDeserializer.Deserialize<LlmConfig>(File.ReadAllText(filepath, Encoding.UTF8));

            config.Urls = config.Urls.Select(u => u.Trim()).ToList();

            if (config.Urls == null || config.Urls.Count == 0)
                throw new Exception("No URLs configured for the endpoint.");

            bool endpointFound = false;

            foreach (var baseUrl in config.Urls)
            {
                var trimmedBase = BaseEndpointBehavior.GetDomain(baseUrl);
                var Combine = new Func<string, string>(path => BaseEndpointBehavior.CombineUrl(trimmedBase, path));

                // Try common endpoint variants
                var variants = new List<string>
                {
                    Combine("responses"),
                    Combine("v1/responses"),
                     Combine("v1/chat/completions"),
                    Combine("chat/completions"),
                };
                foreach (var url in variants)
                {
                    try
                    {
                        var req = (HttpWebRequest)WebRequest.Create(url);
                        req.Method = "POST";
                        req.ContentType = "application/json";
                        if (!string.IsNullOrWhiteSpace(config.ApiKey))
                            req.Headers["Authorization"] = $"Bearer {config.ApiKey}";

                        using (var writer = new StreamWriter(req.GetRequestStream()))
                        {
                            writer.Write($@"
                            {{
                                ""model"": ""{config.Model}"",
                                ""messages"": [
                                    {{
                                        ""role"": ""user"",
                                        ""content"": ""Hello there.""
                                    }}
                                ]
                            }}");
                        }
                        using var resp = (HttpWebResponse)req.GetResponse();
                        config.Urls = new List<string> { url };
                        endpointFound = true;
                        break;
                    }
                    catch {}
                }
                if (!endpointFound)
                throw new Exception("No valid endpoint URL found or API Key is not provided on required endpoint.");
            }

            // When model is not set (Usually for local)
            if (string.IsNullOrWhiteSpace(config.Model))
            {
                foreach (var baseUrl in config.Urls)
                {
                    var trimmedBase = BaseEndpointBehavior.GetDomain(baseUrl);
                    var Combine = new Func<string, string>(path => BaseEndpointBehavior.CombineUrl(trimmedBase, path));

                    var variants = new List<string>
                    {
                        Combine("v1/model"),
                        Combine("model"),
                        Combine("api/v1/model"),
                    };
                    foreach (var url in variants)
                    {
                        try
                        {
                            var req = WebRequest.CreateHttp(url);
                            req.Method = "GET";

                            using (var resp = (HttpWebResponse)req.GetResponse())
                            {
                                if (resp.StatusCode == HttpStatusCode.OK)
                                {
                                    using var reader = new StreamReader(resp.GetResponseStream());
                                    var body = reader.ReadToEnd()?.Trim();
                                    string model = ExtractResult(body);
                                    config.Model = model;

                                    Console.WriteLine($"Model parameter is blank, use detected model = {model} in {url}");
                                    return config;
                                }
                            }
                        }
                        catch
                        {
                            // Try next link
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(config.Model))
                        break; // exit outer loop if model found
                }
                throw new Exception("Could not auto-detect model name from the provided URL. Please set the model");
            }
            return config;
        }
        throw new FileNotFoundException($"Could not find '{file}' in AutoTranslator or BepInEx config folder.");
    }
    private static string ExtractResult(string body)
    {
        var m = Regex.Match(body, "\"result\"\\s*:\\s*\"(?:[^/\"]*/)?([^\"]+)\"");
        return m.Success ? m.Groups[1].Value : body;
    }
}