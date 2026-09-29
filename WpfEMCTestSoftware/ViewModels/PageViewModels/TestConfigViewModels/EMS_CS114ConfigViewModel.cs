using ClassLibrary.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System.Windows;
using WpfEMCTestSoftware.Enums;
using WpfEMCTestSoftware.Models;
using WpfEMCTestSoftware.Models.EventMessages;
using WpfEMCTestSoftware.Models.TestModels;
using WpfEMCTestSoftware.Views.PageViews.TestConfigViews;

namespace WpfEMCTestSoftware.ViewModels.PageViewModels.TestConfigViewModels
{
    public partial class EMS_CS114ConfigViewModel : BaseTestBaseViewModel
    {
        public EMS_CS114ConfigViewModel()
        {
            //测试阶段切换 → 联动"导入校准数据"按钮的显隐（仅正式测试可见）
            HookConfigModel(CS114TestModel);
        }

        /// <summary>
        /// 模型实例被整体替换时（如 MainViewModel 回填已保存的配置），重新挂钩并刷新界面状态
        /// </summary>
        partial void OnCS114TestModelChanged(EMS_CS114ConfigModel value) => HookConfigModel(value);

        private void HookConfigModel(EMS_CS114ConfigModel? model)
        {
            if (model == null) return;
            model.PropertyChanged -= OnConfigModelPropertyChanged;   //防重复订阅
            model.PropertyChanged += OnConfigModelPropertyChanged;
            IsFormalTest = model.CurTestStageType == TestStageType.FormalTest;
            RefreshCalDataStatus();
        }

