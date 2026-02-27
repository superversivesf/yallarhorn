namespace Yallarhorn.Tests.Unit.Configuration;

using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Xunit;
using Yallarhorn.Configuration;

public class OptionsTests
{
    #region YallarhornOptions Tests

    [Fact]
    public void YallarhornOptions_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new YallarhornOptions();

        // Assert
        options.Version.Should().Be("1.0");
        options.PollInterval.Should().Be(3600);
        options.MaxConcurrentDownloads.Should().Be(3);
        options.DownloadDir.Should().Be("./downloads");
        options.TempDir.Should().Be("./temp");
        options.Channels.Should().BeEmpty();
    }

    [Fact]
    public void YallarhornOptions_Validate_WithNoChannels_ShouldReturnError()
    {
        // Arrange
        var options = new YallarhornOptions();

        // Act
        var results = options.Validate().ToList();

        // Assert
        results.Should().Contain(r => r.ErrorMessage!.Contains("At least one channel must be defined"));
    }

    [Fact]
    public void YallarhornOptions_Validate_WithValidChannel_ShouldPass()
    {
        // Arrange
        var options = new YallarhornOptions
        {
            Channels = new List<ChannelDefinitionOptions>
            {
                new() { Name = "Test Channel", Url = "https://www.youtube.com/@testchannel" }
            }
        };

        // Act
        var results = options.Validate().ToList();

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void YallarhornOptions_Validate_PollIntervalBelowMinimum_ShouldReturnError()
    {
        // Arrange
        var options = new YallarhornOptions
        {
            PollInterval = 100,
            Channels = new List<ChannelDefinitionOptions>
            {
                new() { Name = "Test", Url = "https://www.youtube.com/@test" }
            }
        };

        // Act
        var results = options.Validate().ToList();

        // Assert
        results.Should().Contain(r => r.MemberNames.Contains(nameof(YallarhornOptions.PollInterval)));
    }

    [Fact]
    public void YallarhornOptions_GetEnabledChannels_ShouldReturnOnlyEnabledChannels()
    {
        // Arrange
        var options = new YallarhornOptions
        {
            Channels = new List<ChannelDefinitionOptions>
            {
                new() { Name = "Active 1", Url = "https://www.youtube.com/@active1", Enabled = true },
                new() { Name = "Inactive", Url = "https://www.youtube.com/@inactive", Enabled = false },
                new() { Name = "Active 2", Url = "https://www.youtube.com/@active2", Enabled = true }
            }
        };

        // Act
        var enabled = options.GetEnabledChannels();

        // Assert
        enabled.Should().HaveCount(2);
        enabled.All(c => c.Enabled).Should().BeTrue();
    }

    #endregion

    #region TranscodeOptions Tests

    [Fact]
    public void TranscodeOptions_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new TranscodeOptions();

        // Assert
        options.AudioFormat.Should().Be("mp3");
        options.AudioBitrate.Should().Be("192k");
        options.AudioSampleRate.Should().Be(44100);
        options.VideoFormat.Should().Be("mp4");
        options.VideoCodec.Should().Be("h264");
        options.VideoQuality.Should().Be(23);
        options.Threads.Should().Be(4);
        options.KeepOriginal.Should().BeFalse();
    }

    [Theory]
    [InlineData("mp3", true)]
    [InlineData("aac", true)]
    [InlineData("ogg", true)]
    [InlineData("m4a", true)]
    [InlineData("wav", false)]
    [InlineData("flac", false)]
    public void TranscodeOptions_Validate_AudioFormatValues(string format, bool isValid)
    {
        // Arrange
        var options = new TranscodeOptions { AudioFormat = format };

        // Act
        var results = options.Validate().ToList();

        // Assert
        if (isValid)
        {
            results.Should().NotContain(r => r.MemberNames.Contains(nameof(TranscodeOptions.AudioFormat)));
        }
        else
        {
            results.Should().Contain(r => r.MemberNames.Contains(nameof(TranscodeOptions.AudioFormat)));
        }
    }

    [Theory]
    [InlineData("192k", true)]
    [InlineData("128K", true)]
    [InlineData("1m", true)]
    [InlineData("256M", true)]
    [InlineData("192", false)]
    [InlineData("kbps", false)]
    [InlineData("", false)]
    public void TranscodeOptions_Validate_AudioBitrateFormat(string bitrate, bool isValid)
    {
        // Arrange
        var options = new TranscodeOptions { AudioBitrate = bitrate };

        // Act
        var results = options.Validate().ToList();

        // Assert
        if (isValid)
        {
            results.Should().NotContain(r => r.MemberNames.Contains(nameof(TranscodeOptions.AudioBitrate)));
        }
        else
        {
            results.Should().Contain(r => r.MemberNames.Contains(nameof(TranscodeOptions.AudioBitrate)));
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(18, true)]
    [InlineData(23, true)]
    [InlineData(28, true)]
    [InlineData(51, true)]
    [InlineData(52, false)]
    public void TranscodeOptions_Validate_VideoQualityRange(int quality, bool isValid)
    {
        // Arrange
        var options = new TranscodeOptions { VideoQuality = quality };

        // Act
        var results = options.Validate().ToList();

        // Assert
        if (isValid)
        {
            results.Should().NotContain(r => r.MemberNames.Contains(nameof(TranscodeOptions.VideoQuality)));
        }
        else
        {
            results.Should().Contain(r => r.MemberNames.Contains(nameof(TranscodeOptions.VideoQuality)));
        }
    }

    [Fact]
    public void TranscodeOptions_ValidateAndThrow_WithInvalidOptions_ShouldThrow()
    {
        // Arrange
        var options = new TranscodeOptions { Threads = 0 };

        // Act & Assert
        options.Invoking(o => o.ValidateAndThrow())
            .Should().Throw<ValidationException>();
    }

    #endregion

    #region DatabaseOptions Tests

    [Fact]
    public void DatabaseOptions_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new DatabaseOptions();

        // Assert
        options.Path.Should().Be("./yallarhorn.db");
        options.PoolSize.Should().Be(5);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(100, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(101, false)]
    public void DatabaseOptions_Validate_PoolSizeRange(int poolSize, bool isValid)
    {
        // Arrange
        var options = new DatabaseOptions { PoolSize = poolSize };

        // Act
        var results = options.Validate().ToList();

        // Assert
        if (isValid)
        {
            results.Should().NotContain(r => r.MemberNames.Contains(nameof(DatabaseOptions.PoolSize)));
        }
        else
        {
            results.Should().Contain(r => r.MemberNames.Contains(nameof(DatabaseOptions.PoolSize)));
        }
    }

    [Fact]
    public void DatabaseOptions_Validate_WithEmptyPath_ShouldReturnError()
    {
        // Arrange
        var options = new DatabaseOptions { Path = "" };

        // Act
        var results = options.Validate().ToList();

        // Assert
        results.Should().Contain(r => r.MemberNames.Contains(nameof(DatabaseOptions.Path)));
    }

    #endregion

    #region AuthOptions Tests

    [Fact]
    public void AuthOptions_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new AuthOptions();

        // Assert
        options.FeedCredentials.Should().NotBeNull();
        options.AdminAuth.Should().NotBeNull();
        options.FeedCredentials.Enabled.Should().BeFalse();
        options.AdminAuth.Enabled.Should().BeFalse();
    }

    [Fact]
    public void AuthOptions_WithEnabledFeedCredentialsButNoCredentials_ShouldReturnError()
    {
        // Arrange
        var options = new AuthOptions
        {
            FeedCredentials = new FeedCredentials { Enabled = true }
        };

        // Act
        var results = options.Validate().ToList();

        // Assert
        results.Should().Contain(r => r.ErrorMessage!.Contains("Username is required when feed_credentials is enabled"));
        results.Should().Contain(r => r.ErrorMessage!.Contains("Password is required when feed_credentials is enabled"));
    }

    [Fact]
    public void AuthOptions_WithEnabledAdminAuthButNoCredentials_ShouldReturnError()
    {
        // Arrange
        var options = new AuthOptions
        {
            AdminAuth = new AdminAuth { Enabled = true }
        };

        // Act
        var results = options.Validate().ToList();

        // Assert
        results.Should().Contain(r => r.ErrorMessage!.Contains("Username is required when admin_auth is enabled"));
        results.Should().Contain(r => r.ErrorMessage!.Contains("Password is required when admin_auth is enabled"));
    }

    [Fact]
    public void AuthOptions_WithValidCredentials_ShouldPass()
    {
        // Arrange
        var options = new AuthOptions
        {
            FeedCredentials = new FeedCredentials { Enabled = true, Username = "feeduser", Password = "feedpass" },
            AdminAuth = new AdminAuth { Enabled = true, Username = "admin", Password = "adminpass" }
        };

        // Act
        var results = options.Validate().ToList();

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void FeedCredentials_DefaultRealm_ShouldBeCorrect()
    {
        // Arrange & Act
        var credentials = new FeedCredentials();

        // Assert
        credentials.Realm.Should().Be("Yallarhorn Feeds");
    }

    #endregion

    #region ChannelDefinitionOptions Tests

    [Fact]
    public void ChannelDefinitionOptions_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new ChannelDefinitionOptions();

        // Assert
        options.Name.Should().BeEmpty();
        options.Url.Should().BeEmpty();
        options.EpisodeCount.Should().Be(50);
        options.Enabled.Should().BeTrue();
        options.FeedType.Should().Be("audio");
        options.CustomSettings.Should().BeNull();
        options.Tags.Should().BeEmpty();
    }

    [Theory]
    [InlineData("https://www.youtube.com/@techchannel", true)]
    [InlineData("https://www.youtube.com/c/channelname", true)]
    [InlineData("https://www.youtube.com/channel/UC1234567890", true)]
    [InlineData("https://www.youtube.com/user/username", true)]
    [InlineData("http://www.youtube.com/@channel", false)] // Not HTTPS
    [InlineData("https://www.youtube.com/watch?v=abc123", false)] // Video URL
    [InlineData("https://www.youtube.com/playlist?list=xyz", false)] // Playlist URL
    [InlineData("https://www.youtube.com/v/abc123", false)] // Video URL
    [InlineData("", false)]
    [InlineData("https://other-site.com/channel", false)]
    public void ChannelDefinitionOptions_Validate_YouTubeUrlValidation(string url, bool isValid)
    {
        // Arrange
        var options = new ChannelDefinitionOptions 
        { 
            Name = "Test Channel", 
            Url = url 
        };

        // Act
        var results = options.Validate().ToList();

        // Assert
        var urlError = results.FirstOrDefault(r => r.MemberNames.Contains(nameof(ChannelDefinitionOptions.Url)));
        if (isValid)
        {
            urlError.Should().BeNull();
        }
        else
        {
            urlError.Should().NotBeNull();
        }
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(50, true)]
    [InlineData(1000, true)]
    [InlineData(0, false)]
    [InlineData(1001, false)]
    public void ChannelDefinitionOptions_Validate_EpisodeCountRange(int count, bool isValid)
    {
        // Arrange
        var options = new ChannelDefinitionOptions
        {
            Name = "Test",
            Url = "https://www.youtube.com/@test",
            EpisodeCount = count
        };

        // Act
        var results = options.Validate().ToList();

        // Assert
        var countError = results.FirstOrDefault(r => r.MemberNames.Contains(nameof(ChannelDefinitionOptions.EpisodeCount)));
        if (isValid)
        {
            countError.Should().BeNull();
        }
        else
        {
            countError.Should().NotBeNull();
        }
    }

    [Theory]
    [InlineData("audio", true)]
    [InlineData("video", true)]
    [InlineData("both", true)]
    [InlineData("invalid", false)]
    public void ChannelDefinitionOptions_Validate_FeedType(string feedType, bool isValid)
    {
        // Arrange
        var options = new ChannelDefinitionOptions
        {
            Name = "Test",
            Url = "https://www.youtube.com/@test",
            FeedType = feedType
        };

        // Act
        var results = options.Validate().ToList();

        // Assert
        var feedTypeError = results.FirstOrDefault(r => r.MemberNames.Contains(nameof(ChannelDefinitionOptions.FeedType)));
        if (isValid)
        {
            feedTypeError.Should().BeNull();
        }
        else
        {
            feedTypeError.Should().NotBeNull();
        }
    }

    [Fact]
    public void ChannelDefinitionOptions_Validate_WithCustomSettings_ShouldValidateNested()
    {
        // Arrange
        var options = new ChannelDefinitionOptions
        {
            Name = "Test",
            Url = "https://www.youtube.com/@test",
            CustomSettings = new TranscodeOptions { AudioBitrate = "invalid" }
        };

        // Act
        var results = options.Validate().ToList();

        // Assert
        results.Should().NotBeEmpty();
    }

    [Fact]
    public void ChannelDefinitionOptions_Validate_AndThrow_WithInvalidOptions_ShouldThrow()
    {
        // Arrange
        var options = new ChannelDefinitionOptions { Name = "", Url = "" };

        // Act & Assert
        options.Invoking(o => o.ValidateAndThrow())
            .Should().Throw<ValidationException>();
    }

    #endregion

    #region ServerOptions Tests

    [Fact]
    public void ServerOptions_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new ServerOptions();

        // Assert
        options.Host.Should().Be("0.0.0.0");
        options.Port.Should().Be(8080);
        options.BaseUrl.Should().Be("http://localhost:8080");
        options.FeedPath.Should().Be("/feeds");
        options.UseHttps.Should().BeFalse();
        options.MaxConcurrentConnections.Should().Be(100);
        options.RequestTimeoutSeconds.Should().Be(30);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(8080, true)]
    [InlineData(65535, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(65536, false)]
    public void ServerOptions_Validate_PortRange(int port, bool isValid)
    {
        // Arrange
        var options = new ServerOptions { Port = port };

        // Act
        var results = options.Validate().ToList();

        // Assert
        if (isValid)
        {
            results.Should().NotContain(r => r.MemberNames.Contains(nameof(ServerOptions.Port)));
        }
        else
        {
            results.Should().Contain(r => r.MemberNames.Contains(nameof(ServerOptions.Port)));
        }
    }

    [Fact]
    public void ServerOptions_GetFullFeedUrl_ShouldCombineBaseUrlAndFeedPath()
    {
        // Arrange
        var options = new ServerOptions
        {
            BaseUrl = "http://example.com",
            FeedPath = "/feeds"
        };

        // Act
        var result = options.GetFullFeedUrl();

        // Assert
        result.Should().Be("http://example.com/feeds");
    }

    [Fact]
    public void ServerOptions_GetFullFeedUrl_WithTrailingSlashOnBaseUrl_ShouldWork()
    {
        // Arrange
        var options = new ServerOptions
        {
            BaseUrl = "http://example.com/",
            FeedPath = "/feeds"
        };

        // Act
        var result = options.GetFullFeedUrl();

        // Assert
        result.Should().Be("http://example.com/feeds");
    }

    [Fact]
    public void ServerOptions_GetFullFeedUrl_WithNoLeadingSlashOnFeedPath_ShouldAddSlash()
    {
        // Arrange
        var options = new ServerOptions
        {
            BaseUrl = "http://example.com",
            FeedPath = "feeds"
        };

        // Act
        var result = options.GetFullFeedUrl();

        // Assert
        result.Should().Be("http://example.com/feeds");
    }

    #endregion
}