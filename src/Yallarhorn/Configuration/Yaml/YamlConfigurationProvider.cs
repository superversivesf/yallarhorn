using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using YamlDotNet.RepresentationModel;
using Yallarhorn.Utilities;

namespace Yallarhorn.Configuration.Yaml;

/// <summary>
/// Configuration provider that reads from YAML files with environment variable substitution.
/// </summary>
public class YamlConfigurationProvider : ConfigurationProvider
{
    private readonly YamlConfigurationSource _source;

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlConfigurationProvider"/> class.
    /// </summary>
    /// <param name="source">The configuration source.</param>
    public YamlConfigurationProvider(YamlConfigurationSource source)
    {
        _source = source;
    }

    /// <inheritdoc />
    public override void Load()
    {
        var fileProvider = _source.FileProvider ?? new PhysicalFileProvider(Directory.GetCurrentDirectory());

        var fileInfos = new List<IFileInfo>();
        
        foreach (var filePath in _source.FilePaths)
        {
            var fileInfo = fileProvider.GetFileInfo(filePath);
            if (fileInfo.Exists)
            {
                fileInfos.Add(fileInfo);
            }
            else if (_source.Optional)
            {
                continue;
            }
            else
            {
                throw new FileNotFoundException($"Configuration file not found: {filePath}");
            }
        }

        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var fileInfo in fileInfos)
        {
            using var stream = fileInfo.CreateReadStream();
            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();

            if (_source.ExpandEnvironmentVariables)
            {
                content = EnvVarExpander.Expand(content!);
            }

            var yaml = new YamlStream();
            yaml.Load(new StringReader(content!));

            if (yaml.Documents.Count > 0)
            {
                var root = yaml.Documents[0].RootNode;
                if (root is YamlMappingNode mapping)
                {
                    Flatten(mapping, string.Empty, data);
                }
            }
        }

        Data = data;
    }

    private static void Flatten(YamlMappingNode node, string prefix, Dictionary<string, string?> data)
    {
        foreach (var entry in node.Children)
        {
            var key = entry.Key.ToString();
            var fullKey = string.IsNullOrEmpty(prefix) ? key : $"{prefix}:{key}";

            switch (entry.Value)
            {
                case YamlMappingNode mapping:
                    Flatten(mapping, fullKey, data);
                    break;
                case YamlSequenceNode sequence:
                    FlattenSequence(sequence, fullKey, data);
                    break;
                case YamlScalarNode scalar:
                    data[fullKey] = scalar.Value;
                    break;
                default:
                    data[fullKey] = entry.Value.ToString();
                    break;
            }
        }
    }

    private static void FlattenSequence(YamlSequenceNode node, string prefix, Dictionary<string, string?> data)
    {
        for (var i = 0; i < node.Children.Count; i++)
        {
            var key = $"{prefix}:{i}";
            var value = node.Children[i];

            switch (value)
            {
                case YamlMappingNode mapping:
                    Flatten(mapping, key, data);
                    break;
                case YamlSequenceNode sequence:
                    FlattenSequence(sequence, key, data);
                    break;
                case YamlScalarNode scalar:
                    data[key] = scalar.Value;
                    break;
                default:
                    data[key] = value.ToString();
                    break;
            }
        }
    }
}