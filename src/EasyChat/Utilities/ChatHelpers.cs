using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EasyChat.Models;

namespace EasyChat.Utilities;

public static class ChatHelpers
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp",
        ".gif",
        ".jpeg",
        ".jpg",
        ".png",
        ".tif",
        ".tiff"
    };

    public static bool IsImageFile(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        return ImageExtensions.Contains(Path.GetExtension(fileName));
    }

    public static bool MatchesSearch(ChatModel chat, string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        var query = searchText.Trim();
        return Contains(chat.NickName, query)
               || Contains(chat.RemarkName, query)
               || Contains(chat.DisplayName, query)
               || Contains(chat.Uid, query)
               || Contains(chat.Message, query);
    }

    public static string BuildUnreadPreview(string senderName, string message, string fileName, bool isFile, int maxLength = 80)
    {
        var content = isFile
            ? $"{MqttContent.FILE_STRING}{fileName}"
            : message;
        var preview = string.IsNullOrWhiteSpace(senderName) ? content : $"{senderName}: {content}";
        return TrimPreview(preview, maxLength);
    }

    public static string TrimPreview(string value, int maxLength)
    {
        if (maxLength <= 0)
        {
            return string.Empty;
        }

        var normalized = (value ?? string.Empty).ReplaceLineEndings(" ");
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        if (maxLength <= 3)
        {
            return normalized[..maxLength];
        }

        return normalized[..Math.Max(0, maxLength - 3)] + "...";
    }

    public static string CreateThumbnailBase64(string filePath, int decodePixelWidth = 180)
    {
        if (!File.Exists(filePath) || !IsImageFile(filePath))
        {
            return string.Empty;
        }

        try
        {
            using var input = File.OpenRead(filePath);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = decodePixelWidth;
            bitmap.StreamSource = input;
            bitmap.EndInit();
            bitmap.Freeze();

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var output = new MemoryStream();
            encoder.Save(output);
            return Convert.ToBase64String(output.ToArray());
        }
        catch
        {
            return string.Empty;
        }
    }

    public static ImageSource? ThumbnailBase64ToImageSource(string? base64, int decodePixelWidth = 180)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(base64);
            using var input = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = decodePixelWidth;
            bitmap.StreamSource = input;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public static string FlowTextFromRuns(IEnumerable<string> parts)
    {
        var builder = new StringBuilder();
        foreach (var part in parts)
        {
            builder.Append(part);
        }

        return builder.ToString().TrimEnd();
    }

    private static bool Contains(string? value, string query)
    {
        return !string.IsNullOrEmpty(value)
               && value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
