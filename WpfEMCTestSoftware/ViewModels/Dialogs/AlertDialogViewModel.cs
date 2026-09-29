using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Models;
using WpfEMCTestSoftware.Models.EventMessages;

namespace WpfEMCTestSoftware.ViewModels.Dialogs
{
    public partial class AlertDialogViewModel : BaseViewModel
    {
        /// <summary>
        /// 弹窗结果（点击确定/取消时生成，供 MMessageBox 在 ShowDialog 返回后直接读取）
        /// </summary>
        public DialogResult Result { get; private set; } = new(false);

        /// <summary>
        /// 标题
        /// </summary>
        [ObservableProperty]
        private string _title = string.Empty;
        /// <summary>
        /// 第一标签
        /// </summary>
        [ObservableProperty]
        private string _labelFirst = string.Empty;
        /// <summary>
        /// 第二标签
        /// </summary>
        [ObservableProperty]
        private string _labelSecond = string.Empty;
        /// <summary>
        /// 项
        /// </summary>
        [ObservableProperty]
        private List<string> _items = [];
        /// <summary>
        /// 选中项
        /// </summary>
        [ObservableProperty]
        private string _item = string.Empty;
        /// <summary>
        /// 输入框信息
        /// </summary>
        [ObservableProperty]
        private string? _data = string.Empty;

        public AlertDialogViewModel()
        {
        }

        /// <summary>
        /// 初始化弹窗页面
        /// </summary>
        /// <param name="title"></param>
        /// <param name="items"></param>
        /// <param name="First"></param>
        /// <param name="Second"></param>
        public void Initialize(string title, List<string> items, string First = "", string Second = "", string inputData = "")
        {
            Title = title;
            Items = items;
            if (items.Count > 0)
            {
                Item = items[0];
            }
            LabelFirst = First;
            LabelSecond = Second;
            Data = inputData;
        }

        [RelayCommand]
        private void OnDialog(object obj)
        {
            if (obj != null && obj.ToString() == "true")
            {
                Result = new DialogResult(true, Data);
            }
            else
            {
                Result = new DialogResult(false, Data);
            }
            Messenger.Send(new DialogResultEventMessage(Result), 0);
        }

    }
}
