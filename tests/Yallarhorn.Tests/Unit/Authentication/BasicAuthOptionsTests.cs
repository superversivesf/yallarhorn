namespace Yallarhorn.Tests.Unit.Authentication;

using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Xunit;
using Yallarhorn.Authentication;

public class BasicAuthOptionsTests
{
    [Fact]
    public void BasicAuthOptions_ShouldInheritFromAuthenticationSchemeOptions()
    {
        // Arrange & Act
        var options = new BasicAuthOptions();

        // Assert
        options.Should().BeAssignableTo<AuthenticationSchemeOptions>();
    }

    [Fact]
    public void BasicAuthOptions_ShouldHaveDefaultRealm()
    {
        // Arrange & Act
        var options = new BasicAuthOptions();

        // Assert
        options.Realm.Should().Be("Restricted");
    }

    [Fact]
    public void BasicAuthOptions_ShouldAllowSettingUsername()
    {
        // Arrange & Act
        var options = new BasicAuthOptions
        {
            Username = "testuser"
        };

        // Assert
        options.Username.Should().Be("testuser");
    }

    [Fact]
    public void BasicAuthOptions_ShouldAllowSettingPassword()
    {
        // Arrange & Act
        var options = new BasicAuthOptions
        {
            Password = "testpass"
        };

        // Assert
        options.Password.Should().Be("testpass");
    }

    [Fact]
    public void BasicAuthOptions_ShouldAllowSettingRealm()
    {
        // Arrange & Act
        var options = new BasicAuthOptions
        {
            Realm = "Custom Realm"
        };

        // Assert
        options.Realm.Should().Be("Custom Realm");
    }

    [Fact]
    public void BasicAuthOptions_ShouldHaveNullUsernameByDefault()
    {
        // Arrange & Act
        var options = new BasicAuthOptions();

        // Assert
        options.Username.Should().BeNull();
    }

    [Fact]
    public void BasicAuthOptions_ShouldHaveNullPasswordByDefault()
    {
        // Arrange & Act
        var options = new BasicAuthOptions();

        // Assert
        options.Password.Should().BeNull();
    }
}