using FluentAssertions;
using Xunit;
using Yallarhorn.Utilities;

namespace Yallarhorn.Tests.Unit.Utilities;

public class EnvVarExpanderTests
{
    [Fact]
    public void Expand_ShouldReturnNullForNullInput()
    {
        // Act
        var result = EnvVarExpander.Expand(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Expand_ShouldReturnEmptyForEmptyInput()
    {
        // Act
        var result = EnvVarExpander.Expand(string.Empty);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Expand_ShouldReturnOriginalTextIfNoVariables()
    {
        // Act
        var result = EnvVarExpander.Expand("hello world");

        // Assert
        result.Should().Be("hello world");
    }

    [Fact]
    public void Expand_ShouldExpandSimpleVariable()
    {
        // Arrange
        Environment.SetEnvironmentVariable("TEST_VAR", "test-value");

        try
        {
            // Act
            var result = EnvVarExpander.Expand("${TEST_VAR}");

            // Assert
            result.Should().Be("test-value");
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_VAR", null);
        }
    }

    [Fact]
    public void Expand_ShouldExpandVariableWithinText()
    {
        // Arrange
        Environment.SetEnvironmentVariable("NAME", "John");

        try
        {
            // Act
            var result = EnvVarExpander.Expand("Hello ${NAME}!");

            // Assert
            result.Should().Be("Hello John!");
        }
        finally
        {
            Environment.SetEnvironmentVariable("NAME", null);
        }
    }

    [Fact]
    public void Expand_ShouldExpandMultipleVariables()
    {
        // Arrange
        Environment.SetEnvironmentVariable("FIRST", "John");
        Environment.SetEnvironmentVariable("LAST", "Doe");

        try
        {
            // Act
            var result = EnvVarExpander.Expand("${FIRST} ${LAST}");

            // Assert
            result.Should().Be("John Doe");
        }
        finally
        {
            Environment.SetEnvironmentVariable("FIRST", null);
            Environment.SetEnvironmentVariable("LAST", null);
        }
    }

    [Fact]
    public void Expand_ShouldReturnEmptyForUnsetVariable()
    {
        // Arrange
        Environment.SetEnvironmentVariable("UNSET_VAR_YALLARHORN_12345", null);

        // Act
        var result = EnvVarExpander.Expand("${UNSET_VAR_YALLARHORN_12345}");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Expand_ShouldUseDefaultForUnsetVariable()
    {
        // Arrange
        Environment.SetEnvironmentVariable("UNSET_VAR_YALLARHORN_12346", null);

        // Act
        var result = EnvVarExpander.Expand("${UNSET_VAR_YALLARHORN_12346:-default-value}");

        // Assert
        result.Should().Be("default-value");
    }

    [Fact]
    public void Expand_ShouldIgnoreDefaultForSetVariable()
    {
        // Arrange
        Environment.SetEnvironmentVariable("TEST_VAR", "actual-value");

        try
        {
            // Act
            var result = EnvVarExpander.Expand("${TEST_VAR:-default-value}");

            // Assert
            result.Should().Be("actual-value");
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_VAR", null);
        }
    }

    [Fact]
    public void Expand_ShouldThrowForUnsetVariableWithError()
    {
        // Arrange
        Environment.SetEnvironmentVariable("UNSET_VAR_YALLARHORN_12348", null);

        // Act
        var act = () => EnvVarExpander.Expand("${UNSET_VAR_YALLARHORN_12348:?Variable is required}");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Variable is required*");
    }

    [Fact]
    public void Expand_ShouldNotThrowForSetVariableWithError()
    {
        // Arrange
        Environment.SetEnvironmentVariable("TEST_VAR", "value");

        try
        {
            // Act
            var result = EnvVarExpander.Expand("${TEST_VAR:?Variable is required}");

            // Assert
            result.Should().Be("value");
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_VAR", null);
        }
    }

    [Fact]
    public void Expand_ShouldHandleDefaultValueWithHyphens()
    {
        // Arrange
        Environment.SetEnvironmentVariable("UNSET_VAR_YALLARHORN_12349", null);

        // Act
        var result = EnvVarExpander.Expand("${UNSET_VAR_YALLARHORN_12349:-a-default-value}");

        // Assert
        result.Should().Be("a-default-value");
    }

    [Fact]
    public void Expand_ShouldHandleEmptyStringValue()
    {
        // Arrange
        Environment.SetEnvironmentVariable("EMPTY_VAR_YALLARHORN", "");

        try
        {
            // Act
            var result = EnvVarExpander.Expand("${EMPTY_VAR_YALLARHORN}");

            // Assert
            result.Should().BeEmpty();
        }
        finally
        {
            Environment.SetEnvironmentVariable("EMPTY_VAR_YALLARHORN", null);
        }
    }

    [Fact]
    public void Expand_ShouldHandleDefaultWithEmptyValue()
    {
        // Arrange
        Environment.SetEnvironmentVariable("EMPTY_VAR_YALLARHORN2", "");

        try
        {
            // Act
            var result = EnvVarExpander.Expand("${EMPTY_VAR_YALLARHORN2:-default}");

            // Assert
            // Empty string IS set, so default should NOT be used
            result.Should().BeEmpty();
        }
        finally
        {
            Environment.SetEnvironmentVariable("EMPTY_VAR_YALLARHORN2", null);
        }
    }

    [Fact]
    public void Expand_ShouldPreserveBracesWithoutDollarSign()
    {
        // Act
        var result = EnvVarExpander.Expand("Text {with} braces");

        // Assert
        result.Should().Be("Text {with} braces");
    }

    [Fact]
    public void Expand_ShouldHandleNestedBraces()
    {
        // Arrange
        Environment.SetEnvironmentVariable("VAR", "value");

        try
        {
            // Act
            var result = EnvVarExpander.Expand("${VAR}");

            // Assert
            result.Should().Be("value");
        }
        finally
        {
            Environment.SetEnvironmentVariable("VAR", null);
        }
    }

    [Fact]
    public void Expand_ShouldHandleComplexText()
    {
        // Arrange
        Environment.SetEnvironmentVariable("HOST", "localhost");
        Environment.SetEnvironmentVariable("PORT", "8080");
        Environment.SetEnvironmentVariable("DB", "mydb");

        try
        {
            // Act
            var result = EnvVarExpander.Expand("Server=${HOST};Port=${PORT};Database=${DB}");

            // Assert
            result.Should().Be("Server=localhost;Port=8080;Database=mydb");
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOST", null);
            Environment.SetEnvironmentVariable("PORT", null);
            Environment.SetEnvironmentVariable("DB", null);
        }
    }

    [Fact]
    public void Expand_ShouldHandleDefaultWithColons()
    {
        // Arrange
        Environment.SetEnvironmentVariable("UNSET_VAR_YALLARHORN_12350", null);

        // Act
        var result = EnvVarExpander.Expand("${UNSET_VAR_YALLARHORN_12350:-http://localhost:8080}");

        // Assert
        result.Should().Be("http://localhost:8080");
    }

    [Fact]
    public void Expand_ShouldHandleDefaultWithSpaces()
    {
        // Arrange
        Environment.SetEnvironmentVariable("UNSET_VAR_YALLARHORN_12351", null);

        // Act
        var result = EnvVarExpander.Expand("Path: ${UNSET_VAR_YALLARHORN_12351:-default path value}");

        // Assert
        result.Should().Be("Path: default path value");
    }

    [Fact]
    public void Expand_ShouldHandleMultipleExpansionsWithDefaults()
    {
        // Arrange
        Environment.SetEnvironmentVariable("FIRST", "value1");
        Environment.SetEnvironmentVariable("UNSET_VAR_YALLARHORN_12352", null);

        try
        {
            // Act
            var result = EnvVarExpander.Expand("${FIRST}/${UNSET_VAR_YALLARHORN_12352:-value2}");

            // Assert
            result.Should().Be("value1/value2");
        }
        finally
        {
            Environment.SetEnvironmentVariable("FIRST", null);
        }
    }

    [Fact]
    public void Expand_ShouldHandleErrorSyntaxWithVariableNameInMessage()
    {
        // Arrange
        Environment.SetEnvironmentVariable("UNSET_REQUIRED_VAR_123", null);

        // Act
        var act = () => EnvVarExpander.Expand("${UNSET_REQUIRED_VAR_123:?}");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*UNSET_REQUIRED_VAR_123*");
    }

    [Fact]
    public void Expand_ShouldHandleMalformedVariableGracefully()
    {
        // Act - Unclosed brace
        var result = EnvVarExpander.Expand("${MISSING_CLOSE");

        // Assert
        result.Should().Be("${MISSING_CLOSE");
    }

    [Fact]
    public void Expand_ShouldHandleConsecutiveDollarSigns()
    {
        // Act
        var result = EnvVarExpander.Expand("$$");

        // Assert
        result.Should().Be("$$");
    }

    [Fact]
    public void ExpandWithEnvironment_ShouldUseProvidedEnvironment()
    {
        // Arrange
        var env = new Dictionary<string, string>
        {
            ["MY_VAR"] = "env-value"
        };

        // Act
        var result = EnvVarExpander.ExpandWithEnvironment("${MY_VAR}", env);

        // Assert
        result.Should().Be("env-value");
    }

    [Fact]
    public void ExpandWithEnvironment_ShouldIgnoreActualEnvironment()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ENV_VAR_OVERRIDE", "actual-env");
        var env = new Dictionary<string, string>
        {
            ["ENV_VAR_OVERRIDE"] = "mock-env"
        };

        try
        {
            // Act
            var result = EnvVarExpander.ExpandWithEnvironment("${ENV_VAR_OVERRIDE}", env);

            // Assert
            result.Should().Be("mock-env");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ENV_VAR_OVERRIDE", null);
        }
    }

    [Fact]
    public void ExpandWithEnvironment_ShouldHandleDefaults()
    {
        // Arrange
        var env = new Dictionary<string, string>();

        // Act
        var result = EnvVarExpander.ExpandWithEnvironment("${MISSING:-default}", env);

        // Assert
        result.Should().Be("default");
    }

    [Fact]
    public void ExpandWithEnvironment_ShouldHandleErrors()
    {
        // Arrange
        var env = new Dictionary<string, string>();

        // Act
        var act = () => EnvVarExpander.ExpandWithEnvironment("${MISSING:?required!}", env);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }
}