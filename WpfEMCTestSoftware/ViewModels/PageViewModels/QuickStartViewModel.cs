using CommunityToolkit.Mvvm.Input;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Enums;
using WpfEMCTestSoftware.Models.EventMessages;

namespace WpfEMCTestSoftware.ViewModels
{
    public partial class QuickStartViewModel : BaseViewModel
    {
        /// <summary>
        /// 给主页面发送消息，page跳转到运行页面，并传递当前的测试环境名称“混响室”
        /// </summary>
        /// <param name="obj"></param>
        [RelayCommand]
        private void ItemIconClick(object obj)
        {
            if (obj != null)
                Messenger.Send(new RunConfigEventMessage(obj.ToString()), (int)IntMessageToken.SwithToRunConfigPage);
        }

        [RelayCommand]
        private void OnswitchPage(object index)
        {
            string strIndex = index?.ToString() ?? "0";
            Messenger.Send(new UniversalIntMessage(int.Parse(strIndex)), (int)IntMessageToken.SwitchPage);
        }
    }
}
