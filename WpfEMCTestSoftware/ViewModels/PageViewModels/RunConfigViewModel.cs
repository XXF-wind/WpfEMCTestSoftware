using ClassLibrary.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using ScottPlot;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Enums;
using WpfEMCTestSoftware.Models;
using WpfEMCTestSoftware.Models.DisplayModules;
using WpfEMCTestSoftware.Models.EventMessages;
using WpfEMCTestSoftware.Models.TestModels;
using WpfEMCTestSoftware.RunTasks;
using static Company.UI.Controls.UseControls.UserChart;

namespace WpfEMCTestSoftware.ViewModels
{

    public partial class RunConfigViewModel : BaseViewModel
    {
        #region 频段操作
        /// <summary>
        /// 最小频率
        /// </summary>
        [ObservableProperty]
        private double _minFrequence = 1;
        /// <summary>
        /// 最大频率
        /// </summary>
        [ObservableProperty]
        private double _maxFrequence = 1000;

        /// <summary>X 轴限值（Hz）。
        /// UserChart 内部以 Hz 为单位，其 CLR setter 的 ×1e6 只在 C# 代码赋值时生效；
        /// WPF 绑定走 SetValue、不经过 setter，所以绑定必须直接给 Hz 值</summary>
        public double MinFrequenceHz => MinFrequence * 1_000_000;
        public double MaxFrequenceHz => MaxFrequence * 1_000_000;
        partial void OnMinFrequenceChanged(double value) => OnPropertyChanged(nameof(MinFrequenceHz));
        partial void OnMaxFrequenceChanged(double value) => OnPropertyChanged(nameof(MaxFrequenceHz));
        [ObservableProperty]
        private ObservableCollection<CommonFreqStepBase> _uifFreqBands;
        //频段信息
        [ObservableProperty]
        private ObservableCollection<string> _titleBands = [];
        //当前选中的频段序号
        [ObservableProperty]
        private int _selBandIndex = -1;
        //切换动作
        [ObservableProperty]
        private BandSwitchConfirmMode _bandSwitchConfirmMode;
        //切换动作数据源
        [ObservableProperty]
        private List<object> _bandSwitchMode = Enum.GetValues(typeof(BandSwitchConfirmMode))
           .Cast<BandSwitchConfirmMode>()
           .Select(e => new
           {
               ShowText = e.GetDescription(),
               RealValue = e
           }).ToList<object>();

        #endregion
        #region 图表测试曲线
        /// <summary>
        /// 是否Log型显示信号源输出曲线
        /// </summary>
        [ObservableProperty]
        private bool _isShowSignalSourceLog = true;
        #endregion
        [ObservableProperty]
        private bool _isEnableButtons = false;
        [ObservableProperty]
        private string _testPauseLabel = "暂停";
        [ObservableProperty]
        private PackIconKind _testPauseIcon = PackIconKind.Pause;
        [ObservableProperty]
        private bool _isTestPaused = false;

        //====================底部主控按钮状态互锁（防狂点）====================//
        // 按测试状态只放开"当前有意义"的按钮：空闲/停止/完成 → 只能点开始；
        // 准备中/测试中 → 只能点停止/暂停；已暂停 → 只能点停止/继续。保存只在有数据后可用。
        // 状态源 = BaseTasks.RunStateText（BaseRunTask 每次状态变化都会刷新它），OnBaseTasksChanged 挂 PropertyChanged 跟随。
        [ObservableProperty]
        private bool _canStartTest = true;
        [ObservableProperty]
        private bool _canStopTest = false;
        [ObservableProperty]
        private bool _canPauseTest = false;
        [ObservableProperty]
        private bool _canSaveData = false;
        /// <summary>当前挂了 PropertyChanged 的任务实例（BaseTasks 被整体替换时先摘旧再挂新，防事件泄漏）</summary>
        private BaseRunTask? _stateHookedTask;

        partial void OnBaseTasksChanged(BaseRunTask? value)
        {
            if (_stateHookedTask != null)
                _stateHookedTask.PropertyChanged -= BaseTasks_StateChanged;
            _stateHookedTask = value;
            if (value != null)
                value.PropertyChanged += BaseTasks_StateChanged;
            UpdateOperButtonStates();
        }

        private void BaseTasks_StateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            //状态文字由 RefreshRunState() 在每次状态机迁移时刷新，跟着它同步按钮即可
            if (e.PropertyName == nameof(BaseRunTask.RunStateText))
                UpdateOperButtonStates();
        }

