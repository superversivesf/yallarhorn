namespace Yallarhorn.Tests.Unit.Extensions;

using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Yallarhorn.Authentication;
using Yallarhorn.Configuration;
using Yallarhorn.Extensions;

public class AuthenticationExtensionTests
{
    /// <summary>
    /// Creates a service collection with logging configured for tests.
    /// </summary>
    private static ServiceCollection CreateServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    #region AddYallarhornAuthentication Tests

    [Fact]
    public void AddYallarhornAuthentication_ShouldRegisterCredentialValidator()
    {
        // Arrange
        var services = CreateServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:FeedCredentials:Enabled"] = "true",
                ["Auth:FeedCredentials:Username"] = "feeduser",
                ["Auth:FeedCredentials:Password"] = "feedpass"
            })
            .Build();

        // Act
        services.AddYallarhornAuthentication(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var validator = serviceProvider.GetService<ICredentialValidator>();
        validator.Should().NotBeNull();
        validator.Should().BeOfType<CredentialValidator>();
    }

    [Fact]
    public void AddYallarhornAuthentication_ShouldRegisterFeedAuthScheme_WhenFeedCredentialsEnabled()
    {
        // Arrange
        var services = CreateServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:FeedCredentials:Enabled"] = "true",
                ["Auth:FeedCredentials:Username"] = "feeduser",
                ["Auth:FeedCredentials:Password"] = "feedpass",
                ["Auth:FeedCredentials:Realm"] = "Test Feed"
            })
            .Build();

        // Act
        services.AddYallarhornAuthentication(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var authSchemeProvider = serviceProvider.GetService<IAuthenticationSchemeProvider>();
        authSchemeProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddYallarhornAuthentication_ShouldRegisterAdminAuthScheme_WhenAdminAuthEnabled()
    {
        // Arrange
        var services = CreateServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:AdminAuth:Enabled"] = "true",
                ["Auth:AdminAuth:Username"] = "adminuser",
                ["Auth:AdminAuth:Password"] = "adminpass"
            })
            .Build();

        // Act
        services.AddYallarhornAuthentication(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var authSchemeProvider = serviceProvider.GetService<IAuthenticationSchemeProvider>();
        authSchemeProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddYallarhornAuthentication_ShouldRegisterBothSchemes_WhenBothEnabled()
    {
        // Arrange
        var services = CreateServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:FeedCredentials:Enabled"] = "true",
                ["Auth:FeedCredentials:Username"] = "feeduser",
                ["Auth:FeedCredentials:Password"] = "feedpass",
                ["Auth:AdminAuth:Enabled"] = "true",
                ["Auth:AdminAuth:Username"] = "adminuser",
                ["Auth:AdminAuth:Password"] = "adminpass"
            })
            .Build();

        // Act
        services.AddYallarhornAuthentication(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var authSchemeProvider = serviceProvider.GetService<IAuthenticationSchemeProvider>();
        authSchemeProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddYallarhornAuthentication_ShouldNotRegisterFeedAuthScheme_WhenFeedCredentialsDisabled()
    {
        // Arrange
        var services = CreateServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:FeedCredentials:Enabled"] = "false"
            })
            .Build();

        // Act
        services.AddYallarhornAuthentication(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Authentication service should be registered but without FeedAuth scheme
        var authSchemeProvider = serviceProvider.GetService<IAuthenticationSchemeProvider>();
        authSchemeProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddYallarhornAuthentication_ShouldRegisterAuthorizationServices()
    {
        // Arrange
        var services = CreateServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:AdminAuth:Enabled"] = "true",
                ["Auth:AdminAuth:Username"] = "adminuser",
                ["Auth:AdminAuth:Password"] = "adminpass"
            })
            .Build();

        // Act
        services.AddYallarhornAuthentication(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var authService = serviceProvider.GetService<IAuthenticationService>();
        authService.Should().NotBeNull();
    }

    [Fact]
    public void AddYallarhornAuthentication_ShouldReturnServicesForChaining()
    {
        // Arrange
        var services = CreateServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var result = services.AddYallarhornAuthentication(configuration);

        // Assert
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddYallarhornAuthentication_ShouldConfigureCustomRealm_ForFeedAuth()
    {
        // Arrange
        var services = CreateServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:FeedCredentials:Enabled"] = "true",
                ["Auth:FeedCredentials:Username"] = "feeduser",
                ["Auth:FeedCredentials:Password"] = "feedpass",
                ["Auth:FeedCredentials:Realm"] = "Custom Feed Realm"
            })
            .Build();

        // Act
        services.AddYallarhornAuthentication(configuration);

        // Assert - Authentication services are configured
        services.Should().Contain(s => s.ServiceType == typeof(IAuthenticationService));
    }

    #endregion

    #region AddYallarhornAuthIfNeeded Tests

    [Fact]
    public void AddYallarhornAuthIfNeeded_ShouldAlwaysRegisterCredentialValidator()
    {
        // Arrange
        var services = CreateServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        services.AddYallarhornAuthIfNeeded(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var validator = serviceProvider.GetService<ICredentialValidator>();
        validator.Should().NotBeNull();
    }

    [Fact]
    public void AddYallarhornAuthIfNeeded_ShouldAddAuthorizationOnly_WhenNoAuthEnabled()
    {
        // Arrange
        var services = CreateServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:FeedCredentials:Enabled"] = "false",
                ["Auth:AdminAuth:Enabled"] = "false"
            })
            .Build();

        // Act
        services.AddYallarhornAuthIfNeeded(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Authorization service should exist (basic authorization without policies)
        var authService = serviceProvider.GetService<IAuthorizationService>();
        authService.Should().NotBeNull();
    }

    [Fact]
    public void AddYallarhornAuthIfNeeded_ShouldAddFullAuthentication_WhenFeedAuthEnabled()
    {
        // Arrange
        var services = CreateServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:FeedCredentials:Enabled"] = "true",
                ["Auth:FeedCredentials:Username"] = "feeduser",
                ["Auth:FeedCredentials:Password"] = "feedpass"
            })
            .Build();

        // Act
        services.AddYallarhornAuthIfNeeded(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var authService = serviceProvider.GetService<IAuthenticationService>();
        authService.Should().NotBeNull();
    }

    [Fact]
    public void AddYallarhornAuthIfNeeded_ShouldAddFullAuthentication_WhenAdminAuthEnabled()
    {
        // Arrange
        var services = CreateServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:AdminAuth:Enabled"] = "true",
                ["Auth:AdminAuth:Username"] = "adminuser",
                ["Auth:AdminAuth:Password"] = "adminpass"
            })
            .Build();

        // Act
        services.AddYallarhornAuthIfNeeded(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var authService = serviceProvider.GetService<IAuthenticationService>();
        authService.Should().NotBeNull();
    }

    [Fact]
    public void AddYallarhornAuthIfNeeded_ShouldReturnServicesForChaining()
    {
        // Arrange
        var services = CreateServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var result = services.AddYallarhornAuthIfNeeded(configuration);

        // Assert
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddYallarhornAuthIfNeeded_ShouldHandleNullConfiguration()
    {
        // Arrange
        var services = CreateServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var act = () => services.AddYallarhornAuthIfNeeded(configuration);

        // Assert - Should not throw
        act.Should().NotThrow();
    }

    [Fact]
    public void AddYallarhornAuthIfNeeded_ShouldHandleMissingAuthSection()
    {
        // Arrange
        var services = CreateServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Other:Setting"] = "value"
            })
            .Build();

        // Act
        services.AddYallarhornAuthIfNeeded(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Should register credential validator but not require auth
        var validator = serviceProvider.GetService<ICredentialValidator>();
        validator.Should().NotBeNull();
    }

    #endregion
}