using FluentAssertions;
using Xunit;
using Yallarhorn.Utilities;

namespace Yallarhorn.Tests.Unit.Utilities;

public class FileSizeFormatterTests
{
    [Fact]
    public void Format_ShouldFormatBytes()
    {
        // Act
        var result = FileSizeFormatter.Format(512);

        // Assert
        result.Should().Be("512 B");
    }

    [Fact]
    public void Format_ShouldFormatZeroBytes()
    {
        // Act
        var result = FileSizeFormatter.Format(0);

        // Assert
        result.Should().Be("0 B");
    }

    [Fact]
    public void Format_ShouldFormatKilobytes()
    {
        // Act
        var result = FileSizeFormatter.Format(1024);

        // Assert
        result.Should().Be("1 KB");
    }

    [Fact]
    public void Format_ShouldFormatMegabytes()
    {
        // Act
        var result = FileSizeFormatter.Format(1048576);

        // Assert
        result.Should().Be("1 MB");
    }

    [Fact]
    public void Format_ShouldFormatGigabytes()
    {
        // Act
        var result = FileSizeFormatter.Format(1073741824L);

        // Assert
        result.Should().Be("1 GB");
    }

    [Fact]
    public void Format_ShouldFormatTerabytes()
    {
        // Act
        var result = FileSizeFormatter.Format(1099511627776L);

        // Assert
        result.Should().Be("1 TB");
    }

    [Fact]
    public void Format_ShouldFormatPetabytes()
    {
        // Act
        var result = FileSizeFormatter.Format(1125899906842624L);

        // Assert
        result.Should().Be("1 PB");
    }

    [Fact]
    public void Format_ShouldFormatWithDecimals()
    {
        // Act
        var result = FileSizeFormatter.Format(1536);

        // Assert
        result.Should().Be("1.5 KB");
    }

    [Fact]
    public void Format_ShouldRoundToOneDecimalByDefault()
    {
        // Act
        var result = FileSizeFormatter.Format(1234567);

        // Assert
        result.Should().Be("1.2 MB");
    }

    [Fact]
    public void Format_WithCustomDecimalPlaces_ShouldFormatCorrectly()
    {
        // Act
        var result = FileSizeFormatter.Format(1536, decimalPlaces: 2);

        // Assert
        result.Should().Be("1.50 KB");
    }

    [Fact]
    public void Format_WithZeroDecimalPlaces_ShouldFormatCorrectly()
    {
        // Act
        var result = FileSizeFormatter.Format(1536, decimalPlaces: 0);

        // Assert
        result.Should().Be("2 KB");
    }

    [Fact]
    public void Format_ShouldHandleTypicalMp3Size()
    {
        // Arrange
        var bytes = 5_242_880; // 5 MB

        // Act
        var result = FileSizeFormatter.Format(bytes);

        // Assert
        result.Should().Be("5 MB");
    }

    [Fact]
    public void Format_ShouldHandleTypicalVideoSize()
    {
        // Arrange
        var bytes = 1_073_741_824; // 1 GB

        // Act
        var result = FileSizeFormatter.Format(bytes);

        // Assert
        result.Should().Be("1 GB");
    }

    [Fact]
    public void Format_ShouldHandleLargeVideoFile()
    {
        // Arrange
        var bytes = 2_147_483_648L; // 2 GB

        // Act
        var result = FileSizeFormatter.Format(bytes);

        // Assert
        result.Should().Be("2 GB");
    }

    [Fact]
    public void Format_ShouldFormatFileSizeUnder1KB()
    {
        // Act
        var result = FileSizeFormatter.Format(999);

        // Assert
        result.Should().Be("999 B");
    }

    [Fact]
    public void Format_ShouldFormatRoughlyOneAndHalfMegabytes()
    {
        // Act
        var result = FileSizeFormatter.Format(1_572_864);

        // Assert
        result.Should().Be("1.5 MB");
    }

    [Fact]
    public void Format_ShouldThrowForNegativeBytes()
    {
        // Act
        var act = () => FileSizeFormatter.Format(-1);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("bytes");
    }

    [Fact]
    public void FormatBinary_ShouldUse1024Base()
    {
        // Act
        var result = FileSizeFormatter.FormatBinary(1024);

        // Assert
        result.Should().Be("1 KiB");
    }

    [Fact]
    public void FormatBinary_ShouldFormatMebibytes()
    {
        // Act
        var result = FileSizeFormatter.FormatBinary(1048576);

        // Assert
        result.Should().Be("1 MiB");
    }

    [Fact]
    public void FormatBinary_ShouldFormatGibibytes()
    {
        // Act
        var result = FileSizeFormatter.FormatBinary(1073741824L);

        // Assert
        result.Should().Be("1 GiB");
    }

    [Fact]
    public void FormatBinary_ShouldFormatTebibytes()
    {
        // Act
        var result = FileSizeFormatter.FormatBinary(1099511627776L);

        // Assert
        result.Should().Be("1 TiB");
    }

    [Fact]
    public void FormatBinary_ShouldThrowForNegativeBytes()
    {
        // Act
        var act = () => FileSizeFormatter.FormatBinary(-1);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("bytes");
    }
}