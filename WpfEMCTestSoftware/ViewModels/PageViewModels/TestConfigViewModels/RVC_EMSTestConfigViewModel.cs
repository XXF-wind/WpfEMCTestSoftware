using ClassLibrary.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using WpfEMCTestSoftware.Enums;
using WpfEMCTestSoftware.Models;
using WpfEMCTestSoftware.Models.EventMessages;
using WpfEMCTestSoftware.Models.TestModels;
using WpfEMCTestSoftware.ViewModels.PageViewModels.HardwareViewModels;
using WpfEMCTestSoftware.Views.PageViews.HardwareViews;

namespace WpfEMCTestSoftware.ViewModels.PageViewModels.TestConfigViewModels
{
    public partial class RVC_EMSTestConfigViewModel : BaseTestBaseViewModel
    {
        public RVC_EMSTestConfigViewModel()
        {

        }

        //混响室快速测试数据模型
        [ObservableProperty]
        private RVC_EMSTestConfigModel _rVCEMSTestModel = new RVC_EMSTestConfigModel();
        //当前选中/正在编辑的测试频段条目（包含起止频率、步进、场强限值等单段频段参数）
        [ObservableProperty]
        private RVC_EMSFreBand _curRVCEMSFreBand = new RVC_EMSFreBand();
        /// <summary>
        /// 存储标志
        /// </summary>     
        public string SaveFlag = string.Empty;
        /// <summary>
        /// 当前测试环境名称:混响室或者暗室
        /// </summary>
        [ObservableProperty]
        private string _curTestEnvName;
        /// <summary>
        /// 当前测试配置路径
        /// </summary>
        [ObservableProperty]
        private string _alreadyTestEnvPath = "";
        //当前硬件配置页面
        [ObservableProperty]
        private Page _curHardPage;

        [RelayCommand]
        void OnFirstLoadPage()
        {
            RVC_HardwarePage HardPage = new RVC_HardwarePage();
            CurHardPage = HardPage;
        }
        [RelayCommand]
        void OnPageUnloaded()
        {

        }

        [RelayCommand]
        private void OnAddFreqBand()
        {
            var newFreBand = GenericHelper.DeepClone<RVC_EMSFreBand>(CurRVCEMSFreBand);
            RVCEMSTestModel.ListRvcFreBands.Add(newFreBand);
            if (CurHardPage.DataContext is RVC_HardwareViewModel hardvm)
            {
                hardvm.CurRVCHardModel = CurRVCEMSFreBand.RVCHardModel;
            }
            CurRVCEMSFreBand = newFreBand;
        }

        [RelayCommand]
        private void OnDelFreqBand()
        {
            if ((RVCEMSTestModel.ListRvcFreBands != null) && (RVCEMSTestModel.ListRvcFreBands.Count > 0))
            {
                RVCEMSTestModel.ListRvcFreBands.Remove(CurRVCEMSFreBand);
                CurRVCEMSFreBand = new RVC_EMSFreBand();
            }
        }

        [RelayCommand]
        private void OnSelectionChanged()
        {
            if (CurRVCEMSFreBand == null)
                return;
            if (CurHardPage.DataContext is RVC_HardwareViewModel hardvm)
            {
                hardvm.CurRVCHardModel = CurRVCEMSFreBand.RVCHardModel;
            }
        }

        private void WriteAsFile(string json)
        {
            if (System.Windows.Application.Current.Dispatcher.HasShutdownStarted) return;

            string baseDir = Path.Combine(GenericParameter.SysPath, "System", "测试模板", CurTestEnvName);
            if (!Directory.Exists(baseDir))
            {   //递归创建所有不存在的文件夹
                Directory.CreateDirectory(baseDir);
            }
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = baseDir,   // 默认打开到你的环境目录
                Title = "保存测试配置",
                FileName = "新建测试配置",     // 默认文件名
                DefaultExt = ".rvcems",
                Filter = "配置文件|*.rvcems|所有文件|*.*"
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
                //记住本次保存的模板，返回运行配置页时自动选中
                RunConfigViewModel.RememberLastTemplate(CurTestEnvName, Path.GetFileName(fullfilePath));
                MessageBox.Show("配置保存成功！", "提示");
            }
        }

