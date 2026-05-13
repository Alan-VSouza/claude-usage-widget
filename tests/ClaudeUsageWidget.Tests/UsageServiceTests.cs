using ClaudeUsageWidget;

namespace ClaudeUsageWidget.Tests;

public class UsageServiceTests
{
    [Fact]
    public void Parse_ValidOutput_ReturnsPercent()
    {
        var data = UsageService.Parse("Usage: 62% used\nResets in: 2h 18m");
        Assert.True(data.ParseSuccess);
        Assert.Equal(62, data.Percent);
    }

    [Fact]
    public void Parse_NoPercent_ReturnsFailed()
    {
        var data = UsageService.Parse("No usage data available");
        Assert.False(data.ParseSuccess);
        Assert.Equal(0, data.Percent);
    }

    [Fact]
    public void Parse_HoursAndMinutes_ParsedCorrectly()
    {
        var data = UsageService.Parse("62% — Resets in 2h 18m");
        Assert.True(data.ResetsIn.HasValue);
        Assert.Equal(2, (int)data.ResetsIn!.Value.TotalHours);
        Assert.Equal(18, data.ResetsIn!.Value.Minutes);
    }

    [Fact]
    public void Parse_MinutesOnly_ParsedCorrectly()
    {
        var data = UsageService.Parse("45% — Resets in 45m");
        Assert.True(data.ResetsIn.HasValue);
        Assert.Equal(45, (int)data.ResetsIn!.Value.TotalMinutes);
    }

    [Fact]
    public void Parse_HoursOnly_ParsedCorrectly()
    {
        var data = UsageService.Parse("30% — Resets in 3h");
        Assert.True(data.ResetsIn.HasValue);
        Assert.Equal(3, (int)data.ResetsIn!.Value.TotalHours);
    }

    [Fact]
    public void Parse_PercentOver100_ClampedTo100()
    {
        var data = UsageService.Parse("150% used");
        Assert.Equal(100, data.Percent);
    }

    [Fact]
    public void Parse_StripAnsiEscapeCodes_StillFindsPercent()
    {
        var data = UsageService.Parse("\x1b[32m62%\x1b[0m reset in 1h 30m");
        Assert.True(data.ParseSuccess);
        Assert.Equal(62, data.Percent);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsFailed()
    {
        var data = UsageService.Parse("");
        Assert.False(data.ParseSuccess);
    }
}
