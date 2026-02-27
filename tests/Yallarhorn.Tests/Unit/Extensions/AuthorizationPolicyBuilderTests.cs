namespace Yallarhorn.Tests.Unit.Extensions;

using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yallarhorn.Extensions;

public class AuthorizationPolicyBuilderTests
{
    #region Policy Constants Tests

    [Fact]
    public void AuthorizationPolicies_ShouldHaveFeedPolicy()
    {
        // Assert
        AuthorizationPolicies.FeedPolicy.Should().Be("FeedPolicy");
    }

    [Fact]
    public void AuthorizationPolicies_ShouldHaveAdminPolicy()
    {
        // Assert
        AuthorizationPolicies.AdminPolicy.Should().Be("AdminPolicy");
    }

    [Fact]
    public void AuthorizationPolicies_ShouldHaveFeedAuthScheme()
    {
        // Assert
        AuthorizationPolicies.FeedAuthScheme.Should().Be("FeedAuth");
    }

    [Fact]
    public void AuthorizationPolicies_ShouldHaveAdminAuthScheme()
    {
        // Assert
        AuthorizationPolicies.AdminAuthScheme.Should().Be("AdminAuth");
    }

    #endregion

    #region AddYallarhornPolicies Tests

    [Fact]
    public void AddYallarhornPolicies_ShouldAddFeedPolicy()
    {
        // Arrange
        var options = new AuthorizationOptions();

        // Act
        options.AddYallarhornPolicies();

        // Assert
        var policy = options.GetPolicy(AuthorizationPolicies.FeedPolicy);
        policy.Should().NotBeNull();
    }

    [Fact]
    public void AddYallarhornPolicies_ShouldAddAdminPolicy()
    {
        // Arrange
        var options = new AuthorizationOptions();

        // Act
        options.AddYallarhornPolicies();

        // Assert
        var policy = options.GetPolicy(AuthorizationPolicies.AdminPolicy);
        policy.Should().NotBeNull();
    }

    [Fact]
    public void AddYallarhornPolicies_FeedPolicyShouldRequireAuthenticatedUser()
    {
        // Arrange
        var options = new AuthorizationOptions();

        // Act
        options.AddYallarhornPolicies();

        // Assert
        var policy = options.GetPolicy(AuthorizationPolicies.FeedPolicy);
        policy.Should().NotBeNull();
        // Check that the policy has authentication requirements
        policy!.Requirements.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AddYallarhornPolicies_AdminPolicyShouldRequireAuthenticatedUser()
    {
        // Arrange
        var options = new AuthorizationOptions();

        // Act
        options.AddYallarhornPolicies();

        // Assert
        var policy = options.GetPolicy(AuthorizationPolicies.AdminPolicy);
        policy.Should().NotBeNull();
        // Check that the policy has authentication requirements
        policy!.Requirements.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AddYallarhornPolicies_FeedPolicyShouldUseFeedAuthScheme()
    {
        // Arrange
        var options = new AuthorizationOptions();

        // Act
        options.AddYallarhornPolicies();

        // Assert
        var policy = options.GetPolicy(AuthorizationPolicies.FeedPolicy);
        policy.Should().NotBeNull();
        policy!.AuthenticationSchemes.Should().Contain(AuthorizationPolicies.FeedAuthScheme);
    }

    [Fact]
    public void AddYallarhornPolicies_AdminPolicyShouldUseAdminAuthScheme()
    {
        // Arrange
        var options = new AuthorizationOptions();

        // Act
        options.AddYallarhornPolicies();

        // Assert
        var policy = options.GetPolicy(AuthorizationPolicies.AdminPolicy);
        policy.Should().NotBeNull();
        policy!.AuthenticationSchemes.Should().Contain(AuthorizationPolicies.AdminAuthScheme);
    }

    [Fact]
    public void AddYallarhornPolicies_ShouldReturnOptionsForChaining()
    {
        // Arrange
        var options = new AuthorizationOptions();

        // Act
        var result = options.AddYallarhornPolicies();

        // Assert
        result.Should().BeSameAs(options);
    }

    #endregion

    #region AddYallarhornAuthorization Tests

    [Fact]
    public void AddYallarhornAuthorization_ShouldRegisterAuthorizationServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddYallarhornAuthorization();

        // Assert
        services.Should().Contain(s => s.ServiceType == typeof(IAuthorizationService));
    }

    [Fact]
    public void AddYallarhornAuthorization_ShouldAddPolicies()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddYallarhornAuthorization();
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetService<Microsoft.Extensions.Options.IOptions<AuthorizationOptions>>();

        // Assert
        options.Should().NotBeNull();
        options!.Value.GetPolicy(AuthorizationPolicies.FeedPolicy).Should().NotBeNull();
        options.Value.GetPolicy(AuthorizationPolicies.AdminPolicy).Should().NotBeNull();
    }

    [Fact]
    public void AddYallarhornAuthorization_ShouldReturnServicesForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddYallarhornAuthorization();

        // Assert
        result.Should().BeSameAs(services);
    }

    #endregion
}