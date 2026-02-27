using FluentAssertions;
using Xunit;
using Yallarhorn.Data.Enums;

namespace Yallarhorn.Tests.Unit.Data.Enums;

/// <summary>
/// Unit tests for the QueueStatus enum.
/// </summary>
public class QueueStatusTests
{
    [Fact]
    public void QueueStatus_ShouldHaveSixValues()
    {
        var values = Enum.GetValues<QueueStatus>();

        values.Should().HaveCount(6);
    }

    [Theory]
    [InlineData(QueueStatus.Pending, 0)]
    [InlineData(QueueStatus.InProgress, 1)]
    [InlineData(QueueStatus.Completed, 2)]
    [InlineData(QueueStatus.Retrying, 3)]
    [InlineData(QueueStatus.Failed, 4)]
    [InlineData(QueueStatus.Cancelled, 5)]
    public void QueueStatus_ShouldHaveExpectedValues(QueueStatus status, int expectedValue)
    {
        ((int)status).Should().Be(expectedValue);
    }

    [Fact]
    public void QueueStatus_ShouldParseFromString()
    {
        var pending = Enum.Parse<QueueStatus>("Pending");
        var inProgress = Enum.Parse<QueueStatus>("InProgress");
        var completed = Enum.Parse<QueueStatus>("Completed");
        var retrying = Enum.Parse<QueueStatus>("Retrying");
        var failed = Enum.Parse<QueueStatus>("Failed");
        var cancelled = Enum.Parse<QueueStatus>("Cancelled");

        pending.Should().Be(QueueStatus.Pending);
        inProgress.Should().Be(QueueStatus.InProgress);
        completed.Should().Be(QueueStatus.Completed);
        retrying.Should().Be(QueueStatus.Retrying);
        failed.Should().Be(QueueStatus.Failed);
        cancelled.Should().Be(QueueStatus.Cancelled);
    }

    [Fact]
    public void QueueStatus_ShouldHaveValidNames()
    {
        Enum.GetNames<QueueStatus>().Should().Contain(
        [
            "Pending", "InProgress", "Completed", "Retrying", "Failed", "Cancelled"
        ]);
    }
}