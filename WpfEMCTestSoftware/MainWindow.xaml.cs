using System.Windows;
using System.Windows.Media;
using WpfEMCTestSoftware.Core;
namespace WpfEMCTestSoftware
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            //navigation.SetImage("pack://application:,,,/Company.UI.Controls;component/Image/Sinpel.png");
            Closing += MainWindow_Closing;
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 获取鼠标点击的位置
            Point mousePosition = e.GetPosition(this);

            // 检查鼠标点击的位置是否在用户控件之外
            Rect bounds = VisualTreeHelper.GetDescendantBounds(navigation);
            if (!bounds.Contains(mousePosition))
            {
                navigation.FoldUp();
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // 释放主VM资源
            if (DataContext is IReleaseable mainVm)
            {
                mainVm.Release();
            }

            // 清空DataContext（避免引用）
            DataContext = null;
            // 强制GC
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Application.Current.Shutdown();
        }
    }
}