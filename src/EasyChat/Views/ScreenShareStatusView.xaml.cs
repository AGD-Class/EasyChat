using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EasyChat.Models;

namespace EasyChat.Views;

public partial class ScreenShareStatusView
{
    private readonly Brush _accentBrush;
    private bool _isResolutionReady;

    public ScreenShareStatusView(
        IEnumerable<ScreenShareResolutionOption> resolutions,
        ScreenShareResolutionOption selectedResolution)
    {
        InitializeComponent();
        _accentBrush = CreateAccentBrush();
        AccentDot.Fill = _accentBrush;
        InitResolutionOptions(resolutions, selectedResolution);
    }

    public event Action? StopShareRequested;

    public event Action? DisconnectControlRequested;

    public event Action<ScreenShareResolutionOption>? ResolutionChanged;

    /// <summary>
    /// 初始化共享方控制条里的分辨率选项，避免被共享方再去调整共享参数。
    /// </summary>
    private void InitResolutionOptions(
        IEnumerable<ScreenShareResolutionOption> resolutions,
        ScreenShareResolutionOption selectedResolution)
    {
        var options = resolutions.ToList();
        ResolutionComboBox.ItemsSource = options;
        ResolutionComboBox.SelectedItem = options.FirstOrDefault(option =>
            option.Width == selectedResolution.Width && option.Height == selectedResolution.Height)
            ?? options.FirstOrDefault();
        UpdateResolution((ResolutionComboBox.SelectedItem as ScreenShareResolutionOption)?.Name ?? selectedResolution.Name);
        _isResolutionReady = true;
    }

    public void UpdateResolution(string resolutionName)
    {
        ResolutionText.Text = string.IsNullOrWhiteSpace(resolutionName) ? "" : resolutionName;
    }

    public void SelectResolution(ScreenShareResolutionOption resolution)
    {
        foreach (var item in ResolutionComboBox.Items.OfType<ScreenShareResolutionOption>())
        {
            if (item.Width == resolution.Width && item.Height == resolution.Height)
            {
                ResolutionComboBox.SelectedItem = item;
                UpdateResolution(item.Name);
                return;
            }
        }

        UpdateResolution(resolution.Name);
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

    private void ResolutionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isResolutionReady || ResolutionComboBox.SelectedItem is not ScreenShareResolutionOption resolution)
        {
            return;
        }

        UpdateResolution(resolution.Name);
        ResolutionChanged?.Invoke(resolution);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