        private void OnConfigModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EMS_CS114ConfigModel.CurTestStageType))
                IsFormalTest = CS114TestModel.CurTestStageType == TestStageType.FormalTest;
        }
        /// <summary>
        /// CS114 配置模板（通用参数 + 分频段参数，每频段一份硬件模板）
        /// </summary>
        [ObservableProperty]
        private EMS_CS114ConfigModel _CS114TestModel = new EMS_CS114ConfigModel();
        /// <summary>
        /// 当前选中的测试频段（左侧硬件模板随之切换）
        /// </summary>
        [ObservableProperty]
        private EMS_CS114FreBand _curFreBand = new EMS_CS114FreBand();
        /// <summary>
        /// 系数列表当前选中行
        /// </summary>
        [ObservableProperty]
        private CS114CableLoss _curLinkCoeff = new CS114CableLoss();
        /// <summary>
        /// 调制类型下拉数据源（频段表“调制”列）
        /// </summary>
        [ObservableProperty]
        private List<object> _modList = new List<object> { "CW 连续波", "AM_1", "AM_2", "AM_3", "FM_1", "FM_2", "FM_3", "Pulse_1", "Pulse_2", "Pulse_3" };
        /// <summary>
        /// 反馈控制模式下拉数据源
        /// </summary>
        [ObservableProperty]
        private List<string> _feedbackModeList = new List<string> { "禁用", "启用前向功率", "闭环监测" };


        /// <summary>
        /// 限制值类型下拉数据源（定值/文件）
        /// </summary>
        [ObservableProperty]
        private List<object> _limitTypeList = Enum.GetValues(typeof(LimitType))
            .Cast<LimitType>()
            .Select(e => new
            {
                ShowText = e.GetDescription(),
                RealValue = e
            }).ToList<object>();

        /// <summary>
        /// 存储标志
        /// </summary>
        public string SaveFlag = string.Empty;

        /// <summary>当前是否为正式测试阶段（控制"导入校准数据"按钮显隐）</summary>
        [ObservableProperty]
        private bool _isFormalTest;

        /// <summary>校准数据状态显示（已导入点数 / 来源文件）</summary>
        [ObservableProperty]
        private string _calDataStatusText = "未导入校准数据";

        /// <summary>
        /// 导入校准功率表（仅正式测试）：选择校准阶段导出的 CSV，解析后写入配置
        /// </summary>
        [RelayCommand]
        private void OnImportCalData()
        {
            if (CS114TestModel.CurTestStageType != TestStageType.FormalTest)
            {
                MessageBox.Show("校准阶段无需导入校准数据，请先切换到\"正式测试\"。", "提示");
                return;
            }

            //校准模式跑完点【保存】后，校准文件落在 System\校准文件\CS114（CS114RunTask.ExportCalibrationData），
            //导入对话框直接落在同一目录，操作员不用到处找文件
            string calDir = Path.Combine(GenericParameter.SysPath, "System", "校准文件", "CS114");
            OpenFileDialog dlg = new OpenFileDialog
            {
                Title = "选择校准数据文件",
                Filter = "校准数据文件|*.csv|所有文件|*.*",
                InitialDirectory = Directory.Exists(calDir) ? calDir : ""
            };
            if (dlg.ShowDialog() != true) return;

            var points = ParseCalCsv(dlg.FileName);
            if (points.Count == 0)
            {
                MessageBox.Show("文件中没有有效的校准数据行（每行应为：频率MHz,前向功率W）。", "导入失败",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            //写入配置（随"保存"一起序列化进 .cs114 文件）
            CS114TestModel.CalPowerTable = points;
            CS114TestModel.CalPowerTablePath = dlg.FileName;
            RefreshCalDataStatus();
            MessageBox.Show($"校准数据导入成功：{points.Count} 个频点。", "提示");
        }

        /// <summary>解析校准 CSV：跳过表头，取每行前两个可解析数值（频率MHz, 前向功率W）</summary>
        private static List<CS114CalPoint> ParseCalCsv(string path)
        {
            try
            {
                var list = new List<CS114CalPoint>();
                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim().TrimStart('\uFEFF');
                    if (line.Length == 0 || line.StartsWith("Frequency", StringComparison.OrdinalIgnoreCase)) continue;

                    var parts = line.Split(new[] { ',', '，', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;
                    if (!double.TryParse(parts[0].Trim(), out double freq)) continue;
                    if (!double.TryParse(parts[1].Trim(), out double power)) continue;

                    list.Add(new CS114CalPoint { FreqMHz = freq, ForwardPowerW = power });
                }
                return list.OrderBy(p => p.FreqMHz).ToList();
            }
            catch
            {
                return [];
            }
        }

        private void RefreshCalDataStatus()
        {
            int n = CS114TestModel.CalPowerTable?.Count ?? 0;
            CalDataStatusText = n == 0
                ? "未导入校准数据"
                : $"已导入 {n} 个频点（{System.IO.Path.GetFileName(CS114TestModel.CalPowerTablePath)}）";
        }

        /// <summary>
        /// 当前测试环境名称（由运行配置页面跳转时传入，用于保存目录）
        /// </summary>
        [ObservableProperty]
        private string _curTestEnvName = "CS114";

        /// <summary>
        /// 各测试项的中文说明（本配置页由 CS114/CE101/RE102/RS103 共用，标题跟着测试项变）
        /// </summary>
        private static readonly Dictionary<string, string> TestItemDescriptions = new()
        {
            ["CS114"] = "电缆束注入传导敏感度",
            ["CE101"] = "电源线传导发射（传导安全裕度）",
            ["RE102"] = "电场辐射发射（辐射安全裕度）",
            ["RS103"] = "电场辐射敏感度（辐射安全裕度）",
        };

        /// <summary>硬件模板区标题：随当前测试项变化（跳转进来时由 MainViewModel 注入 CurTestEnvName）</summary>
        public string HardwareTemplateTitle
        {
            get
            {
                string name = string.IsNullOrEmpty(CurTestEnvName) ? "CS114" : CurTestEnvName;
                string desc = TestItemDescriptions.TryGetValue(name, out var d) ? d : "测试配置";
                return $"硬件模板 — {name} {desc}（当前频段）";
            }
        }

        partial void OnCurTestEnvNameChanged(string value) => OnPropertyChanged(nameof(HardwareTemplateTitle));

        /// <summary>
        /// 当前测试配置路径
        /// </summary>
        [ObservableProperty]
        private string _alreadyTestEnvPath = "";



        // ==================== 页面生命周期 ====================
        [RelayCommand]
        void OnFirstLoadPage()
        {
            // 设备名称下拉数据源（来自设备库缓存）
            LoadSortDeviceNames();
            // 初始化默认频段（GJB151B CS114: 10kHz ~ 400MHz）
            CS114TestModel.EnsureDefaults();
            // 外部（运行配置页跳转）已指定频段时不再覆盖
            if (CurFreBand == null || !CS114TestModel.ListFreBands.Contains(CurFreBand))
                CurFreBand = CS114TestModel.ListFreBands.FirstOrDefault() ?? new EMS_CS114FreBand();
        }

        [RelayCommand]
        void OnPageUnloaded()
        {

        }

        // ==================== 分频段操作 ====================
        /// <summary>
        /// 添加频段：复制当前频段的测试参数与硬件模板（设备/系数可再单独修改）
        /// </summary>
        [RelayCommand]
        private void OnAddFreqBand()
        {
            var src = CurFreBand;
            var newBand = new EMS_CS114FreBand();
            if (src != null)
            {
                newBand.StartFre = src.StartFre;
                newBand.StartFreUnit = src.StartFreUnit;
                newBand.EndFreq = src.EndFreq;
                newBand.EndFreUnit = src.EndFreUnit;
                newBand.Stepsize = src.Stepsize;
                newBand.StepUnit = src.StepUnit;
                newBand.Mstepmode = src.Mstepmode;
                newBand.FreqDwellTime = src.FreqDwellTime;

                // 复制频段独有参数（等级/限值/暂停/调制/线损，深拷贝、互不影响）
                newBand.BandParms = src.BandParms.Clone();

                // 复制硬件设备信息，新频段可再单独修改
                for (int i = 0; i < src.RVCHardModel.ListDeviceInfos.Count; i++)
                {
                    if (i >= newBand.RVCHardModel.ListDeviceInfos.Count) break;
                    newBand.RVCHardModel.ListDeviceInfos[i].Name = src.RVCHardModel.ListDeviceInfos[i].Name;
                    newBand.RVCHardModel.ListDeviceInfos[i].Model = src.RVCHardModel.ListDeviceInfos[i].Model;
                    newBand.RVCHardModel.ListDeviceInfos[i].Address = src.RVCHardModel.ListDeviceInfos[i].Address;
                    newBand.RVCHardModel.ListDeviceInfos[i].Communicationmode = src.RVCHardModel.ListDeviceInfos[i].Communicationmode;
                }
            }
            CS114TestModel.ListFreBands.Add(newBand);
            CurFreBand = newBand;
        }

        /// <summary>
        /// 删除当前选中频段
        /// </summary>
        [RelayCommand]
        private void OnDelFreqBand()
        {
            if (CS114TestModel.ListFreBands.Count == 0) return;
            if (CurFreBand != null)
            {
                CS114TestModel.ListFreBands.Remove(CurFreBand);
            }
            CurFreBand = CS114TestModel.ListFreBands.FirstOrDefault() ?? new EMS_CS114FreBand();
        }

        // ==================== 链路系数 ====================
        /// <summary>
        /// 打开系数弹窗（点击硬件连接图上的链路标签 或 系数列表"编辑"按钮）
        /// </summary>
        [RelayCommand]
        private void OnOpenCoeffDialog(object? param)
        {
            if (param is not CS114CableLoss link) return;
            CurLinkCoeff = link;
            var dialog = new CS114CoeffDialog(link);
            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                link.Points = dialog.Result.Points;
                link.Source = dialog.Result.Source;
                link.LastModified = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                link.RefreshStatus();
            }
        }

        /// <summary>
        /// 清除某条链路的系数
        /// </summary>
        [RelayCommand]
        private void OnClearCoeff(object? param)
        {
            if (param is not CS114CableLoss link) return;
            if (MessageBox.Show($"确定清除【{link.DisplayName}】的系数吗？", "提示",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            link.Points.Clear();
            link.Source = "";
            link.LastModified = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            link.RefreshStatus();
        }

        /// <summary>
        /// 编辑系数列表当前选中行
        /// </summary>
        [RelayCommand]
        private void OnEditCurCoeff()
        {
            if (CurLinkCoeff == null) return;
            OnOpenCoeffDialog(CurLinkCoeff);
        }

        // ==================== 仪器地址（自动/手动获取）=================
        /// <summary>
        /// 打开仪器地址弹窗（点击硬件连接图上的设备图标/地址标签）
        /// </summary>
        [RelayCommand]
        private void OnOpenAddressScan(object? param)
        {
            if (param is not DeviceInfo device) return;
            var dialog = new CS114AddressScanDialog(device);
            dialog.ShowDialog();
        }

        // ==================== 限值文件 / 裕度数据导入 ====================
        /// <summary>
        /// 打开限值数据导入弹窗：导入传导安全裕度测试数据 + 偏置（默认+6dB）生成分频段限值曲线
        /// </summary>
        [RelayCommand]
        private void OnOpenLimitImport(object? param)
        {
            if (param is not EMS_CS114FreBand band) return;
            var dialog = new CS114LimitImportDialog(band);
            if (dialog.ShowDialog() == true)
            {
                var bp = band.BandParms;
                bp.LimitTable = dialog.ResultPoints ?? new List<CS114LimitPoint>();
                bp.LimitOffsetDB = dialog.Offset;
                if (!string.IsNullOrEmpty(dialog.SourcePath))
                    bp.ImmunityTableAddr = dialog.SourcePath;
                if (bp.LimitTable.Count > 0)
                    bp.LimitMode = LimitType.File;
                bp.RefreshLimitStatus();
            }
        }

        [RelayCommand]
        private void OnBrowseLimitFile()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "选择限值表文件",
                Filter = "限值表文件|*.csv;*.txt;*.json|所有文件|*.*"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                if (CurFreBand != null)
                    CurFreBand.BandParms.ImmunityTableAddr = openFileDialog.FileName;
            }
        }

        [RelayCommand]
        private void OnBrowseReportTemplate()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "选择报告模板",
                Filter = "报告模板|*.docx;*.doc;*.dotx|所有文件|*.*"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                CS114TestModel.GeneralCS114Parms.ReportTemplatePath = openFileDialog.FileName;
            }
        }

        // ==================== 保存/另存/退出 ====================
        [RelayCommand]
        private void OnSaveAsTestConfig()
        {
            OnSaveConfig("SaveAs");
        }

        [RelayCommand]
        private void OnSaveTestConfig()
        {
            OnSaveConfig("Save");
        }

        [RelayCommand]
        private void OnExitTestConfig()
        {
            Messenger.Send(new RunConfigEventMessage(null), (int)IntMessageToken.SwithToRunConfigPage);
        }

        private void OnSaveConfig(string saveFlag)
        {
            if (CS114TestModel == null) return;
            // 1.按【设备类型+设备名称】匹配数据库中存放的仪器，把地址/型号带出
            MatchDatabaseDevice();
            string json = JsonSerializer.Serialize(CS114TestModel, new JsonSerializerOptions
            {
                WriteIndented = true,
                IncludeFields = false,
                //中文不转义成 \uXXXX，保存出来的配置文件直接可读
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            if (saveFlag == "SaveAs")
            {
                WriteAsFile(json);
            }
            else if (saveFlag == "Save")
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

        private void WriteAsFile(string json)
        {
            if (System.Windows.Application.Current.Dispatcher.HasShutdownStarted) return;

            string envName = string.IsNullOrEmpty(CurTestEnvName) ? "CS114" : CurTestEnvName;
            string baseDir = Path.Combine(GenericParameter.SysPath, "System", "测试模板", envName);
            if (!Directory.Exists(baseDir))
                Directory.CreateDirectory(baseDir);

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = baseDir,
                Title = "保存测试配置",
                FileName = "新建测试配置",
                DefaultExt = ".cs114",
                Filter = "配置文件|*.cs114|所有文件|*.*"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                string fullfilePath = saveFileDialog.FileName;
                if (!File.Exists(fullfilePath))
                {
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
        /// 按【设备类型+设备名称】匹配设备库，刷新所有频段的型号/通讯方式
        /// </summary>
        private void MatchDatabaseDevice()
        {
            if (GenericHelper.globalDevices == null) return;
            foreach (var band in CS114TestModel.ListFreBands)
            {
                for (int i = 0; i < band.RVCHardModel.ListDeviceInfos.Count; i++)
                {
                    var slot = band.RVCHardModel.ListDeviceInfos[i];
                    if (string.IsNullOrEmpty(slot.Name) || slot.Name == "No Device") continue;
                    var device = GenericHelper.globalDevices.FirstOrDefault(d => d != null && d.Name == slot.Name);
                    if (device != null)
                    {
                        band.RVCHardModel.ListDeviceInfos[i] = device;
                    }
                }
            }
        }
    }
}