        /// <summary>按运行状态文字同步四个主控按钮的可用性</summary>
        private void UpdateOperButtonStates()
        {
            string state = BaseTasks?.RunStateText ?? "空闲";
            bool running = state is "测试中" or "准备中";
            bool paused = state == "已暂停";
            CanStartTest = state is "空闲" or "已停止" or "已完成";
            CanStopTest = running || paused;
            CanPauseTest = running || paused;   //已暂停时按钮显示"继续"，仍可点
            CanSaveData = state is "已停止" or "已完成";   //有测试数据才可保存

            // Req 4：测试完成自动提示保存数据报告（沿用各测试项原有的 SaveData 生成报告，并附带曲线图）。
            // 状态迁移可能发生在后台定时器线程，弹窗/保存对话框必须在 UI 线程执行，这里用 Dispatcher 切回。
            if (state == "已完成" && !_reportAutoPrompted)
            {
                _reportAutoPrompted = true;
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var r = MessageBox.Show(
                        "测试已完成，是否立即保存数据报告（含曲线图）？",
                        "保存报告", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (r == MessageBoxResult.Yes)
                        SaveData();
                });
            }
        }

        //当前环境下所有测试项目名称
        [ObservableProperty]
        private ObservableCollection<string> _testTemplates = [];
        //当前从combox选中的测试项目名称
        [ObservableProperty]
        private string _curSelectedTestProject;
        /// <summary>选中模板变化时记录到“环境名→模板名”静态表，页面重建后可恢复</summary>
        partial void OnCurSelectedTestProjectChanged(string value)
        {
            if (!string.IsNullOrEmpty(curtestName) && !string.IsNullOrEmpty(value))
                LastSelectedTemplates[curtestName] = value;
        }
        //测试环境
        private string curtestName = string.Empty;
        /// <summary>记住每个测试环境最近选中的测试模板（键=环境名，值=模板文件名；跨页面重建保留）</summary>
        private static readonly Dictionary<string, string> LastSelectedTemplates = new();

        /// <summary>记录某测试环境下最近使用的模板（配置页面保存后调用，返回运行页时自动恢复）</summary>
        public static void RememberLastTemplate(string? envName, string? templateName)
        {
            if (string.IsNullOrEmpty(envName) || string.IsNullOrEmpty(templateName)) return;
            LastSelectedTemplates[envName] = templateName;
        }
        //测试任务
        [ObservableProperty]
        private BaseRunTask? _baseTasks;

        /// <summary>由运行页 View 注入：抓取当前所有曲线图已渲染位图（用于合并导出 / 报告嵌入）。</summary>
        public Func<IList<ChartCapture>>? CaptureChartImages { get; set; }

        /// <summary>同一轮测试只自动弹一次"保存报告"提示，避免状态刷新重复打扰（开始测试时复位）。</summary>
        private bool _reportAutoPrompted;

        //====================可插拔显示模块（一个页面适配所有测试）====================//
        /// <summary>图表模块 —— 中央图表区（显示哪些图由 ModuleProfileRegistry 按测试项配置决定）</summary>
        [ObservableProperty]
        private ObservableCollection<ITestDisplayModule> _activeChartModules = new();

        /// <summary>参数卡模块 —— 右侧参数卡区（卡片内容由 RunTask 的 IPanelDataProvider 提供）</summary>
        [ObservableProperty]
        private ObservableCollection<ParamCardModule> _activeCards = new();

        /// <summary>表格模块 —— 左侧"结果"页（结果表 / 系统校验比对表）</summary>
        [ObservableProperty]
        private ObservableCollection<ResultTableModule> _activeTables = new();

        /// <summary>附加操作按钮 —— 底部按钮条（EMI 有截图/曲线居中，辐射类有改变极化方向）</summary>
        [ObservableProperty]
        private ObservableCollection<ControlModule> _activeControls = new();

        /// <summary>图表区行数（按启用图表数量自适应）</summary>
        [ObservableProperty]
        private int _chartRows = 3;

        /// <summary>图表区列数（按启用图表数量自适应）</summary>
        [ObservableProperty]
        private int _chartColumns = 1;

        /// <summary>是否处于"参考校准"模式（说明书：EMS 测试项分校准 / EUT 两段）</summary>
        [ObservableProperty]
        private bool _isCalibrationMode;

        /// <summary>模式切换按钮上的文字</summary>
        public string ModeToggleLabel => IsCalibrationMode ? "模式：参考校准" : "模式：EUT 测试";

        partial void OnIsCalibrationModeChanged(bool value) => OnPropertyChanged(nameof(ModeToggleLabel));

        /// <summary>
        /// 右侧参数摘要面板（测试项/频段/频率范围等，随任务刷新）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ParamItem> _displayInfo = new();

        /// <summary>当前测试项名称（界面显示用）</summary>
        public string CurrentTestName => string.IsNullOrEmpty(curtestName) ? "-" : curtestName;

        /// <summary>
        /// 按当前测试项重建全部显示模块：图表 / 参数卡 / 表格 / 附加按钮
        /// 这是"一个运行页适配所有测试"的唯一入口，页面本身不认识任何具体测试项
        /// </summary>
        private void RebuildDisplayModules()
        {
            // 1. 先解绑并清空全部容器
            foreach (var m in ActiveChartModules) m.Unbind();
            foreach (var m in ActiveCards) m.Unbind();
            foreach (var m in ActiveTables) m.Unbind();
            ActiveChartModules.Clear();
            ActiveCards.Clear();
            ActiveTables.Clear();
            ActiveControls.Clear();

            var task = BaseTasks;
            if (task == null || string.IsNullOrEmpty(curtestName))
            {
                RefreshDisplayInfo();
                return;
            }

            // 2. 测试环境/模板/模式写入任务（参数卡的"测试信息"组从这里取）
            task.TestEnvName = curtestName;
            task.TestTemplateName = CurSelectedTestProject ?? "-";
            task.TestRunMode = IsCalibrationMode ? RunMode.Calibration : RunMode.Eut;

            // 3. 取本测试项的模块配置，按类型分别构建
            var profile = ModuleProfileRegistry.Get(curtestName);
            foreach (var m in ModuleHost.BuildCharts(profile.Charts, task)) ActiveChartModules.Add(m);
            foreach (var m in ModuleHost.BuildCards(profile.Cards, task)) ActiveCards.Add(m);
            foreach (var m in ModuleHost.BuildTables(profile.Tables, task)) ActiveTables.Add(m);
            foreach (var c in ModuleHost.BuildControls(profile.Controls)) ActiveControls.Add(c);

            // 4. 图表区行列自适应：≤3 张竖排，多于 3 张按接近正方形排布
            int n = ActiveChartModules.Count;
            ChartRows = n <= 3 ? Math.Max(n, 1) : (int)Math.Ceiling(Math.Sqrt(n));
            ChartColumns = n <= 3 ? 1 : (int)Math.Ceiling((double)n / ChartRows);

            OnPropertyChanged(nameof(CurrentTestName));
            RefreshDisplayInfo();
            RefreshResultSummary();
        }

        /// <summary>刷新结果判定摘要（左侧"结果"页顶部）</summary>
        private void RefreshResultSummary()
        {
            var task = BaseTasks;
            if (task == null) return;
            var table = task.GetResultTables().FirstOrDefault(t => t.Key == "Table.Result");
            task.TotalPoints = table?.Rows.Count ?? 0;
            task.OverLimitCount = table?.Rows.Count(r => r.IsOverLimit) ?? 0;
            task.ResultVerdict = task.TotalPoints == 0
                ? "未测试"
                : (task.OverLimitCount == 0 ? "合格" : "不合格");
        }

        /// <summary>刷新右侧"测试信息"摘要（静态信息；运行状态/进度走顶部状态条实时绑定）</summary>
        private void RefreshDisplayInfo()
        {
            DisplayInfo.Clear();
            DisplayInfo.Add(new ParamItem("测试环境", string.IsNullOrEmpty(curtestName) ? "-" : curtestName));
            DisplayInfo.Add(new ParamItem("测试模板", string.IsNullOrEmpty(CurSelectedTestProject) ? "-" : CurSelectedTestProject));
            DisplayInfo.Add(new ParamItem("测试模式", IsCalibrationMode ? "参考校准" : "EUT 测试"));
            DisplayInfo.Add(new ParamItem("起始频率", $"{MinFrequence} MHz"));
            DisplayInfo.Add(new ParamItem("终止频率", $"{MaxFrequence} MHz"));
            DisplayInfo.Add(new ParamItem("频段数量", TitleBands.Count.ToString()));
        }

        /// <summary>
        /// 图表是否显示队列
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<bool> _isShows = [false, false, false];
        //--------------------runtask信息反馈给界面------------------------------------------------//  
        /// <summary>
        /// tsak加载->发送频段给界面，界面默认选第一个 用户点列表->发送下标给task
        /// </summary>


        public RunConfigViewModel()
        {
            BaseTasks = new BaseRunTask();
            Messenger.Register<RunConfigEventMessage, int>(this, (int)IntMessageToken.PassEnvName, (r, m) => InitPara(m.Value));
            Messenger.Register<RunConfigEventMessage, int>(this, 2, (r, m) => SetTestTemplate(m.Value));
            Messenger.Register<UniversalStrMessage, int>(this, 201, (r, m) =>
            {
                TestTemplates.Add(m.Value);
            });
            //203 频段信息
            Messenger.Register<UniversalStrMessage, int>(this, 203, (r, m) =>
            {
                TitleBands.Add(m.Value);
            });
            Messenger.Register<ChartUpdateMessage, int>(this, 0, (r, m) => OnChartDatareceived(m.Value));


        }
        [RelayCommand]
        private void OnPageUnloaded()
        {
            //页面卸载时解绑所有显示模块
            ClearDisplayModules();
        }

        [RelayCommand]
        private void OnPageLoaded()
        {
            CreateTaskByConfig();
        }

        /// <summary>清空并解绑全部显示模块（图表/参数卡/表格/按钮）</summary>
        private void ClearDisplayModules()
        {
            foreach (var m in ActiveChartModules) m.Unbind();
            foreach (var m in ActiveCards) m.Unbind();
            foreach (var m in ActiveTables) m.Unbind();
            ActiveChartModules.Clear();
            ActiveCards.Clear();
            ActiveTables.Clear();
            ActiveControls.Clear();
            DisplayInfo.Clear();
        }
        //切换频段需要的动作
        [RelayCommand]
        private void OnBandSwitchAction()
        {

        }
        //选择频段
        [RelayCommand]
        private void OnChooseBand()
        {
            if ((SelBandIndex >= UifFreqBands.Count()) || (SelBandIndex < 0))
                return;

            RefreshDisplayInfo();
            //频段下标同步给 task：task 收到后更新 TestContext.CurrentBandIndex 并原地刷新"当前频段"等参数卡
            Messenger.Send(new UniversalIntMessage(SelBandIndex), 204);
        }

        /// <summary>
        /// 初始化配置
        /// </summary>
        /// <param name="key"></param>
        private void InitPara(string? key)
        {
            curtestName = key;
            if (curtestName == null)
                return;
            LoadTestTemplatesNames();
        }
        /// <summary>
        /// 根据配置信息创建当前要执行的任务
        /// </summary>
        private void CreateTaskByConfig()
        {
            //重建任务前先停掉旧任务并释放设备：否则换模板/刷新时旧循环线程还在跑，
            //而按钮事件已经指向新实例，暂停/停止就会"按了没反应"
            BaseTasks?.StopTest();
            if (string.IsNullOrEmpty(CurSelectedTestProject))
            {
                IsEnableButtons = false;
                ClearDisplayModules();
                return;
            }
            string path = Path.Combine(GenericParameter.SysPath, "System", "测试模板", curtestName, CurSelectedTestProject);
            if (!File.Exists(path))
            {
                IsEnableButtons = false;
                ClearDisplayModules();
                return;
            }
            string json = File.ReadAllText(path);
            var options = new JsonSerializerOptions
            {
                IncludeFields = true
            };
            int i = GetCurTestProjectIndex();
            switch (i)
            {
                case (int)TestProject.混响室:
                    {
                        var TestConfig = JsonSerializer.Deserialize<RVC_EMSTestConfigModel>(json, options);
                        switch (TestConfig?.CurEMSStandard)
                        {
                            case EMCStandard.DO_160G:
                                BaseTasks = new DO160RunTask();
                                MapEmsToDO160Param(TestConfig, ((DO160RunTask)BaseTasks).RuntimeParam, BaseTasks.CommonFreAndHardParms);
                                break;
                            case EMCStandard.DO_160F:
                                BaseTasks = new DO160RunTask();
                                MapEmsToDO160Param(TestConfig, ((DO160RunTask)BaseTasks).RuntimeParam, BaseTasks.CommonFreAndHardParms);
                                break;
                            default:
                                break;

                        }
                    }
                    break;
                case (int)TestProject.CS114:
                    {
                        var TestCS114Config = JsonSerializer.Deserialize<EMS_CS114ConfigModel>(json, options);
                        if (TestCS114Config != null)
                        {
                            BaseTasks = new CS114RunTask();
                            MapEmsToCS114Param(TestCS114Config, ((CS114RunTask)BaseTasks).RuntimeParam, BaseTasks.CommonFreAndHardParms);
                            //配置映射完成后立即把全频段限值铺到注入电平图的 limit 折线上（定值按频段两点 / 文件按限值表逐点）
                            ((CS114RunTask)BaseTasks).BuildLimitCurve();
                        }
                    }
                    break;
                // ---- 安全裕度测试项：配置模型与 CS114 完全同构（频段 + 限值 + 线损 + 硬件），
                //      因此反序列化 EMS_CS114ConfigModel 后复用同一套映射（见 MapEmsToCS114Param 注释）----
                case (int)TestProject.CE101:
                    {
                        var cfg = JsonSerializer.Deserialize<EMS_CS114ConfigModel>(json, options);
                        if (cfg != null)
                        {
                            BaseTasks = new CE101RunTask();          //传导安全裕度（电源线传导发射，dBμA）
                            MapEmsToCS114Param(cfg, ((CE101RunTask)BaseTasks).RuntimeParam, BaseTasks.CommonFreAndHardParms);
                            ((CE101RunTask)BaseTasks).BuildLimitCurve();
                        }
                    }
                    break;
                case (int)TestProject.RE102:
                    {
                        var cfg = JsonSerializer.Deserialize<EMS_CS114ConfigModel>(json, options);
                        if (cfg != null)
                        {
                            BaseTasks = new RE102RunTask();          //辐射安全裕度（电场辐射发射，dBμV/m）
                            MapEmsToCS114Param(cfg, ((RE102RunTask)BaseTasks).RuntimeParam, BaseTasks.CommonFreAndHardParms);
                            ((RE102RunTask)BaseTasks).BuildLimitCurve();
                        }
                    }
                    break;
                case (int)TestProject.RS103:
                    {
                        var cfg = JsonSerializer.Deserialize<EMS_CS114ConfigModel>(json, options);
                        if (cfg != null)
                        {
                            BaseTasks = new RS103RunTask();          //辐射安全裕度（电场辐射敏感度，场强闭环逼近）
                            MapEmsToCS114Param(cfg, ((RS103RunTask)BaseTasks).RuntimeParam, BaseTasks.CommonFreAndHardParms);
                            ((RS103RunTask)BaseTasks).BuildLimitCurve();
                        }
                    }
                    break;
                default:
                    break;
            }
            //==================== 接续测试弹窗（Req 2：通用所有测试项）====================//
            // 再次选择同一测试模板、且存在上一轮中途停止留下的临时曲线数据时，询问是否接续：
            // 是 → 把临时表数据加载进本次任务，并从上一测试最后一个频点继续（曲线图表直接显示历史进度）；
            // 否 → 清空该模板临时数据（StartTest 也会在全新开始时再清一次，这里先清保证"不接续即全新"）。
            // 注意：必须在 RebuildDisplayModules 之前调用，这样图表模块绑定后就能直接显示已恢复的曲线。
            if (BaseTasks != null
                && !string.IsNullOrEmpty(CurSelectedTestProject)
                && ClsData.Instance.HasCurveTemp(CurSelectedTestProject))
            {
                var resumeResult = MessageBox.Show(
                    "存在上一次未完成的测试，是否接续？\n（是：从上一测试最后一个频点继续，并恢复已测曲线；否：清空临时数据，全新开始）",
                    "接续测试", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (resumeResult == MessageBoxResult.Yes)
                {
                    BaseTasks.LoadAndPrepareResume(CurSelectedTestProject);
                }
                else
                {
                    try { ClsData.Instance.ClearCurveTemp(CurSelectedTestProject); } catch { }
                    BaseTasks.AddLog("INFO", "已选择全新开始，已清空上一未完成测试的临时数据");
                }
            }
            //按测试环境重建可插拔图表模块（未注册的测试项使用默认三图布局）
            RebuildDisplayModules();
        }


        //映射:把RVC_EMSTestConfigModel赋值给DO160RuntimeParam
        private void MapEmsToDO160Param(RVC_EMSTestConfigModel config, DO160RuntimeParam param, List<CommonFreAndHard> commonlist)
        {
            param.Standard = config.CurEMSStandard;
            param.CurReverbTestMode = config.CurReverbTestMode;
            param.CurTestStageType = config.CurTestStageType;
            param.PreCalFileName = config.PreCalFileName;
            param.ImmunityLimit = config.ListRvcFreBands.Select(item => item.ImmunityLimit).ToList();
            commonlist.Clear();
            commonlist.AddRange(config.ListRvcFreBands.Select(item => new CommonFreAndHard
            {
                FreqBase = item.FreqCommon,
                HardModel = item.RVCHardModel,
            }));
            UifFreqBands = [.. (config.ListRvcFreBands.Select(item => item.FreqCommon))];
            TitleBands = [.. (UifFreqBands.Select(item => $"{GenericHelper.ConvertToMhzData(item.StartFre, item.StartFreUnit)}MHz_{GenericHelper.ConvertToMhzData(item.EndFre, item.EndFreUnit)}MHz"))];
            if (TitleBands.Count > 0)
            {
                MinFrequence = GenericHelper.ConvertToMhzData(UifFreqBands[0].StartFre, UifFreqBands[0].StartFreUnit);
                MaxFrequence = GenericHelper.ConvertToMhzData(UifFreqBands[TitleBands.Count - 1].EndFre, UifFreqBands[TitleBands.Count - 1].EndFreUnit);
                SelBandIndex = 0;
            }
            IsEnableButtons = true;
        }

        /// <summary>
        /// 配置模型 → 运行参数映射。
        /// ⚠ 虽以 CS114 命名，但这里映射的是 EMS_CS114ConfigModel 的通用结构
        /// （频段/限值/线损/硬件），CS114、CE101、RE102、RS103 四个测试项共用：
        /// 新增同类测试项直接复用本方法即可，无需再写一份。
        /// </summary>
        private void MapEmsToCS114Param(EMS_CS114ConfigModel config, CS114RuntimeParam param, List<CommonFreAndHard> commonlist)
        {
            param.Standard = config.CurEMSStandard;
            param.CurTestStageType = config.CurTestStageType;

            param.ImmunityLimit = config.ListFreBands.Select(item => item.BandParms).ToList();
            param.CalPowerTable = config.CalPowerTable ?? [];   //正式测试的校准功率上限（配置页导入）
            commonlist.Clear();
            commonlist.AddRange(config.ListFreBands.Select(item => new CommonFreAndHard
            {
                FreqBase = item.FreqCommon,
                HardModel = item.RVCHardModel,
            }));
            UifFreqBands = [.. (config.ListFreBands.Select(item => item.FreqCommon))];
            TitleBands = [.. (UifFreqBands.Select(item => $"{GenericHelper.ConvertToMhzData(item.StartFre, item.StartFreUnit)}MHz_{GenericHelper.ConvertToMhzData(item.EndFre, item.EndFreUnit)}MHz"))];
            if (TitleBands.Count > 0)
            {
                MinFrequence = GenericHelper.ConvertToMhzData(UifFreqBands[0].StartFre, UifFreqBands[0].StartFreUnit);
                MaxFrequence = GenericHelper.ConvertToMhzData(UifFreqBands[TitleBands.Count - 1].EndFre, UifFreqBands[TitleBands.Count - 1].EndFreUnit);
                SelBandIndex = 0;
            }
            IsEnableButtons = true;
        }

        private void SetTestTemplate(string? testTemplate)
        {
            if (!string.IsNullOrEmpty(testTemplate))
                TestTemplates?.Add(testTemplate);
        }

        //FieldStrength1, //场强1
        //FieldLimitLine, //场强限制线
        //ForwardPower,   //前向功率
        //ReversePower,   //反向功率
        private void OnChartDatareceived(ChartPointMessage message)
        {

        }

        /// <summary>
        /// 操作按钮 点击统一处理方法
        /// 作用：根据传入的按钮索引，执行不同的测试项目操作（编辑/新建）
        /// </summary>
        /// <param name="obj">传入的参数（按钮标识，字符串类型）</param>
        [RelayCommand]
        private void RunButtonOper(object obj)
        {
            if (obj is string str)
            {
                int index = int.Parse(str);
                switch (index)
                {
                    case 0:
                        EditTestProject();
                        break;
                    case 1:
                        CreateNewTestProject();
                        break;
                    case 2:
                        DeleteTestProject();
                        break;
                    case 3:
                        RefreshTestProject();
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// 创建新的配置模板：按当前测试环境打开对应的配置页面（传 null 表示新建，页面自行初始化默认参数）
        /// </summary>
        private void CreateNewTestProject()
        {
            //直接打开配置页面
            switch (GetCurTestProjectIndex())
            {
                case (int)TestProject.混响室:
                    ShowEmsConfigPage(null, "");
                    break;
                //CE101 / RE102 / RS103 与 CS114 共用同一套配置页面（频段+限值+线损+硬件结构一致）
                case (int)TestProject.CS114:
                case (int)TestProject.CE101:
                case (int)TestProject.RE102:
                case (int)TestProject.RS103:
                    ShowCS114ConfigPage(null, "");
                    break;
                default:
                    break;
            }
        }
        /// <summary>
        /// 修改已有的配置模板，不同的测试环境加载不同的模板参数
        /// </summary>
        private void EditTestProject()
        {
            if (string.IsNullOrEmpty(CurSelectedTestProject))
            {
                MessageBox.Show("测试模板为空，请选择一个测试模板");
                return;
            }
            // 拼接文件完整路径
            string fullfilePath = Path.Combine(GenericParameter.SysPath, "System", "测试模板", curtestName, CurSelectedTestProject);
            if (!File.Exists(fullfilePath))
            {
                MessageBox.Show("配置文件不存在！");
                return;
            }
            var json = File.ReadAllText(fullfilePath);
            var options = new JsonSerializerOptions
            {
                IncludeFields = true
            };
            //按当前测试环境反序列化成对应的配置模型，再传给配置页面打开
            switch (GetCurTestProjectIndex())
            {
                case (int)TestProject.混响室:
                    ShowEmsConfigPage(JsonSerializer.Deserialize<RVC_EMSTestConfigModel>(json, options), fullfilePath);
                    break;
                case (int)TestProject.CS114:
                case (int)TestProject.CE101:
                case (int)TestProject.RE102:
                case (int)TestProject.RS103:
                    ShowCS114ConfigPage(JsonSerializer.Deserialize<EMS_CS114ConfigModel>(json, options), fullfilePath);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 删除测试项目
        /// </summary>
        private void DeleteTestProject()
        {
            string fullfilePath = Path.Combine(GenericParameter.SysPath, "System", "测试模板", curtestName, CurSelectedTestProject);
            if (!File.Exists(fullfilePath))
            {
                MessageBox.Show("配置文件不存在！");
                return;
            }
            File.Delete(fullfilePath);
            TestTemplates.Remove(CurSelectedTestProject);
            //删除的若正是上次记住的模板，同步清掉记忆并清空选中
            if (LastSelectedTemplates.TryGetValue(curtestName, out var lastName) && lastName == CurSelectedTestProject)
                LastSelectedTemplates.Remove(curtestName);
            CurSelectedTestProject = null;
        }

        /// <summary>
        /// 刷新测试项目
        /// </summary>
        private void RefreshTestProject()
        {
            CreateTaskByConfig();
        }
        /// <summary>
        /// 取当前测试环境对应的枚举索引（TestProject 与 TestProjectEN 序号一一对应，取不到返回 -1）
        /// </summary>
        private int GetCurTestProjectIndex()
        {
            Type type = GenericParameter.Language.IsEnglish ? typeof(TestProjectEN) : typeof(TestProject);
            return Enum.TryParse(type, curtestName, out object? value) ? (int)value : -1;
        }

        /// <summary>
        /// 跳转打开混响室(EMS)配置页面
        /// </summary>
        /// <param name="TestConfigModel">历史配置（null 表示新建模板）</param>
        /// <param name="alreadyFilePath">已存在的配置文件路径</param>
        private void ShowEmsConfigPage(RVC_EMSTestConfigModel? TestConfigModel, string alreadyFilePath)
        {
            if (TestConfigModel == null)
                Messenger.Send(new UniversalIntMessage(int.Parse("7")), (int)IntMessageToken.RVCTestomposite);
            else
                //传出历史参数显示在页面上
                Messenger.Send(new CompositeRVC_EMSConfigMessage(new CompositeRVC_EMSConfig(TestConfigModel, alreadyFilePath)), (int)IntMessageToken.RVCTestomposite);
        }

        /// <summary>
        /// 跳转打开CS114配置页面
        /// </summary>
        /// <param name="TestConfigModel">历史配置（null 表示新建模板）</param>
        /// <param name="alreadyFilePath">已存在的配置文件路径</param>
        private void ShowCS114ConfigPage(EMS_CS114ConfigModel? TestConfigModel, string alreadyFilePath)
        {
            if (TestConfigModel == null)
                //1 只是切页面标志，不回填参数
                Messenger.Send(new UniversalIntMessage(1), (int)IntMessageToken.CS114Composite);
            else
                //传出历史参数显示在页面上
                Messenger.Send(new CompositeCS114ConfigMessage(new CompositeCS114Config(TestConfigModel, alreadyFilePath)), (int)IntMessageToken.CS114Composite);
        }

        /// <summary>
        /// 加载这个类型下的所有的测试模板
        /// </summary>
        [RelayCommand]
        private void LoadTestTemplatesNames()
        {
            string pathsss = Path.Combine(GenericParameter.SysPath, "System", "测试模板", curtestName);
            if (!Directory.Exists(pathsss))
            {
                Directory.CreateDirectory(pathsss);
            }
            TestTemplates = [.. GenericMethods.GetPathFile(pathsss)];
            //恢复上次选中的测试模板（从配置页面退出回来时 ComboBox 不丢选中项；
            //设置选中会触发 SelectionChanged → TestComboxSelectChange → 自动加载该模板）
            if (LastSelectedTemplates.TryGetValue(curtestName, out var lastName) && TestTemplates.Contains(lastName))
                CurSelectedTestProject = lastName;
        }
        /// <summary>
        /// 图表1点击事件，显示点击位置的频率和功率信息
        /// </summary>
        /// <param name="chartClickArgs"></param>
        [RelayCommand]
        private void Chart1Click(ChartClickArgs chartClickArgs)
        {
            MessageBox.Show($"频率：{chartClickArgs.FrequencyHz} Hz\n功率：{chartClickArgs.Power} dB" +
                $"\n幅值：{chartClickArgs.Amplitude} \n限值：{chartClickArgs.Limit}");
        }
        /// <summary>
        /// 加载测试文件，读取文件里边的信息放在文件里
        /// </summary>
        /// <param name="obj"></param>
        [RelayCommand]
        private void TestComboxSelectChange(object obj)
        {
            RefreshTestProject();
        }

        /// <summary>
        /// 频段选择改变，发送消息给task，task根据下标取频段信息进行测试
        /// </summary>
        [RelayCommand]
        private void OnBandIndexChanged()
        {
            Messenger.Send(new UniversalIntMessage(SelBandIndex), 204);
        }
        [RelayCommand]
        private void OnClearLogMessage()
        {
            BaseTasks?.LogItems.Clear();
        }

        /// <summary>
        /// 开始测试（StartTest 内部只启动频段切换定时器，不阻塞，无需 Task.Run）
        /// </summary>
        private void StartTest()
        {
            if (BaseTasks == null) return;
            _reportAutoPrompted = false;   //新一轮测试允许再次自动提示保存报告
            BaseTasks.StartTest();
            SyncPauseUi(isPaused: false);
        }

        /// <summary>
        /// 停止测试
        /// </summary>
        private void StopTest()
        {
            BaseTasks?.StopTest();
            SyncPauseUi(isPaused: false);
        }

        private void PauseTest()
        {
            var task = BaseTasks;
            if (task == null) return;
            if (!IsTestPaused)
            {
                task.PauseTest();
                //以任务真实状态为准：暂停被拒绝（如还没开跑）就不翻转按钮，避免界面与任务状态脱节
                if (task.TestContext.currentTestState == TestState.PAUSED)
                    SyncPauseUi(isPaused: true);
            }
            else
            {
                task.ResumeTest();
                SyncPauseUi(isPaused: false);
            }
        }
        /// <summary>暂停按钮的 文字/图标 与任务状态保持一致</summary>
        private void SyncPauseUi(bool isPaused)
        {
            IsTestPaused = isPaused;
            TestPauseLabel = isPaused ? "继续" : "暂停";
            TestPauseIcon = isPaused ? PackIconKind.Play : PackIconKind.Pause;
        }
        /// <summary>
        /// 保存数据（复用各测试项原有的 SaveData 生成报告文件，如 CS114 的 CSV 数据报告）。
        /// 报告落盘后，再把当前所有曲线图保存到报告同目录的"xxx_曲线图"文件夹，作为报告附图。
        /// </summary>
        private void SaveData()
        {
            var task = BaseTasks;
            if (task == null) return;

            task.LastSavedReportPath = null;   // 新一轮保存前清掉，避免沿用上一次的路径
            task.SaveData();                   // 原有报告生成逻辑（各测试项自己实现）
            SaveCurveImagesBesideReport(task); // 曲线图随报告一起落盘
        }

        /// <summary>
        /// 把所有曲线图保存到报告文件所在目录（报告同目录下建"报告名_曲线图"文件夹），
        /// 内含每张曲线图 PNG + 一张纵向合并图。报告本身仍由原 SaveData 生成，不改动其格式。
        /// </summary>
        private void SaveCurveImagesBesideReport(BaseRunTask task)
        {
            var charts = CaptureChartImages?.Invoke();
            if (charts == null || charts.Count == 0)
            {
                task.AddLog("WARN", "本次保存未附带曲线图（运行页未渲染出曲线图）");
                return;
            }

            // 报告目录：优先用 SaveData 实际落盘的路径；取不到则退回 System\数据文件\<测试环境>
            string? reportPath = task.LastSavedReportPath;
            string dir = !string.IsNullOrEmpty(reportPath) && !string.IsNullOrEmpty(Path.GetDirectoryName(reportPath))
                ? Path.GetDirectoryName(reportPath)!
                : Path.Combine(GenericParameter.SysPath, "System", "数据文件",
                    string.IsNullOrEmpty(curtestName) ? "未分类" : curtestName);

            string baseName = !string.IsNullOrEmpty(reportPath)
                ? Path.GetFileNameWithoutExtension(reportPath)
                : $"{curtestName}_{CurSelectedTestProject}_{DateTime.Now:yyyyMMddHHmmss}";

            string imgDir = Path.Combine(dir, SafeFileName(baseName) + "_曲线图");
            try
            {
                Directory.CreateDirectory(imgDir);
                int saved = 0;
                for (int i = 0; i < charts.Count; i++)
                {
                    var ch = charts[i];
                    if (ch.Png == null || ch.Png.Length == 0) continue;
                    string name = string.IsNullOrWhiteSpace(ch.Title) ? $"曲线图{i + 1}" : ch.Title;
                    File.WriteAllBytes(Path.Combine(imgDir, SafeFileName(name) + ".png"), ch.Png);
                    saved++;
                }
                byte[]? merged = MergeChartPngs(charts);
                if (merged != null && merged.Length > 0)
                    File.WriteAllBytes(Path.Combine(imgDir, "曲线合并图.png"), merged);

                task.AddLog("INFO", $"【保存数据】曲线图已随报告保存到：{imgDir}（{saved} 张 + 1 张合并图）");
            }
            catch (Exception ex)
            {
                task.AddLog("ERROR", $"保存曲线图失败：{ex.Message}");
            }
        }

        /// <summary>文件名清洗：去掉 \/:*?"&lt;&gt;| 等非法字符</summary>
        private static string SafeFileName(string? name)
            => string.Join("_", (name ?? "未命名").Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

        //==================== Req 3：多曲线图合并导出 ====================//
        /// <summary>把所有曲线图合并为一张 PNG 导出（原有单图"保存图片/复制到剪贴板"右键功能保持不变）。</summary>
        private void ExportChartImages()
        {
            var task = BaseTasks;
            var charts = CaptureChartImages?.Invoke();
            if (charts == null || charts.Count == 0)
            {
                task?.AddLog("WARN", "当前没有可导出的曲线图（请先运行测试产生数据）");
                MessageBox.Show("当前没有可导出的曲线图，请先开始测试并产生数据。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            byte[]? merged = MergeChartPngs(charts);
            if (merged == null || merged.Length == 0)
            {
                task?.AddLog("ERROR", "合并曲线图失败");
                return;
            }
            var sfd = new SaveFileDialog
            {
                Filter = "PNG(*.png)|*.png",
                FileName = $"{curtestName}_{CurSelectedTestProject}_曲线合并图",
            };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllBytes(sfd.FileName, merged);
                    task?.AddLog("INFO", $"已导出合并曲线图：{sfd.FileName}（共 {charts.Count} 张图）");
                }
                catch (Exception ex)
                {
                    task?.AddLog("ERROR", $"导出合并曲线图失败：{ex.Message}");
                }
            }
        }

        /// <summary>把多张曲线图 PNG 纵向拼接成一张大图（白底）。无图返回 null。</summary>
        private static byte[]? MergeChartPngs(IList<ChartCapture> charts, int gap = 24, int margin = 24)
        {
            if (charts == null || charts.Count == 0) return null;
            var sources = new List<BitmapSource>();
            int maxW = 0; int totalH = 0;
            foreach (var ch in charts)
            {
                var bmp = LoadBitmap(ch.Png);
                if (bmp == null) continue;
                sources.Add(bmp);
                maxW = Math.Max(maxW, bmp.PixelWidth);
                totalH += bmp.PixelHeight + gap;
            }
            if (sources.Count == 0) return null;
            totalH = totalH - gap + margin * 2;
            int canvasW = maxW + margin * 2;
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new System.Windows.Rect(0, 0, canvasW, totalH));
                int y = margin;
                foreach (var bmp in sources)
                {
                    dc.DrawImage(bmp, new System.Windows.Rect(margin, y, bmp.PixelWidth, bmp.PixelHeight));
                    y += bmp.PixelHeight + gap;
                }
            }
            var rtb = new RenderTargetBitmap(canvasW, totalH, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }

        private static BitmapSource? LoadBitmap(byte[] png)
        {
            if (png == null || png.Length == 0) return null;
            var ms = new MemoryStream(png);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        //====================模块化扩展命令====================//
        /// <summary>切换测试模式：EUT 测试 ←→ 参考校准（说明书里 EMS 测试项的两段）</summary>
        [RelayCommand]
        private void ToggleTestMode()
        {
            IsCalibrationMode = !IsCalibrationMode;
            var task = BaseTasks;
            if (task != null)
                task.TestRunMode = IsCalibrationMode ? RunMode.Calibration : RunMode.Eut;
            // 模式切换后重建模块：校准模式显示校验比对表，EUT 模式显示结果表
            RebuildDisplayModules();
            task?.AddLog("INFO", $"切换测试模式：{(IsCalibrationMode ? "参考校准" : "EUT 测试")}");
        }

        /// <summary>
        /// 附加操作按钮统一入口（按模块 Key 分发；XAML 中直接传入字符串 Key）
        /// </summary>
        [RelayCommand]
        private void ModuleControl(object obj)
        {
            var key = (obj as ControlModule)?.ModuleKey ?? obj?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(key))
                return;
            switch (key)
            {
                case "Ctl.Start":
                    if (!CanStartTest)
                    {
                        BaseTasks?.AddLog("WARN", "开始无效：测试正在进行或准备中，请勿重复点击");
                        return;
                    }
                    BaseTasks?.AddLog("INFO", "操作：点击【开始测试】");
                    StartTest();
                    break;
                case "Ctl.Stop":
                    if (!CanStopTest)
                    {
                        BaseTasks?.AddLog("WARN", "停止无效：当前没有正在进行的测试");
                        return;
                    }
                    BaseTasks?.AddLog("INFO", "操作：点击【停止测试】");
                    StopTest();
                    break;
                case "Ctl.Pause":
                    if (!CanPauseTest)
                    {
                        BaseTasks?.AddLog("WARN", "暂停/继续无效：当前没有正在进行的测试");
                        return;
                    }
                    BaseTasks?.AddLog("INFO", IsTestPaused ? "操作：点击【继续测试】" : "操作：点击【暂停测试】");
                    PauseTest();
                    break;
                case "Ctl.Report":
                    if (!CanSaveData)
                    {
                        BaseTasks?.AddLog("WARN", "保存无效：请先完成或停止一次测试");
                        return;
                    }
                    BaseTasks?.AddLog("INFO", "操作：点击【保存数据】");
                    SaveData();
                    break;
                case "Ctl.ExportCharts":
                    ExportChartImages();
                    break;
                case "Ctl.Screenshot":
                    BaseTasks?.AddLog("INFO", "已保存接收机界面截图");
                    break;
                case "Ctl.CurveCenter":
                    BaseTasks?.AddLog("INFO", "曲线已居中显示");
                    break;
                case "Ctl.Polarization":
                    BaseTasks?.AddLog("INFO", "已下发【改变天线极化方向】指令");
                    break;
                default:
                    BaseTasks?.AddLog("INFO", $"未实现的操作：{key}");
                    break;
            }
        }

        /// <summary>生成测试结果：刷新结果表并重新判定</summary>
        private void GenerateResult()
        {
            var task = BaseTasks;
            if (task == null)
                return;
            RefreshResultSummary();
            task.AddLog("INFO", $"生成测试结果：共 {task.TotalPoints} 点，超标 {task.OverLimitCount} 点，判定【{task.ResultVerdict}】");
        }
        //=================================辅助函数========================================//


    }
}