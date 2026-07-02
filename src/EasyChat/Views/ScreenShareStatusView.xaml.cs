using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace EasyChat.Views;

public partial class ScreenShareStatusView
{
    private readonly Brush _accentBrush;

    public ScreenShareStatusView(string resolutionName)
    {
        InitializeComponent();
        _accentBrush = CreateAccentBrush();
        AccentDot.Fill = _accentBrush;
        UpdateResolution(resolutionName);
    }

    public event Action? StopShareRequested;

    public event Action? DisconnectControlRequested;

    public void UpdateResolution(string resolutionName)
    {
        ResolutionText.Text = string.IsNullOrWhiteSpace(resolutionName) ? "" : resolutionName;
    }

    public void UpdateController(string controllerName)
    {
        var hasController = !string.IsNullOrWhiteSpace(controllerName);
        ControllerText.Text = hasController ? $"{controllerName} 正在控制" : "无人控制";
        DisconnectControlButton.IsEnabled = hasController;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Left = SystemParameters.VirtualScreenLeft + (SystemParameters.VirtualScreenWidth - ActualWidth) / 2;
        Top = SystemParameters.VirtualScreenTop + 12;
    }

    private static Brush CreateAccentBrush()
    {
        var color = SystemParameters.WindowGlassColor;
        if (color.A < 32)
        {
            color = Color.FromRgb(34, 211, 238);
        }

        return new SolidColorBrush(color);
    }

    private void StopShareButton_Click(object sender, RoutedEventArgs e)
    {
        StopShareRequested?.Invoke();
    }

    private void DisconnectControlButton_Click(object sender, RoutedEventArgs e)
    {
        DisconnectControlRequested?.Invoke();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
