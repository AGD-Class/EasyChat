using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyChat.Controls;
using EasyChat.Handle;
using EasyChat.Models;
using EasyChat.Service;
using EasyChat.Utilities;
using EasyChat.ViewModels.SubVms;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Application = System.Windows.Application;
using DialogResult = System.Windows.Forms.DialogResult;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;

namespace EasyChat.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly MyMqttClient _myClient = MyMqttClient.Instance;
    private readonly EventHelper _eventHelper = EventHelper.Instance;
    private readonly ThemeService _themeService = new();
    private readonly Dictionary<string, BindingList<ChatMessage>> _chatMessageDic = new();
    private readonly SocketServer _socketServer;
    private string _sendTopic = string.Empty;
    private int _allNewMessageCount;
    private bool _isTheme;

    public MainViewModel()
    {
        var nickName = string.IsNullOrEmpty(MqttContent.USER_NAME) ? _myClient.MyClientUid : MqttContent.USER_NAME;
        MyChatModel = new ChatModel
        {
            Uid = _myClient.MyClientUid,
            NickName = nickName,
            Image = MqttContent.GetRandomImg(),
            IpAddress = MqttContent.GetLocalIp(MqttContent.IPADDRESS),
            Port = MqttContent.GetLocalOkPort(MqttContent.SOCKET_PORT, null)
        };

        UserListVm.Add(MyChatModel);
        EnsureMessageList(MyChatModel.Uid);
        InitGroup();

        _myClient.StartClient(MqttContent.IPADDRESS, MyChatModel);
        _socketServer = SocketServer.GetInstance(MyChatModel.Port);
        _myClient.OnlinePersonEvent += ClientChangeOnlinePerson;
        _myClient.ReceiveMsgEvent += ClientChangeReceiveMsg;
        _myClient.FileSendEvent += ClientChangeReceiveFile;

        UserListVm.OnSelected += UserSelect;
        UserListVm.RightClicked += ShowContactCard;
        UserListVm.AvatarClicked += UserAvatarClick;
        _eventHelper.ClearNewMessage += ClearNewMessage;
        _eventHelper.FileReceive += DealReceiveImageOrFile;

        _myClient.AddTopic(MqttContent.GROUP);
    }

    #region 事件回调方法

    /// <summary>
    /// 客户端修改页面在线用户，群聊不作为在线用户。
    /// </summary>
    private void ClientChangeOnlinePerson(MsgModel msgModel)
    {
        if (msgModel == null)
        {
            return;
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            UserListVm.RemoveWhere(user => user.Uid != MyChatModel.Uid && !user.IsGroup);
            foreach (var userModel in msgModel.userModels)
            {
                UserListVm.AddOrUpdate(MqttContent.ToChatModel(userModel));
                EnsureMessageList(userModel.uid);
            }

            UserListVm.SortByDescending(user => user.Uid == MyChatModel.Uid, user => user.DisplayName);
        });
    }

    /// <summary>
    /// 客户端接受消息。
    /// </summary>
    private void ClientChangeReceiveMsg(MsgModel newMsg)
    {
        var chats = CreateChatMessage(newMsg);
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (newMsg.isGroupMsg)
            {
                DealGroupMessage(newMsg, chats);
            }
            else
            {
                DealPersonMessage(newMsg, chats);
            }

            BindCurrentMessages();
        });
    }

    /// <summary>
    /// 客户端接收文件发送确认。
    /// </summary>
    private void ClientChangeReceiveFile(MsgModel newMsg)
    {
        if (newMsg.isServerReceived)
        {
            DealSendImageOrFile(newMsg);
        }
    }

    private void UserSelect(ChatModel chatModel)
    {
        if (chatModel == null)
        {
            return;
        }

        try
        {
            _sendTopic = chatModel.Uid;
            _allNewMessageCount = Math.Max(0, _allNewMessageCount - chatModel.MessageCount);
            chatModel.MessageCount = 0;
            ChatObj = chatModel;
            BindCurrentMessages();

            if (_allNewMessageCount == 0)
            {
                ClearUnreadPreview();
            }
        }
        catch
        {
        }
    }

    private void ClearNewMessage()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var item in UserListVm.SourceUsers)
            {
                item.MessageCount = 0;
            }

            _allNewMessageCount = 0;
            ClearUnreadPreview();
        });
    }

    #endregion

    #region 私有方法

    private void DealGroupMessage(MsgModel newMsg, ChatMessage chats)
    {
        var groupName = string.IsNullOrWhiteSpace(newMsg.groupName) ? "群聊" : newMsg.groupName;
        var group = EnsureGroup(groupName);
        EnsureMessageList(group.Uid).Add(chats);
        group.Message = GetMessagePreview(newMsg);

        if (!chats.IsMyMessage && group.Uid != ChatObj.Uid)
        {
            AddUnread(group, chats, newMsg);
        }
    }

    private void DealPersonMessage(MsgModel newMsg, ChatMessage chats)
    {
        var senderUid = newMsg.userModel.uid;
        var chat = UserListVm.FindByUid(senderUid);
        if (chat == null)
        {
            chat = MqttContent.ToChatModel(newMsg.userModel);
            UserListVm.Add(chat);
        }

        EnsureMessageList(senderUid).Add(chats);
        chat.Message = GetMessagePreview(newMsg);

        if (!chats.IsMyMessage && senderUid != ChatObj.Uid)
        {
            AddUnread(chat, chats, newMsg);
        }
    }

    private void DealReceiveImageOrFile(ChatMessage chats)
    {
        try
        {
            var saveFileDialog = new SaveFileDialog
            {
                Title = "选择保存文件的位置",
                Filter = "All Files (*.*)|*.*",
                FileName = chats.FileName,
                InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "")
            };

            if (saveFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            var localFilePath = saveFileDialog.FileName;
            if (string.IsNullOrEmpty(localFilePath))
            {
                return;
            }

            _ = _socketServer.StartReceiveAsync(localFilePath);
            _myClient.SendMsg(MqttContent.FILE, new MsgModel
            {
                userModel = MqttContent.ToUserModel(MyChatModel),
                sendTime = DateTime.Now,
                isServerReceived = true,
                isImageOrFile = chats.IsFile,
                isImage = chats.IsImage,
                thumbnailBase64 = chats.ThumbnailBase64,
                fileName = chats.FileName,
                fileSize = chats.FileSize,
                clientFilePath = chats.FilePath
            });
            chats.IsReceived = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"文件处理发生错误: {ex.Message}");
        }
    }

    private void DealSendImageOrFile(MsgModel newMsg)
    {
        var filePath = newMsg.clientFilePath;
        if (!File.Exists(filePath)
            || string.IsNullOrWhiteSpace(newMsg.userModel.ipAddress)
            || newMsg.userModel.port <= 0)
        {
            return;
        }

        var socketClient = SocketClient.GetInctance(newMsg.userModel.ipAddress, newMsg.userModel.port);
        _ = socketClient.SendFileAsync(filePath);
    }

    private BindingList<ChatMessage> EnsureMessageList(string uid)
    {
        if (!_chatMessageDic.TryGetValue(uid, out var messages))
        {
            messages = [];
            _chatMessageDic.Add(uid, messages);
        }

        return messages;
    }

    private ChatModel EnsureGroup(string groupName)
    {
        var group = UserListVm.FindByUid(groupName);
        if (group != null)
        {
            return group;
        }

        group = new ChatModel
        {
            Uid = groupName,
            NickName = groupName,
            GroupName = groupName,
            Image = MqttContent.GetRandomImg(),
            IsGroup = true
        };
        UserListVm.Add(group);
        EnsureMessageList(group.Uid);
        return group;
    }

    private ChatMessage CreateChatMessage(MsgModel msgModel)
    {
        var chatMessage = MqttContent.ToChatMessage(msgModel, MyChatModel.Uid);
        var sender = msgModel.userModel.uid == MyChatModel.Uid
            ? MyChatModel
            : UserListVm.FindByUid(msgModel.userModel.uid);
        if (sender != null)
        {
            chatMessage.NickName = sender.DisplayName;
        }

        return chatMessage;
    }

    private void BindCurrentMessages()
    {
        if (!string.IsNullOrEmpty(ChatObj.Uid))
        {
            ChatMessages.Messages = EnsureMessageList(ChatObj.Uid);
        }
    }

    private void AddUnread(ChatModel chat, ChatMessage chatMessage, MsgModel msgModel)
    {
        chat.MessageCount++;
        _allNewMessageCount++;
        _eventHelper.StartBlink();

        var senderName = msgModel.isGroupMsg
            ? chatMessage.NickName ?? chat.DisplayName
            : chat.DisplayName;
        UpdateUnreadPreview(ChatHelpers.BuildUnreadPreview(
            senderName,
            msgModel.message,
            msgModel.fileName,
            msgModel.isImageOrFile));
    }

    private void UpdateUnreadPreview(string preview)
    {
        TaskbarPreviewText = string.IsNullOrWhiteSpace(preview) ? "EasyChat" : preview;
        _eventHelper.UpdateUnreadPreview(TaskbarPreviewText);
    }

    private void ClearUnreadPreview()
    {
        _eventHelper.StopBlink();
        UpdateUnreadPreview("EasyChat");
    }

    private static string GetMessagePreview(MsgModel msgModel)
    {
        return msgModel.isImageOrFile ? MqttContent.FILE_STRING + msgModel.fileName : msgModel.message;
    }

    private void ShowContactCard(ChatModel chatModel)
    {
        var info = chatModel.IsGroup
            ? $"名称: {chatModel.DisplayName}\n类型: 全员群聊"
            : $"昵称: {chatModel.NickName}\nUID: {chatModel.Uid}\nIP: {chatModel.IpAddress}:{chatModel.Port}";
        var remark = EcInputBox.Show("联系人信息", $"{info}\n\n备注（留空则使用昵称）:", chatModel.RemarkName);
        if (remark == null)
        {
            return;
        }

        chatModel.RemarkName = remark.Trim();
        UserListVm.RefreshFilter();
    }

    private void UserAvatarClick(ChatModel chatModel)
    {
        if (chatModel.Uid == MyChatModel.Uid)
        {
            ImageClick();
            return;
        }

        ShowContactCard(chatModel);
    }

    private void AddFiles(IEnumerable<string> filePaths)
    {
        foreach (var filePath in filePaths.Where(File.Exists))
        {
            var fileInfo = new FileInfo(filePath);
            var isImage = ChatHelpers.IsImageFile(filePath);
            PendingFiles.Add(new FileModel
            {
                FileName = fileInfo.Name,
                ClientFilePath = fileInfo.FullName,
                FileSize = MqttContent.FileSizeToString(fileInfo.Length),
                IsImage = isImage,
                ThumbnailBase64 = isImage ? ChatHelpers.CreateThumbnailBase64(fileInfo.FullName) : ""
            });
        }
    }

    private void PublishMessage(string topic, MsgModel msgModel)
    {
        _myClient.SendMsg(topic, msgModel);
        if (!msgModel.isGroupMsg && !MyChatModel.Uid.Equals(_sendTopic))
        {
            AddOutgoingPrivateMessage(_sendTopic, msgModel);
        }
    }

    private void AddOutgoingPrivateMessage(string conversationUid, MsgModel msgModel)
    {
        var messages = EnsureMessageList(conversationUid);
        messages.Add(CreateChatMessage(msgModel));
        var chat = UserListVm.FindByUid(conversationUid);
        if (chat != null)
        {
            chat.MessageCount = 0;
            chat.Message = GetMessagePreview(msgModel);
        }

        BindCurrentMessages();
    }

    private MsgModel CreateOutgoingMessage(string message, bool isGroupMsg)
    {
        return new MsgModel
        {
            userModel = MqttContent.ToUserModel(MyChatModel),
            sendTime = DateTime.Now,
            message = message,
            isGroupMsg = isGroupMsg,
            groupName = isGroupMsg ? ChatObj.GroupName : ""
        };
    }

    private MsgModel CreateOutgoingFileMessage(FileModel file, bool isGroupMsg)
    {
        return new MsgModel
        {
            userModel = MqttContent.ToUserModel(MyChatModel),
            sendTime = DateTime.Now,
            isGroupMsg = isGroupMsg,
            groupName = isGroupMsg ? ChatObj.GroupName : "",
            isImageOrFile = true,
            isImage = file.IsImage,
            thumbnailBase64 = file.ThumbnailBase64,
            fileName = file.FileName,
            clientFilePath = file.ClientFilePath,
            fileSize = file.FileSize
        };
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void Minimize(Window window)
    {
        window.WindowState = WindowState.Minimized;
    }

    [RelayCommand]
    private void Maximize(Window window)
    {
        if (window.WindowState == WindowState.Maximized)
        {
            window.WindowState = WindowState.Normal;
            IsMaximized = false;
        }
        else
        {
            window.WindowState = WindowState.Maximized;
            IsMaximized = true;
        }
    }

    [RelayCommand]
    private void ToggleTopmost(Window window)
    {
        window.Topmost = !window.Topmost;
        IsTopmost = window.Topmost;
    }

    [RelayCommand]
    private void Hide(Window window)
    {
        window.Hide();
    }

    [RelayCommand]
    private void Send()
    {
        if (string.IsNullOrEmpty(ChatObj.Uid))
        {
            EcMsgBox.Show("先选择用户");
            return;
        }

        try
        {
            var messageText = FlowDocumentToString(SendMsg);
            if (string.IsNullOrWhiteSpace(messageText) && PendingFiles.Count == 0)
            {
                EcMsgBox.Show("发送内容或对象不可为空");
                return;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                var isGroupMsg = ChatObj.IsGroup;
                var topic = isGroupMsg ? MqttContent.GROUP : MqttContent.MESSAGE + _sendTopic;

                if (!string.IsNullOrWhiteSpace(messageText))
                {
                    PublishMessage(topic, CreateOutgoingMessage(messageText, isGroupMsg));
                }

                foreach (var file in PendingFiles.ToList())
                {
                    PublishMessage(topic, CreateOutgoingFileMessage(file, isGroupMsg));
                }

                PendingFiles.Clear();
                SendMsg = new FlowDocument();
            });
        }
        catch (Exception ex)
        {
            EcMsgBox.Show("发送失败");
            System.Diagnostics.Debug.WriteLine(">>>>" + ex);
        }
    }

    [RelayCommand]
    private void Enter()
    {
        Send();
    }

    [RelayCommand]
    private void Nothing()
    {
        EcMsgBox.Show("这个功能还没做");
    }

    [RelayCommand]
    private void ImageClick()
    {
        MyChatModel.Image = MqttContent.GetRandomImg();
        _isTheme = !_isTheme;
        _themeService.SetTheme(_isTheme ? ApplicationTheme.Dark : ApplicationTheme.Light);
    }

    [RelayCommand]
    private void FileBrowse()
    {
        if (string.IsNullOrEmpty(ChatObj.Uid))
        {
            EcMsgBox.Show("先选择用户");
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "所有文件(*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            AddFiles(dialog.FileNames);
        }
    }

    [RelayCommand]
    private void RemovePendingFile(FileModel file)
    {
        PendingFiles.Remove(file);
    }

    public void AddDroppedFiles(IEnumerable<string> filePaths)
    {
        if (string.IsNullOrEmpty(ChatObj.Uid))
        {
            EcMsgBox.Show("先选择用户");
            return;
        }

        AddFiles(filePaths);
    }

    #endregion

    #region Property

    [ObservableProperty] private bool _isMaximized;

    [ObservableProperty] private bool _isTopmost;

    [ObservableProperty] private string _taskbarPreviewText = "EasyChat";

    public UserListVm UserListVm { get; } = new();

    public MessageListVm ChatMessages { get; set; } = new();

    public ObservableCollection<FileModel> PendingFiles { get; } = [];

    /// <summary>
    /// 发送信息。
    /// </summary>
    [ObservableProperty] private FlowDocument _sendMsg = new();

    /// <summary>
    /// 用户自己。
    /// </summary>
    [ObservableProperty] private ChatModel _myChatModel = new();

    /// <summary>
    /// 当前聊天对象。
    /// </summary>
    [ObservableProperty] private ChatModel _chatObj = new();

    #endregion

    #region 后门方法

    private void InitGroup()
    {
        EnsureGroup("群聊");
    }

    private string FlowDocumentToString(FlowDocument document)
    {
        var builder = new StringBuilder();
        foreach (var block in document.Blocks.OfType<Paragraph>())
        {
            foreach (var item in block.Inlines)
            {
                if (item is Run run)
                {
                    builder.Append(run.Text);
                }
                else if (item is LineBreak)
                {
                    builder.AppendLine();
                }
                else if (item is InlineUIContainer { Child: ContentControl { Content: not null } content })
                {
                    builder.Append(content.Content);
                }
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    #endregion
}
