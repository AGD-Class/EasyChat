namespace EasyChat.Models;

/// <summary>
/// 消息封装体对象
/// </summary>
public class MsgModel
{
    /// <summary>
    /// userModel
    /// </summary>
    public UserModel userModel { get; set; } = new UserModel();

    /// <summary>
    /// 发消息的时间
    /// </summary>
    public DateTime sendTime { get; set; }

    /// <summary>
    /// 消息体
    /// </summary>
    public string message { get; set; } = "";

    /// <summary>
    /// 是否是群消息
    /// </summary>
    public bool isGroupMsg { get; set; }

    /// <summary>
    /// 群名称
    /// </summary>
    public string groupName { get; set; } = "";

    /// <summary>
    /// 所有在线用户，仅服务端需要
    /// </summary>
    public List<UserModel> userModels { get; set; } = [];

    /// <summary>
    /// 文件或者图片消息
    /// </summary>
    public bool isImageOrFile { get; set; }

    /// <summary>
    /// 是否是图片文件
    /// </summary>
    public bool isImage { get; set; }

    /// <summary>
    /// 图片缩略图Base64
    /// </summary>
    public string thumbnailBase64 { get; set; } = "";
    
    /// <summary>
    /// 文件名，主要用来获取文件后缀
    /// </summary>
    public string fileName { get; set; } = "";

    /// <summary>
    /// 文件大小
    /// </summary>
    public string fileSize { get; set; } = "";

    /// <summary>
    /// 发送方本地文件路径
    /// 当发送方发送文件时，先用MQTT发送消息，告诉接收方文件大小和格式
    /// 当接收方点击下载按钮，才会进行Socket通讯
    /// </summary>
    public string clientFilePath { get; set; } = "";

    /// <summary>
    /// 服务端是否确认接收
    /// </summary>
    public bool isServerReceived { get; set; } = false;

    /// <summary>
    /// 大文件分片传输：片总数
    /// </summary>
    public int totalChunks { get; set; } = 1;
    /// <summary>
    /// 大文件分片传输：当前片
    /// </summary>
    public int thisChunk { get; set; }
}
