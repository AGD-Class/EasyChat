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
using EasyChat.Views;
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
    private ScreenShareServer? _screenShareServer;
    private ScreenShareStatusView? _screenShareStatusView;
    private ScreenShareView? _screenShareView;
    private string _screenShareSessionId = string.Empty;
    private string _screenShareTargetUid = string.Empty;
    private bool _screenShareIsGroup;
    private string _screenShareControllerUid = string.Empty;
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
        SelectedScreenShareResolution = ScreenShareResolutions[1];

        _myClient.StartClient(MqttContent.IPADDRESS, MyChatModel);
        _socketServer = SocketServer.GetInstance(MyChatModel.Port);
        _myClient.OnlinePersonEvent += ClientChangeOnlinePerson;
        _myClient.ReceiveMsgEvent += ClientChangeReceiveMsg;
        _myClient.FileSendEvent += ClientChangeReceiveFile;
        _myClient.ScreenShareEvent += ClientChangeScreenShare;

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

    private void ClientChangeScreenShare(MsgModel msgModel)
    {
        if (!msgModel.isScreenShare
            || msgModel.userModel.uid == MyChatModel.Uid
            || (!string.IsNullOrWhiteSpace(msgModel.screenShareTargetUid)
                && msgModel.screenShareTargetUid != MyChatModel.Uid))
        {
            return;
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            switch (msgModel.screenShareAction)
            {
                case MqttContent.SCREEN_SHARE_REQUEST:
                    HandleScreenShareRequest(msgModel);
                    break;
                case MqttContent.SCREEN_SHARE_ACCEPT:
                case MqttContent.SCREEN_SHARE_REJECT:
                    HandleScreenShareResponse(msgModel);
                    break;
                case MqttContent.SCREEN_SHARE_STOP:
                    HandleScreenShareStop(msgModel);
                    break;
                case MqttContent.SCREEN_SHARE_RESOLUTION_CHANGE:
                    HandleScreenShareResolutionChange(msgModel);
                    break;
                case MqttContent.SCREEN_CONTROL_REQUEST:
                    HandleScreenControlRequest(msgModel);
                    break;
                case MqttContent.SCREEN_CONTROL_DIRECT_REQUEST:
                    HandleScreenControlDirectRequest(msgModel);
                    break;
                case MqttContent.SCREEN_CONTROL_DIRECT_ACCEPT:
                case MqttContent.SCREEN_CONTROL_DIRECT_REJECT:
                    HandleScreenControlDirectResponse(msgModel);
                    break;
                case MqttContent.SCREEN_CONTROL_ACCEPT:
                case MqttContent.SCREEN_CONTROL_REJECT:
                    HandleScreenControlResponse(msgModel);
                    break;
                case MqttContent.SCREEN_CONTROL_REVOKE:
                    HandleScreenControlRevoke(msgModel);
                    break;
                case MqttContent.SCREEN_CONTROL_RELEASE:
                    HandleScreenControlRelease(msgModel);
                    break;
                case MqttContent.SCREEN_CONTROL_INPUT:
                    HandleScreenControlInput(msgModel);
                    break;
            }
        });
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

            StartFileTransfer(chats, "准备接收");
            var progress = CreateFileTransferProgress(chats, "接收中", "接收完成");
            _ = _socketServer.StartReceiveAsync(localFilePath, progress);
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
            FailFileTransfer(chats, "接收失败");
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
        ChatMessage? transferMessage = null;
        Application.Current.Dispatcher.Invoke(() =>
        {
            transferMessage = FindOutgoingFileMessage(filePath);
        });

        StartFileTransfer(transferMessage, "准备发送");
        var progress = CreateFileTransferProgress(transferMessage, "发送中", "发送完成");
        _ = SendFileWithProgressAsync(socketClient, filePath, transferMessage, progress);
    }

    private async Task SendFileWithProgressAsync(
        SocketClient socketClient,
        string filePath,
        ChatMessage? transferMessage,
        IProgress<double> progress)
    {
        var sent = await socketClient.SendFileAsync(filePath, progress);
        if (!sent)
        {
            FailFileTransfer(transferMessage, "发送失败");
        }
    }

    private ChatMessage? FindOutgoingFileMessage(string filePath)
    {
        return _chatMessageDic.Values
            .SelectMany(messages => messages)
            .LastOrDefault(message => message.IsMyMessage
                                      && message.IsFile
                                      && string.Equals(message.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
    }

    private IProgress<double> CreateFileTransferProgress(
        ChatMessage? message,
        string activeStatus,
        string completedStatus)
    {
        return new Progress<double>(progress =>
            UpdateFileTransferProgress(message, progress, activeStatus, completedStatus));
    }

    private void StartFileTransfer(ChatMessage? message, string status)
    {
        UpdateChatMessageOnUi(message, target =>
        {
            target.IsFileTransferVisible = true;
            target.FileTransferProgress = 0;
            target.FileTransferStatus = status;
        });
    }

    private void UpdateFileTransferProgress(
        ChatMessage? message,
        double progress,
        string activeStatus,
        string completedStatus)
    {
        UpdateChatMessageOnUi(message, target =>
        {
            var normalizedProgress = Math.Clamp(progress, 0, 100);
            target.IsFileTransferVisible = true;
            target.FileTransferProgress = normalizedProgress;
            target.FileTransferStatus = normalizedProgress >= 100
                ? completedStatus
                : $"{activeStatus} {normalizedProgress:0}%";
        });
    }

    private void FailFileTransfer(ChatMessage? message, string status)
    {
        UpdateChatMessageOnUi(message, target =>
        {
            target.IsFileTransferVisible = true;
            target.FileTransferStatus = status;
        });
    }

    private static void UpdateChatMessageOnUi(ChatMessage? message, Action<ChatMessage> update)
    {
        if (message == null)
        {
            return;
        }

        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            update(message);
            return;
        }

        dispatcher.InvokeAsync(() => update(message));
    }

    private void HandleScreenShareRequest(MsgModel msgModel)
    {
        var hostIp = string.IsNullOrWhiteSpace(msgModel.screenShareHostIp)
            ? msgModel.userModel.ipAddress
            : msgModel.screenShareHostIp;
        if (string.IsNullOrWhiteSpace(hostIp) || msgModel.screenSharePort <= 0)
        {
            EcMsgBox.Show("屏幕共享连接信息无效");
            return;
        }

        var senderName = GetScreenShareSenderName(msgModel);
        var resolutionText = msgModel.screenShareWidth > 0 && msgModel.screenShareHeight > 0
            ? $"（{msgModel.screenShareWidth}x{msgModel.screenShareHeight}）"
            : "";
        var requestText = msgModel.isGroupMsg
            ? $"{senderName} 在群聊中发起屏幕共享{resolutionText}，是否查看？"
            : $"{senderName} 请求共享屏幕{resolutionText}，是否查看？";
        var shouldView = EcMsgBox.Confirm(
            requestText,
            "查看",
            "拒绝");

        if (!shouldView)
        {
            SendScreenShareSignal(
                MqttContent.SCREEN_SHARE_REJECT,
                msgModel.userModel.uid,
                msgModel.screenShareSessionId);
            return;
        }

        OpenScreenShareView(
            senderName,
            msgModel.screenShareSessionId,
            hostIp,
            msgModel.screenSharePort,
            msgModel.userModel.uid);
        SendScreenShareSignal(
            MqttContent.SCREEN_SHARE_ACCEPT,
            msgModel.userModel.uid,
            msgModel.screenShareSessionId);
    }

    private void HandleScreenShareResponse(MsgModel msgModel)
    {
        if (msgModel.screenShareSessionId != _screenShareSessionId)
        {
            return;
        }

        var senderName = GetScreenShareSenderName(msgModel);
        if (msgModel.screenShareAction == MqttContent.SCREEN_SHARE_ACCEPT)
        {
            EcMsgBox.Show($"{senderName} 已开始观看你的屏幕");
        }
        else
        {
            if (!_screenShareIsGroup)
            {
                StopLocalScreenShare(false);
            }

            EcMsgBox.Show($"{senderName} 拒绝了屏幕共享");
        }
    }

    private void HandleScreenShareStop(MsgModel msgModel)
    {
        if (_screenShareView == null
            || (!string.IsNullOrWhiteSpace(msgModel.screenShareSessionId)
                && _screenShareView.SessionId != msgModel.screenShareSessionId))
        {
            return;
        }

        _screenShareView.Close();
        _screenShareView = null;
        EcMsgBox.Show($"{GetScreenShareSenderName(msgModel)} 已停止屏幕共享");
    }

    private void HandleScreenShareResolutionChange(MsgModel msgModel)
    {
        if (_screenShareServer == null
            || msgModel.screenShareSessionId != _screenShareSessionId
            || msgModel.screenShareWidth <= 0
            || msgModel.screenShareHeight <= 0)
        {
            return;
        }

        _screenShareServer.SetResolution(msgModel.screenShareWidth, msgModel.screenShareHeight);
        var resolution = FindResolutionOption(msgModel.screenShareWidth, msgModel.screenShareHeight);
        SelectedScreenShareResolution = resolution;
        _screenShareStatusView?.SelectResolution(resolution);
    }

    private void HandleScreenControlRequest(MsgModel msgModel)
    {
        if (_screenShareServer == null || msgModel.screenShareSessionId != _screenShareSessionId)
        {
            return;
        }

        var requesterUid = msgModel.userModel.uid;
        var requesterName = GetScreenShareSenderName(msgModel);
        if (!string.IsNullOrWhiteSpace(_screenShareControllerUid)
            && _screenShareControllerUid != requesterUid)
        {
            SendScreenShareSignal(MqttContent.SCREEN_CONTROL_REJECT, requesterUid, _screenShareSessionId);
            EcMsgBox.Show($"已有用户正在控制屏幕，已拒绝 {requesterName} 的请求");
            return;
        }

        var accepted = EcMsgBox.Confirm(
            $"{requesterName} 请求控制你的屏幕，是否允许？",
            "允许",
            "拒绝");

        if (!accepted)
        {
            SendScreenShareSignal(MqttContent.SCREEN_CONTROL_REJECT, requesterUid, _screenShareSessionId);
            return;
        }

        _screenShareControllerUid = requesterUid;
        _screenShareStatusView?.UpdateController(requesterName);
        SendScreenShareSignal(MqttContent.SCREEN_CONTROL_ACCEPT, requesterUid, _screenShareSessionId);
        EcMsgBox.Show($"{requesterName} 已获得屏幕控制权");
    }

    /// <summary>
    /// 处理“主动申请控制”信令：被申请方确认后自动开启屏幕共享，并把控制权授予请求方。
    /// </summary>
    private void HandleScreenControlDirectRequest(MsgModel msgModel)
    {
        var requesterUid = msgModel.userModel.uid;
        var requesterName = GetScreenShareSenderName(msgModel);

        if (_screenShareServer != null
            && !_screenShareIsGroup
            && !string.IsNullOrWhiteSpace(_screenShareTargetUid)
            && _screenShareTargetUid != requesterUid)
        {
            SendScreenShareSignal(MqttContent.SCREEN_CONTROL_DIRECT_REJECT, requesterUid, "");
            EcMsgBox.Show($"正在向其他用户共享屏幕，已拒绝 {requesterName} 的控制请求");
            return;
        }

        if (!string.IsNullOrWhiteSpace(_screenShareControllerUid)
            && _screenShareControllerUid != requesterUid)
        {
            SendScreenShareSignal(MqttContent.SCREEN_CONTROL_DIRECT_REJECT, requesterUid, _screenShareSessionId);
            EcMsgBox.Show($"已有用户正在控制屏幕，已拒绝 {requesterName} 的请求");
            return;
        }

        var accepted = EcMsgBox.Confirm(
            $"{requesterName} 请求远程控制你的屏幕，是否允许？允许后会自动共享屏幕给对方。",
            "允许",
            "拒绝");

        if (!accepted)
        {
            SendScreenShareSignal(MqttContent.SCREEN_CONTROL_DIRECT_REJECT, requesterUid, _screenShareSessionId);
            return;
        }

        StartDirectControlShare(requesterUid, requesterName);
    }

    /// <summary>
    /// 处理主动控制申请的结果：同意时直接打开对方屏幕并进入已授权控制状态。
    /// </summary>
    private void HandleScreenControlDirectResponse(MsgModel msgModel)
    {
        var senderName = GetScreenShareSenderName(msgModel);
        if (msgModel.screenShareAction == MqttContent.SCREEN_CONTROL_DIRECT_REJECT)
        {
            EcMsgBox.Show($"{senderName} 拒绝了远程控制请求");
            return;
        }

        var hostIp = string.IsNullOrWhiteSpace(msgModel.screenShareHostIp)
            ? msgModel.userModel.ipAddress
            : msgModel.screenShareHostIp;
        if (string.IsNullOrWhiteSpace(hostIp) || msgModel.screenSharePort <= 0)
        {
            EcMsgBox.Show("远程控制连接信息无效");
            return;
        }

        OpenScreenShareView(
            senderName,
            msgModel.screenShareSessionId,
            hostIp,
            msgModel.screenSharePort,
            msgModel.userModel.uid);
        _screenShareView?.GrantRemoteControl();
    }

    private void HandleScreenControlResponse(MsgModel msgModel)
    {
        if (_screenShareView == null || msgModel.screenShareSessionId != _screenShareView.SessionId)
        {
            return;
        }

        if (msgModel.screenShareAction == MqttContent.SCREEN_CONTROL_ACCEPT)
        {
            _screenShareView.GrantRemoteControl();
        }
        else
        {
            _screenShareView.RejectRemoteControl();
        }
    }

    private void HandleScreenControlRevoke(MsgModel msgModel)
    {
        if (_screenShareView == null || msgModel.screenShareSessionId != _screenShareView.SessionId)
        {
            return;
        }

        _screenShareView.RevokeRemoteControl();
    }

    private void HandleScreenControlRelease(MsgModel msgModel)
    {
        if (_screenShareServer == null
            || msgModel.screenShareSessionId != _screenShareSessionId
            || msgModel.userModel.uid != _screenShareControllerUid)
        {
            return;
        }

        _screenShareControllerUid = string.Empty;
        _screenShareStatusView?.UpdateController("");
        EcMsgBox.Show($"{GetScreenShareSenderName(msgModel)} 已停止控制你的屏幕");
    }

    private void HandleScreenControlInput(MsgModel msgModel)
    {
        if (_screenShareServer == null
            || msgModel.screenShareSessionId != _screenShareSessionId
            || msgModel.userModel.uid != _screenShareControllerUid)
        {
            return;
        }

        RemoteControlInputService.Apply(msgModel);
    }

    private void StartLocalScreenShare()
    {
        try
        {
            var resolution = SelectedScreenShareResolution ?? ScreenShareResolutions[1];
            _screenShareServer = new ScreenShareServer(
                MqttContent.SCREEN_SHARE_PORT,
                resolution.Width,
                resolution.Height);
            _screenShareServer.Start();
            _screenShareIsGroup = ChatObj.IsGroup;
            _screenShareTargetUid = _screenShareIsGroup ? string.Empty : ChatObj.Uid;
            _screenShareSessionId = Guid.NewGuid().ToString("N");
            IsScreenSharing = true;
            ShowScreenShareStatus(resolution);

            SendScreenShareSignal(
                MqttContent.SCREEN_SHARE_REQUEST,
                _screenShareTargetUid,
                _screenShareSessionId,
                MyChatModel.IpAddress,
                _screenShareServer.Port,
                _screenShareIsGroup,
                _screenShareIsGroup ? ChatObj.GroupName : "",
                resolution.Width,
                resolution.Height);

            EcMsgBox.Show(_screenShareIsGroup
                ? $"已向群聊发送屏幕共享邀请（{resolution.Name}）"
                : $"已发送屏幕共享邀请（{resolution.Name}）");
        }
        catch (Exception ex)
        {
            StopLocalScreenShare(false);
            EcMsgBox.Show($"屏幕共享启动失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 主动控制被同意后，被控制方会在这里启动或复用屏幕共享，并把连接信息回传给请求方。
    /// </summary>
    private void StartDirectControlShare(string requesterUid, string requesterName)
    {
        try
        {
            var resolution = SelectedScreenShareResolution ?? ScreenShareResolutions[1];
            if (_screenShareServer == null)
            {
                _screenShareServer = new ScreenShareServer(
                    MqttContent.SCREEN_SHARE_PORT,
                    resolution.Width,
                    resolution.Height);
                _screenShareServer.Start();
                _screenShareIsGroup = false;
                _screenShareTargetUid = requesterUid;
                _screenShareSessionId = Guid.NewGuid().ToString("N");
                IsScreenSharing = true;
                ShowScreenShareStatus(resolution);
            }

            _screenShareControllerUid = requesterUid;
            _screenShareStatusView?.UpdateController(requesterName);
            SendScreenShareSignal(
                MqttContent.SCREEN_CONTROL_DIRECT_ACCEPT,
                requesterUid,
                _screenShareSessionId,
                MyChatModel.IpAddress,
                _screenShareServer.Port,
                width: resolution.Width,
                height: resolution.Height);
            EcMsgBox.Show($"{requesterName} 已获得屏幕控制权");
        }
        catch (Exception ex)
        {
            StopLocalScreenShare(false);
            SendScreenShareSignal(MqttContent.SCREEN_CONTROL_DIRECT_REJECT, requesterUid, _screenShareSessionId);
            EcMsgBox.Show($"远程控制启动失败：{ex.Message}");
        }
    }

    private void StopLocalScreenShare(bool notifyTarget)
    {
        var targetUid = _screenShareTargetUid;
        var sessionId = _screenShareSessionId;
        var isGroup = _screenShareIsGroup;

        _screenShareServer?.Dispose();
        _screenShareServer = null;
        _screenShareStatusView?.Close();
        _screenShareStatusView = null;
        _screenShareTargetUid = string.Empty;
        _screenShareSessionId = string.Empty;
        _screenShareIsGroup = false;
        _screenShareControllerUid = string.Empty;
        IsScreenSharing = false;

        if (notifyTarget && !string.IsNullOrWhiteSpace(sessionId))
        {
            SendScreenShareSignal(MqttContent.SCREEN_SHARE_STOP, targetUid, sessionId, isGroupMsg: isGroup);
        }
    }

    private void ShowScreenShareStatus(ScreenShareResolutionOption resolution)
    {
        _screenShareStatusView?.Close();

        var statusView = new ScreenShareStatusView(ScreenShareResolutions, resolution);
        statusView.StopShareRequested += () =>
        {
            StopLocalScreenShare(true);
            EcMsgBox.Show("已停止屏幕共享");
        };
        statusView.DisconnectControlRequested += DisconnectCurrentScreenController;
        statusView.ResolutionChanged += resolution =>
        {
            SelectedScreenShareResolution = resolution;
        };
        statusView.Closed += (_, _) =>
        {
            if (ReferenceEquals(_screenShareStatusView, statusView))
            {
                _screenShareStatusView = null;
            }
        };

        _screenShareStatusView = statusView;
        statusView.Show();
    }

    private void DisconnectCurrentScreenController()
    {
        if (string.IsNullOrWhiteSpace(_screenShareControllerUid)
            || string.IsNullOrWhiteSpace(_screenShareSessionId))
        {
            return;
        }

        var controllerUid = _screenShareControllerUid;
        _screenShareControllerUid = string.Empty;
        _screenShareStatusView?.UpdateController("");
        SendScreenShareSignal(MqttContent.SCREEN_CONTROL_REVOKE, controllerUid, _screenShareSessionId);
    }

    private void OpenScreenShareView(
        string senderName,
        string sessionId,
        string hostIp,
        int port,
        string ownerUid)
    {
        _screenShareView?.Close();

        var view = new ScreenShareView(senderName, sessionId, new ScreenShareClient(hostIp, port));
        var owner = Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive);
        if (owner != null && owner != view)
        {
            view.Owner = owner;
        }

        view.Closed += (_, _) =>
        {
            if (ReferenceEquals(_screenShareView, view))
            {
                _screenShareView = null;
            }
        };
        view.RemoteControlRequested += () =>
            SendScreenShareSignal(MqttContent.SCREEN_CONTROL_REQUEST, ownerUid, sessionId);
        view.RemoteControlReleased += () =>
            SendScreenShareSignal(MqttContent.SCREEN_CONTROL_RELEASE, ownerUid, sessionId);
        view.RemoteControlInput += input =>
            SendScreenControlInput(ownerUid, sessionId, input);

        _screenShareView = view;
        view.Show();
    }

    private void SendScreenShareSignal(
        string action,
        string targetUid,
        string sessionId,
        string hostIp = "",
        int port = 0,
        bool isGroupMsg = false,
        string groupName = "",
        int width = 0,
        int height = 0)
    {
        _myClient.SendMsg(MqttContent.SCREEN, new MsgModel
        {
            userModel = MqttContent.ToUserModel(MyChatModel),
            sendTime = DateTime.Now,
            isGroupMsg = isGroupMsg,
            groupName = groupName,
            isScreenShare = true,
            screenShareAction = action,
            screenShareTargetUid = targetUid,
            screenShareSessionId = sessionId,
            screenShareHostIp = hostIp,
            screenSharePort = port,
            screenShareWidth = width,
            screenShareHeight = height
        });
    }

    private void SendScreenControlInput(string targetUid, string sessionId, ScreenControlInputEvent input)
    {
        _myClient.SendMsg(MqttContent.SCREEN, new MsgModel
        {
            userModel = MqttContent.ToUserModel(MyChatModel),
            sendTime = DateTime.Now,
            isScreenShare = true,
            screenShareAction = MqttContent.SCREEN_CONTROL_INPUT,
            screenShareTargetUid = targetUid,
            screenShareSessionId = sessionId,
            screenControlEvent = input.EventType,
            screenControlX = input.X,
            screenControlY = input.Y,
            screenControlMouseButton = input.MouseButton,
            screenControlKey = input.Key,
            screenControlDelta = input.Delta
        });
    }

    private string GetScreenShareSenderName(MsgModel msgModel)
    {
        return UserListVm.FindByUid(msgModel.userModel.uid)?.DisplayName
               ?? msgModel.userModel.nickName
               ?? msgModel.userModel.uid;
    }

    private ScreenShareResolutionOption FindResolutionOption(int width, int height)
    {
        return ScreenShareResolutions.FirstOrDefault(option => option.Width == width && option.Height == height)
               ?? ScreenShareResolutions[1];
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
    private void ScreenShare()
    {
        if (_screenShareServer != null)
        {
            StopLocalScreenShare(true);
            EcMsgBox.Show("已停止屏幕共享");
            return;
        }

        if (string.IsNullOrEmpty(ChatObj.Uid))
        {
            EcMsgBox.Show("先选择用户");
            return;
        }

        if (!ChatObj.IsGroup && ChatObj.Uid == MyChatModel.Uid)
        {
            EcMsgBox.Show("不能向自己共享屏幕");
            return;
        }

        if (!ChatObj.IsGroup && !ChatObj.IsOnline)
        {
            EcMsgBox.Show("对方不在线");
            return;
        }

        StartLocalScreenShare();
    }

    /// <summary>
    /// 主动向当前私聊对象申请远程控制；对方同意后会自动共享屏幕并授予控制权。
    /// </summary>
    [RelayCommand]
    private void RequestScreenControl()
    {
        if (string.IsNullOrEmpty(ChatObj.Uid))
        {
            EcMsgBox.Show("先选择用户");
            return;
        }

        if (ChatObj.IsGroup)
        {
            EcMsgBox.Show("主动远程控制仅支持私聊");
            return;
        }

        if (ChatObj.Uid == MyChatModel.Uid)
        {
            EcMsgBox.Show("不能控制自己");
            return;
        }

        if (!ChatObj.IsOnline)
        {
            EcMsgBox.Show("对方不在线");
            return;
        }

        SendScreenShareSignal(MqttContent.SCREEN_CONTROL_DIRECT_REQUEST, ChatObj.Uid, "");
        EcMsgBox.Show("已发送远程控制申请");
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

    [ObservableProperty] private bool _isScreenSharing;

    [ObservableProperty] private string _taskbarPreviewText = "EasyChat";

    public string ScreenShareButtonToolTip => IsScreenSharing ? "停止屏幕共享" : "共享屏幕";

    partial void OnIsScreenSharingChanged(bool value)
    {
        OnPropertyChanged(nameof(ScreenShareButtonToolTip));
    }

    public ObservableCollection<ScreenShareResolutionOption> ScreenShareResolutions { get; } =
    [
        new("480p", 854, 480),
        new("720p", 1280, 720),
        new("1080p", 1920, 1080)
    ];

    [ObservableProperty] private ScreenShareResolutionOption _selectedScreenShareResolution = null!;

    partial void OnSelectedScreenShareResolutionChanged(ScreenShareResolutionOption value)
    {
        if (_screenShareServer == null || value == null)
        {
            return;
        }

        _screenShareServer.SetResolution(value.Width, value.Height);
        _screenShareStatusView?.SelectResolution(value);
    }

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
