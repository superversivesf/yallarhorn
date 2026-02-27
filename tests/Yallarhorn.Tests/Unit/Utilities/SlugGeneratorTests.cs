using FluentAssertions;
using Xunit;
using Yallarhorn.Utilities;

namespace Yallarhorn.Tests.Unit.Utilities;

public class SlugGeneratorTests
{
    [Fact]
    public void Generate_ShouldReturnEmptyForNull()
    {
        // Act
        var result = SlugGenerator.Generate(null);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Generate_ShouldReturnEmptyForEmptyString()
    {
        // Act
        var result = SlugGenerator.Generate(string.Empty);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Generate_ShouldConvertToLowerCase()
    {
        // Act
        var result = SlugGenerator.Generate("HELLO WORLD");

        // Assert
        result.Should().Be("hello-world");
    }

    [Fact]
    public void Generate_ShouldReplaceSpacesWithHyphens()
    {
        // Act
        var result = SlugGenerator.Generate("hello world");

        // Assert
        result.Should().Be("hello-world");
    }

    [Fact]
    public void Generate_ShouldReplaceMultipleSpacesWithSingleHyphen()
    {
        // Act
        var result = SlugGenerator.Generate("hello    world");

        // Assert
        result.Should().Be("hello-world");
    }

    [Fact]
    public void Generate_ShouldReplaceSpecialCharactersWithHyphens()
    {
        // Act
        var result = SlugGenerator.Generate("hello!@#$%world");

        // Assert
        result.Should().Be("hello-world");
    }

    [Fact]
    public void Generate_ShouldRemoveLeadingAndTrailingHyphens()
    {
        // Act
        var result = SlugGenerator.Generate("---hello world---");

        // Assert
        result.Should().Be("hello-world");
    }

    [Fact]
    public void Generate_ShouldCollapseMultipleHyphens()
    {
        // Act
        var result = SlugGenerator.Generate("hello-----world");

        // Assert
        result.Should().Be("hello-world");
    }

    [Fact]
    public void Generate_ShouldPreserveNumbers()
    {
        // Act
        var result = SlugGenerator.Generate("Episode 123");

        // Assert
        result.Should().Be("episode-123");
    }

    [Fact]
    public void Generate_ShouldHandleComplexChannelName()
    {
        // Arrange
        var input = "My Awesome Podcast Channel!";
        var expected = "my-awesome-podcast-channel";

        // Act
        var result = SlugGenerator.Generate(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Generate_ShouldHandleSpecialCharactersOnly()
    {
        // Act
        var result = SlugGenerator.Generate("!@#$%^&*()");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Generate_ShouldPreserveHyphens()
    {
        // Act
        var result = SlugGenerator.Generate("hello-world");

        // Assert
        result.Should().Be("hello-world");
    }

    [Fact]
    public void Generate_ShouldHandleUnderscores()
    {
        // Act
        var result = SlugGenerator.Generate("hello_world");

        // Assert
        result.Should().Be("hello-world");
    }

    [Fact]
    public void Generate_ShouldHandleMixedCaseAndSpecial()
    {
        // Arrange
        var input = "The Developer's Guide to C# & .NET";
        var expected = "the-developer-s-guide-to-c-net";

        // Act
        var result = SlugGenerator.Generate(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Generate_ShouldHandleConsecutiveSpacesAndSpecialChars()
    {
        // Arrange
        var input = "Hello   !!! World";
        var expected = "hello-world";

        // Act
        var result = SlugGenerator.Generate(input);

        // Assert
        result.Should().Be(expected);
    }
}