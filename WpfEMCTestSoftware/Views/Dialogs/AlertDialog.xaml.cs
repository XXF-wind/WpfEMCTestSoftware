using System.Windows;
using System.Windows.Input;
using WpfEMCTestSoftware.Models;

namespace WpfEMCTestSoftware.Views.Dialogs
{
    /// <summary>
    /// AlertDialog.xaml 的交互逻辑
    /// </summary>
    public partial class AlertDialog : Window
    {
        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        public AlertDialog(DialogInfo dialog)
        {
            InitializeComponent();
            if (DataContext is ViewModels.Dialogs.AlertDialogViewModel vm)
            {
                vm.Initialize(dialog.Title, dialog.Items, dialog.LabelFirst, dialog.LabelSecond, dialog.InputData);
            }
        }

        /// <summary>
        /// 弹窗结果：ShowDialog 返回后由 MMessageBox 直接读取，不再依赖 Messenger 回传时序
        /// </summary>
        public Models.DialogResult? VmResult =>
            (DataContext as ViewModels.Dialogs.AlertDialogViewModel)?.Result;

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }
    }
}
