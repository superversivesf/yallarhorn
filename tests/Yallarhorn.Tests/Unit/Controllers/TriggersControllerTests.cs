namespace Yallarhorn.Tests.Unit.Controllers;

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Yallarhorn.Controllers;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Data.Repositories;
using Yallarhorn.Models.Api;
using Yallarhorn.Services;

public class TriggersControllerTests : IDisposable
{
    private readonly Mock<IChannelRepository> _channelRepositoryMock;
    private readonly Mock<IEpisodeRepository> _episodeRepositoryMock;
    private readonly Mock<IChannelRefreshService> _channelRefreshServiceMock;
    private readonly Mock<IDownloadQueueService> _downloadQueueServiceMock;
    private readonly Mock<IDownloadQueueRepository> _downloadQueueRepositoryMock;
    private readonly Mock<ILogger<TriggersController>> _loggerMock;
    private readonly TriggersController _controller;

    public TriggersControllerTests()
    {
        _channelRepositoryMock = new Mock<IChannelRepository>();
        _episodeRepositoryMock = new Mock<IEpisodeRepository>();
        _channelRefreshServiceMock = new Mock<IChannelRefreshService>();
        _downloadQueueServiceMock = new Mock<IDownloadQueueService>();
        _downloadQueueRepositoryMock = new Mock<IDownloadQueueRepository>();
        _loggerMock = new Mock<ILogger<TriggersController>>();

        _controller = new TriggersController(
            _channelRepositoryMock.Object,
            _episodeRepositoryMock.Object,
            _channelRefreshServiceMock.Object,
            _downloadQueueServiceMock.Object,
            _downloadQueueRepositoryMock.Object,
            _loggerMock.Object);

        // Set up HttpContext with Request
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost:8080");
        httpContext.Request.PathBase = new PathString("");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    public void Dispose()
    {
        // Nothing to dispose
    }

    #region RefreshChannel Tests

    [Fact]
    public async Task RefreshChannel_ShouldReturn404_WhenChannelNotFound()
    {
        // Arrange
        var channelId = "ch-nonexistent";
        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Channel?)null);

        // Act
        var result = await _controller.RefreshChannel(channelId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = result as NotFoundObjectResult;
        var response = notFoundResult!.Value!.GetType().GetProperty("error")!.GetValue(notFoundResult.Value);
        response.Should().NotBeNull();
    }

