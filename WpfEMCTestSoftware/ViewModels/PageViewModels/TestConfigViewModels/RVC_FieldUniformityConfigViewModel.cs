using ClassLibrary.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using WpfEMCTestSoftware.Enums;
using WpfEMCTestSoftware.Models.EventMessages;
using WpfEMCTestSoftware.Models.TestModels;
using WpfEMCTestSoftware.ViewModels.PageViewModels.HardwareViewModels;
using WpfEMCTestSoftware.Views.PageViews.HardwareViews;

namespace WpfEMCTestSoftware.ViewModels.PageViewModels.TestConfigViewModels
{
    public partial class RVC_FieldUniformityConfigViewModel : BaseTestBaseViewModel
    {
        public RVC_FieldUniformityConfigViewModel()
        {

        }
        //位置点测试状态
        [ObservableProperty]
        private ObservableCollection<ChannelStatus> _channelStates = new()
        {
            ChannelStatus.Idle,
            ChannelStatus.Idle,
            ChannelStatus.Idle,
            ChannelStatus.Idle,
            ChannelStatus.Idle,
            ChannelStatus.Idle,
            ChannelStatus.Idle,
            ChannelStatus.Idle,
            ChannelStatus.Idle
        };

        [ObservableProperty]
        public ObservableCollection<int> _currentPointList = new ObservableCollection<int>();

        //混响室场均匀性配置页面数据模型
        [ObservableProperty]
        private RVC_FieldUniformityConfigModel _rVCFieldUniformityConfigModel = new();

        private DataTable ConfigDataTable;

        //当前测试频段
        [ObservableProperty]
        private RVC_FUFreBand _curRVCFreBand = new();
        //当前绑定的硬件页面
        [ObservableProperty]
        private Page _curHardPage;

        [RelayCommand]
        void OnFirstLoadPage()
        {
            ChannelStates[0] = ChannelStatus.Idle;
            RVC_HardwarePage HardPage = new RVC_HardwarePage();
            CurHardPage = HardPage;
        }

        [RelayCommand]
        void SelectPositionPoint()
        {
            CurrentPointList.Clear();
            for (int i = 1; i <= RVCFieldUniformityConfigModel?.MinPositionPoints; i++)
            {
                CurrentPointList.Add(i);
            }
        }
        /// <summary>
        /// 添加频段
        /// 页面绑定 hardvm.CurRVCHardModel → 数据是硬件 VM 的 → 应该硬件 VM 把值同步给频段(错误)
        /// </summary>
        /// <param name="obj"></param>
        [RelayCommand]
        private void OnAddFreqBand(object obj)
        {
            //深克隆当前选择频段
            var newRvcBand = GenericHelper.DeepClone<RVC_FUFreBand>(CurRVCFreBand);
            RVCFieldUniformityConfigModel?.ListFreBand.Add(newRvcBand);
            if (CurHardPage.DataContext is RVC_HardwareViewModel hardvm)
            {
                hardvm.CurRVCHardModel = newRvcBand.RVCHardModel;
            }
            //新增后自动选择新频段
            CurRVCFreBand = newRvcBand;
        }
        [RelayCommand]
        private void OnSelectionChanged()
        {
            if (CurHardPage.DataContext is RVC_HardwareViewModel hardvm)
            {
                hardvm.CurRVCHardModel = CurRVCFreBand.RVCHardModel;
            }
        }

        /// <summary>
        /// 删除频段
        /// </summary>
        /// <param name="obj"></param>
        [RelayCommand]
        private void OnDelFreqBand(object obj)
        {
            if ((RVCFieldUniformityConfigModel.ListFreBand != null) && (RVCFieldUniformityConfigModel.ListFreBand.Count > 0))
            {
                RVCFieldUniformityConfigModel.ListFreBand.Remove(CurRVCFreBand);
                CurRVCFreBand = new RVC_FUFreBand();
            }
        }
        [ObservableProperty]
        private int _currentPoint = 1;

        [RelayCommand]
        private void OnCurrentSelectPoint()
        {

            if (RVCFieldUniformityConfigModel.CurrentTestPoint > 0)
                CurrentPoint = RVCFieldUniformityConfigModel.CurrentTestPoint;
        }

        //导入已有的配置
        [RelayCommand]
        private void OnImportConfig()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "请选择配置文件",
                InitialDirectory = Path.Combine(GenericParameter.SysPath, "System", "均匀性验证", "配置文件"),
                Filter = "配置文件|*.rvcuniform",
            };
            if (openFileDialog.ShowDialog() != true)
                return;
            var json = File.ReadAllText(openFileDialog.FileName);
            var config = JsonSerializer.Deserialize<RVC_FieldUniformityConfigModel>
            (json, new JsonSerializerOptions
            {
                IncludeFields = true
            });
            if (config == null)
                return;
            RVCFieldUniformityConfigModel = config;

        }
        //保存配置
        [RelayCommand]
        private void OnSaveConfig()
        {
            if (System.Windows.Application.Current?.Dispatcher.HasShutdownStarted == true)
                return;
            string json = JsonSerializer.Serialize(RVCFieldUniformityConfigModel, new JsonSerializerOptions
            {
                WriteIndented = true, // 格式化，好看
                IncludeFields = false  // 支持私有字段
            });
            string configBaseDir = Path.Combine(GenericParameter.SysPath, "System", "均匀性验证", "配置文件");
            if (!Directory.Exists(configBaseDir))
            {
                Directory.CreateDirectory(configBaseDir);
            }
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = configBaseDir,
                Title = "保存配置",
                FileName = "新建配置",     // 默认文件名
                DefaultExt = ".rvcuniform",
                Filter = "配置文件|*.rvcuniform|所有文件|*.*"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                string fullfilePath = saveFileDialog.FileName;
                if (!File.Exists(fullfilePath))
                {
                    // 只获取文件名（不含路径）
                    string fileNameOnly = Path.GetFileName(fullfilePath);
                    Messenger.Send(new UniversalStrMessage(fileNameOnly), 201);
                }
                File.WriteAllText(fullfilePath, json);
                MessageBox.Show("配置保存成功！", "提示");
            }

        }
        //检查配置
        [RelayCommand]
        private void OnCheckConfig()
        {

        }
        //进入测试
        [RelayCommand]
        private void OnEnterTest()
        {
            //直接发送场均匀性复合消息给主页面
            //主页面接收槽函数里切换测试页面
            RVCCompositeConfig config = new RVCCompositeConfig(RVCFieldUniformityConfigModel, ConfigDataTable);
            Messenger.Send(new RVCCompositeConfigMessage(config), (int)IntMessageToken.UnifComposite);
        }
        //退出
        [RelayCommand]
        private void OnExit()
        {
            Messenger.Send(new ExitClickEventMessage(0xff), (int)PageExitMessageToken.SubPageToMainWindow);
        }


    }
}
