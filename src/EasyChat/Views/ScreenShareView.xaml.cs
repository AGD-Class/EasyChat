using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using EasyChat.Models;
using EasyChat.Service;

namespace EasyChat.Views;

public partial class ScreenShareView
{
    private readonly ScreenShareClient _client;
    private readonly List<ScreenShareResolutionOption> _resolutionOptions =
    [
        new("480p", 854, 480),
        new("720p", 1280, 720),
        new("1080p", 1920, 1080)
    ];
    private readonly CancellationTokenSource _cts = new();
    private bool _isResolutionInitialized;
    private bool _hasRemoteControl;
    private bool _isControlRequestPending;
    private bool _hasFirstFrame;
    private DateTime _lastMouseMoveSent = DateTime.MinValue;

    public ScreenShareView(
        string shareOwnerName,
        string sessionId,
        ScreenShareClient client,
        int initialWidth,
        int initialHeight)
    {
        InitializeComponent();
        _client = client;
        SessionId = sessionId;
        Title = $"{shareOwnerName} 的屏幕共享";
        InitResolutionOptions(initialWidth, initialHeight);
        _client.FrameReceived += ClientFrameReceived;
        Loaded += ScreenShareView_Loaded;
    }

    public string SessionId { get; }

    public event Action? RemoteControlRequested;

    public event Action? RemoteControlReleased;

    public event Action<ScreenControlInputEvent>? RemoteControlInput;

    public event Action<ScreenShareResolutionOption>? ResolutionChanged;

    private void InitResolutionOptions(int initialWidth, int initialHeight)
    {
        ResolutionComboBox.ItemsSource = _resolutionOptions;
        var selected = _resolutionOptions.FirstOrDefault(option =>
            option.Width == initialWidth && option.Height == initialHeight)
            ?? _resolutionOptions[1];
        ResolutionComboBox.SelectedItem = selected;
        _isResolutionInitialized = true;
    }

    public void GrantRemoteControl()
    {
        _isControlRequestPending = false;
        _hasRemoteControl = true;
        ControlButton.IsEnabled = true;
        ControlButton.Content = "停止控制";
        ScreenImage.Focus();
        ShowStatus("已获得远程控制权");
    }

    public void RejectRemoteControl()
    {
        _isControlRequestPending = false;
        _hasRemoteControl = false;
        ControlButton.IsEnabled = true;
        ControlButton.Content = "请求控制";
        ShowStatus("对方拒绝了远程控制请求");
    }

    public void ReleaseRemoteControl()
    {
        _isControlRequestPending = false;
        _hasRemoteControl = false;
        ControlButton.IsEnabled = true;
        ControlButton.Content = "请求控制";
    }

    public void RevokeRemoteControl()
    {
        ReleaseRemoteControl();
        ShowStatus("对方已断开远程控制");
    }

