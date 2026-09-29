using System.Windows;
using System.Windows.Controls;
using WpfEMCTestSoftware.Core;

namespace WpfEMCTestSoftware.Views
{
    /// <summary>
    /// AmpifierParaPage.xaml 的交互逻辑
    /// </summary>
    public partial class AmpifierParaPage : Page
    {
        public AmpifierParaPage()
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
        /// 子类重写：释放页面自身资源（如定时器、事件、图片等）
        /// </summary>
        protected virtual void ReleasePageResources()
        {
            // 示例：清理页面中的定时器、网络连接、图片资源等
            // if (_timer != null) { _timer.Stop(); _timer.Dispose(); }
        }
    }
}
