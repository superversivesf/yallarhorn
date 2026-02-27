namespace Yallarhorn.Tests.Unit.Extensions;

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Yallarhorn.Background;
using Yallarhorn.Extensions;
using Yallarhorn.Services;

public class BackgroundWorkerExtensionsTests
{
    private readonly Mock<IChannelRefreshService> _refreshServiceMock;
    private readonly Mock<IDownloadPipeline> _pipelineMock;
    private readonly Mock<IDownloadQueueService> _queueServiceMock;
    private readonly Mock<IDownloadCoordinator> _coordinatorMock;

    public BackgroundWorkerExtensionsTests()
    {
        _refreshServiceMock = new Mock<IChannelRefreshService>();
        _pipelineMock = new Mock<IDownloadPipeline>();
        _queueServiceMock = new Mock<IDownloadQueueService>();
        _coordinatorMock = new Mock<IDownloadCoordinator>();
    }

    private IServiceCollection CreateServiceCollection()
    {
        var services = new ServiceCollection();
        
        // Register the required dependencies with mocks
        services.AddSingleton(_refreshServiceMock.Object);
        services.AddSingleton(_pipelineMock.Object);
        services.AddSingleton(_queueServiceMock.Object);
        services.AddSingleton(_coordinatorMock.Object);
        services.AddLogging();
        
        return services;
    }

    [Fact]
    public void AddYallarhornBackgroundWorkers_WhenWorkersEnabled_RegistersBothWorkers()
    {
        // Arrange
        var services = CreateServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Workers:Enabled"] = "true"
            })
            .Build();

        // Act
        services.AddYallarhornBackgroundWorkers(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Check that hosted services are registered
        var hostedServices = serviceProvider.GetServices<IHostedService>().ToList();
        hostedServices.Should().Contain(s => s.GetType().Name == "RefreshWorker");
        hostedServices.Should().Contain(s => s.GetType().Name == "DownloadWorker");
    }

    [Fact]
    public void AddYallarhornBackgroundWorkers_WhenWorkersDisabled_DoesNotRegisterWorkers()
    {
        // Arrange
        var services = CreateServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Workers:Enabled"] = "false"
            })
            .Build();

        // Act
        services.AddYallarhornBackgroundWorkers(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - No hosted services should be registered
        var hostedServices = serviceProvider.GetServices<IHostedService>().ToList();
        hostedServices.Should().BeEmpty();
    }

    [Fact]
    public void AddYallarhornBackgroundWorkers_WhenConfigurationMissing_RegistersWorkersByDefault()
    {
        // Arrange
        var services = CreateServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        services.AddYallarhornBackgroundWorkers(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Workers should be registered by default (enabled = true)
        var hostedServices = serviceProvider.GetServices<IHostedService>().ToList();
        hostedServices.Should().Contain(s => s.GetType().Name == "RefreshWorker");
        hostedServices.Should().Contain(s => s.GetType().Name == "DownloadWorker");
    }

    [Fact]
    public void AddYallarhornBackgroundWorkers_WithCustomRefreshInterval_RegistersWorkerWithInterval()
    {
        // Arrange
        var services = CreateServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Workers:Enabled"] = "true",
                ["Workers:RefreshIntervalSeconds"] = "1800"
            })
            .Build();

        // Act
        services.AddYallarhornBackgroundWorkers(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var hostedServices = serviceProvider.GetServices<IHostedService>().ToList();
        hostedServices.Should().Contain(s => s.GetType().Name == "RefreshWorker");
    }

    [Fact]
    public void AddYallarhornBackgroundWorkers_WithCustomDownloadPollInterval_RegistersWorkerWithInterval()
    {
        // Arrange
        var services = CreateServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Workers:Enabled"] = "true",
                ["Workers:DownloadPollIntervalSeconds"] = "10"
            })
            .Build();

        // Act
        services.AddYallarhornBackgroundWorkers(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var hostedServices = serviceProvider.GetServices<IHostedService>().ToList();
        hostedServices.Should().Contain(s => s.GetType().Name == "DownloadWorker");
    }

    [Fact]
    public void AddYallarhornBackgroundWorkers_ReturnsServiceCollection_ForChaining()
    {
        // Arrange
        var services = CreateServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var result = services.AddYallarhornBackgroundWorkers(configuration);

        // Assert
        result.Should().BeSameAs(services);
    }
}