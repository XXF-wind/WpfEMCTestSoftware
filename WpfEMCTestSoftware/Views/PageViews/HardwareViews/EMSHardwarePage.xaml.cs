using System.Windows;
using System.Windows.Controls;
using WpfEMCTestSoftware.ViewModels;

namespace WpfEMCTestSoftware.Views
{
    /// <summary>
    /// EMSHardwarePage.xaml 的交互逻辑
    /// </summary>
    public partial class EMSHardwarePage : Page
    {
        public EMSHardwarePage()
        {
            InitializeComponent();
            Unloaded += BasePage_Unloaded;
        }

        private void BasePage_Unloaded(object sender, RoutedEventArgs e)
        {
            // 释放当前页面资源
            ReleasePageResources();

            //// 释放ViewModel资源
            //if (DataContext is IReleaseable releaseableVm)
            //{
            //    releaseableVm.Release();
            //}
            // 页面关闭 → 强制释放ViewModel消息
            if (DataContext is EMSHardViewModel vm)
            {
                vm.UnregisterMessages();
            }
            // 解除事件绑定（避免内存泄漏）
            Unloaded -= BasePage_Unloaded;
        }

        /// <summary>
        /// 子类重写：释放页面自身资源（如定时器、事件、图片等）
        /// </summary>
        protected virtual void ReleasePageResources()
        {
        }

    }
}
