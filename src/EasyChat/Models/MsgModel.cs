namespace EasyChat.Models;

/// <summary>
/// Mqtt消息封装体对象
/// </summary>
public class MsgModel
{
    /// <summary>
    /// 发送者userModel
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
    /// 服务端IP地址
    /// </summary>
    public string serverIp { get; set; } = "";

    /// <summary>
    /// 服务端端口
    /// </summary>
    public int serverPort { get; set; }

    /// <summary>
    /// 大文件分片传输：片总数
    /// </summary>
    public int totalChunks { get; set; } = 1;
    /// <summary>
    /// 大文件分片传输：当前片
    /// </summary>
    public int thisChunk { get; set; }

    /// <summary>
    /// Screen sharing control message.
    /// </summary>
    public bool isScreenShare { get; set; }

    /// <summary>
    /// Screen sharing action: request, accept, reject, stop.
    /// </summary>
    public string screenShareAction { get; set; } = "";

    /// <summary>
    /// Target user uid for the screen sharing signal.
    /// </summary>
    public string screenShareTargetUid { get; set; } = "";

    /// <summary>
    /// Correlates request/accept/stop messages for one sharing session.
    /// </summary>
    public string screenShareSessionId { get; set; } = "";

    /// <summary>
    /// LAN IP address of the user sharing the screen.
    /// </summary>
    public string screenShareHostIp { get; set; } = "";

    /// <summary>
    /// TCP port for the screen frame stream.
    /// </summary>
    public int screenSharePort { get; set; }

    /// <summary>
    /// Maximum shared frame width.
    /// </summary>
    public int screenShareWidth { get; set; }

    /// <summary>
    /// Maximum shared frame height.
    /// </summary>
    public int screenShareHeight { get; set; }

    /// <summary>
    /// Remote control input event type.
    /// </summary>
    public string screenControlEvent { get; set; } = "";

    /// <summary>
    /// Normalized cursor X position in the shared screen.
    /// </summary>
    public double screenControlX { get; set; }

    /// <summary>
    /// Normalized cursor Y position in the shared screen.
    /// </summary>
    public double screenControlY { get; set; }

    /// <summary>
    /// Mouse button: 0 left, 1 right, 2 middle.
    /// </summary>
    public int screenControlMouseButton { get; set; }

    /// <summary>
    /// Windows virtual key code for remote keyboard events.
    /// </summary>
    public int screenControlKey { get; set; }

    /// <summary>
    /// Mouse wheel delta.
    /// </summary>
    public int screenControlDelta { get; set; }
}