    [Fact]
    public async Task RefreshChannel_ShouldReturn202_WhenChannelExists()
    {
        // Arrange
        var channel = CreateTestChannel();
        var refreshResult = new RefreshResult
        {
            ChannelId = channel.Id,
            VideosFound = 5,
            EpisodesQueued = 2,
            RefreshedAt = DateTimeOffset.UtcNow
        };

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _channelRefreshServiceMock
            .Setup(s => s.RefreshChannelAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshResult);

        // Act
        var result = await _controller.RefreshChannel(channel.Id);

        // Assert
        var acceptedResult = result.Should().BeOfType<AcceptedResult>().Subject;
        acceptedResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task RefreshChannel_ShouldCallRefreshService()
    {
        // Arrange
        var channel = CreateTestChannel();
        var refreshResult = new RefreshResult
        {
            ChannelId = channel.Id,
            VideosFound = 5,
            EpisodesQueued = 2,
            RefreshedAt = DateTimeOffset.UtcNow
        };

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _channelRefreshServiceMock
            .Setup(s => s.RefreshChannelAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshResult)
            .Verifiable();

        // Act
        await _controller.RefreshChannel(channel.Id);

        // Assert
        _channelRefreshServiceMock.Verify(
            s => s.RefreshChannelAsync(channel.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RefreshChannel_ShouldReturnChannelIdInResponse()
    {
        // Arrange
        var channel = CreateTestChannel();
        var refreshResult = new RefreshResult
        {
            ChannelId = channel.Id,
            VideosFound = 5,
            EpisodesQueued = 2,
            RefreshedAt = DateTimeOffset.UtcNow
        };

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _channelRefreshServiceMock
            .Setup(s => s.RefreshChannelAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshResult);

        // Act
        var result = await _controller.RefreshChannel(channel.Id);

        // Assert
        var acceptedResult = result.Should().BeOfType<AcceptedResult>().Subject;
        var response = acceptedResult.Value.Should().BeOfType<RefreshChannelResponse>().Subject;
        response.Message.Should().Be("Refresh queued");
        response.ChannelId.Should().Be(channel.Id);
    }

    [Fact]
    public async Task RefreshChannel_WithForceTrue_ShouldPassForceParameter()
    {
        // Arrange
        var channel = CreateTestChannel();
        var request = new RefreshChannelRequest { Force = true };
        var refreshResult = new RefreshResult
        {
            ChannelId = channel.Id,
            VideosFound = 5,
            EpisodesQueued = 2,
            RefreshedAt = DateTimeOffset.UtcNow
        };

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _channelRefreshServiceMock
            .Setup(s => s.RefreshChannelAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshResult);

        // Act
        var result = await _controller.RefreshChannel(channel.Id, request);

        // Assert
        result.Should().BeOfType<AcceptedResult>();
        // The force parameter is currently informational (future enhancement)
    }

    #endregion

    #region RefreshAllChannels Tests

    [Fact]
    public async Task RefreshAllChannels_ShouldReturn202_WithChannelsRefreshed()
    {
        // Arrange
        var refreshResults = new List<RefreshResult>
        {
            new() { ChannelId = "ch-1", VideosFound = 5, EpisodesQueued = 2, RefreshedAt = DateTimeOffset.UtcNow },
            new() { ChannelId = "ch-2", VideosFound = 3, EpisodesQueued = 1, RefreshedAt = DateTimeOffset.UtcNow }
        };

        _channelRefreshServiceMock
            .Setup(s => s.RefreshAllChannelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshResults);

        // Act
        var result = await _controller.RefreshAllChannels();

        // Assert
        var acceptedResult = result.Should().BeOfType<AcceptedResult>().Subject;
        acceptedResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task RefreshAllChannels_ShouldReturnCorrectChannelCount()
    {
        // Arrange
        var refreshResults = new List<RefreshResult>
        {
            new() { ChannelId = "ch-1", VideosFound = 5, EpisodesQueued = 2, RefreshedAt = DateTimeOffset.UtcNow },
            new() { ChannelId = "ch-2", VideosFound = 3, EpisodesQueued = 1, RefreshedAt = DateTimeOffset.UtcNow },
            new() { ChannelId = "ch-3", VideosFound = 1, EpisodesQueued = 0, RefreshedAt = DateTimeOffset.UtcNow }
        };

        _channelRefreshServiceMock
            .Setup(s => s.RefreshAllChannelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshResults);

        // Act
        var result = await _controller.RefreshAllChannels();

        // Assert
        var acceptedResult = result.Should().BeOfType<AcceptedResult>().Subject;
        var response = acceptedResult.Value.Should().BeOfType<RefreshAllChannelsResponse>().Subject;
        response.Message.Should().Be("Refresh all queued");
        response.ChannelsRefreshed.Should().Be(3);
    }

    [Fact]
    public async Task RefreshAllChannels_ShouldCallRefreshAllService()
    {
        // Arrange
        var refreshResults = new List<RefreshResult>
        {
            new() { ChannelId = "ch-1", VideosFound = 5, EpisodesQueued = 2, RefreshedAt = DateTimeOffset.UtcNow }
        };

        _channelRefreshServiceMock
            .Setup(s => s.RefreshAllChannelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshResults)
            .Verifiable();

        // Act
        await _controller.RefreshAllChannels();

        // Assert
        _channelRefreshServiceMock.Verify(
            s => s.RefreshAllChannelsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RefreshAllChannels_ShouldReturnZero_WhenNoEnabledChannels()
    {
        // Arrange
        var refreshResults = new List<RefreshResult>();

        _channelRefreshServiceMock
            .Setup(s => s.RefreshAllChannelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshResults);

        // Act
        var result = await _controller.RefreshAllChannels();

        // Assert
        var acceptedResult = result.Should().BeOfType<AcceptedResult>().Subject;
        var response = acceptedResult.Value.Should().BeOfType<RefreshAllChannelsResponse>().Subject;
        response.ChannelsRefreshed.Should().Be(0);
    }

    #endregion

    #region RetryEpisode Tests

    [Fact]
    public async Task RetryEpisode_ShouldReturn404_WhenEpisodeNotFound()
    {
        // Arrange
        var episodeId = "ep-nonexistent";
        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync(episodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Episode?)null);

        // Act
        var result = await _controller.RetryEpisode(episodeId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RetryEpisode_ShouldReturn400_WhenEpisodeNotFailedOrRetrying()
    {
        // Arrange
        var episode = CreateTestEpisode("ch-1", EpisodeStatus.Completed);

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync(episode.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        // Act
        var result = await _controller.RetryEpisode(episode.Id);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task RetryEpisode_ShouldReturn202_WhenEpisodeIsFailed()
    {
        // Arrange
        var episode = CreateTestEpisode("ch-1", EpisodeStatus.Failed);
        episode.RetryCount = 3;
        episode.ErrorMessage = "Download failed";

        var queueItem = new DownloadQueue
        {
            Id = "dq-1",
            EpisodeId = episode.Id,
            Priority = 5,
            Status = QueueStatus.Failed,
            Attempts = 3,
            MaxAttempts = 5,
            LastError = "Download failed",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync(episode.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        _downloadQueueRepositoryMock
            .Setup(r => r.GetByEpisodeIdAsync(episode.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueItem);

        _downloadQueueServiceMock
            .Setup(s => s.EnqueueAsync(episode.Id, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueItem);

        _episodeRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Episode>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.RetryEpisode(episode.Id);

        // Assert
        result.Should().BeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task RetryEpisode_ShouldResetAttemptsAndClearNextRetryAt()
    {
        // Arrange
        var episode = CreateTestEpisode("ch-1", EpisodeStatus.Failed);
        episode.RetryCount = 3;
        episode.ErrorMessage = "Download failed";

        var queueItem = new DownloadQueue
        {
            Id = "dq-1",
            EpisodeId = episode.Id,
            Priority = 5,
            Status = QueueStatus.Failed,
            Attempts = 3,
            MaxAttempts = 5,
            LastError = "Download failed",
            NextRetryAt = DateTimeOffset.UtcNow.AddDays(1),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        Episode? updatedEpisode = null;

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync(episode.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        _downloadQueueRepositoryMock
            .Setup(r => r.GetByEpisodeIdAsync(episode.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueItem);

        _downloadQueueServiceMock
            .Setup(s => s.EnqueueAsync(episode.Id, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueItem);

        _episodeRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Episode>(), It.IsAny<CancellationToken>()))
            .Callback<Episode, CancellationToken>((e, _) => updatedEpisode = e)
            .Returns(Task.CompletedTask);

        // Act
        await _controller.RetryEpisode(episode.Id);

        // Assert
        updatedEpisode.Should().NotBeNull();
        updatedEpisode!.Status.Should().Be(EpisodeStatus.Pending);
        updatedEpisode.RetryCount.Should().Be(0);
        updatedEpisode.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task RetryEpisode_ShouldRequeueForDownload_WhenStatusIsFailed()
    {
        // Arrange
        var episode = CreateTestEpisode("ch-1", EpisodeStatus.Failed);
        episode.RetryCount = 3;
        episode.ErrorMessage = "Download failed";

        var queueItem = new DownloadQueue
        {
            Id = "dq-1",
            EpisodeId = episode.Id,
            Priority = 5,
            Status = QueueStatus.Failed,
            Attempts = 3,
            MaxAttempts = 5,
            LastError = "Download failed",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync(episode.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        _downloadQueueRepositoryMock
            .Setup(r => r.GetByEpisodeIdAsync(episode.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueItem);

        _downloadQueueServiceMock
            .Setup(s => s.EnqueueAsync(episode.Id, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueItem)
            .Verifiable();

        _episodeRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Episode>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.RetryEpisode(episode.Id);

        // Assert
        _downloadQueueServiceMock.Verify(
            s => s.EnqueueAsync(episode.Id, It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RetryEpisode_ShouldReturn400_WhenEpisodeIsPending()
    {
        // Arrange
        var episode = CreateTestEpisode("ch-1", EpisodeStatus.Pending);

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync(episode.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        // Act
        var result = await _controller.RetryEpisode(episode.Id);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RetryEpisode_ShouldReturn400_WhenEpisodeIsDownloading()
    {
        // Arrange
        var episode = CreateTestEpisode("ch-1", EpisodeStatus.Downloading);

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync(episode.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        // Act
        var result = await _controller.RetryEpisode(episode.Id);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RetryEpisode_ShouldReturn400_WhenEpisodeIsProcessing()
    {
        // Arrange
        var episode = CreateTestEpisode("ch-1", EpisodeStatus.Processing);

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync(episode.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        // Act
        var result = await _controller.RetryEpisode(episode.Id);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RetryEpisode_ShouldReturn400_WhenEpisodeIsDeleted()
    {
        // Arrange
        var episode = CreateTestEpisode("ch-1", EpisodeStatus.Deleted);

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync(episode.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        // Act
        var result = await _controller.RetryEpisode(episode.Id);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region Helper Methods

    private static Channel CreateTestChannel()
    {
        var now = DateTimeOffset.UtcNow;
        return new Channel
        {
            Id = $"ch-{Guid.NewGuid():N}",
            Url = "https://youtube.com/@testchannel",
            Title = "Test Channel",
            Description = "Test Description",
            EpisodeCountConfig = 50,
            FeedType = FeedType.Audio,
            Enabled = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static Episode CreateTestEpisode(string channelId, EpisodeStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        return new Episode
        {
            Id = $"ep-{Guid.NewGuid():N}",
            VideoId = $"vid-{Guid.NewGuid():N}",
            ChannelId = channelId,
            Title = "Test Episode",
            Description = "Test Description",
            Status = status,
            RetryCount = 0,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    #endregion
}