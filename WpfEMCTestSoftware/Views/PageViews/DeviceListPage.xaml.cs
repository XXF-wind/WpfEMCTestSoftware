using System.Windows;
using System.Windows.Controls;
using WpfEMCTestSoftware.Models;
using WpfEMCTestSoftware.ViewModels;
namespace WpfEMCTestSoftware.Views
{
    /// <summary>
    /// DeviceList.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceListPage : Page
    {
        public DeviceListPage()
        {
            InitializeComponent();
            Unloaded += BasePage_Unloaded;
        }
        /// <summary>
        /// 页面卸载事件处理程序
        /// 在页面从可视化树移除时调用，用于执行资源释放和清理操作
        /// </summary>
        /// <param name="sender">事件源，即DeviceListPage页面实例</param>
        /// <param name="e">路由事件参数，包含与事件相关的信息</param>
        /// <remarks>
        /// 此方法的主要目的：
        /// 1. 防止内存泄漏 - 及时释放页面占用的资源
        /// 2. 清理ViewModel状态 - 确保ViewModel可以正确重置
        /// 3. 断开事件绑定 - 避免事件持有页面引用导致无法垃圾回收
        /// 4. 释放非托管资源 - 如文件句柄、数据库连接等
        private void BasePage_Unloaded(object sender, RoutedEventArgs e)
        {
            // 步骤1：释放当前页面占用的UI资源
            ReleasePageResources();
            //步骤2：清理ViewModel的状态和资源
            if (DataContext is DeviceListViewModel deviceListViewModel)
            {
                // 调用ViewModel的退出方法，执行业务逻辑清理
                // 例如：停止设备扫描、断开设备连接、保存状态等
                deviceListViewModel.updateDeviceList();
                // 调用ViewModel的资源释放方法
                // 释放ViewModel持有的非托管资源或事件订阅
                deviceListViewModel.Release();
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
            ((DeviceListViewModel)DataContext).SelectedTreeItem((TreeItem)m);
        }

        /// <summary>
        /// DataGrid右键菜单打开前：选中鼠标所在行
        /// 否则右键菜单操作的是"上次左键选中的行"（或空设备），重命名/删除/复制会作用到错误的设备上
        /// </summary>
        private void DeviceGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source)
            {
                DataGridRow? row = FindAncestor<DataGridRow>(source);
                if (row != null)
                {
                    row.IsSelected = true;
                }
            }
        }

        /// <summary>
        /// 沿可视化树向上查找指定类型的祖先元素
        /// </summary>
        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T target) return target;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
