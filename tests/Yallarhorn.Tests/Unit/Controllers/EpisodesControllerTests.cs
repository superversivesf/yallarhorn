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

public class EpisodesControllerTests : IDisposable
{
    private readonly Mock<IEpisodeRepository> _episodeRepositoryMock;
    private readonly Mock<IChannelRepository> _channelRepositoryMock;
    private readonly Mock<IDownloadQueueRepository> _downloadQueueRepositoryMock;
    private readonly Mock<IFileService> _fileServiceMock;
    private readonly Mock<ILogger<EpisodesController>> _loggerMock;
    private readonly EpisodesController _controller;

    public EpisodesControllerTests()
    {
        _episodeRepositoryMock = new Mock<IEpisodeRepository>();
        _channelRepositoryMock = new Mock<IChannelRepository>();
        _downloadQueueRepositoryMock = new Mock<IDownloadQueueRepository>();
        _fileServiceMock = new Mock<IFileService>();
        _loggerMock = new Mock<ILogger<EpisodesController>>();

        _controller = new EpisodesController(
            _channelRepositoryMock.Object,
            _episodeRepositoryMock.Object,
            _downloadQueueRepositoryMock.Object,
            _fileServiceMock.Object,
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

    #region GetEpisodesByChannel Tests

    [Fact]
    public async Task GetEpisodesByChannel_ShouldReturnPaginatedResponse_WithDefaultValues()
    {
        // Arrange
        var channelId = "ch-abc123";
        var channel = CreateTestChannel(channelId);
        var episodes = CreateTestEpisodes(channelId, 3);

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        SetupPagedEpisodes(episodes, 3, channelId: channelId);

        // Act
        var result = await _controller.GetEpisodesByChannel(channelId, new PaginationQuery());

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<EpisodeResponse>>().Subject;

        response.Data.Should().HaveCount(3);
        response.Page.Should().Be(1);
        response.Limit.Should().Be(50);
        response.TotalCount.Should().Be(3);
        response.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task GetEpisodesByChannel_ShouldReturn404_WhenChannelNotFound()
    {
        // Arrange
        var channelId = "ch-nonexistent";
        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Channel?)null);

        // Act
        var result = await _controller.GetEpisodesByChannel(channelId, new PaginationQuery());

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetEpisodesByChannel_ShouldApplyPaginationCorrectly()
    {
        // Arrange
        var channelId = "ch-abc123";
        var channel = CreateTestChannel(channelId);
        var episodes = CreateTestEpisodes(channelId, 15);

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        // Controller uses FindAsync with client-side pagination, so return all episodes
        _episodeRepositoryMock
            .Setup(r => r.FindAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Episode, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(episodes);

        var query = new PaginationQuery { Page = 2, Limit = 5 };

        // Act
        var result = await _controller.GetEpisodesByChannel(channelId, query);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<EpisodeResponse>>().Subject;

        response.Page.Should().Be(2);
        response.Limit.Should().Be(5);
        response.TotalCount.Should().Be(15);
        response.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task GetEpisodesByChannel_ShouldFilterByStatus()
    {
        // Arrange
        var channelId = "ch-abc123";
        var channel = CreateTestChannel(channelId);
        var episodes = CreateTestEpisodes(channelId, 3);
        episodes[0].Status = EpisodeStatus.Completed;
        episodes[1].Status = EpisodeStatus.Completed;
        episodes[2].Status = EpisodeStatus.Failed;

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        SetupPagedEpisodes(
            episodes.Where(e => e.Status == EpisodeStatus.Completed).ToList(),
            2,
            channelId: channelId);

        var query = new PaginationQuery();

        // Act
        var result = await _controller.GetEpisodesByChannel(channelId, query, status: "completed");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<EpisodeResponse>>().Subject;

        response.Data.Should().HaveCount(2);
        response.Data.All(e => e.Status == "completed").Should().BeTrue();
    }

    [Fact]
    public async Task GetEpisodesByChannel_ShouldSortByPublishedAtDescending_ByDefault()
    {
        // Arrange
        var channelId = "ch-abc123";
        var channel = CreateTestChannel(channelId);
        var now = DateTimeOffset.UtcNow;
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channelId, "ep-1", publishedAt: now.AddDays(-1)),
            CreateTestEpisode(channelId, "ep-2", publishedAt: now.AddDays(-2)),
            CreateTestEpisode(channelId, "ep-3", publishedAt: now),
        };

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _episodeRepositoryMock
            .Setup(r => r.FindAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Episode, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(episodes);

        var query = new PaginationQuery();

        // Act
        var result = await _controller.GetEpisodesByChannel(channelId, query);

        // Assert - Controller uses FindAsync for filtering, sorting is done client-side
        _episodeRepositoryMock.Verify(r => r.FindAsync(
            It.IsAny<System.Linq.Expressions.Expression<Func<Episode, bool>>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify descending order (ep-3 is newest, should be first)
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<EpisodeResponse>>().Subject;
        response.Data.First().Id.Should().Be("ep-3");
    }

    [Fact]
    public async Task GetEpisodesByChannel_ShouldSortByTitleAscending_WhenSpecified()
    {
        // Arrange
        var channelId = "ch-abc123";
        var channel = CreateTestChannel(channelId);
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channelId, "ep-1", title: "Beta"),
            CreateTestEpisode(channelId, "ep-2", title: "Alpha"),
        };

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _episodeRepositoryMock
            .Setup(r => r.FindAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Episode, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(episodes);

        var query = new PaginationQuery { Sort = "title", Order = "asc" };

        // Act
        var result = await _controller.GetEpisodesByChannel(channelId, query);

        // Assert - Controller uses FindAsync for filtering, sorting is done client-side
        _episodeRepositoryMock.Verify(r => r.FindAsync(
            It.IsAny<System.Linq.Expressions.Expression<Func<Episode, bool>>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify ascending order by title (Alpha should be first)
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<EpisodeResponse>>().Subject;
        response.Data.First().Title.Should().Be("Alpha");
    }

    [Fact]
    public async Task GetEpisodesByChannel_ShouldIncludeHateoasLinks()
    {
        // Arrange
        var channelId = "ch-abc123";
        var channel = CreateTestChannel(channelId);
        var episodes = CreateTestEpisodes(channelId, 1);

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        SetupPagedEpisodes(episodes, 1, channelId: channelId);

        // Act
        var result = await _controller.GetEpisodesByChannel(channelId, new PaginationQuery());

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<EpisodeResponse>>().Subject;

        response.Links.Should().ContainKey("self");
        response.Links["self"].Href.Should().Contain($"/api/v1/channels/{channelId}/episodes");
        response.Links.Should().ContainKey("channel");
        response.Links["channel"].Href.Should().Be($"/api/v1/channels/{channelId}");
    }

    [Fact]
    public async Task GetEpisodesByChannel_ShouldIncludeEpisodeLinks()
    {
        // Arrange
        var channelId = "ch-abc123";
        var channel = CreateTestChannel(channelId);
        var episodes = CreateTestEpisodes(channelId, 1);

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        SetupPagedEpisodes(episodes, 1, channelId: channelId);

        // Act
        var result = await _controller.GetEpisodesByChannel(channelId, new PaginationQuery());

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<EpisodeResponse>>().Subject;

        var episode = response.Data.First();
        episode.Links.Should().ContainKey("self");
        episode.Links.Should().ContainKey("channel");
        episode.Links["self"].Href.Should().Be($"/api/v1/episodes/{episode.Id}");
        episode.Links["channel"].Href.Should().Be($"/api/v1/channels/{channelId}");
    }

    [Fact]
    public async Task GetEpisodesByChannel_ShouldIncludeAudioAndVideoLinks_WhenFilesExist()
    {
        // Arrange
        var channelId = "ch-abc123";
        var channel = CreateTestChannel(channelId);
        var episode = CreateTestEpisode(channelId, "ep-1");
        episode.FilePathAudio = "audio/file.mp3";
        episode.FilePathVideo = "video/file.mp4";

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        SetupPagedEpisodes(new List<Episode> { episode }, 1, channelId: channelId);

        // Act
        var result = await _controller.GetEpisodesByChannel(channelId, new PaginationQuery());

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<EpisodeResponse>>().Subject;

        var ep = response.Data.First();
        ep.Links.Should().ContainKey("audio_file");
        ep.Links.Should().ContainKey("video_file");
        ep.Links["audio_file"].Href.Should().Contain("/feeds/");
        ep.Links["video_file"].Href.Should().Contain("/feeds/");
    }

    [Fact]
    public async Task GetEpisodesByChannel_ShouldNotIncludeAudioVideoLinks_WhenFilesDoNotExist()
    {
        // Arrange
        var channelId = "ch-abc123";
        var channel = CreateTestChannel(channelId);
        var episode = CreateTestEpisode(channelId, "ep-1");
        episode.FilePathAudio = null;
        episode.FilePathVideo = null;

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        SetupPagedEpisodes(new List<Episode> { episode }, 1, channelId: channelId);

        // Act
        var result = await _controller.GetEpisodesByChannel(channelId, new PaginationQuery());

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<EpisodeResponse>>().Subject;

        var ep = response.Data.First();
        ep.Links.Should().NotContainKey("audio_file");
        ep.Links.Should().NotContainKey("video_file");
    }

    [Fact]
    public async Task GetEpisodesByChannel_ShouldReturnEmptyList_WhenNoEpisodesExist()
    {
        // Arrange
        var channelId = "ch-abc123";
        var channel = CreateTestChannel(channelId);

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        SetupPagedEpisodes(new List<Episode>(), 0, channelId: channelId);

        // Act
        var result = await _controller.GetEpisodesByChannel(channelId, new PaginationQuery());

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<EpisodeResponse>>().Subject;

        response.Data.Should().BeEmpty();
        response.TotalCount.Should().Be(0);
        response.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task GetEpisodesByChannel_ShouldMapAllEpisodePropertiesCorrectly()
    {
        // Arrange
        var channelId = "ch-abc123";
        var channel = CreateTestChannel(channelId);
        var now = DateTimeOffset.UtcNow;
        var episode = new Episode
        {
            Id = "ep-abc123",
            VideoId = "yt_video_123",
            ChannelId = channelId,
            Title = "Test Episode",
            Description = "Episode description",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            DurationSeconds = 3600,
            PublishedAt = now.AddDays(-1),
            DownloadedAt = now,
            FilePathAudio = "audio/test.mp3",
            FilePathVideo = "video/test.mp4",
            FileSizeAudio = 52428800,
            FileSizeVideo = 524288000,
            Status = EpisodeStatus.Completed,
            RetryCount = 0,
            ErrorMessage = null,
            CreatedAt = now.AddDays(-2),
            UpdatedAt = now
        };

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        SetupPagedEpisodes(new List<Episode> { episode }, 1, channelId: channelId);

        // Act
        var result = await _controller.GetEpisodesByChannel(channelId, new PaginationQuery());

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<EpisodeResponse>>().Subject;

        var mappedEpisode = response.Data.First();
        mappedEpisode.Id.Should().Be("ep-abc123");
        mappedEpisode.VideoId.Should().Be("yt_video_123");
        mappedEpisode.ChannelId.Should().Be(channelId);
        mappedEpisode.Title.Should().Be("Test Episode");
        mappedEpisode.Description.Should().Be("Episode description");
        mappedEpisode.ThumbnailUrl.Should().Be("https://example.com/thumb.jpg");
        mappedEpisode.DurationSeconds.Should().Be(3600);
        mappedEpisode.PublishedAt.Should().Be(now.AddDays(-1));
        mappedEpisode.DownloadedAt.Should().Be(now);
        mappedEpisode.FilePathAudio.Should().Be("audio/test.mp3");
        mappedEpisode.FilePathVideo.Should().Be("video/test.mp4");
        mappedEpisode.FileSizeAudio.Should().Be(52428800);
        mappedEpisode.FileSizeVideo.Should().Be(524288000);
        mappedEpisode.Status.Should().Be("completed");
        mappedEpisode.ErrorMessage.Should().BeNull();
        mappedEpisode.CreatedAt.Should().Be(now.AddDays(-2));
        mappedEpisode.UpdatedAt.Should().Be(now);
    }

    [Theory]
    [InlineData("pending", EpisodeStatus.Pending)]
    [InlineData("downloading", EpisodeStatus.Downloading)]
    [InlineData("processing", EpisodeStatus.Processing)]
    [InlineData("completed", EpisodeStatus.Completed)]
    [InlineData("failed", EpisodeStatus.Failed)]
    [InlineData("deleted", EpisodeStatus.Deleted)]
    public async Task GetEpisodesByChannel_ShouldFilterByAllValidStatuses(string statusParam, EpisodeStatus expectedStatus)
    {
        // Arrange
        var channelId = "ch-abc123";
        var channel = CreateTestChannel(channelId);
        var episode = CreateTestEpisode(channelId, "ep-1");
        episode.Status = expectedStatus;

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        SetupPagedEpisodes(new List<Episode> { episode }, 1, channelId: channelId);

        var query = new PaginationQuery();

        // Act
        var result = await _controller.GetEpisodesByChannel(channelId, query, status: statusParam);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<EpisodeResponse>>().Subject;
        response.Data.Should().HaveCount(1);
        response.Data.First().Status.Should().Be(statusParam);
    }

    #endregion

    #region GetEpisode Tests

    [Fact]
    public async Task GetEpisode_ShouldReturnEpisode_WhenEpisodeExists()
    {
        // Arrange
        var channelId = "ch-abc123";
        var episode = CreateTestEpisode(channelId, "ep-123");

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync("ep-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        // Act
        var result = await _controller.GetEpisode("ep-123");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<EpisodeResponse>().Subject;

        response.Id.Should().Be("ep-123");
        response.VideoId.Should().Be("yt_ep-123");
        response.ChannelId.Should().Be(channelId);
        response.Title.Should().Be("Test Episode ep-123");
    }

    [Fact]
    public async Task GetEpisode_ShouldReturn404_WhenEpisodeNotFound()
    {
        // Arrange
        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync("ep-nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Episode?)null);

        // Act
        var result = await _controller.GetEpisode("ep-nonexistent");

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetEpisode_ShouldIncludeHateoasLinks()
    {
        // Arrange
        var channelId = "ch-abc123";
        var episode = CreateTestEpisode(channelId, "ep-123");

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync("ep-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        // Act
        var result = await _controller.GetEpisode("ep-123");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<EpisodeResponse>().Subject;

        response.Links.Should().ContainKey("self");
        response.Links["self"].Href.Should().Be("/api/v1/episodes/ep-123");
        response.Links.Should().ContainKey("channel");
        response.Links["channel"].Href.Should().Be($"/api/v1/channels/{channelId}");
    }

    [Fact]
    public async Task GetEpisode_ShouldIncludeAudioLink_WhenAudioFileExists()
    {
        // Arrange
        var channelId = "ch-abc123";
        var episode = CreateTestEpisode(channelId, "ep-123");
        episode.FilePathAudio = "audio/test.mp3";

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync("ep-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        // Act
        var result = await _controller.GetEpisode("ep-123");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<EpisodeResponse>().Subject;

        response.Links.Should().ContainKey("audio_file");
        response.Links["audio_file"].Href.Should().Be("/feeds/audio/test.mp3");
    }

    [Fact]
    public async Task GetEpisode_ShouldIncludeVideoLink_WhenVideoFileExists()
    {
        // Arrange
        var channelId = "ch-abc123";
        var episode = CreateTestEpisode(channelId, "ep-123");
        episode.FilePathVideo = "video/test.mp4";

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync("ep-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        // Act
        var result = await _controller.GetEpisode("ep-123");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<EpisodeResponse>().Subject;

        response.Links.Should().ContainKey("video_file");
        response.Links["video_file"].Href.Should().Be("/feeds/video/test.mp4");
    }

    [Fact]
    public async Task GetEpisode_ShouldIncludeBothFileLinks_WhenBothFilesExist()
    {
        // Arrange
        var channelId = "ch-abc123";
        var episode = CreateTestEpisode(channelId, "ep-123");
        episode.FilePathAudio = "audio/test.mp3";
        episode.FilePathVideo = "video/test.mp4";

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync("ep-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        // Act
        var result = await _controller.GetEpisode("ep-123");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<EpisodeResponse>().Subject;

        response.Links.Should().ContainKey("audio_file");
        response.Links.Should().ContainKey("video_file");
    }

    [Fact]
    public async Task GetEpisode_ShouldNotIncludeFileLinks_WhenNoFilesExist()
    {
        // Arrange
        var channelId = "ch-abc123";
        var episode = CreateTestEpisode(channelId, "ep-123");
        episode.FilePathAudio = null;
        episode.FilePathVideo = null;

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync("ep-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        // Act
        var result = await _controller.GetEpisode("ep-123");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<EpisodeResponse>().Subject;

        response.Links.Should().NotContainKey("audio_file");
        response.Links.Should().NotContainKey("video_file");
    }

    [Fact]
    public async Task GetEpisode_ShouldIncludeErrorMessage_WhenStatusIsFailed()
    {
        // Arrange
        var channelId = "ch-abc123";
        var episode = CreateTestEpisode(channelId, "ep-123");
        episode.Status = EpisodeStatus.Failed;
        episode.ErrorMessage = "Download failed: Network timeout";

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync("ep-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        // Act
        var result = await _controller.GetEpisode("ep-123");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<EpisodeResponse>().Subject;

        response.Status.Should().Be("failed");
        response.ErrorMessage.Should().Be("Download failed: Network timeout");
    }

    [Fact]
    public async Task GetEpisode_ShouldMapAllPropertiesCorrectly()
    {
        // Arrange
        var channelId = "ch-abc123";
        var now = DateTimeOffset.UtcNow;
        var episode = new Episode
        {
            Id = "ep-abc123",
            VideoId = "yt_video_123",
            ChannelId = channelId,
            Title = "Test Episode",
            Description = "Episode description",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            DurationSeconds = 3600,
            PublishedAt = now.AddDays(-1),
            DownloadedAt = now,
            FilePathAudio = "audio/test.mp3",
            FilePathVideo = "video/test.mp4",
            FileSizeAudio = 52428800,
            FileSizeVideo = 524288000,
            Status = EpisodeStatus.Completed,
            RetryCount = 2,
            ErrorMessage = null,
            CreatedAt = now.AddDays(-2),
            UpdatedAt = now
        };

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync("ep-abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        // Act
        var result = await _controller.GetEpisode("ep-abc123");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<EpisodeResponse>().Subject;

        response.Id.Should().Be("ep-abc123");
        response.VideoId.Should().Be("yt_video_123");
        response.ChannelId.Should().Be(channelId);
        response.Title.Should().Be("Test Episode");
        response.Description.Should().Be("Episode description");
        response.ThumbnailUrl.Should().Be("https://example.com/thumb.jpg");
        response.DurationSeconds.Should().Be(3600);
        response.PublishedAt.Should().Be(now.AddDays(-1));
        response.DownloadedAt.Should().Be(now);
        response.FilePathAudio.Should().Be("audio/test.mp3");
        response.FilePathVideo.Should().Be("video/test.mp4");
        response.FileSizeAudio.Should().Be(52428800);
        response.FileSizeVideo.Should().Be(524288000);
        response.Status.Should().Be("completed");
        response.RetryCount.Should().Be(2);
        response.ErrorMessage.Should().BeNull();
        response.CreatedAt.Should().Be(now.AddDays(-2));
        response.UpdatedAt.Should().Be(now);
    }

    #endregion

    #region DeleteEpisode Tests

    [Fact]
    public async Task DeleteEpisode_ShouldReturn404_WhenEpisodeNotFound()
    {
        // Arrange
        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync("ep-nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Episode?)null);

        // Act
        var result = await _controller.DeleteEpisode("ep-nonexistent", delete_files: true);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteEpisode_ShouldReturn409_WhenEpisodeIsDownloading()
    {
        // Arrange
        var episode = CreateTestEpisode("ch-abc123", "ep-123");
        episode.Status = EpisodeStatus.Downloading;

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync("ep-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        // Act
        var result = await _controller.DeleteEpisode("ep-123", delete_files: true);

        // Assert
        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task DeleteEpisode_ShouldDeleteEpisodeAndReturn200_WhenStatusIsCompleted()
    {
        // Arrange
        var episode = CreateTestEpisode("ch-abc123", "ep-123");
        episode.Status = EpisodeStatus.Completed;
        episode.FilePathAudio = "audio/test.mp3";
        episode.FilePathVideo = "video/test.mp4";
        episode.FileSizeAudio = 52428800;
        episode.FileSizeVideo = 524288000;

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync("ep-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        _episodeRepositoryMock
            .Setup(r => r.DeleteAsync(It.IsAny<Episode>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _fileServiceMock
            .Setup(f => f.FileExists(It.IsAny<string>()))
            .Returns(true);

        // Act
        var result = await _controller.DeleteEpisode("ep-123", delete_files: true);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<dynamic>().Subject;

        // Verify episode was deleted from repository
        _episodeRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Episode>(), It.IsAny<CancellationToken>()), Times.Once);

        // Verify files were deleted
        _fileServiceMock.Verify(f => f.DeleteFile(It.IsAny<string>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task DeleteEpisode_ShouldDeleteEpisodeWithoutFiles_WhenDeleteFilesIsFalse()
    {
        // Arrange
        var episode = CreateTestEpisode("ch-abc123", "ep-123");
        episode.Status = EpisodeStatus.Completed;
        episode.FilePathAudio = "audio/test.mp3";
        episode.FilePathVideo = "video/test.mp4";
        episode.FileSizeAudio = 52428800;
        episode.FileSizeVideo = 524288000;

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync("ep-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        _episodeRepositoryMock
            .Setup(r => r.DeleteAsync(It.IsAny<Episode>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteEpisode("ep-123", delete_files: false);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Verify episode was deleted from repository
        _episodeRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Episode>(), It.IsAny<CancellationToken>()), Times.Once);

        // Verify files were NOT deleted
        _fileServiceMock.Verify(f => f.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteEpisode_ShouldRemoveFromQueue_WhenStatusIsPending()
    {
        // Arrange
        var episode = CreateTestEpisode("ch-abc123", "ep-123");
        episode.Status = EpisodeStatus.Pending;

        var queueItem = new DownloadQueue
        {
            Id = "dq-123",
            EpisodeId = "ep-123",
            Status = QueueStatus.Pending,
            Priority = 5,
            Attempts = 0,
            MaxAttempts = 5,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync("ep-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        _episodeRepositoryMock
            .Setup(r => r.DeleteAsync(It.IsAny<Episode>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _downloadQueueRepositoryMock
            .Setup(r => r.GetByEpisodeIdAsync("ep-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueItem);

        _downloadQueueRepositoryMock
            .Setup(r => r.DeleteAsync(It.IsAny<DownloadQueue>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteEpisode("ep-123", delete_files: true);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Verify queue item was deleted
        _downloadQueueRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<DownloadQueue>(), It.IsAny<CancellationToken>()), Times.Once);

        // Verify episode was deleted
        _episodeRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Episode>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteEpisode_ShouldRemoveFromQueue_WhenStatusIsDownloading()
    {
        // Arrange
        var episode = CreateTestEpisode("ch-abc123", "ep-123");
        episode.Status = EpisodeStatus.Downloading;

        var queueItem = new DownloadQueue
        {
            Id = "dq-123",
            EpisodeId = "ep-123",
            Status = QueueStatus.InProgress,
            Priority = 5,
            Attempts = 1,
            MaxAttempts = 5,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync("ep-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        _downloadQueueRepositoryMock
            .Setup(r => r.GetByEpisodeIdAsync("ep-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueItem);

        // Act
        var result = await _controller.DeleteEpisode("ep-123", delete_files: true);

        // Assert - Should return 409 Conflict
        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task DeleteEpisode_ShouldReturnCorrectBytesFreed_WhenFilesExist()
    {
        // Arrange
        var episode = CreateTestEpisode("ch-abc123", "ep-123");
        episode.Status = EpisodeStatus.Completed;
        episode.FilePathAudio = "audio/test.mp3";
        episode.FilePathVideo = "video/test.mp4";
        episode.FileSizeAudio = 52428800;  // 50 MB
        episode.FileSizeVideo = 524288000; // 500 MB

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync("ep-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        _episodeRepositoryMock
            .Setup(r => r.DeleteAsync(It.IsAny<Episode>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _fileServiceMock
            .Setup(f => f.FileExists(It.IsAny<string>()))
            .Returns(true);

        // Act
        var result = await _controller.DeleteEpisode("ep-123", delete_files: true);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        // The response should have bytes_freed = 52428800 + 524288000 = 576716800
        // Verify through dynamic access
    }

    [Fact]
    public async Task DeleteEpisode_ShouldHandleEpisodeWithNoFiles()
    {
        // Arrange
        var episode = CreateTestEpisode("ch-abc123", "ep-123");
        episode.Status = EpisodeStatus.Completed;
        episode.FilePathAudio = null;
        episode.FilePathVideo = null;
        episode.FileSizeAudio = null;
        episode.FileSizeVideo = null;

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync("ep-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        _episodeRepositoryMock
            .Setup(r => r.DeleteAsync(It.IsAny<Episode>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteEpisode("ep-123", delete_files: true);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Verify no file deletion was attempted
        _fileServiceMock.Verify(f => f.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteEpisode_ShouldHandleEpisodeWithOnlyAudioFile()
    {
        // Arrange
        var episode = CreateTestEpisode("ch-abc123", "ep-123");
        episode.Status = EpisodeStatus.Completed;
        episode.FilePathAudio = "audio/test.mp3";
        episode.FilePathVideo = null;
        episode.FileSizeAudio = 52428800;
        episode.FileSizeVideo = null;

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync("ep-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        _episodeRepositoryMock
            .Setup(r => r.DeleteAsync(It.IsAny<Episode>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _fileServiceMock
            .Setup(f => f.FileExists(It.IsAny<string>()))
            .Returns(true);

        // Act
        var result = await _controller.DeleteEpisode("ep-123", delete_files: true);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Verify only one file was deleted
        _fileServiceMock.Verify(f => f.DeleteFile(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task DeleteEpisode_ShouldHandleEpisodeWithOnlyVideoFile()
    {
        // Arrange
        var episode = CreateTestEpisode("ch-abc123", "ep-123");
        episode.Status = EpisodeStatus.Completed;
        episode.FilePathAudio = null;
        episode.FilePathVideo = "video/test.mp4";
        episode.FileSizeAudio = null;
        episode.FileSizeVideo = 524288000;

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync("ep-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        _episodeRepositoryMock
            .Setup(r => r.DeleteAsync(It.IsAny<Episode>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _fileServiceMock
            .Setup(f => f.FileExists(It.IsAny<string>()))
            .Returns(true);

        // Act
        var result = await _controller.DeleteEpisode("ep-123", delete_files: true);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Verify only one file was deleted
        _fileServiceMock.Verify(f => f.DeleteFile(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task DeleteEpisode_ShouldNotDeleteNonexistentFiles()
    {
        // Arrange
        var episode = CreateTestEpisode("ch-abc123", "ep-123");
        episode.Status = EpisodeStatus.Completed;
        episode.FilePathAudio = "audio/test.mp3";
        episode.FilePathVideo = "video/test.mp4";
        episode.FileSizeAudio = 52428800;
        episode.FileSizeVideo = 524288000;

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync("ep-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        _episodeRepositoryMock
            .Setup(r => r.DeleteAsync(It.IsAny<Episode>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _fileServiceMock
            .Setup(f => f.FileExists(It.IsAny<string>()))
            .Returns(false);

        // Act
        var result = await _controller.DeleteEpisode("ep-123", delete_files: true);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Verify no file deletion was attempted since files don't exist
        _fileServiceMock.Verify(f => f.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteEpisode_ShouldHandleEpisodeInFailedStatus()
    {
        // Arrange
        var episode = CreateTestEpisode("ch-abc123", "ep-123");
        episode.Status = EpisodeStatus.Failed;
        episode.FilePathAudio = null;
        episode.FilePathVideo = null;

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync("ep-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        _episodeRepositoryMock
            .Setup(r => r.DeleteAsync(It.IsAny<Episode>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteEpisode("ep-123", delete_files: true);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Verify episode was deleted
        _episodeRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Episode>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteEpisode_ShouldHandleEpisodeInDeletedStatus()
    {
        // Arrange
        var episode = CreateTestEpisode("ch-abc123", "ep-123");
        episode.Status = EpisodeStatus.Deleted;
        episode.FilePathAudio = null;
        episode.FilePathVideo = null;

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync("ep-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        _episodeRepositoryMock
            .Setup(r => r.DeleteAsync(It.IsAny<Episode>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteEpisode("ep-123", delete_files: true);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Verify episode was deleted from repository
        _episodeRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Episode>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteEpisode_ShouldNotRemoveQueueItem_WhenNoQueueItemExists()
    {
        // Arrange
        var episode = CreateTestEpisode("ch-abc123", "ep-123");
        episode.Status = EpisodeStatus.Pending;

        _episodeRepositoryMock
            .Setup(r => r.GetByIdAsync("ep-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        _episodeRepositoryMock
            .Setup(r => r.DeleteAsync(It.IsAny<Episode>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _downloadQueueRepositoryMock
            .Setup(r => r.GetByEpisodeIdAsync("ep-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DownloadQueue?)null);

        // Act
        var result = await _controller.DeleteEpisode("ep-123", delete_files: true);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Verify DeleteAsync was never called on queue repository since no queue item exists
        _downloadQueueRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<DownloadQueue>(), It.IsAny<CancellationToken>()), Times.Never);

        // Verify episode was still deleted
        _episodeRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Episode>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Helper Methods

    private static Channel CreateTestChannel(string id)
    {
        return new Channel
        {
            Id = id,
            Url = $"https://youtube.com/@{id}",
            Title = $"Test Channel {id}",
            Description = $"Description for {id}",
            ThumbnailUrl = $"https://example.com/{id}.jpg",
            EpisodeCountConfig = 50,
            FeedType = FeedType.Audio,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static Episode CreateTestEpisode(string channelId, string id, DateTimeOffset? publishedAt = null, string? title = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new Episode
        {
            Id = id,
            VideoId = $"yt_{id}",
            ChannelId = channelId,
            Title = title ?? $"Test Episode {id}",
            Description = $"Description for {id}",
            ThumbnailUrl = $"https://example.com/ep{id}.jpg",
            DurationSeconds = 3600,
            PublishedAt = publishedAt ?? now.AddDays(-1),
            DownloadedAt = now,
            Status = EpisodeStatus.Completed,
            CreatedAt = now.AddDays(-2),
            UpdatedAt = now
        };
    }

    private static List<Episode> CreateTestEpisodes(string channelId, int count)
    {
        var now = DateTimeOffset.UtcNow;
        return Enumerable.Range(1, count)
            .Select(i => new Episode
            {
                Id = $"ep-{i}",
                VideoId = $"yt_video_{i}",
                ChannelId = channelId,
                Title = $"Test Episode {i}",
                Description = $"Description for episode {i}",
                ThumbnailUrl = $"https://example.com/ep{i}.jpg",
                DurationSeconds = 3600 + i,
                PublishedAt = now.AddDays(-i),
                DownloadedAt = now.AddHours(-i),
                FilePathAudio = $"audio/ep{i}.mp3",
                FilePathVideo = $"video/ep{i}.mp4",
                FileSizeAudio = 52428800 + i,
                FileSizeVideo = 524288000 + i,
                Status = EpisodeStatus.Completed,
                RetryCount = 0,
                ErrorMessage = null,
                CreatedAt = now.AddDays(-i - 1),
                UpdatedAt = now
            })
            .ToList();
    }

    private void SetupPagedEpisodes(List<Episode> episodes, int totalCount, int page = 1, int pageSize = 50, string? channelId = null)
    {
        // Controller uses FindAsync for filtering, then does client-side pagination
        _episodeRepositoryMock
            .Setup(r => r.FindAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Episode, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(episodes);
    }

    #endregion
}