        /// <summary>
        /// 保存整个配置模板
        /// </summary>
        /// <param name="recipient"></param>
        /// <param name="message"></param>
        private void OnSaveConfig(string SaveFlag)
        {
            if (RVCEMSTestModel != null)
            {
                // 1.按【设备类型+设备名称】匹配数据库中存放的仪器
                MatchDatabaseDevice();
                //2.根据标准判断仪器是否合理选择
                if (!CheckDeviceConfigByStandard())
                {
                    MessageBox.Show("未按标准配置设备");
                    return;
                }

                string json = JsonSerializer.Serialize(RVCEMSTestModel, new JsonSerializerOptions
                {
                    WriteIndented = true, // 格式化，好看
                    IncludeFields = false  // 支持私有字段
                });
                if (SaveFlag == "SaveAs")
                {
                    WriteAsFile(json);
                }
                else if (SaveFlag == "Save")
                {
                    if (AlreadyTestEnvPath != "")
                    {
                        File.WriteAllText(AlreadyTestEnvPath, json);
                        //记住本次保存的模板，返回运行配置页时自动选中
                        RunConfigViewModel.RememberLastTemplate(CurTestEnvName, Path.GetFileName(AlreadyTestEnvPath));
                        MessageBox.Show("配置保存成功！", "提示");
                    }
                    else
                    {
                        WriteAsFile(json);
                    }
                }
            }
        }
        /// <summary>
        /// 按【设备类型+设备名称】精确匹配
        /// </summary>
        private void MatchDatabaseDevice()
        {
            foreach (var config in RVCEMSTestModel.ListRvcFreBands)
            {


                for (int i = 0; i < config.RVCHardModel.ListDeviceInfos.Count; i++)
                {
                    string name = config.RVCHardModel.ListDeviceInfos[i].Name;
                    if (string.IsNullOrEmpty(config.RVCHardModel.ListDeviceInfos[i].Name))
                        continue;
                    else if (name != "No Device")
                        config.RVCHardModel.ListDeviceInfos[i] = GenericHelper.globalDevices.FirstOrDefault(d => d.Name == config.RVCHardModel.ListDeviceInfos[i].Name);
                }
            }

        }
        private bool IsDeviceValid(DeviceInfo deviceInfo)
        {
            if (deviceInfo == null)
                return false;

            return !string.IsNullOrEmpty(deviceInfo.Name) && deviceInfo.Name != "No Device";
        }
        private bool CheckDeviceConfigByStandard()
        {

            // 空值判断
            if (RVCEMSTestModel == null || RVCEMSTestModel.ListRvcFreBands.Count == 0)
            {
                MessageBox.Show("频段配置信息为空，请先配置频段");
                return false;
            }

            // 错误信息集合
            List<string> errorMessages = new List<string>();

            // DO-160G 校验
            if (RVCEMSTestModel.CurEMSStandard == EMCStandard.DO_160G)
            {
                var configList = RVCEMSTestModel.ListRvcFreBands;

                if (RVCEMSTestModel.CurTestStageType == TestStageType.CalibrationTest)
                {
                    // 预校准：检查 信号源(0)、场强(3)、前向功率(4)
                    int index = 0;
                    foreach (var config in configList)
                    {
                        index++;
                        if (!IsDeviceValid(config.RVCHardModel.ListDeviceInfos[0]))
                            errorMessages.Add($"测试频段【{index}】信号源未配置");

                        if (!IsDeviceValid(config.RVCHardModel.ListDeviceInfos[3]))
                            errorMessages.Add($"测试频段【{index}】场强探头未配置");

                        if (!IsDeviceValid(config.RVCHardModel.ListDeviceInfos[5]))
                            errorMessages.Add($"测试频段【{index}】前向功率计未配置");
                    }
                }
                else if (RVCEMSTestModel.CurTestStageType == TestStageType.FormalTest)
                {
                    // 正式测试：检查 信号源(0)、前向功率(4)
                    int index = 0;
                    foreach (var config in configList)
                    {
                        index++;
                        if (!IsDeviceValid(config.RVCHardModel.ListDeviceInfos[0]))
                            errorMessages.Add($"测试频段【{index}】信号源未配置");

                        if (!IsDeviceValid(config.RVCHardModel.ListDeviceInfos[5]))
                            errorMessages.Add($"测试频段【{index}】前向功率计未配置");
                    }
                }
            }

            // 如果有错误 → 统一弹出
            if (errorMessages.Count > 0)
            {
                string msg = "设备配置不完整，请检查以下项：\n\n" + string.Join("\n", errorMessages);
                MessageBox.Show(msg, "配置校验失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        //另存配置文件
        [RelayCommand]
        private void OnSaveAsTestConfig()
        {
            OnSaveConfig("SaveAs");
        }
        //保存配置文件
        [RelayCommand]
        private void OnSaveTestConfig()
        {
            OnSaveConfig("Save");
        }
        //退出页面
        [RelayCommand]
        private void OnExitTestConfig()
        {
            Messenger.Send(new RunConfigEventMessage(null), (int)IntMessageToken.SwithToRunConfigPage);
        }
    }
}
