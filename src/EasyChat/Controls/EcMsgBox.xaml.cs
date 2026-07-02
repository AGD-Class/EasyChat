using System.Windows;
using System.Windows.Input;

namespace EasyChat.Controls;

public partial class EcMsgBox
{
    public EcMsgBox(string message) : this(message, "好(*╹▽╹*)~", null)
    {
    }

    private EcMsgBox(string message, string okText, string? cancelText)
    {
        InitializeComponent();
        MessageTextBlock.Text = message;
        OkButton.Content = okText;

        if (!string.IsNullOrWhiteSpace(cancelText))
        {
            CancelButton.Content = cancelText;
            CancelButton.Visibility = Visibility.Visible;
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    public static void Show(string message)
    {
        var messageBox = new EcMsgBox(message);
        SetOwner(messageBox);
        messageBox.ShowDialog();
    }

    public static bool Confirm(string message, string okText = "确定", string cancelText = "取消")
    {
        var messageBox = new EcMsgBox(message, okText, cancelText);
        SetOwner(messageBox);
        return messageBox.ShowDialog() == true;
    }

    private static void SetOwner(Window messageBox)
    {
        var owner = Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive);
        if (owner != null && owner != messageBox)
        {
            messageBox.Owner = owner;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