    private async void ScreenShareView_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _client.StartAsync(_cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (!_cts.IsCancellationRequested)
            {
                ShowStatus($"屏幕共享已断开：{ex.Message}");
            }
        }
    }

    private void ClientFrameReceived(byte[] frame)
    {
        try
        {
            Dispatcher.BeginInvoke(() =>
            {
                ScreenImage.Source = CreateBitmapImage(frame);
                if (!_hasFirstFrame)
                {
                    StatusBorder.Visibility = Visibility.Collapsed;
                    _hasFirstFrame = true;
                }
            });
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static BitmapImage CreateBitmapImage(byte[] frame)
    {
        using var stream = new MemoryStream(frame);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private void ShowStatus(string message)
    {
        try
        {
            Dispatcher.BeginInvoke(() =>
            {
                StatusText.Text = message;
                StatusBorder.Visibility = Visibility.Visible;
            });
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ControlButton_Click(object sender, RoutedEventArgs e)
    {
        if (_hasRemoteControl)
        {
            RemoteControlReleased?.Invoke();
            ReleaseRemoteControl();
            ShowStatus("已停止远程控制");
            return;
        }

        if (_isControlRequestPending)
        {
            return;
        }

        _isControlRequestPending = true;
        ControlButton.IsEnabled = false;
        ControlButton.Content = "等待同意";
        ShowStatus("已发送远程控制请求");
        RemoteControlRequested?.Invoke();
    }

    private void ResolutionComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_isResolutionInitialized || ResolutionComboBox.SelectedItem is not ScreenShareResolutionOption resolution)
        {
            return;
        }

        ShowStatus($"正在切换到 {resolution.Name}");
        ResolutionChanged?.Invoke(resolution);
    }

    private void ScreenImage_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_hasRemoteControl || DateTime.Now - _lastMouseMoveSent < TimeSpan.FromMilliseconds(35))
        {
            return;
        }

        if (TryCreatePointerInput(MqttContent.SCREEN_CONTROL_MOUSE_MOVE, e, out var input))
        {
            _lastMouseMoveSent = DateTime.Now;
            RemoteControlInput?.Invoke(input);
        }
    }

    private void ScreenImage_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_hasRemoteControl)
        {
            return;
        }

        ScreenImage.Focus();
        if (TryCreatePointerInput(MqttContent.SCREEN_CONTROL_MOUSE_DOWN, e, out var input))
        {
            input.MouseButton = ToMouseButton(e.ChangedButton);
            RemoteControlInput?.Invoke(input);
            e.Handled = true;
        }
    }

    private void ScreenImage_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_hasRemoteControl)
        {
            return;
        }

        if (TryCreatePointerInput(MqttContent.SCREEN_CONTROL_MOUSE_UP, e, out var input))
        {
            input.MouseButton = ToMouseButton(e.ChangedButton);
            RemoteControlInput?.Invoke(input);
            e.Handled = true;
        }
    }

    private void ScreenImage_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!_hasRemoteControl)
        {
            return;
        }

        if (TryCreatePointerInput(MqttContent.SCREEN_CONTROL_MOUSE_WHEEL, e, out var input))
        {
            input.Delta = e.Delta;
            RemoteControlInput?.Invoke(input);
            e.Handled = true;
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        SendKeyInput(MqttContent.SCREEN_CONTROL_KEY_DOWN, e);
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        SendKeyInput(MqttContent.SCREEN_CONTROL_KEY_UP, e);
    }

    private void SendKeyInput(string eventType, KeyEventArgs e)
    {
        if (!_hasRemoteControl)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey <= 0)
        {
            return;
        }

        RemoteControlInput?.Invoke(new ScreenControlInputEvent
        {
            EventType = eventType,
            Key = virtualKey
        });
        e.Handled = true;
    }

    private bool TryCreatePointerInput(string eventType, MouseEventArgs e, out ScreenControlInputEvent input)
    {
        input = new ScreenControlInputEvent { EventType = eventType };
        if (ScreenImage.Source is not BitmapSource bitmapSource
            || ScreenImage.ActualWidth <= 0
            || ScreenImage.ActualHeight <= 0)
        {
            return false;
        }

        var point = e.GetPosition(ScreenImage);
        var scale = Math.Min(ScreenImage.ActualWidth / bitmapSource.PixelWidth, ScreenImage.ActualHeight / bitmapSource.PixelHeight);
        var displayWidth = bitmapSource.PixelWidth * scale;
        var displayHeight = bitmapSource.PixelHeight * scale;
        var offsetX = (ScreenImage.ActualWidth - displayWidth) / 2;
        var offsetY = (ScreenImage.ActualHeight - displayHeight) / 2;
        var x = (point.X - offsetX) / displayWidth;
        var y = (point.Y - offsetY) / displayHeight;

        if (x < 0 || x > 1 || y < 0 || y > 1)
        {
            return false;
        }

        input = new ScreenControlInputEvent
        {
            EventType = eventType,
            X = x,
            Y = y
        };
        return true;
    }

    private static int ToMouseButton(MouseButton button)
    {
        return button switch
        {
            MouseButton.Right => 1,
            MouseButton.Middle => 2,
            _ => 0
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_hasRemoteControl || _isControlRequestPending)
        {
            RemoteControlReleased?.Invoke();
        }

        _cts.Cancel();
        _client.FrameReceived -= ClientFrameReceived;
        _client.Dispose();
        _cts.Dispose();
        base.OnClosed(e);
    }
}
