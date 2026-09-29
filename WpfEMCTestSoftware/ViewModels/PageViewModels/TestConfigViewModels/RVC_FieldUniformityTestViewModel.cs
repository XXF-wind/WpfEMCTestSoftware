using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Models.EventMessages;
using WpfEMCTestSoftware.Models.TestModels;
using WpfEMCTestSoftware.RunTasks;

namespace WpfEMCTestSoftware.ViewModels.PageViewModels.TestConfigViewModels
{
    public partial class RVC_FieldUniformityTestViewModel : BaseViewModel
    {
        //测试任务
        [ObservableProperty]
        private BaseRunTask _baseTasks;
        public RVC_FieldUniformityTestViewModel()
        {
            BaseTasks = new RCFURunTask();
        }

        //接收从场均配置页面传递来的数据
        public RVCCompositeConfig M_RVCCompositeConfig { get; set; }
        /// <summary>
        /// 从历史记录恢复测试与普通的暂停再继续有区别
        /// 硬件需要重连，扫频参数需要重新计算
        /// </summary>
        [RelayCommand]
        private void FirstLoadPage()
        {
            MapToRCFUParam(M_RVCCompositeConfig.Model, ((RCFURunTask)BaseTasks).m_RCFCRuntimeParam, BaseTasks.CommonFreAndHardParms);
            var configModel = M_RVCCompositeConfig.Model;
            int currentStandard = (int)configModel.CurEMSStandard;
            TempHistoryInfo historyInfo = ClsData.Instance.GetAnyHistoryTempInfo();
            // 没有任何历史临时数据，直接就绪
            if (historyInfo == null) return;
            //情况1：历史标准和当前一样
            if (historyInfo.HistoryStandard == currentStandard)
            {
                string msg = $"检测到同标准未完成测试：{historyInfo.HistoryTestName}\n" +
                    "【是】恢复上次测试\n【否】清空历史数据，新建测试";
                if (MessageBox.Show(msg, "", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    //恢复测试，回填测试名称
                    BaseTasks.TestContext.TestName = historyInfo.HistoryTestName;
                    //加载已测数据
                    BaseTasks.LoadHistoryFromDataBase(historyInfo.HistoryTestName);
                    //更新已测点位
                    BaseTasks.UpdateDataToPos();
                    //调用 StartTest()，走标准启动链路（频段定时器入口）
                    BaseTasks.StartTest();
                }
                else
                {
                    //清空该任务数据，全新开始
                    ClsData.Instance.ClearOldTemplate(historyInfo.HistoryTestName);
                    BaseTasks.TestContext.TestName = string.Empty;
                }
            }
            // =========情况2：历史标准 和 当前标准不一致=========
            else
            {
                string msg = $"临时表存在其他标准历史测试【{historyInfo.HistoryTestName}】\n" +
                             "【确定】清空所有旧数据，开始新标准测试\n【取消】保留原有数据,开始新标准测试";
                if (MessageBox.Show(msg, "", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    ClsData.Instance.ClearOldTemplate();
                    BaseTasks.TestContext.TestName = string.Empty;
                }
                // 用户取消：不清理、不初始化，数据就放那               
            }
        }
        /// <summary>
        /// 匹配混响室测试参数
        /// </summary>
        /// <param name="config"></param>
        /// <param name="param"></param>
        /// <param name="commonlist"></param>
        void MapToRCFUParam(RVC_FieldUniformityConfigModel config, RCFCRuntimeParam param, List<CommonFreAndHard> commonlist)
        {
            param.CurrentTestPoint = M_RVCCompositeConfig.Model.CurrentTestPoint;
            param.Standard = M_RVCCompositeConfig.Model.CurEMSStandard;
            param.CurReverbTestMode = M_RVCCompositeConfig.Model.AdjustmentPriority;
            param.MinPositionPoints = M_RVCCompositeConfig.Model.MinPositionPoints;
            commonlist = [.. M_RVCCompositeConfig.Model.ListFreBand.Select(item => new CommonFreAndHard {
                FreqBase = item.FreqCommon,
                HardModel = item.RVCHardModel,
            })];
        }
        /// <summary>
        /// 开始场均校准测试
        /// </summary>
        [RelayCommand]
        private void OnStartRCFUTest()
        {

        }
        /// <summary>
        /// 停止场均校准测试
        /// </summary>
        [RelayCommand]
        private void OnStopRCFUTest()
        {

        }
        /// <summary>
        /// 暂停/继续测试切换
        /// </summary>
        [RelayCommand]
        private void OnContinueRCFUTest()
        {

        }
        /// <summary>
        /// 跳转到分析页面
        /// </summary>
        [RelayCommand]
        private void OnToAnalysisPage()
        {

        }
        /// <summary>
        /// 导入数据
        /// </summary>
        [RelayCommand]
        private void OnInportData()
        {

        }
    }
}
