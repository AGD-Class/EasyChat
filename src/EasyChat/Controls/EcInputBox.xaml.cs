using System.Windows;

namespace EasyChat.Controls;

public partial class EcInputBox
{
    public EcInputBox(string title, string message, string defaultValue)
    {
        InitializeComponent();
        Title = title;
        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;
        ValueTextBox.Text = defaultValue;
        ValueTextBox.SelectAll();
        ValueTextBox.Focus();
    }

    public string Value => ValueTextBox.Text;

    public static string? Show(string title, string message, string defaultValue = "")
    {
        var inputBox = new EcInputBox(title, message, defaultValue);
        var owner = Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive);
        if (owner != null && owner != inputBox)
        {
            inputBox.Owner = owner;
        }

        return inputBox.ShowDialog() == true ? inputBox.Value : null;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
