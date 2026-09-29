using System.Windows.Controls;
using System.Windows.Input;

namespace WpfEMCTestSoftware.Views.PageViews.TestConfigViews
{
    /// <summary>
    /// EMS_CS114ConfigPage.xaml 的交互逻辑
    /// </summary>
    public partial class EMS_CS114ConfigPage : Page
    {
        public EMS_CS114ConfigPage()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 小数点的"输入法兼容"处理（实现统一放在 ImeInputHelper，弹窗 CS114CoeffDialog /
        /// CS114LimitImportDialog 也挂在各自的 Window 上）。
        /// 本页所有数值框都是 MahApps 的 NumericUpDown，它内部会用当前区域性（Culture）校验每个字符，
        /// 只认半角小数点（NumberDecimalSeparator = "."），**不认识的字符会被静默丢弃**（不是显示成全角，而是根本不进去）。
        /// 中文输入法在"中文标点"状态下，主键盘的小数点键打出的是全角"。"，因此表现为"小数点输不进去"。
        /// 这里在隧道（Preview）阶段先把全角转成半角，再交给控件自己的校验。
        /// 只处理含全角字符的输入，正常半角输入原样放行，不影响其它行为。
        /// </summary>
        private void Page_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Generic.ImeInputHelper.HandlePreviewTextInput(e);
        }

        /// <summary>
        /// 小数点按键级兜底：主键盘句点/小键盘点键直接拦截并手动插入 "."，
        /// 不依赖输入法状态与 NumericUpDown 内部校验（详见 ImeInputHelper）。
        /// </summary>
        private void Page_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Generic.ImeInputHelper.HandlePreviewKeyDown(e);
        }
    }
}
