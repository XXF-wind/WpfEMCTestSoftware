using ClassLibrary;
using ClassLibrary.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Models;
using WpfEMCTestSoftware.Models.EventMessages;

namespace WpfEMCTestSoftware.ViewModels
{
    public partial class HardwareConfigViewModel : BaseViewModel
    {
        /// <summary>
        /// 树形结构数据
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<TreeItem>? _treeItems = [];

        // 当前选中的节点
        [ObservableProperty]
        private TreeItem _curTreeItem = new();

        /// <summary>
        /// 模板页
        /// </summary>
        [ObservableProperty]
        private Page? _templatePage = null;
        private readonly Dispatcher _dispatcher;

        public HardwareConfigViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            LoadTreeItems();
        }

        /// <summary>
        /// 加载硬件模板文件树节点
        /// </summary>
        private void LoadTreeItems()
        {
            string TemplatePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "HardwareTemplates");
            TreeItem? item = GenericHelper.LoadTreeItemsFromDirectory(TemplatePath);
            if (item != null)
                TreeItems?.Add(item);
        }

        /// <summary>
        /// 设备树节点选中
        /// </summary>
        /// <param name="item"></param>
        public void SelectedTreeItem(TreeItem item)
        {
            CurTreeItem = item;
            if (item.Children?.Count == 0)
            {
                TreeItem Parent = GenericHelper.FindParentNode(TreeItems!, item)!;
                if (Parent != null)
                {
                    string template = Parent.Name + "Page";
                    object? obj = GenericMethods.CreateInstanceByClassName(template, "WpfEMCTestSoftware.Views", out string msg);
                    if (obj != null)
                    {
                        ReleasePreviousPage();
                        TemplatePage = obj as Page;

                        string TransmitData = File.ReadAllText(item.FullName) ?? string.Empty;
                        Messenger.Send(new TransmitDataEventMessage(TransmitData), 0);
                    }
                    else
                    {
                        LogManager.WriteLog(msg);
                    }
                }
            }
        }

        /// <summary>
        /// 释放前一页资源
        /// </summary>
        private void ReleasePreviousPage()
        {
            if (TemplatePage == null) return;

            // 强制触发页面Unloaded事件（确保BasePage的释放逻辑执行）
            _dispatcher.Invoke(() =>
            {
                // 解除Frame的绑定（避免引用）
                if (TemplatePage.Parent is Frame frame)
                {
                    frame.Content = null;
                }
                // 手动触发Unloaded（防止未触发）
                TemplatePage.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                // 释放VM资源（双重保障）
                if (TemplatePage.DataContext is IReleaseable releaseableVm)
                {
                    releaseableVm.Release();
                }

                // 清空前一页引用（关键：让GC回收）
                TemplatePage.DataContext = null;
                TemplatePage = null;
            });

            // 强制GC（可选，加速回收）
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }


        /// <summary>
        /// 双击模板行数据
        /// </summary>
        /// <param name="obj"></param>
        [RelayCommand]
        private void DoubleTemplateData(object obj)
        {

        }

        /// <summary>
        /// 确认/保存点击命令
        /// </summary>
        [RelayCommand]
        private void OnConfirm()
        {
            Messenger.Send(new SaveClickEventMessage(true), 1);
        }

        /// <summary>
        /// 取消点击命令
        /// </summary>
        [RelayCommand]
        private void OnCencel()
        {
            Messenger.Send(new ExitClickEventMessage(-1), (int)PageExitMessageToken.SubPageToMainWindow);
        }
    }
}
