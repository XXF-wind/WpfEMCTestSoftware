using System.Windows;
using WpfEMCTestSoftware.ViewModels;

namespace WpfEMCTestSoftware.Views
{
    /// <summary>
    /// LoginPage.xaml 的交互逻辑
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            ((LoginViewModel)DataContext).LoginResultEvent += LoginWindow_LoginResultEvent;
        }

        private void LoginWindow_LoginResultEvent(bool obj)
        {
            DialogResult = obj;
        }

        private void StatusIconTextButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
            Application.Current.Shutdown();
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}
