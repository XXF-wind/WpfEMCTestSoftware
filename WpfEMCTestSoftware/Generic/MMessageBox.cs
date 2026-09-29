using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Models;
using WpfEMCTestSoftware.Views.Dialogs;

namespace WpfEMCTestSoftware.Generic
{
    public class MMessageBox : BaseViewModel
    {
        public static MMessageBox Instance { get; } = new MMessageBox();
        private MMessageBox()
        {
        }
        public void Show(DialogInfo dialog, out DialogResult Result)
        {
            AlertDialog alertDialog = new AlertDialog(dialog);
            bool? confirmed = alertDialog.ShowDialog();
            DialogResult? vmResult = alertDialog.VmResult;
            //直接读弹窗VM的结果：不再依赖消息回传（此前btnOK_Click先关窗口、命令后执行，会读到空结果）
            if (confirmed == true && vmResult != null && vmResult.IsConfirmed == true)
            {
                Result = vmResult;
            }
            else
            {
                Result = new DialogResult() { IsConfirmed = false, Data = string.Empty };
            }
        }
    }
}
