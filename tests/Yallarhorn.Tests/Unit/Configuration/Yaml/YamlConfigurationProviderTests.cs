namespace Yallarhorn.Tests.Unit.Configuration.Yaml;

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Xunit;
using Yallarhorn.Configuration.Yaml;

public class YamlConfigurationProviderTests : IDisposable
{
    private readonly string _tempDir;

    public YamlConfigurationProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"yaml_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void Load_SimpleYaml_ShouldParseCorrectly()
    {
        var yamlContent = """
            version: "1.0"
            poll_interval: 3600
            download_dir: ./downloads
            """;
        var filePath = Path.Combine(_tempDir, "config.yaml");
        File.WriteAllText(filePath, yamlContent);

        var config = new ConfigurationBuilder()
            .AddYamlFile(s =>
            {
                s.FileProvider = new PhysicalFileProvider(_tempDir);
                s.FilePaths.Add("config.yaml");
                s.Optional = false;
                s.ExpandEnvironmentVariables = false;
            })
            .Build();

        config["version"].Should().Be("1.0");
        config["poll_interval"].Should().Be("3600");
        config["download_dir"].Should().Be("./downloads");
    }

    [Fact]
    public void Load_NestedYaml_ShouldParseWithColonNotation()
    {
        var yamlContent = """
            server:
              host: "0.0.0.0"
              port: 8080
            database:
              path: ./data.db
              pool_size: 5
            """;
        var filePath = Path.Combine(_tempDir, "config.yaml");
        File.WriteAllText(filePath, yamlContent);

        var config = new ConfigurationBuilder()
            .AddYamlFile(s =>
            {
                s.FileProvider = new PhysicalFileProvider(_tempDir);
                s.FilePaths.Add("config.yaml");
                s.Optional = false;
                s.ExpandEnvironmentVariables = false;
            })
            .Build();

        config["server:host"].Should().Be("0.0.0.0");
        config["server:port"].Should().Be("8080");
        config["database:path"].Should().Be("./data.db");
        config["database:pool_size"].Should().Be("5");
    }

    [Fact]
    public void Load_ArrayYaml_ShouldParseWithIndexNotation()
    {
        var yamlContent = """
            channels:
              - name: Channel One
                url: https://www.youtube.com/@one
              - name: Channel Two
                url: https://www.youtube.com/@two
            """;
        var filePath = Path.Combine(_tempDir, "config.yaml");
        File.WriteAllText(filePath, yamlContent);

        var config = new ConfigurationBuilder()
            .AddYamlFile(s =>
            {
                s.FileProvider = new PhysicalFileProvider(_tempDir);
                s.FilePaths.Add("config.yaml");
                s.Optional = false;
                s.ExpandEnvironmentVariables = false;
            })
            .Build();

        config["channels:0:name"].Should().Be("Channel One");
        config["channels:0:url"].Should().Be("https://www.youtube.com/@one");
        config["channels:1:name"].Should().Be("Channel Two");
        config["channels:1:url"].Should().Be("https://www.youtube.com/@two");
    }

    [Fact]
    public void Load_WithEnvVarExpansion_ShouldExpandVariables()
    {
        Environment.SetEnvironmentVariable("TEST_VAR", "test_value");
        
        var yamlContent = """
            download_dir: ${TEST_VAR}/downloads
            """;
        var filePath = Path.Combine(_tempDir, "config.yaml");
        File.WriteAllText(filePath, yamlContent);

        var config = new ConfigurationBuilder()
            .AddYamlFile(s =>
            {
                s.FileProvider = new PhysicalFileProvider(_tempDir);
                s.FilePaths.Add("config.yaml");
                s.Optional = false;
                s.ExpandEnvironmentVariables = true;
            })
            .Build();

        config["download_dir"].Should().Be("test_value/downloads");
        
        Environment.SetEnvironmentVariable("TEST_VAR", null);
    }

    [Fact]
    public void Load_WithEnvVarDefault_ShouldUseDefaultWhenNotSet()
    {
        Environment.SetEnvironmentVariable("MISSING_VAR", null);
        
        var yamlContent = """
            download_dir: ${MISSING_VAR:-./default}/downloads
            """;
        var filePath = Path.Combine(_tempDir, "config.yaml");
        File.WriteAllText(filePath, yamlContent);

        var config = new ConfigurationBuilder()
            .AddYamlFile(s =>
            {
                s.FileProvider = new PhysicalFileProvider(_tempDir);
                s.FilePaths.Add("config.yaml");
                s.Optional = false;
                s.ExpandEnvironmentVariables = true;
            })
            .Build();

        config["download_dir"].Should().Be("./default/downloads");
    }

    [Fact]
    public void Load_OptionalMissingFile_ShouldNotThrow()
    {
        var act = () => new ConfigurationBuilder()
            .AddYamlFile(s =>
            {
                s.FileProvider = new PhysicalFileProvider(_tempDir);
                s.FilePaths.Add("nonexistent.yaml");
                s.Optional = true;
                s.ExpandEnvironmentVariables = false;
            })
            .Build();

        act.Should().NotThrow();
    }

    [Fact]
    public void Load_RequiredMissingFile_ShouldThrow()
    {
        var act = () => new ConfigurationBuilder()
            .AddYamlFile(s =>
            {
                s.FileProvider = new PhysicalFileProvider(_tempDir);
                s.FilePaths.Add("nonexistent.yaml");
                s.Optional = false;
                s.ExpandEnvironmentVariables = false;
            })
            .Build();

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Load_MultipleFiles_ShouldLayerConfiguration()
    {
        var baseYaml = """
            version: "1.0"
            poll_interval: 3600
            download_dir: ./downloads
            """;
        var overrideYaml = """
            poll_interval: 7200
            temp_dir: ./temp
            """;
        
        File.WriteAllText(Path.Combine(_tempDir, "base.yaml"), baseYaml);
        File.WriteAllText(Path.Combine(_tempDir, "override.yaml"), overrideYaml);

        var config = new ConfigurationBuilder()
            .AddYamlFile(s =>
            {
                s.FileProvider = new PhysicalFileProvider(_tempDir);
                s.FilePaths.Add("base.yaml");
                s.FilePaths.Add("override.yaml");
                s.Optional = false;
                s.ExpandEnvironmentVariables = false;
            })
            .Build();

        config["version"].Should().Be("1.0");
        config["poll_interval"].Should().Be("7200");
        config["download_dir"].Should().Be("./downloads");
        config["temp_dir"].Should().Be("./temp");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}