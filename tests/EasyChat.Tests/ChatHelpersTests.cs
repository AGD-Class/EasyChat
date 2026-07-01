using EasyChat.Models;
using EasyChat.Utilities;
using Xunit;

namespace EasyChat.Tests;

public class ChatHelpersTests
{
    [Theory]
    [InlineData(1023, "1023B")]
    [InlineData(1024, "1.00KB")]
    [InlineData(1048576, "1.00MB")]
    [InlineData(1073741824, "1.00GB")]
    public void FileSizeToString_FormatsExpectedUnits(long size, string expected)
    {
        Assert.Equal(expected, MqttContent.FileSizeToString(size));
    }

    [Theory]
    [InlineData("photo.jpg", true)]
    [InlineData("photo.PNG", true)]
    [InlineData("archive.zip", false)]
    [InlineData("", false)]
    public void IsImageFile_DetectsSupportedImages(string fileName, bool expected)
    {
        Assert.Equal(expected, ChatHelpers.IsImageFile(fileName));
    }

    [Fact]
    public void MatchesSearch_ChecksRemarkNicknameUidAndLastMessage()
    {
        var chat = new ChatModel
        {
            NickName = "Alice",
            RemarkName = "Workshop",
            Uid = "client-42",
            Message = "calibration done"
        };

        Assert.True(ChatHelpers.MatchesSearch(chat, "alice"));
        Assert.True(ChatHelpers.MatchesSearch(chat, "work"));
        Assert.True(ChatHelpers.MatchesSearch(chat, "42"));
        Assert.True(ChatHelpers.MatchesSearch(chat, "calibration"));
        Assert.False(ChatHelpers.MatchesSearch(chat, "missing"));
    }

    [Fact]
    public void BuildUnreadPreview_TrimsAndNormalizesText()
    {
        var preview = ChatHelpers.BuildUnreadPreview("Alice", "hello\r\nworld from a long message", "", false, 18);

        Assert.DoesNotContain('\n', preview);
        Assert.True(preview.Length <= 18);
        Assert.StartsWith("Alice: hello", preview);
    }

    [Fact]
    public void BuildUnreadPreview_UsesFileLabelForFiles()
    {
        var preview = ChatHelpers.BuildUnreadPreview("Alice", "ignored", "photo.png", true);

        Assert.Equal("Alice: [文件]photo.png", preview);
    }

    [Fact]
    public void DisplayName_PrefersRemarkAndFallsBackToNickname()
    {
        var chat = new ChatModel { NickName = "Alice" };

        Assert.Equal("Alice", chat.DisplayName);

        chat.RemarkName = "Line 1";
        Assert.Equal("Line 1", chat.DisplayName);

        chat.RemarkName = "";
        Assert.Equal("Alice", chat.DisplayName);
    }
}
