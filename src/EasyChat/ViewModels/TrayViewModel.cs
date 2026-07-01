using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyChat.Handle;

namespace EasyChat.ViewModels;

public partial class TrayViewModel : ObservableObject
{
    private DispatcherTimer _blinkTimer;
    private EventHelper evt =  EventHelper.Instance;
    private bool _isIconVisible = true;
    private readonly ImageSource _favicon = new BitmapImage(new Uri("pack://application:,,,/Resources/favicon.ico"));
    private readonly ImageSource _favicon_new = new BitmapImage(new Uri("pack://application:,,,/Resources/favicon_new.ico"));
    private readonly ImageSource _favicon_null = new BitmapImage(new Uri("pack://application:,,,/Resources/favicon_null.ico"));

    public TrayViewModel()
    {
        // 初始化定时器
        _blinkTimer = new DispatcherTimer();
        _blinkTimer.Interval = TimeSpan.FromMilliseconds(500); // 每500ms切换一次
        _blinkTimer.Tick += (sender, e) => ToggleIcon();
        evt.StartBlinkEvent += StartBlinking;
        evt.StopBlinkEvent += StopBlinking;
        evt.UnreadPreviewChanged += ChangeUnreadPreview;
    }

    [ObservableProperty] private ImageSource _trayIconSource = new BitmapImage(new Uri("pack://application:,,,/Resources/favicon.ico"));

    [ObservableProperty] private string _trayToolTip = "EasyChat";

    private void StartBlinking()
    {
        _blinkTimer.Start();
    }

    private void StopBlinking()
    {
        _blinkTimer.Stop();
        TrayIconSource = _favicon;
    }

    private void ToggleIcon()
    {
        if (_isIconVisible)
        {
            TrayIconSource = _favicon_null;
        }
        else
        {
            TrayIconSource = _favicon_new;
        }

        _isIconVisible = !_isIconVisible;
    }

    private void ChangeUnreadPreview(string preview)
    {
        TrayToolTip = string.IsNullOrWhiteSpace(preview) ? "EasyChat" : preview;
    }

    [RelayCommand]
    private void Open(Window window)
    {
        window.Show();
        window.Activate();
    }

    [RelayCommand]
    private void Hide(Window window)
    {
        window.Hide();
    }
    [RelayCommand]
    private void ClearNewMessage()
    {
        evt.ClearNewMessageCount();
    }
    [RelayCommand]
    private void Exit()
    {
        try
        {
            Application.Current.Shutdown();
        }
        catch
        {
            Environment.Exit(-1);
        }
    }
}
