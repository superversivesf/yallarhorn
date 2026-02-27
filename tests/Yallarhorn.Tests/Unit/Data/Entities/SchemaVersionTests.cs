using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Xunit;
using Yallarhorn.Data.Entities;

namespace Yallarhorn.Tests.Unit.Data.Entities;

/// <summary>
/// Unit tests for the SchemaVersion entity.
/// </summary>
public class SchemaVersionTests
{
    [Fact]
    public void SchemaVersion_ShouldHaveCorrectTableName()
    {
        // Arrange & Act
        var attributes = typeof(SchemaVersion)
            .GetCustomAttributes(typeof(TableAttribute), false);

        // Assert
        attributes.Should().HaveCount(1);
        var tableAttr = (TableAttribute)attributes[0];
        tableAttr.Name.Should().Be("schema_version");
    }

    [Fact]
    public void SchemaVersion_ShouldCreateWithRequiredProperties()
    {
        // Arrange & Act
        var appliedAt = DateTimeOffset.UtcNow;
        var schemaVersion = new SchemaVersion
        {
            Version = 1,
            AppliedAt = appliedAt,
            Description = "Initial schema"
        };

        // Assert
        schemaVersion.Version.Should().Be(1);
        schemaVersion.AppliedAt.Should().Be(appliedAt);
        schemaVersion.Description.Should().Be("Initial schema");
    }

    [Fact]
    public void SchemaVersion_ShouldAllowNullDescription()
    {
        // Arrange & Act
        var schemaVersion = new SchemaVersion
        {
            Version = 2,
            AppliedAt = DateTimeOffset.UtcNow,
            Description = null
        };

        // Assert
        schemaVersion.Description.Should().BeNull();
    }

    [Fact]
    public void SchemaVersion_ShouldHaveRequiredAttributeOnAppliedAt()
    {
        // Arrange
        var appliedAtProperty = typeof(SchemaVersion).GetProperty(nameof(SchemaVersion.AppliedAt));

        // Act
        var requiredAttr = appliedAtProperty?.GetCustomAttributes(typeof(RequiredAttribute), false);

        // Assert
        requiredAttr.Should().HaveCount(1);
    }

    [Fact]
    public void SchemaVersion_ShouldHaveKeyAttributeOnVersion()
    {
        // Arrange
        var versionProperty = typeof(SchemaVersion).GetProperty(nameof(SchemaVersion.Version));

        // Act
        var keyAttr = versionProperty?.GetCustomAttributes(typeof(KeyAttribute), false);

        // Assert
        keyAttr.Should().HaveCount(1);
    }

    [Fact]
    public void SchemaVersion_ShouldSupportMultipleVersions()
    {
        // Arrange & Act
        var versions = new[]
        {
            new SchemaVersion { Version = 1, AppliedAt = DateTimeOffset.UtcNow.AddDays(-7), Description = "Initial schema" },
            new SchemaVersion { Version = 2, AppliedAt = DateTimeOffset.UtcNow.AddDays(-3), Description = "Add published_at index" },
            new SchemaVersion { Version = 3, AppliedAt = DateTimeOffset.UtcNow, Description = "Add feed type column" }
        };

        // Assert
        versions.Should().HaveCount(3);
        versions[0].Version.Should().Be(1);
        versions[1].Version.Should().Be(2);
        versions[2].Version.Should().Be(3);
    }
}