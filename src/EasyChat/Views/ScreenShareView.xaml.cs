using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using EasyChat.Service;

namespace EasyChat.Views;

public partial class ScreenShareView
{
    private readonly ScreenShareClient _client;
    private readonly CancellationTokenSource _cts = new();

    public ScreenShareView(string shareOwnerName, string sessionId, ScreenShareClient client)
    {
        InitializeComponent();
        _client = client;
        SessionId = sessionId;
        Title = $"{shareOwnerName} 的屏幕共享";
        _client.FrameReceived += ClientFrameReceived;
        Loaded += ScreenShareView_Loaded;
    }

    public string SessionId { get; }

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
                StatusBorder.Visibility = Visibility.Collapsed;
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

    protected override void OnClosed(EventArgs e)
    {
        _cts.Cancel();
        _client.FrameReceived -= ClientFrameReceived;
        _client.Dispose();
        _cts.Dispose();
        base.OnClosed(e);
    }
}
