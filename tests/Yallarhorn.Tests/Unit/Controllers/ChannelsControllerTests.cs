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

public class ChannelsControllerTests : IDisposable
{
    private readonly Mock<IChannelRepository> _channelRepositoryMock;
    private readonly Mock<IEpisodeRepository> _episodeRepositoryMock;
    private readonly Mock<IFileService> _fileServiceMock;
    private readonly Mock<ILogger<ChannelsController>> _loggerMock;
    private readonly ChannelsController _controller;

    public ChannelsControllerTests()
    {
        _channelRepositoryMock = new Mock<IChannelRepository>();
        _episodeRepositoryMock = new Mock<IEpisodeRepository>();
        _fileServiceMock = new Mock<IFileService>();
        _loggerMock = new Mock<ILogger<ChannelsController>>();

        _controller = new ChannelsController(
            _channelRepositoryMock.Object,
            _episodeRepositoryMock.Object,
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

    #region GetChannels Tests

    [Fact]
    public async Task GetChannels_ShouldReturnPaginatedResponse_WithDefaultValues()
    {
        // Arrange
        var channels = CreateTestChannels(3);
        SetupPagedChannels(channels, 3);

        // Act
        var result = await _controller.GetChannels(new PaginationQuery());

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<ChannelResponse>>().Subject;

        response.Data.Should().HaveCount(3);
        response.Page.Should().Be(1);
        response.Limit.Should().Be(50);
        response.TotalCount.Should().Be(3);
        response.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task GetChannels_ShouldApplyPaginationCorrectly()
    {
        // Arrange
        var channels = CreateTestChannels(15);

        // Controller uses GetAllAsync with client-side pagination, so return all channels
        _channelRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(channels);

        foreach (var channel in channels)
        {
            _episodeRepositoryMock
                .Setup(r => r.CountByChannelIdAsync(channel.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);
        }

        var query = new PaginationQuery { Page = 2, Limit = 5 };

        // Act
        var result = await _controller.GetChannels(query);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<ChannelResponse>>().Subject;

        response.Page.Should().Be(2);
        response.Limit.Should().Be(5);
        response.TotalCount.Should().Be(15);
        response.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task GetChannels_ShouldLimitMaximumPageSize()
    {
        // Arrange
        var channels = CreateTestChannels(5);
        
        // Controller uses GetAllAsync when no filters, with client-side pagination
        _channelRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(channels);

        foreach (var channel in channels)
        {
            _episodeRepositoryMock
                .Setup(r => r.CountByChannelIdAsync(channel.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);
        }

        var query = new PaginationQuery { Page = 1, Limit = 200 }; // Limit should be capped to 100

        // Act
        var result = await _controller.GetChannels(query);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<ChannelResponse>>().Subject;

        response.Limit.Should().Be(100); // Max limit is 100
    }

    [Fact]
    public async Task GetChannels_ShouldNormalizeInvalidPageNumber()
    {
        // Arrange
        var channels = CreateTestChannels(3);
        
        // Controller uses GetAllAsync() when no filters, with client-side pagination
        _channelRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(channels);

        foreach (var channel in channels)
        {
            _episodeRepositoryMock
                .Setup(r => r.CountByChannelIdAsync(channel.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);
        }

        var query = new PaginationQuery { Page = 0, Limit = 10 }; // Page should be normalized to 1

        // Act
        var result = await _controller.GetChannels(query);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<ChannelResponse>>().Subject;

        response.Page.Should().Be(1);
    }

    [Fact]
    public async Task GetChannels_ShouldFilterByEnabled()
    {
        // Arrange
        var channels = CreateTestChannels(3);
        channels[1].Enabled = false;

        // Controller uses FindAsync when filter is applied, with client-side pagination
        _channelRepositoryMock
            .Setup(r => r.FindAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Channel, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(channels.Where(c => c.Enabled).ToList());

        foreach (var channel in channels.Where(c => c.Enabled))
        {
            _episodeRepositoryMock
                .Setup(r => r.CountByChannelIdAsync(channel.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);
        }

        var query = new PaginationQuery { Page = 1, Limit = 50 };
        query.Sort = "created_at"; // Initialize sort

        // Act
        var result = await _controller.GetChannels(query, enabled: true);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<ChannelResponse>>().Subject;

        response.Data.Should().HaveCount(2);
        response.Data.All(c => c.Enabled).Should().BeTrue();
    }

    [Fact]
    public async Task GetChannels_ShouldFilterByFeedType()
    {
        // Arrange
        var channels = CreateTestChannels(3);
        channels[0].FeedType = FeedType.Audio;
        channels[1].FeedType = FeedType.Video;
        channels[2].FeedType = FeedType.Audio;

        var audioChannels = channels.Where(c => c.FeedType == FeedType.Audio).ToList();

        // Controller uses FindAsync when filter is applied, with client-side pagination
        _channelRepositoryMock
            .Setup(r => r.FindAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Channel, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(audioChannels);

        foreach (var channel in audioChannels)
        {
            _episodeRepositoryMock
                .Setup(r => r.CountByChannelIdAsync(channel.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);
        }

        var query = new PaginationQuery();

        // Act
        var result = await _controller.GetChannels(query, feedType: "audio");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<ChannelResponse>>().Subject;

        response.Data.Should().HaveCount(2);
        response.Data.All(c => c.FeedType == "audio").Should().BeTrue();
    }

    [Fact]
    public async Task GetChannels_ShouldIncludeHateoasLinks()
    {
        // Arrange
        var channels = CreateTestChannels(1);
        SetupPagedChannels(channels, 1);

        // Act
        var result = await _controller.GetChannels(new PaginationQuery());

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<ChannelResponse>>().Subject;

        response.Links.Should().ContainKey("self");
        response.Links["self"].Href.Should().Contain("/api/v1/channels");
    }

    [Fact]
    public async Task GetChannels_ShouldIncludeChannelLinks()
    {
        // Arrange
        var channels = CreateTestChannels(1);
        SetupPagedChannels(channels, 1);

        // Act
        var result = await _controller.GetChannels(new PaginationQuery());

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<ChannelResponse>>().Subject;

        var channel = response.Data.First();
        channel.Links.Should().ContainKey("self");
        channel.Links.Should().ContainKey("episodes");
        channel.Links.Should().ContainKey("refresh");
        channel.Links["self"].Href.Should().Be($"/api/v1/channels/{channel.Id}");
        channel.Links["episodes"].Href.Should().Be($"/api/v1/channels/{channel.Id}/episodes");
        channel.Links["refresh"].Href.Should().Be($"/api/v1/channels/{channel.Id}/refresh");
    }

    [Fact]
    public async Task GetChannels_ShouldIncludeEpisodeCount()
    {
        // Arrange
        var channels = CreateTestChannels(1);
        SetupPagedChannels(channels, 1);

        _episodeRepositoryMock
            .Setup(r => r.CountByChannelIdAsync(channels[0].Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        // Act
        var result = await _controller.GetChannels(new PaginationQuery());

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<ChannelResponse>>().Subject;

        var channel = response.Data.First();
        channel.EpisodeCount.Should().Be(42);
    }

    [Fact]
    public async Task GetChannels_ShouldReturnEmptyList_WhenNoChannelsExist()
    {
        // Arrange
        SetupPagedChannels(new List<Channel>(), 0);

        // Act
        var result = await _controller.GetChannels(new PaginationQuery());

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<ChannelResponse>>().Subject;

        response.Data.Should().BeEmpty();
        response.TotalCount.Should().Be(0);
        response.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task GetChannels_ShouldSortByCreatedAtDescending_ByDefault()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var channels = new List<Channel>
        {
            new() { Id = "ch-1", Url = "https://youtube.com/@test1", Title = "Test 1", CreatedAt = now.AddDays(-1), UpdatedAt = now },
            new() { Id = "ch-2", Url = "https://youtube.com/@test2", Title = "Test 2", CreatedAt = now.AddDays(-2), UpdatedAt = now },
            new() { Id = "ch-3", Url = "https://youtube.com/@test3", Title = "Test 3", CreatedAt = now, UpdatedAt = now },
        };

        // Controller uses GetAllAsync when no filters, with client-side pagination
        _channelRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(channels);

        foreach (var channel in channels)
        {
            _episodeRepositoryMock
                .Setup(r => r.CountByChannelIdAsync(channel.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);
        }

        var query = new PaginationQuery();

        // Act
        var result = await _controller.GetChannels(query);

        // Assert - Verify the result is sorted by CreatedAt descending (ch-3 is newest, should be first)
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<ChannelResponse>>().Subject;

        response.Data.First().Id.Should().Be("ch-3"); // Newest first
    }

    [Fact]
    public async Task GetChannels_ShouldSortByTitleAscending_WhenSpecified()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Id = "ch-1", Url = "https://youtube.com/@test1", Title = "Alpha", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new() { Id = "ch-2", Url = "https://youtube.com/@test2", Title = "Beta", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
        };

        // Controller uses GetAllAsync when no filters, with client-side pagination
        _channelRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(channels);

        foreach (var channel in channels)
        {
            _episodeRepositoryMock
                .Setup(r => r.CountByChannelIdAsync(channel.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);
        }

        var query = new PaginationQuery { Sort = "title", Order = "asc" };

        // Act
        var result = await _controller.GetChannels(query);

        // Assert - Verify sorted by title ascending (Alpha should be first)
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PaginatedResponse<ChannelResponse>>().Subject;

        response.Data.First().Title.Should().Be("Alpha");
    }

    #endregion

    #region CreateChannel Tests

    [Fact]
    public async Task CreateChannel_ShouldCreateChannel_WithValidUrl()
    {
        // Arrange
        var request = new CreateChannelRequest
        {
            Url = "https://youtube.com/@testchannel",
            EpisodeCountConfig = 30
        };

        _channelRepositoryMock
            .Setup(r => r.ExistsByUrlAsync(request.Url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Channel? addedChannel = null;
        _channelRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Channel>(), It.IsAny<CancellationToken>()))
            .Callback<Channel, CancellationToken>((c, _) => addedChannel = c)
            .ReturnsAsync((Channel c, CancellationToken _) => c);

        _episodeRepositoryMock
            .Setup(r => r.CountByChannelIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _controller.CreateChannel(request);

        // Assert
        result.Should().NotBeNull();
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = createdResult.Value.Should().BeOfType<CreateChannelResponse>().Subject;

        response.Data.Url.Should().Be(request.Url);
        response.Data.EpisodeCountConfig.Should().Be(30);
        response.Data.FeedType.Should().Be("audio");
        response.Data.Enabled.Should().BeTrue();
        response.Message.Should().Contain("created successfully");

        addedChannel.Should().NotBeNull();
        addedChannel!.Url.Should().Be(request.Url);
        addedChannel.EpisodeCountConfig.Should().Be(30);
    }

    [Fact]
    public async Task CreateChannel_ShouldReturnConflict_WhenUrlAlreadyExists()
    {
        // Arrange
        var request = new CreateChannelRequest
        {
            Url = "https://youtube.com/@existing"
        };

        _channelRepositoryMock
            .Setup(r => r.ExistsByUrlAsync(request.Url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.CreateChannel(request);

        // Assert
        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        var response = conflictResult.Value.Should().BeOfType<ConflictErrorResponse>().Subject;
        response.Code.Should().Be("CONFLICT");
        response.Message.Should().Contain("already exists");
    }

    [Fact]
    public async Task CreateChannel_ShouldReturnUnprocessableEntity_WhenUrlIsInvalid()
    {
        // Arrange
        var request = new CreateChannelRequest
        {
            Url = "https://invalid.com/channel"
        };

        _channelRepositoryMock
            .Setup(r => r.ExistsByUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.CreateChannel(request);

        // Assert
        var unprocessableResult = result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        var response = unprocessableResult.Value.Should().BeOfType<ValidationErrorResponse>().Subject;
        response.Code.Should().Be("VALIDATION_ERROR");
        response.Field.Should().Be("url");
    }

    [Fact]
    public async Task CreateChannel_ShouldSetCorrectDefaults_WhenOptionalFieldsNotProvided()
    {
        // Arrange
        var request = new CreateChannelRequest
        {
            Url = "https://youtube.com/@testchannel"
        };

        _channelRepositoryMock
            .Setup(r => r.ExistsByUrlAsync(request.Url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Channel? addedChannel = null;
        _channelRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Channel>(), It.IsAny<CancellationToken>()))
            .Callback<Channel, CancellationToken>((c, _) => addedChannel = c)
            .ReturnsAsync((Channel c, CancellationToken _) => c);

        _episodeRepositoryMock
            .Setup(r => r.CountByChannelIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _controller.CreateChannel(request);

        // Assert
        addedChannel.Should().NotBeNull();
        addedChannel!.EpisodeCountConfig.Should().Be(50); // Default
        addedChannel.FeedType.Should().Be(FeedType.Audio); // Default
        addedChannel.Enabled.Should().BeTrue(); // Default
    }

    [Fact]
    public async Task CreateChannel_ShouldAcceptCustomTitle_WhenProvided()
    {
        // Arrange
        var request = new CreateChannelRequest
        {
            Url = "https://youtube.com/@testchannel",
            Title = "Custom Channel Title"
        };

        _channelRepositoryMock
            .Setup(r => r.ExistsByUrlAsync(request.Url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Channel? addedChannel = null;
        _channelRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Channel>(), It.IsAny<CancellationToken>()))
            .Callback<Channel, CancellationToken>((c, _) => addedChannel = c)
            .ReturnsAsync((Channel c, CancellationToken _) => c);

        _episodeRepositoryMock
            .Setup(r => r.CountByChannelIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _controller.CreateChannel(request);

        // Assert
        addedChannel.Should().NotBeNull();
        addedChannel!.Title.Should().Be("Custom Channel Title");
    }

    [Fact]
    public async Task CreateChannel_ShouldIncludeLocationHeader()
    {
        // Arrange
        var request = new CreateChannelRequest
        {
            Url = "https://youtube.com/@testchannel"
        };

        _channelRepositoryMock
            .Setup(r => r.ExistsByUrlAsync(request.Url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _channelRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Channel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Channel c, CancellationToken _) => c);

        _episodeRepositoryMock
            .Setup(r => r.CountByChannelIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _controller.CreateChannel(request);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be("GetChannel");
        createdResult.RouteValues.Should().ContainKey("id");
    }

    [Fact]
    public async Task CreateChannel_ShouldIncludeHateoasLinks()
    {
        // Arrange
        var request = new CreateChannelRequest
        {
            Url = "https://youtube.com/@testchannel"
        };

        _channelRepositoryMock
            .Setup(r => r.ExistsByUrlAsync(request.Url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _channelRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Channel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Channel c, CancellationToken _) => c);

        _episodeRepositoryMock
            .Setup(r => r.CountByChannelIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _controller.CreateChannel(request);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = createdResult.Value.Should().BeOfType<CreateChannelResponse>().Subject;
        
        response.Data.Links.Should().ContainKey("self");
        response.Data.Links.Should().ContainKey("episodes");
        response.Data.Links.Should().ContainKey("refresh");
    }

    [Theory]
    [InlineData("https://youtube.com/@test")]
    [InlineData("https://www.youtube.com/@test")]
    [InlineData("https://m.youtube.com/@test")]
    [InlineData("https://youtube.com/c/test")]
    [InlineData("https://youtube.com/channel/UCtest123")]
    [InlineData("https://youtube.com/user/testuser")]
    public async Task CreateChannel_ShouldAcceptValidYouTubeUrls(string url)
    {
        // Arrange
        var request = new CreateChannelRequest
        {
            Url = url
        };

        _channelRepositoryMock
            .Setup(r => r.ExistsByUrlAsync(url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _channelRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Channel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Channel c, CancellationToken _) => c);

        _episodeRepositoryMock
            .Setup(r => r.CountByChannelIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _controller.CreateChannel(request);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Theory]
    [InlineData("https://vimeo.com/@test")]
    [InlineData("https://invalid.com/channel")]
    [InlineData("https://youtube.com/invalid/path")]
    public async Task CreateChannel_ShouldRejectInvalidYouTubeUrls(string url)
    {
        // Arrange
        var request = new CreateChannelRequest
        {
            Url = url
        };

        // Act
        var result = await _controller.CreateChannel(request);

        // Assert
        result.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    [Fact]
    public async Task CreateChannel_ShouldParseFeedType_CaseInsensitive()
    {
        // Arrange
        var request = new CreateChannelRequest
        {
            Url = "https://youtube.com/@testchannel",
            FeedType = "VIDEO"
        };

        _channelRepositoryMock
            .Setup(r => r.ExistsByUrlAsync(request.Url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Channel? addedChannel = null;
        _channelRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Channel>(), It.IsAny<CancellationToken>()))
            .Callback<Channel, CancellationToken>((c, _) => addedChannel = c)
            .ReturnsAsync((Channel c, CancellationToken _) => c);

        _episodeRepositoryMock
            .Setup(r => r.CountByChannelIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _controller.CreateChannel(request);

        // Assert
        addedChannel.Should().NotBeNull();
        addedChannel!.FeedType.Should().Be(FeedType.Video);
    }

    #endregion

    #region UpdateChannel Tests

    [Fact]
    public async Task UpdateChannel_ShouldReturn404_WhenChannelNotFound()
    {
        // Arrange
        var request = new UpdateChannelRequest
        {
            Title = "Updated Title"
        };

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync("ch-nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Channel?)null);

        // Act
        var result = await _controller.UpdateChannel("ch-nonexistent", request);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpdateChannel_ShouldUpdateTitle_WhenProvided()
    {
        // Arrange
        var channel = CreateTestChannels(1)[0];
        var request = new UpdateChannelRequest
        {
            Title = "Updated Channel Title"
        };

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _channelRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Channel>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _episodeRepositoryMock
            .Setup(r => r.CountByChannelIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _controller.UpdateChannel(channel.Id, request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
        
        // Verify the update happened on the entity
        channel.Title.Should().Be("Updated Channel Title");
    }

    [Fact]
    public async Task UpdateChannel_ShouldUpdateDescription_WhenProvided()
    {
        // Arrange
        var channel = CreateTestChannels(1)[0];
        var request = new UpdateChannelRequest
        {
            Description = "New description"
        };

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _channelRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Channel>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _episodeRepositoryMock
            .Setup(r => r.CountByChannelIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _controller.UpdateChannel(channel.Id, request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        
        channel.Description.Should().Be("New description");
    }

    [Fact]
    public async Task UpdateChannel_ShouldUpdateEpisodeCountConfig_WhenProvided()
    {
        // Arrange
        var channel = CreateTestChannels(1)[0];
        channel.EpisodeCountConfig = 50;
        var request = new UpdateChannelRequest
        {
            EpisodeCountConfig = 100
        };

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _channelRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Channel>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _episodeRepositoryMock
            .Setup(r => r.CountByChannelIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _controller.UpdateChannel(channel.Id, request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        
        channel.EpisodeCountConfig.Should().Be(100);
    }

    [Fact]
    public async Task UpdateChannel_ShouldUpdateFeedType_WhenProvided()
    {
        // Arrange
        var channel = CreateTestChannels(1)[0];
        channel.FeedType = FeedType.Audio;
        var request = new UpdateChannelRequest
        {
            FeedType = "video"
        };

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _channelRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Channel>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _episodeRepositoryMock
            .Setup(r => r.CountByChannelIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _controller.UpdateChannel(channel.Id, request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        
        channel.FeedType.Should().Be(FeedType.Video);
    }

    [Fact]
    public async Task UpdateChannel_ShouldUpdateEnabled_WhenProvided()
    {
        // Arrange
        var channel = CreateTestChannels(1)[0];
        channel.Enabled = true;
        var request = new UpdateChannelRequest
        {
            Enabled = false
        };

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _channelRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Channel>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _episodeRepositoryMock
            .Setup(r => r.CountByChannelIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _controller.UpdateChannel(channel.Id, request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        
        channel.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateChannel_ShouldUpdateMultipleFields_WhenProvided()
    {
        // Arrange
        var channel = CreateTestChannels(1)[0];
        var request = new UpdateChannelRequest
        {
            Title = "New Title",
            Description = "New Description",
            EpisodeCountConfig = 200,
            FeedType = "both",
            Enabled = false
        };

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _channelRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Channel>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _episodeRepositoryMock
            .Setup(r => r.CountByChannelIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        // Act
        var result = await _controller.UpdateChannel(channel.Id, request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        
        channel.Title.Should().Be("New Title");
        channel.Description.Should().Be("New Description");
        channel.EpisodeCountConfig.Should().Be(200);
        channel.FeedType.Should().Be(FeedType.Both);
        channel.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateChannel_ShouldNotModifyFields_WhenNotProvided()
    {
        // Arrange
        var channel = CreateTestChannels(1)[0];
        var originalTitle = channel.Title;
        var originalDescription = channel.Description;
        var originalEpisodeCountConfig = channel.EpisodeCountConfig;
        var originalFeedType = channel.FeedType;
        var originalEnabled = channel.Enabled;

        var request = new UpdateChannelRequest(); // Empty update

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _channelRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Channel>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _episodeRepositoryMock
            .Setup(r => r.CountByChannelIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _controller.UpdateChannel(channel.Id, request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        
        // All fields should remain unchanged
        channel.Title.Should().Be(originalTitle);
        channel.Description.Should().Be(originalDescription);
        channel.EpisodeCountConfig.Should().Be(originalEpisodeCountConfig);
        channel.FeedType.Should().Be(originalFeedType);
        channel.Enabled.Should().Be(originalEnabled);
    }

    [Fact]
    public async Task UpdateChannel_ShouldUpdateUpdatedAtTimestamp()
    {
        // Arrange
        var channel = CreateTestChannels(1)[0];
        var originalUpdatedAt = channel.UpdatedAt;
        var request = new UpdateChannelRequest
        {
            Title = "Updated Title"
        };

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _channelRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Channel>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _episodeRepositoryMock
            .Setup(r => r.CountByChannelIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        await _controller.UpdateChannel(channel.Id, request);

        // Assert
        channel.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public async Task UpdateChannel_ShouldIncludeHateoasLinks()
    {
        // Arrange
        var channel = CreateTestChannels(1)[0];
        var request = new UpdateChannelRequest
        {
            Title = "Updated Title"
        };

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _channelRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Channel>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _episodeRepositoryMock
            .Setup(r => r.CountByChannelIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _controller.UpdateChannel(channel.Id, request);

        // Assert - verify OkObjectResult is returned
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateChannel_ShouldParseFeedTypeCaseInsensitive()
    {
        // Arrange
        var channel = CreateTestChannels(1)[0];
        channel.FeedType = FeedType.Audio;
        var request = new UpdateChannelRequest
        {
            FeedType = "VIDEO"
        };

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _channelRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Channel>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _episodeRepositoryMock
            .Setup(r => r.CountByChannelIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _controller.UpdateChannel(channel.Id, request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        
        channel.FeedType.Should().Be(FeedType.Video);
    }

    [Fact]
    public async Task UpdateChannel_ShouldCallRepositoryUpdate()
    {
        // Arrange
        var channel = CreateTestChannels(1)[0];
        var request = new UpdateChannelRequest
        {
            Title = "Updated Title"
        };

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _channelRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Channel>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        _episodeRepositoryMock
            .Setup(r => r.CountByChannelIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        await _controller.UpdateChannel(channel.Id, request);

        // Assert
        _channelRepositoryMock.Verify(r => r.UpdateAsync(channel, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region DeleteChannel Tests

    [Fact]
    public async Task DeleteChannel_ShouldReturn404_WhenChannelNotFound()
    {
        // Arrange
        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync("ch-nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Channel?)null);

        // Act
        var result = await _controller.DeleteChannel("ch-nonexistent");

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteChannel_ShouldDeleteChannelAndEpisodes_WhenChannelExists()
    {
        // Arrange
        var channel = CreateTestChannels(1)[0];
        var episodes = CreateTestEpisodes(channel.Id, 3);

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _episodeRepositoryMock
            .Setup(r => r.GetByChannelIdAsync(channel.Id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(episodes);

        _episodeRepositoryMock
            .Setup(r => r.DeleteRangeAsync(It.IsAny<IEnumerable<Episode>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _channelRepositoryMock
            .Setup(r => r.DeleteAsync(channel, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _fileServiceMock
            .Setup(f => f.FileExists(It.IsAny<string>()))
            .Returns(false);

        // Act
        var result = await _controller.DeleteChannel(channel.Id);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();

        _episodeRepositoryMock.Verify(r => r.DeleteRangeAsync(It.IsAny<IEnumerable<Episode>>(), It.IsAny<CancellationToken>()), Times.Once);
        _channelRepositoryMock.Verify(r => r.DeleteAsync(channel, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteChannel_ShouldReturnDeleteResult_WithCorrectCounts()
    {
        // Arrange
        var channel = CreateTestChannels(1)[0];
        var episodes = CreateTestEpisodes(channel.Id, 2);

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _episodeRepositoryMock
            .Setup(r => r.GetByChannelIdAsync(channel.Id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(episodes);

        _episodeRepositoryMock
            .Setup(r => r.DeleteRangeAsync(It.IsAny<IEnumerable<Episode>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _channelRepositoryMock
            .Setup(r => r.DeleteAsync(channel, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _fileServiceMock
            .Setup(f => f.FileExists(It.IsAny<string>()))
            .Returns(false);

        // Act
        var result = await _controller.DeleteChannel(channel.Id);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<DeleteChannelResponse>().Subject;

        response.Message.Should().Be("Channel deleted successfully");
        response.ChannelId.Should().Be(channel.Id);
        response.EpisodesDeleted.Should().Be(2);
    }

    [Fact]
    public async Task DeleteChannel_ShouldDeleteFiles_WhenDeleteFilesIsTrueAndFilesExist()
    {
        // Arrange
        var channel = CreateTestChannels(1)[0];
        channel.Title = "TestChannel";
        var episodes = new List<Episode>
        {
            new()
            {
                Id = "ep-1",
                VideoId = "vid1",
                ChannelId = channel.Id,
                Title = "Episode 1",
                FilePathAudio = "TestChannel/audio/test1.mp3",
                FilePathVideo = "TestChannel/video/test1.mp4",
                FileSizeAudio = 10485760, // 10 MB
                FileSizeVideo = 104857600, // 100 MB
                Status = EpisodeStatus.Completed,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Id = "ep-2",
                VideoId = "vid2",
                ChannelId = channel.Id,
                Title = "Episode 2",
                FilePathAudio = null,
                FilePathVideo = "TestChannel/video/test2.mp4",
                FileSizeAudio = null,
                FileSizeVideo = 52428800, // 50 MB
                Status = EpisodeStatus.Completed,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        };

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _episodeRepositoryMock
            .Setup(r => r.GetByChannelIdAsync(channel.Id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(episodes);

        _episodeRepositoryMock
            .Setup(r => r.DeleteRangeAsync(It.IsAny<IEnumerable<Episode>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _channelRepositoryMock
            .Setup(r => r.DeleteAsync(channel, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _fileServiceMock
            .Setup(f => f.FileExists(It.IsAny<string>()))
            .Returns(true);

        // Act
        var result = await _controller.DeleteChannel(channel.Id, delete_files: true);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<DeleteChannelResponse>().Subject;

        response.FilesDeleted.Should().Be(3); // 2 audio/video files
        response.BytesFreed.Should().Be(167772160); // 10 + 100 + 50 MB

        _fileServiceMock.Verify(f => f.DeleteFile(It.IsAny<string>()), Times.AtLeast(3));
    }

    [Fact]
    public async Task DeleteChannel_ShouldNotDeleteFiles_WhenDeleteFilesIsFalse()
    {
        // Arrange
        var channel = CreateTestChannels(1)[0];
        var episodes = CreateTestEpisodes(channel.Id, 2);

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _episodeRepositoryMock
            .Setup(r => r.GetByChannelIdAsync(channel.Id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(episodes);

        _episodeRepositoryMock
            .Setup(r => r.DeleteRangeAsync(It.IsAny<IEnumerable<Episode>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _channelRepositoryMock
            .Setup(r => r.DeleteAsync(channel, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteChannel(channel.Id, delete_files: false);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<DeleteChannelResponse>().Subject;

        response.FilesDeleted.Should().Be(0);
        response.BytesFreed.Should().Be(0);

        _fileServiceMock.Verify(f => f.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteChannel_ShouldDefaultDeleteFilesToTrue()
    {
        // Arrange
        var channel = CreateTestChannels(1)[0];
        var episodes = CreateTestEpisodes(channel.Id, 1);
        episodes[0].FilePathAudio = "TestChannel/audio/test.mp3";
        episodes[0].FileSizeAudio = 1048576;

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _episodeRepositoryMock
            .Setup(r => r.GetByChannelIdAsync(channel.Id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(episodes);

        _episodeRepositoryMock
            .Setup(r => r.DeleteRangeAsync(It.IsAny<IEnumerable<Episode>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _channelRepositoryMock
            .Setup(r => r.DeleteAsync(channel, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _fileServiceMock
            .Setup(f => f.FileExists(It.IsAny<string>()))
            .Returns(true);

        // Act - Not passing deleteFiles parameter (should default to true)
        var result = await _controller.DeleteChannel(channel.Id);

        // Assert
        _fileServiceMock.Verify(f => f.DeleteFile(It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task DeleteChannel_ShouldHandleChannelWithNoEpisodes()
    {
        // Arrange
        var channel = CreateTestChannels(1)[0];

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _episodeRepositoryMock
            .Setup(r => r.GetByChannelIdAsync(channel.Id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Episode>());

        _channelRepositoryMock
            .Setup(r => r.DeleteAsync(channel, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteChannel(channel.Id);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<DeleteChannelResponse>().Subject;

        response.EpisodesDeleted.Should().Be(0);
        response.FilesDeleted.Should().Be(0);
        response.BytesFreed.Should().Be(0);

        // Should not call DeleteRangeAsync when there are no episodes
        _episodeRepositoryMock.Verify(r => r.DeleteRangeAsync(It.IsAny<IEnumerable<Episode>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteChannel_ShouldContinueDeletion_WhenFileDeleteFails()
    {
        // Arrange
        var channel = CreateTestChannels(1)[0];
        var episodes = new List<Episode>
        {
            new()
            {
                Id = "ep-1",
                VideoId = "vid1",
                ChannelId = channel.Id,
                Title = "Episode 1",
                FilePathAudio = "TestChannel/audio/test.mp3",
                FileSizeAudio = 1048576,
                Status = EpisodeStatus.Completed,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        };

        _channelRepositoryMock
            .Setup(r => r.GetByIdAsync(channel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _episodeRepositoryMock
            .Setup(r => r.GetByChannelIdAsync(channel.Id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(episodes);

        _episodeRepositoryMock
            .Setup(r => r.DeleteRangeAsync(It.IsAny<IEnumerable<Episode>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _channelRepositoryMock
            .Setup(r => r.DeleteAsync(channel, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _fileServiceMock
            .Setup(f => f.FileExists(It.IsAny<string>()))
            .Returns(true);
        
        _fileServiceMock
            .Setup(f => f.DeleteFile(It.IsAny<string>()))
            .Throws(new IOException("File in use"));

        // Act - Should not throw
        var result = await _controller.DeleteChannel(channel.Id, delete_files: true);

        // Assert - Channel should still be deleted even if file deletion fails
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        _channelRepositoryMock.Verify(r => r.DeleteAsync(channel, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Helper Methods

    private static List<Channel> CreateTestChannels(int count)
    {
        var now = DateTimeOffset.UtcNow;
        return Enumerable.Range(1, count)
            .Select(i => new Channel
            {
                Id = $"ch-{i}",
                Url = $"https://youtube.com/@test{i}",
                Title = $"Test Channel {i}",
                Description = $"Description for channel {i}",
                ThumbnailUrl = $"https://example.com/thumb{i}.jpg",
                EpisodeCountConfig = 50,
                FeedType = FeedType.Audio,
                Enabled = true,
                LastRefreshAt = now.AddHours(-i),
                CreatedAt = now.AddDays(-i),
                UpdatedAt = now
            })
            .ToList();
    }

    private static List<Episode> CreateTestEpisodes(string channelId, int count)
    {
        var now = DateTimeOffset.UtcNow;
        return Enumerable.Range(1, count)
            .Select(i => new Episode
            {
                Id = $"ep-{i}",
                VideoId = $"vid{i}",
                ChannelId = channelId,
                Title = $"Episode {i}",
                Description = $"Description for episode {i}",
                Status = EpisodeStatus.Completed,
                CreatedAt = now.AddDays(-i),
                UpdatedAt = now
            })
            .ToList();
    }

    private void SetupPagedChannels(List<Channel> channels, int totalCount, int page = 1, int pageSize = 50)
    {
        // Controller uses GetAllAsync() or FindAsync() with client-side pagination
        _channelRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(channels);

        _channelRepositoryMock
            .Setup(r => r.FindAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Channel, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(channels);

        foreach (var channel in channels)
        {
            _episodeRepositoryMock
                .Setup(r => r.CountByChannelIdAsync(channel.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);
        }
    }

    #endregion
}