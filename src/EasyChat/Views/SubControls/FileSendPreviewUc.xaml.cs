using EasyChat.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;

namespace EasyChat.Views.SubControls
{
    /// <summary>
    /// FileSendPreviewUc.xaml 的交互逻辑
    /// </summary>
    public partial class FileSendPreviewUc : Window
    {
        public FileSendPreviewUc(List<FileModel> fileList, UserModel userModel)
        {
            InitializeComponent();
            FileList = new ObservableCollection<FileModel>(fileList);
            //uModel = userModel;
            DataContext = this;
        }
        public ObservableCollection<FileModel> FileList { get; set; }

        //[ObservableProperty]
        //private UserModel uModel = new UserModel();

        private void OnSendClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void OnCancelClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }
    }
}
