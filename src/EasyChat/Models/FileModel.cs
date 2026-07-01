using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using EasyChat.Utilities;

namespace EasyChat.Models;

public partial class FileModel : ObservableObject
{
    /// <summary>
    /// 文件名，主要用来获取文件后缀
    /// </summary>
    [ObservableProperty] private string fileName = "";

    /// <summary>
    /// 文件大小
    /// </summary>
    [ObservableProperty] private string fileSize = "";

    /// <summary>
    /// 发送方本地文件路径
    /// 当发送方发送文件时，先用MQTT发送消息，告诉接收方文件大小和格式
    /// 当接收方点击下载按钮，才会进行Socket通讯
    /// </summary>
    [ObservableProperty] private string clientFilePath = "";

    [ObservableProperty] private bool isImage;

    [ObservableProperty] private string thumbnailBase64 = "";

    [ObservableProperty] private ImageSource? thumbnailImage;

    partial void OnThumbnailBase64Changed(string value)
    {
        ThumbnailImage = ChatHelpers.ThumbnailBase64ToImageSource(value);
    }
}
