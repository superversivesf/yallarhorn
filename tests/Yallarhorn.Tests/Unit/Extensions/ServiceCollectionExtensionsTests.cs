namespace Yallarhorn.Tests.Unit.Extensions;

using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yallarhorn.Configuration;
using Yallarhorn.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddValidatedOptions_ShouldRegisterOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestOptions:Value"] = "TestValue"
            })
            .Build();

        // Act
        services.AddValidatedOptions<TestOptions>(configuration, "TestOptions");
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetService<Microsoft.Extensions.Options.IOptions<TestOptions>>()?.Value;

        // Assert
        options.Should().NotBeNull();
        options!.Value.Should().Be("TestValue");
    }

    [Fact]
    public void AddYallarhornCoreServices_ShouldConfigureServerOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:Port"] = "8080",
                ["Server:UseHttps"] = "false",
                ["Server:MaxConcurrentConnections"] = "200",
                ["Server:RequestTimeoutSeconds"] = "60"
            })
            .Build();

        // Act
        services.AddYallarhornCoreServices(configuration);
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetService<Microsoft.Extensions.Options.IOptions<ServerOptions>>()?.Value;

        // Assert
        options.Should().NotBeNull();
        options!.Port.Should().Be(8080);
        options.UseHttps.Should().BeFalse();
        options.MaxConcurrentConnections.Should().Be(200);
        options.RequestTimeoutSeconds.Should().Be(60);
    }

    private class TestOptions
    {
        public string? Value { get; set; }
    }
}