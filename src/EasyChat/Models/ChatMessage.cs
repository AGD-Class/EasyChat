using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyChat.Handle;
using EasyChat.Utilities;

namespace EasyChat.Models;

/// <summary>
/// 聊天内容对象
/// </summary>
public partial class ChatMessage : ObservableObject
{
    [ObservableProperty] private string? nickName;

    [ObservableProperty] private string? image;

    [ObservableProperty] private string? message;

    [ObservableProperty] private string? time;

    [ObservableProperty] private bool isMyMessage;

    [ObservableProperty] private string? separatorTitle;

    [ObservableProperty] private string color = "#ff82a3";

    [ObservableProperty] private bool isFile;

    [ObservableProperty] private bool isImage;

    [ObservableProperty] private string fileName = "";

    [ObservableProperty] private string fileSize = "";

    [ObservableProperty] private string filePath = "";

    [ObservableProperty] private string thumbnailBase64 = "";

    [ObservableProperty] private ImageSource? thumbnailImage;

    [ObservableProperty] private bool isReceived;

    [ObservableProperty] private string senderUid = "";

    [ObservableProperty] private string senderIpAddress = "";

    [ObservableProperty] private int senderPort = MqttContent.SOCKET_PORT;

    partial void OnThumbnailBase64Changed(string value)
    {
        ThumbnailImage = ChatHelpers.ThumbnailBase64ToImageSource(value);
    }

    [RelayCommand]
    private void FileReceive()
    {
        EventHelper.Instance.ReceiveFile(this);
    }
}
