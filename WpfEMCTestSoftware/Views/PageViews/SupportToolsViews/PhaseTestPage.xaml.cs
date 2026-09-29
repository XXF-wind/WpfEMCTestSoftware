using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfEMCTestSoftware.Core;

namespace WpfEMCTestSoftware.Views
{
    /// <summary>
    /// PhaseTestPage.xaml 的交互逻辑
    /// </summary>
    public partial class PhaseTestPage : Page
    {
        public PhaseTestPage()
        {
            InitializeComponent();
            Unloaded += BasePage_Unloaded;
        }

        private void BasePage_Unloaded(object sender, RoutedEventArgs e)
        {
            // 释放当前页面资源
            ReleasePageResources();

            // 释放ViewModel资源
            if (DataContext is IReleaseable releaseableVm)
            {
                releaseableVm.Release();
            }

            // 解除事件绑定（避免内存泄漏）
            Unloaded -= BasePage_Unloaded;
        }

        /// <summary>
        /// 仅允许输入数字（循环次数输入框）
        /// </summary>
        private void NumericPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !int.TryParse(e.Text, out _);
        }

        /// <summary>
        /// 允许输入数字和小数点（频率输入框）
        /// </summary>
        private void DecimalPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) { e.Handled = true; return; }
            e.Handled = !(double.TryParse(e.Text, out _) || (e.Text == "." && !tb.Text.Contains('.')));
        }

        /// <summary>
        /// 子类重写：释放页面自身资源（如定时器、事件、图片等）
        /// </summary>
        protected virtual void ReleasePageResources()
        {

        }
    }
}
