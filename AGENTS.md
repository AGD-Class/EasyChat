# EasyChat Agent Notes

## Project Overview

EasyChat is a Windows-only .NET 8 WPF LAN chat application. It uses MVVM, MQTTnet, WPF-UI, AES-encrypted MQTT payloads, and socket-based file transfer. The UI imitates a WeChat-style desktop chat layout.

The product intentionally avoids a database and does not persist chat history. Keep this property unless the user explicitly asks for persistence.

## Important Files

- `README.md`: product summary and feature checklist.
- `EasyChat.sln`: solution entry point.
- `src/EasyChat/EasyChat.csproj`: WPF app project.
- `src/EasyChat/ViewModels/MainViewModel.cs`: main chat workflow, user list integration, file/image sending, unread preview, group chat.
- `src/EasyChat/Views/MainView.xaml`: primary chat UI layout.
- `src/EasyChat/Service/MyMqttClient.cs`: MQTT client publish/subscribe behavior.
- `src/EasyChat/Service/MqttServer.cs`: embedded MQTT server for server mode and online user broadcast.
- `src/EasyChat/Service/SocketServer.cs` / `SocketClient.cs`: file transfer after receiver confirmation.
- `src/EasyChat/Service/ScreenShareServer.cs` / `ScreenShareClient.cs`: first-version LAN screen sharing frame stream.
- `src/EasyChat/Views/ScreenShareView.xaml`: receiver-side screen sharing viewer window.
- `src/EasyChat/Views/ScreenShareStatusView.xaml`: sharer-side movable screen sharing status overlay with stop and control-disconnect buttons.
- `src/EasyChat/Service/RemoteControlInputService.cs`: opt-in remote control input injection on the sharer machine.
- `src/EasyChat/Utilities/ChatHelpers.cs`: reusable helper logic for search, image detection, thumbnail generation, and unread previews.
- `tests/EasyChat.Tests/ChatHelpersTests.cs`: focused unit tests for reusable chat helper behavior.

## Current Feature State

The README checklist is implemented:

- Login/main UI, avatar switching, theme switching, top window controls.
- Private chat and default all-user group chat.
- New message blinking plus taskbar/tray tooltip preview.
- Search by nickname, remark, UID, or last message.
- Avatar click/contact info and in-memory friend remark editing.
- Single and multiple file/image send/receive.
- Image thumbnail preview before original download.
- Drag-and-drop file/image attachment.
- First-version private and default-group screen sharing in LAN: MQTT control signals plus TCP JPEG frame stream, selectable/live-switchable stream resolution, sharer status overlay, and consent-gated remote control.
- Memory-sensitive layout and collection handling improvements.

Remarks, avatar choices, group state, and chat messages are in-memory only and reset after restart.

## Architecture Notes

- Chat messages are transported over MQTT as encrypted `MsgModel` payloads.
- Chat/file MQTT messages should not use retained messages. Online-user broadcast may still use retained messages.
- File transfer is two-step:
  1. Sender publishes file metadata over MQTT.
  2. Receiver chooses a save path and confirms over the `file/` topic.
  3. Sender opens a socket connection to the receiver IP/port and streams the file.
- Do not use a shared `SocketClient` instance across recipients; create/use a client per target IP/port.
- Screen sharing uses MQTT only for control messages on `screen/` (`request`, `accept`, `reject`, `stop`). Do not send screen frames through MQTT.
- Screen sharing frame data is sent directly over TCP from the sharer to the viewer. The first version captures the virtual desktop, scales frames to the selected 480p/720p/1080p cap, and streams JPEG frames around 10fps. The default selected cap is 720p.
- The first-version screen sharing UX supports private chat and the default all-user group chat. Group sharing broadcasts the invite on `screen/`, lets each viewer connect to the same TCP stream independently, and broadcasts `stop` to every viewer. Do not add multi-group membership management unless requested.
- Resolution can be changed while sharing from the sharer toolbar or receiver-side screen sharing window. Because the first version uses one TCP stream per sharer, a receiver-side resolution change affects the shared stream for every viewer.
- The sharer must always see an active-sharing indicator: `ScreenShareStatusView` shows a compact movable top control panel. Do not add screen-edge overlay windows because they can obscure the shared desktop. The panel is the preferred way to stop sharing and disconnect the current controller.
- Remote screen control is opt-in per session: the viewer must request control, the sharer must explicitly allow it with the custom `EcMsgBox`, and only one authorized controller's input is accepted at a time. The sharer can revoke control at any time from the sharing status overlay.
- The default group chat uses the existing `group/` topic and `群聊` conversation. Do not add multi-group membership management unless requested.
- The main send area must keep the send button visible when the window is resized. The current layout uses a fixed bottom send region and a minimum window size.

## Build And Test

Preferred validation commands:

```powershell
dotnet restore EasyChat.sln
dotnet build EasyChat.sln --no-restore
dotnet test EasyChat.sln --no-build
```

If `EasyChat.exe` or Visual Studio is running, the normal build may fail because `bin/Debug/net8.0-windows/EasyChat.exe` or `EasyChat.dll` is locked. In that case, validate compilation with a temporary output directory:

```powershell
dotnet build src\EasyChat\EasyChat.csproj --no-restore -p:OutDir=$env:TEMP\EasyChatVerify\
```

The test project targets `net8.0-windows` and uses `xunit.runner.visualstudio`, so `dotnet test` should discover tests.

## Coding Guidelines For This Repo

- Prefer the existing MVVM pattern with CommunityToolkit.Mvvm attributes.
- Keep UI changes consistent with the current WPF/XAML style; avoid adding database or persistence infrastructure casually.
- Keep chat history non-persistent by default.
- Use `ChatHelpers` for shared search, preview, image, and thumbnail logic instead of duplicating string/file checks.
- Preserve `Shift+Enter` for line breaks and `Enter` for send in the message editor.
- When changing WPF layout, verify behavior at the minimum window size and with pending attachments present.
