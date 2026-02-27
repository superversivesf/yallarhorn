namespace Yallarhorn.Tests.Unit.Configuration.Validation;

using FluentAssertions;
using FluentValidation;
using Xunit;
using Yallarhorn.Configuration;
using Yallarhorn.Configuration.Validation;

public class YallarhornOptionsValidatorTests
{
    private readonly YallarhornOptionsValidator _validator = new();

    [Fact]
    public void Validate_ValidOptions_ShouldPass()
    {
        var options = CreateValidOptions();
        options.Channels.Add(new ChannelDefinitionOptions 
        { 
            Name = "Test", 
            Url = "https://www.youtube.com/@test" 
        });

        var result = _validator.Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NoChannels_ShouldFail()
    {
        var options = CreateValidOptions();

        var result = _validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("At least one channel"));
    }

    [Fact]
    public void Validate_PollIntervalTooLow_ShouldFail()
    {
        var options = CreateValidOptions();
        options.PollInterval = 100;

        var result = _validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PollInterval");
    }

    [Fact]
    public void Validate_InvalidMaxConcurrentDownloads_ShouldFail()
    {
        var options = CreateValidOptions();
        options.MaxConcurrentDownloads = 15;

        var result = _validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MaxConcurrentDownloads");
    }

    private static YallarhornOptions CreateValidOptions()
    {
        return new YallarhornOptions
        {
            Version = "1.0",
            PollInterval = 3600,
            MaxConcurrentDownloads = 3,
            DownloadDir = "./downloads",
            TempDir = "./temp"
        };
    }
}

public class ServerOptionsValidatorTests
{
    private readonly ServerOptionsValidator _validator = new();

    [Fact]
    public void Validate_ValidOptions_ShouldPass()
    {
        var options = new ServerOptions();

        var result = _validator.Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidPort_ShouldFail()
    {
        var options = new ServerOptions { Port = 70000 };

        var result = _validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Port");
    }

    [Fact]
    public void Validate_InvalidBaseUrl_ShouldFail()
    {
        var options = new ServerOptions { BaseUrl = "not-a-url" };

        var result = _validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "BaseUrl");
    }

    [Fact]
    public void Validate_FeedPathWithoutSlash_ShouldFail()
    {
        var options = new ServerOptions { FeedPath = "feeds" };

        var result = _validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FeedPath");
    }
}

public class TranscodeOptionsValidatorTests
{
    private readonly TranscodeOptionsValidator _validator = new();

    [Fact]
    public void Validate_ValidOptions_ShouldPass()
    {
        var options = new TranscodeOptions();

        var result = _validator.Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidAudioFormat_ShouldFail()
    {
        var options = new TranscodeOptions { AudioFormat = "wav" };

        var result = _validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AudioFormat");
    }

    [Fact]
    public void Validate_InvalidAudioBitrate_ShouldFail()
    {
        var options = new TranscodeOptions { AudioBitrate = "192" };

        var result = _validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AudioBitrate");
    }

    [Fact]
    public void Validate_InvalidVideoQuality_ShouldFail()
    {
        var options = new TranscodeOptions { VideoQuality = 10 };

        var result = _validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "VideoQuality");
    }
}

public class ChannelDefinitionOptionsValidatorTests
{
    private readonly ChannelDefinitionOptionsValidator _validator = new();

    [Fact]
    public void Validate_ValidYouTubeChannelUrl_ShouldPass()
    {
        var options = new ChannelDefinitionOptions
        {
            Name = "Test",
            Url = "https://www.youtube.com/@testchannel",
            EpisodeCount = 50,
            FeedType = "audio"
        };

        var result = _validator.Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://www.youtube.com/@channel")]
    [InlineData("https://www.youtube.com/c/channel")]
    [InlineData("https://www.youtube.com/channel/UC123456")]
    [InlineData("https://www.youtube.com/user/username")]
    public void Validate_ValidUrlFormats_ShouldPass(string url)
    {
        var options = new ChannelDefinitionOptions
        {
            Name = "Test",
            Url = url
        };

        var result = _validator.Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("http://www.youtube.com/@channel")]
    [InlineData("https://youtube.com/@channel")]
    [InlineData("https://www.youtube.com/watch?v=abc")]
    [InlineData("https://www.youtube.com/playlist?list=xyz")]
    [InlineData("https://other-site.com/channel")]
    public void Validate_InvalidUrlFormats_ShouldFail(string url)
    {
        var options = new ChannelDefinitionOptions
        {
            Name = "Test",
            Url = url
        };

        var result = _validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Url");
    }

    [Fact]
    public void Validate_EmptyName_ShouldFail()
    {
        var options = new ChannelDefinitionOptions
        {
            Name = "",
            Url = "https://www.youtube.com/@test"
        };

        var result = _validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_InvalidEpisodeCount_ShouldFail()
    {
        var options = new ChannelDefinitionOptions
        {
            Name = "Test",
            Url = "https://www.youtube.com/@test",
            EpisodeCount = 1500
        };

        var result = _validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "EpisodeCount");
    }

    [Fact]
    public void Validate_InvalidFeedType_ShouldFail()
    {
        var options = new ChannelDefinitionOptions
        {
            Name = "Test",
            Url = "https://www.youtube.com/@test",
            FeedType = "invalid"
        };

        var result = _validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FeedType");
    }
}