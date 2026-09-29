using System.Windows;
using System.Windows.Controls;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Models;
using WpfEMCTestSoftware.ViewModels;

namespace WpfEMCTestSoftware.Views
{
    /// <summary>
    /// LimitConfigPage.xaml 的交互逻辑
    /// </summary>
    public partial class LimitConfigPage : Page
    {
        public LimitConfigPage()
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
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            object n = e.OldValue;
            object m = e.NewValue;
            if (m == null) return;
            ((LimitConfigViewModel)DataContext).SelectedTreeItem((TreeItem)m);
        }


    }
}
