using ClassLibrary;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Company.UI.Controls.UseControls;
using EMCDeviceLibrary;
using System.Collections.ObjectModel;
using System.Timers;
using System.Windows.Media;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Enums;
using WpfEMCTestSoftware.Models;
using WpfEMCTestSoftware.Models.DeviceModels;
using WpfEMCTestSoftware.Models.DisplayModules;
using WpfEMCTestSoftware.Models.EventMessages;
using WpfEMCTestSoftware.Models.HardwareModels;
using WpfEMCTestSoftware.Models.TestModels;

public class LogInfo
{
    // 绑定字段：时间列
    public DateTime Time { get; set; }
    // 绑定字段：日志等级 INFO/WARN/ERROR
    public string Level { get; set; } = string.Empty;
    // 绑定字段：日志正文
    public string Message { get; set; } = string.Empty;
    // 绑定字段：等级对应颜色
    public SolidColorBrush LevelColor { get; set; } = Brushes.Black;
    // 格式化显示时间字符串（可选，也可XAML格式化）
    public string TimeText => Time.ToString("HH:mm:ss");
}

public struct TestRunContext
{
    //当前频段下标
    public int CurPosition;
    public int CurrentBandIndex;
    public BandSwitchConfirmMode CurSwitchAction;
    public TestState currentTestState;
    public EMCStandard curStandard;
    public TestMethod testMode;
    //上一个测试位置点，频点和测试角度
    public int LastPos;
    public double LastFrequency;
    public double LastAngele;
    // 上一个仪器下标（用于释放资源）
    public int LastHardIndex;
    public bool receiveFlag;
    //功率数据(前向功率反向功率)
    public double LastForwardPower_W;
    public double LastReversePower_W;
    //信号源输出值记录
    public double LastSignalOut;
    //CS114 实时读数：注入电平(dBμA) / 反馈功率(dBm) / 参考校准电平(dBμA)
    public double LastInjectLevel_dBuA;
    public double LastFeedbackPower_dBm;
    public double LastCalLevel_dBuA;
    /// <summary>
    /// CS114限值
    /// </summary>
    public double LevelLimit;
    public double PowerLimitW;
    //过程实时测试数据
    public string TestName;   //测试名称
    public string TestDate;   //测试时间 年月日时分
    public double CurrentFre;
    public double CurrentStirrerV;
    public double CurrentStirrerH;
    public double Ex;
    public double Ey;
    public double Ez;
    public double Ec;
    public double Pinput; //接收功率
    public bool IsRestoreFromHistory;
    public TestRunContext()
    {
        CurrentBandIndex = 0; //默认从第一个频段开始
        currentTestState = TestState.IDLE;
        LastHardIndex = -1;
        CurrentFre = 0;
        receiveFlag = false;
        LastPos = 0;
        LastFrequency = 0;
        LastAngele = 0;
        IsRestoreFromHistory = false;
    }
}

public class CommonFreAndHard
{
    public CommonFreqStepBase FreqBase { get; set; } = new CommonFreqStepBase();
    public RVC_EMSHardModel HardModel { get; set; } = new();
}

namespace WpfEMCTestSoftware.RunTasks
{
    public partial class BaseRunTask : BaseViewModel, IPanelDataProvider
    {
        #region 图表数据
        /// <summary>
        /// 曲线频率坐标组
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<double> _testFreqList = new ObservableCollection<double>();


        /// <summary>
        /// 曲线通道表：显示模块 Key → 曲线集合
        /// 显示模块（RunConfigPage）通过 Key 从这里取数，测试项新增监测曲线只需注册一个通道，
        /// 不需要改页面、不需要新建模块类
        /// </summary>
        public Dictionary<string, ObservableCollection<ChartLineModel>> CurveChannels { get; } = new();

        /// <summary>
        /// 注册一条曲线通道（子类在构造里调用，声明本测试项要显示的监测曲线）
        /// </summary>
        protected void RegisterChannel(string moduleKey, ObservableCollection<ChartLineModel> lines)
            => CurveChannels[moduleKey] = lines;

        //==================== 曲线通道（基类只提供注册口，不配置任何默认曲线）====================//
        // 约定：基类不注册任何通道、不建任何曲线集合；
        // 子类在"自己的构造函数体"里建集合 + RegisterChannel 挂 Key（参照 CS114RunTask / DO160RunTask）。
        // 为什么不在基类构造里注册默认通道：构造顺序是"基类字段初始化器 → 基类构造函数体 →
        // 子类字段初始化器 → 子类构造函数体"，基类构造时子类集合还不存在，
        // 默认注册只会挂上基类自己的集合，子类同 Key 覆盖后这些集合就成了无人引用的孤儿，徒增困惑。

        #endregion
        #region 日志数据
        [ObservableProperty]
        private ObservableCollection<LogInfo> _logItems;
        #endregion

        #region 运行状态 / 进度（运行页顶部状态条 + 频段面板用）
        /// <summary>测试环境名（由运行页在切换测试项时写入）</summary>
        public string TestEnvName { get; set; } = "-";
        /// <summary>测试模板名（由运行页写入）</summary>
        public string TestTemplateName { get; set; } = "-";
        /// <summary>受试设备名</summary>
        public string EutName { get; set; } = "-";
        /// <summary>测试模式：参考校准 / EUT 测试</summary>
        [ObservableProperty]
        private RunMode _testRunMode = RunMode.Eut;

        /// <summary>模式显示文本（卡片与顶部状态条共用）</summary>
        public string RunModeText => TestRunMode == RunMode.Calibration ? "参考校准" : "EUT 测试";

        partial void OnTestRunModeChanged(RunMode value) => OnPropertyChanged(nameof(RunModeText));

        /// <summary>总体进度 0~100（跨频段的整体进度，按频点数加权，不是按频段数平分）</summary>
        [ObservableProperty]
        private double _testProgress;
        /// <summary>
        /// 频段进度显示文本（总进度的上下文补充）：
        /// "频段 2/3 · 段内 67/340 · 2.35 MHz"——回答"当前跑到哪个频段、这段还有多少点"
        /// </summary>
        [ObservableProperty]
        private string _bandProgressText = "-";
        /// <summary>当前频段内进度 0~100（顶部状态条显示；总进度 TestProgress 在左侧"运行进度"里）</summary>
        [ObservableProperty]
        private double _bandProgress;
        /// <summary>当前频点显示文本</summary>
        [ObservableProperty]
        private string _currentFrequencyText = "-";
        /// <summary>运行状态显示文本</summary>
        [ObservableProperty]
        private string _runStateText = "空闲";

        // ---- 结果判定摘要（左侧"结果"页顶部）----
        [ObservableProperty]
        private string _resultVerdict = "未测试";
        [ObservableProperty]
        private int _totalPoints;
        [ObservableProperty]
        private int _overLimitCount;
        [ObservableProperty]
        private double _worstMargin;

        // ---- 测试经过时间（顶部状态条显示；开始计时，暂停不计时）----
        [ObservableProperty]
        private string _elapsedText = "00:00:00";
        private readonly System.Diagnostics.Stopwatch _elapsedWatch = new();
        private System.Windows.Threading.DispatcherTimer? _elapsedTimer;

        private void StartElapsedTimer()
        {
            if (_elapsedTimer == null)
            {
                _elapsedTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _elapsedTimer.Tick += (_, _) => UpdateElapsedText();
            }
            _elapsedWatch.Start();
            UpdateElapsedText();
            _elapsedTimer.Start();
        }

        private void StopElapsedTimer()
        {
            _elapsedWatch.Stop();
            _elapsedTimer?.Stop();
            UpdateElapsedText();
        }

        private void UpdateElapsedText()
            => ElapsedText = $"{(int)_elapsedWatch.Elapsed.TotalHours:00}:{_elapsedWatch.Elapsed.Minutes:00}:{_elapsedWatch.Elapsed.Seconds:00}";

        /// <summary>把测试状态机状态同步成界面文字/进度（子类在状态变化处调用）</summary>
        protected void RefreshRunState()
        {
            RunStateText = TestContext.currentTestState switch
            {
                TestState.IDLE => "空闲",
                TestState.PRERUN => "准备中",
                TestState.RUNNING => "测试中",
                TestState.PAUSED => "已暂停",
                TestState.STOPPED => "已停止",
                TestState.COMPLETED => "已完成",
                _ => TestContext.currentTestState.ToString(),
            };
            // 同步全局测试状态：主窗体据此锁定导航栏等无关操作
            TestRunMonitor.SetState(TestContext.currentTestState);
        }

        // ==================== 进度（跨频段加权） ====================
        // 为什么总进度要跨频段：分频段是"功放频率范围不够"的技术妥协，不是用户的测试目标——
        // 操作员关心的是"整个测试还剩多久"。若进度只算频段内，会看到进度条跑到 100% 又跳回 0%
        // 反复横推（每切一次频段重来一遍），多频段时像是永远测不完。
        // 为什么按频点数加权而不是按频段数平分：各频段频点数可能差一个数量级
        // （低频段小步进几百点、高频段大步进几十点），平分会让进度条跳变节奏失真。

        /// <summary>各频段总频点数（下标与 CommonFreAndHardParms 对齐）；null = 尚未统计</summary>
        private int[]? _bandPointCounts;

        /// <summary>
        /// 按"已完成频点数 / 总频点数"推导整体进度，并刷新频段文本。
        /// ★ 完全由当前状态推导、不累加计数器：暂停/继续、停止后重启、换频段都不会让进度漂移。
        ///   已完成 = 前面各频段频点数之和 + (当前频段频点数 − 队列剩余数)
        ///   （暂停/停止时剩余频点留在 FrequencyQueue 里等恢复接着测，这样算出来天然正确）
        /// ★ 由"频点开始测"的位置调用（如 CS114RunTask 取频点处），频段起始/结束也各调一次。
        /// </summary>
        protected void UpdateTestProgress()
        {
            EnsureBandPointCounts();
            var counts = _bandPointCounts;
            int total = 0;
            if (counts != null)
                foreach (var c in counts) total += c;

            int bandIndex = TestContext.CurrentBandIndex;
            if (counts == null || total <= 0 || bandIndex < 0 || bandIndex >= counts.Length)
            {
                RunOnUI(() => { TestProgress = 0; BandProgress = 0; BandProgressText = "-"; });
                return;
            }

            int doneBefore = 0;
            for (int i = 0; i < bandIndex; i++) doneBefore += counts[i];

            int bandTotal = counts[bandIndex];
            // Clamp 兜底：历史恢复模式会剪掉队列里的已测点，此时"频点总数 − 队列剩余"会虚高，
            // 夹在本段总数内 → 最多算作本段已跑完，不会出现 >100% 的进度
            int bandDone = Math.Clamp(bandTotal - Math.Max(0, FrequencyQueue.Count), 0, bandTotal);
            double progress = Math.Clamp((doneBefore + bandDone) * 100.0 / total, 0, 100);
            // 频段文本不重复"当前频点"（左侧"运行进度"里已有该行），只补"第几段 / 段内进度"
            double bandPct = bandTotal == 0 ? 0 : bandDone * 100.0 / bandTotal;
            string text = $"频段 {bandIndex + 1}/{counts.Length} · 段内 {bandDone}/{bandTotal}（{bandPct:F0}%）";

            //TestProgress = 左侧"运行进度"的总进度（跨频段）；BandProgress = 顶部状态条的段内进度
            RunOnUI(() => { TestProgress = progress; BandProgress = bandPct; BandProgressText = text; });
        }

        /// <summary>
        /// 统计各频段频点数并缓存。频点数只取决于频率配置、测试过程中不变，
        /// 频段数变了（换配置）会自动重算。开始测试时调用一次即可。
        /// </summary>
        private void EnsureBandPointCounts()
        {
            if (_bandPointCounts != null && _bandPointCounts.Length == CommonFreAndHardParms.Count)
                return;

            var counts = new int[CommonFreAndHardParms.Count];
            int total = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                counts[i] = CalculateAllFreqPoints(i).Count;
                total += counts[i];
            }
            _bandPointCounts = counts;
            // 频点总数上屏：操作员能预估整个测试的规模、判断进度条走得对不对
            AddLog("INFO", $"频点统计：共 {total} 个频点（各频段 {string.Join(" / ", counts)}）");
        }

        /// <summary>进度归零（开始测试、强制停止时调用；不能走 UpdateTestProgress——
        /// 此时队列里可能还留着上一轮的剩余频点，推导出来会是个非 0 的脏值）。
        /// 顺便让频点数缓存失效：用户可能换了配置（频段数相同但频率不同），下次进入频段时重算</summary>
        private void ResetTestProgress()
        {
            _bandPointCounts = null;
            RunOnUI(() => { TestProgress = 0; BandProgress = 0; BandProgressText = "-"; });
        }

        /// <summary>全部频段测完：进度锁定 100%（不能靠推导——UserBandIsComplete 会把频段索引归零）</summary>
        private void MarkTestProgressCompleted()
            => RunOnUI(() =>
            {
                TestProgress = 100;
                BandProgress = 100;
                BandProgressText = $"全部 {CommonFreAndHardParms.Count} 个频段测试完成";
            });
        #endregion

        #region 面板数据提供者（参数卡 / 结果表的数据源）
        private readonly Dictionary<string, ParamGroup> _paramGroups = new();
        private readonly Dictionary<string, ResultTableData> _resultTables = new();

        /// <summary>取/建参数组（缓存实例，保证实时刷新时不重建、不闪烁）</summary>
        protected ParamGroup Group(string key, string title)
        {
            if (!_paramGroups.TryGetValue(key, out var g))
            {
                g = new ParamGroup { Key = key, Title = title };
                _paramGroups[key] = g;
            }
            else if (!string.IsNullOrEmpty(title))
            {
                g.Title = title;
            }
            return g;
        }

        /// <summary>取/建表格数据（行集合与界面共享引用，任务往里加数据界面自动刷新）</summary>
        protected ResultTableData Table(string key, string title, params string[] columns)
        {
            if (!_resultTables.TryGetValue(key, out var t))
            {
                t = ResultTableData.Of(key, title, columns);
                _resultTables[key] = t;
            }
            return t;
        }

        /// <summary>
        /// 步进显示文字：步进模式 LIN = 步进值 + 频率单位（如 1 MHz）；
        /// 步进模式 LOG = 百分比（步进值即百分比数值，如 10 %）
        /// </summary>
        private static string FormatStepText(CommonFreqStepBase fre)
            => fre.MStepMode == STEPMODE.LOG ? $"{fre.StepSize} %" : $"{fre.StepSize} {fre.StepSizeUnit}";

        /// <summary>
        /// 默认参数组：测试信息 + 当前频段（所有测试项通用）
        /// 各测试项 RunTask 可重写本方法，追加自己的参数组（如 CS114 的"调制方式/反馈控制"）
        /// </summary>
        public virtual IEnumerable<ParamGroup> GetParamGroups()
        {
            //1. 当前频段：前 5 项是静态频段信息（一个频段内不会变），后 4 项是实时读数
            var band = Group("Card.Band", "当前频段");
            var cur = CurrentFreqBase;
            band.Set(
                ("频段序号", string.IsNullOrEmpty(CurrentFrequencyText) || CommonFreAndHardParms.Count == 0
                    ? "-" : $"{TestContext.CurrentBandIndex + 1} / {CommonFreAndHardParms.Count}"),
                ("起始频率", cur == null ? "-" : $"{GenericHelper.ConvertToMhzData(cur.StartFre, cur.StartFreUnit)} MHz"),
                ("终止频率", cur == null ? "-" : $"{GenericHelper.ConvertToMhzData(cur.EndFre, cur.EndFreUnit)} MHz"),
                ("步进", cur == null ? "-" : FormatStepText(cur)),
                ("驻留时间", cur == null ? "-" : $"{cur.FreqDwellTime} ms"),
                ("当前频点", CurrentFreqText),
                ("前向功率", ForwardPowerText),
                ("反向功率", ReversePowerText),
                ("信号源输出", SignalOutText));
            yield return band;


        }

        /// <summary>
        /// 重新驱动一遍 GetParamGroups()：Group() 走缓存、Set() 原地更新键值，
        /// 参数卡持有同一 Items 引用，本方法返回后界面自动刷新，无需重建卡片
        /// </summary>
        public void RefreshPanelGroups()
        {
            foreach (var _ in GetParamGroups()) { }
        }
        //==================== "当前频段"卡实时读数 ====================//
        /// <summary>当前频段的频率参数（无频段或索引越界时返回 null）</summary>
        private CommonFreqStepBase? CurrentFreqBase
        {
            get
            {
                int i = TestContext.CurrentBandIndex;
                return CommonFreAndHardParms.Count > 0 && i >= 0 && i < CommonFreAndHardParms.Count
                    ? CommonFreAndHardParms[i].FreqBase
                    : null;
            }
        }

        /// <summary>有可用频段时才显示实时数值，否则一律显示 "-"</summary>
        private bool HasFreqBase => CurrentFreqBase != null;

        /// <summary>"当前频段"卡分组（单项刷新共用，Group() 有缓存、拿到的都是同一个实例）</summary>
        private ParamGroup BandGroup => Group("Card.Band", "当前频段");
        private string CurrentFreqText => HasFreqBase ? $"{TestContext.CurrentFre:0.###} MHz" : "-";
        private string ForwardPowerText => HasFreqBase ? $"{TestContext.LastForwardPower_W:0.###} W" : "-";
        private string ReversePowerText => HasFreqBase ? $"{TestContext.LastReversePower_W:0.###} W" : "-";
        private string SignalOutText => HasFreqBase ? $"{TestContext.LastSignalOut:0.###} dBm" : "-";
        public void RefreshCurrentFrequency() => BandGroup.Set(("当前频点", CurrentFreqText));
        public void RefreshForwardPower() => BandGroup.Set(("前向功率", ForwardPowerText));
        public void RefreshReversePower() => BandGroup.Set(("反向功率", ReversePowerText));
        public void RefreshFREQ()
        {
            BandGroup.Set(("起始频率", CurrentFreqBase == null ? "-" : 
                $"{GenericHelper.ConvertToMhzData(CurrentFreqBase.StartFre, CurrentFreqBase.StartFreUnit)} MHz"));
            BandGroup.Set(("终止频率", CurrentFreqBase == null ? "-" :
                $"{GenericHelper.ConvertToMhzData(CurrentFreqBase.EndFre, CurrentFreqBase.EndFreUnit)} MHz"));
        }

        public void RefreshSignalOut() => BandGroup.Set(("信号源输出", SignalOutText));
        //======================== ""========================================//
        public virtual IEnumerable<ResultTableData> GetResultTables()
        {
            var desc = ModuleCatalog.Get("Table.Result");
            yield return Table("Table.Result", desc.Title, desc.Columns);
        }
        #endregion

        public List<CommonFreAndHard> CommonFreAndHardParms { get; } = [];
        public const double MaxSignalOutFallbackDbm = 6;
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
        public BaseRunTask()
        {
            _logItems = new ObservableCollection<LogInfo>();
            BandSwitchTimer = new System.Timers.Timer();
            //曲线通道完全由子类负责：子类构造函数体里建集合 + RegisterChannel 挂 Key，
            //基类这里不注册任何默认通道（未注册的 Key 对应图表显示为空，不影响其他模块）。
            //频段下标
            Messenger.Register<UniversalIntMessage, int>(this, 204, (r, m) =>
            {
                TestContext.CurrentBandIndex = m.Value;
                RefreshPanelGroups();   //频段下标变化后，原地刷新"当前频段"等参数卡
            });
            BandSwitchTimer.Elapsed += BandSwitchTimer_Elapsed;

        }
        [ObservableProperty]
        private ObservableCollection<SockConnect?> _socks = [];
        [ObservableProperty]
        private int _onLineCount = 0;
        //【总控制器】切换频段，连接设备
        private System.Timers.Timer BandSwitchTimer;
        //==================== 运行代次（解决 停止/暂停/重启 的线程竞争）====================//
        // 每次 StartTest / StopTest 递增；正在跑的循环线程进入时记下"自己那一代"，
        // 之后每一圈用 IsLoopActive 比对：代次对不上（已被停止/重启）立即退出。
        // 这样即使 StopTest 后旧线程还卡在读数等待里，也不会被新一轮 RUNNING 状态"复活"。
        private int _runGeneration;
        /// <summary>当前运行代次（每次启动/停止递增）</summary>
        protected int RunGeneration => Volatile.Read(ref _runGeneration);
        /// <summary>本循环线程所属代次（循环入口前由 BeginLoopCycle 捕获）</summary>
        private int _loopGeneration;
        /// <summary>
        /// 循环继续条件：状态为 RUNNING 且代次未过期。
        /// 子类所有 循环条件/等待中断 都应该用它，不要直接比对 currentTestState
        /// </summary>
        protected bool IsLoopActive => TestContext.currentTestState == TestState.RUNNING
                                       && _loopGeneration == Volatile.Read(ref _runGeneration);
        /// <summary>循环线程入口处调用：记下当前代次（BandSwitchTimer_Elapsed / ResumeTest 里已调）</summary>
        protected void BeginLoopCycle() => _loopGeneration = Volatile.Read(ref _runGeneration);
        //【频率队列】
        public Queue<double> FrequencyQueue = [];
        //【垂直搅拌器】
        public Queue<double> StirrerVQueue = [];
        //【水平搅拌器】
        public Queue<double> StirrerHQueue = [];
        //接收到的数据队列
        public Queue<double> ReceiveDataQueue = [];
        //测试运行参数
        public TestRunContext TestContext;
        public List<DeviceType> MapToDeviceType { get; set; } = new List<DeviceType>();
        #region//--------------------------------------------基类虚函数---------------------------------------------------//
        protected virtual void GenerateatTestName() { }

        public virtual void SaveData() { }

        /// <summary>
        /// 最近一次 SaveData 生成的报告文件全路径（子类保存成功后赋值）。
        /// 界面据此把曲线图保存到报告同目录，作为"报告附图"；未生成报告时为 null。
        /// </summary>
        public string? LastSavedReportPath { get; set; }
        protected virtual bool ModuleInitByFreBand() { return false; }

        protected virtual bool AsiggnBandSubTask() { return true; }

        protected virtual void ReleaseDevice() { }
        /// <summary>
        /// 暂停时由 PauseTest 调用：子类关闭射频输出等危险源。
        /// 安全要求：暂停期间不得向 EUT 注入信号，输出必须关闭
        /// </summary>
        protected virtual void PauseOutput() { }
        /// <summary>
        /// 恢复测试时由 ResumeTest 调用：重新打开射频输出（与 PauseOutput 成对）
        /// </summary>
        protected virtual void ResumeOutput() { }
        //刷新循环队列
        protected virtual void RefreshInOrOutLoopQueue() { }
        //
        protected virtual void RefreshLimitLinesByFreqRange() { }
        /// <summary>
        /// 执行外侧循环事件
        /// </summary>
        protected virtual void ExcuteOutsideLoopEvent() { }
        /// <summary>
        /// 执行内侧循环事件
        /// </summary>
        protected virtual void ExcuteInsideLoopEvent() { }
        /// <summary>
        /// 从数据库加载历史数据
        /// </summary>
        /// <param name="testName"></param>
        public virtual void LoadHistoryFromDataBase(string testName) { }
        /// <summary>
        /// 更新数据到点位
        /// </summary>
        public virtual void UpdateDataToPos() { }
        /// <summary>
        /// 恢复历史测试剔除队列中的历史频点
        /// </summary>
        protected virtual void TrimQueueByHistoryBreakPoint()
        {
        }
        #endregion
        #region//--------------------------------------------通用辅助函数---------------------------------------------------//
        /// <summary>
        /// 计算所有的旋转角度
        /// </summary>
        /// stirrerV垂直角度，stirrerH水平角度
        /// <returns></returns>
        public void CalculateAllAnglePoints(Queue<double> StirrerV, Queue<double> StirrerH)
        {
            var curHardDevice = CommonFreAndHardParms[TestContext.CurrentBandIndex].HardModel;
            StirrerPara StirrerVPara = (StirrerPara)curHardDevice.ListDeviceInfos[DeviceIndexConst.StirrerV];
            StirrerPara StirrerHPara = (StirrerPara)curHardDevice.ListDeviceInfos[DeviceIndexConst.StirrerH];
            double startVAngle = 0, startHAngle = 0.0;
            while (startVAngle <= 360)
            {
                StirrerV.Append(startVAngle);
                startVAngle += StirrerVPara.StepAngeleSet;
            }
            if (StirrerV.Count == 0 || Math.Abs(StirrerV.Last() - 360) > 1e-9)
            {
                StirrerV.Append(360);
            }

            while (startHAngle <= 360)
            {
                StirrerH.Append(startHAngle);
                startHAngle += StirrerHPara.StepAngeleSet;
            }
            if (StirrerH.Count == 0 || Math.Abs(StirrerH.Last() - 360) > 1e-9)
            {
                StirrerH.Append(360);
            }

        }
        /// <summary>
        /// 根据起始终止频率，计算频点数
        /// </summary>
        /// <returns></returns>
        public Queue<double> CalculateAllFreqPoints(int Index)
        {
            Queue<double> LoopQueue = new Queue<double>();
            var CommonFre = CommonFreAndHardParms[Index].FreqBase;
            // 1. 单位换算、频点生成逻辑
            double startMhz = GenericHelper.ConvertToMhzData(CommonFre.StartFre, CommonFre.StartFreUnit);
            double endMHz = GenericHelper.ConvertToMhzData(CommonFre.EndFre, CommonFre.EndFreUnit);
            double stepMhz = GenericHelper.ConvertToMhzData(CommonFre.StepSize, CommonFre.StepSizeUnit);

            if (startMhz > endMHz || stepMhz <= 0)
                return LoopQueue;

            if (CommonFre.MStepMode == STEPMODE.LIN)
            {
                double curr = startMhz;
                while (curr <= endMHz)
                {
                    LoopQueue.Enqueue(curr);
                    curr += stepMhz;
                }
                // 仅当队列最后一个频点不等于终点时，才补充终点
                if (LoopQueue.Count == 0 || Math.Abs(LoopQueue.Last() - endMHz) > 1e-9)
                {
                    LoopQueue.Enqueue(endMHz);
                }
            }
            else
            {
                double curr = startMhz;
                double percent = CommonFre.StepSize / 100.0;
                while (curr <= endMHz)
                {
                    LoopQueue.Enqueue(curr);
                    curr = curr * (1 + percent);
                }
                // 仅当队列最后一个频点不等于终点时，才补充终点
                if (LoopQueue.Count == 0 || Math.Abs(LoopQueue.Last() - endMHz) > 1e-9)
                {
                    LoopQueue.Enqueue(endMHz);
                }
            }
            // 所有分支最终都返回队列
            return LoopQueue;
        }
        /// <summary>
        /// 从队列中获取数据
        /// </summary>
        /// <param name="sock"></param>
        /// <param name="timeouts"></param>
        /// <returns></returns>
        public double GetDataFromQueue(SockConnect sock, int timeouts)
        {

            // 设备为空 → 返回无效
            if (sock == null)
                return double.NaN;

            try
            {
                // VISA 模式：同步读取
                if (sock.CommunicationType == CommunicationMode.VISA)
                {
                    string data = sock.ReadData();
                    return double.TryParse(data, out double val) ? val : double.NaN;

                }
                // 中断/队列模式：等待队列数据
                else
                {
                    return WaitAndGetData(timeouts);
                }
            }
            catch
            {
                return double.NaN;
            }
        }
        private double WaitAndGetData(int timeouts)
        {
            DateTime startTime = DateTime.Now;
            while (ReceiveDataQueue.Count == 0)
            {
                // 检查超时
                if ((DateTime.Now - startTime).TotalMilliseconds > timeouts)
                {
                    return double.NaN; // 超时
                }
                Thread.Sleep(10);
            }
            // 收到完成标志后，从队列获取数据
            if (ReceiveDataQueue.Count > 0)
            {
                return ReceiveDataQueue.Dequeue();
            }
            return double.NaN;
        }
        public void SetAmplifierOut(bool enable, SockConnect? AmplifierSock)
        {
            if (AmplifierSock == null || !AmplifierSock.Initialed)
                return;
            if (enable)
                AmplifierSock.SendData((int)Communication.ENABlE_RFOUT, "");
            else
                AmplifierSock.SendData((int)Communication.DISABlE_RFOUT, "");
            ClsDelay.Delay.MicrosecondDelay(200000);

        }

        /// <summary>
        /// 在 UI 线程执行动作：测试循环跑在线程池线程，
        /// 凡是循环里要碰界面绑定对象（参数卡/图表集合/可观察属性）都必须从这里走，
        /// 否则改 ObservableCollection 会抛 NotSupportedException
        /// </summary>
        protected static void RunOnUI(Action action)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                action();
            else
                dispatcher.Invoke(action);
        }

        /// <summary>
        /// 添加日志快捷方法（内部切到 UI 线程执行：
        /// LogItems 是绑定到 ListBox 的 ObservableCollection，后台线程直接 Add 会抛异常）
        /// </summary>
        public void AddLog(string level, string msg)
        {
            RunOnUI(() =>
            {
                SolidColorBrush Color = level switch
                {
                    "ERROR" => Brushes.Red,
                    "WARN" => Brushes.Orange,
                    _ => Brushes.Black,
                };
                LogItems.Add(new LogInfo
                {
                    Time = DateTime.Now,
                    Level = level,
                    LevelColor = Color,
                    Message = msg
                });
                if (LogItems.Count > 200)
                {
                    LogItems.RemoveAt(0);
                }
                ClassLibrary.LogManager.WriteLog(msg);
            });
        }
        /// <summary>
        /// 往指定曲线通道追加一个数据点。
        /// ★ x 必须是 Hz（UserChart 坐标系与 XMinLimit/XMaxLimit 都是 Hz；传 MHz 会画到可视区外，
        ///   数据在集合里但不显示、且不报错）。y 的单位见 ModuleCatalog 中该 Key 的 YAxisTitle。
        /// ★ 只能按通道 Key 打点：图表模块是拿 ModuleKey 去 CurveChannels 取集合的，
        ///   键与 ModuleProfileRegistry 里配置的一致，数据才画得出来。
        /// 内部自动切 UI 线程；通道未注册时只记日志，不抛异常（不能打断测试循环）
        /// </summary>
        /// <param name="moduleKey">曲线通道 Key（如 Chart.FwdPower，见 ModuleCatalog）</param>
        /// <param name="x">X 值（频率，Hz）</param>
        /// <param name="y">Y 值（单位见 ModuleCatalog 中该 Key 的 YAxisTitle）</param>
        /// <param name="lineTitle">同通道多条曲线时指定曲线标题；为空取第一条</param>
        public void AddChannelData(string moduleKey, double x, double y, string? lineTitle = null)
        {
            RunOnUI(() =>
            {
                if (CurveChannels.TryGetValue(moduleKey, out var lines) && lines.Count > 0)
                {
                    var line = string.IsNullOrEmpty(lineTitle)
                        ? lines[0]
                        : lines.FirstOrDefault(l => l.Title == lineTitle);
                    if (line != null)
                    {
                        line.XData.Add(x);
                        line.YData.Add(y);
                        return;
                    }
                }
                AddLog("WARN", $"曲线通道 {moduleKey} 未注册（或曲线 {lineTitle} 不存在），数据点 {x:0.###} / {y:0.###} 未上图");
            });
        }
        //检测用户频段是否遍历完成
        private bool UserBandIsComplete()
        {
            //还有频段没测完 → 继续
            if (TestContext.CurrentBandIndex < CommonFreAndHardParms.Count)
                return false;

            //所有频段完成：状态置完成、释放设备、索引归零，方便下次从头开始
            TestContext.currentTestState = TestState.COMPLETED;
            ReleaseDevice();
            MarkTestProgressCompleted();   //锁定 100%：紧接着索引会归零，靠推导会算出 0%
            //测试全部完成：清理接续临时表（否则下次选择该模板仍会提示"存在未完成的测试"）
            try { ClsData.Instance.ClearCurveTemp(TestTemplateName); } catch { }
            TestContext.CurrentBandIndex = 0;
            StopElapsedTimer();
            RefreshRunState();
            return true;
        }
        /// <summary>
        /// 提醒外侧循环事件已结束，队列已耗尽
        /// </summary>
        public void NotifyOutsideLoopComplete()
        {
            //本频段队列已耗尽：先按"空队列"刷一次进度（本段算满），再切下一频段，
            //这样进度条是一格一格往前走，不会先跳回 0% 再补上
            UpdateTestProgress();
            TestContext.currentTestState = TestState.PRERUN;
            RefreshRunState();   // 同步全局状态（准备中也应锁定导航栏）
            TestContext.CurrentBandIndex++;
            RunOnUI(RefreshFREQ);
            BandSwitchTimer.Start();
        }

        public virtual void SetIndex(int index, DeviceType type)
        {
            // 把设备类型存起来，和 Socks 一一对应
            MapToDeviceType.Insert(index, type);
        }

        #region 接续测试（Req 2：中途停止后续测，通用所有测试项）
        //==================== 接续测试状态 ====================//
        // 中途停止时把曲线通道（全部幅值/功率）写入数据库临时表；再次选择同一模板时提示接续，
        // 确认后从上一测试最后一个频点继续（按频点队列裁剪，跨频段逐段生效）。
        // 与 DO160 原有的 ReverbUniformityTemp 历史恢复互不干扰：这里是"通用曲线级"续测，不复用其位置/角度模型。
        private double _resumeFromFreqHz = -1;   // 接续起点频率(Hz)；<0 表示非接续
        private bool _pendingResume = false;     // StartTest 时是否已加载接续数据（避免被"清空曲线"逻辑抹掉）

        /// <summary>
        /// 把当前所有曲线通道（非参考线）的实测数据快照写入数据库临时表，供中途停止后续测。
        /// 必须在 UI 线程调用（曲线集合是 ObservableCollection，后台线程读会抛异常）。
        /// </summary>
        public void SaveCurveTempToDatabase()
        {
            if (string.IsNullOrEmpty(TestTemplateName) || TestTemplateName == "-")
                return;
            var rows = new List<ClsData.CurveTempRow>();
            RunOnUI(() =>
            {
                foreach (var kv in CurveChannels)
                {
                    foreach (var line in kv.Value)
                    {
                        if (line.IsYReferenceLine) continue;   // 参考限值线是配置数据，不是实测，不落临时表
                        int n = Math.Min(line.XData.Count, line.YData.Count);
                        for (int i = 0; i < n; i++)
                        {
                            rows.Add(new ClsData.CurveTempRow
                            {
                                ChannelKey = kv.Key,
                                LineTitle = line.Title ?? "",
                                Seq = i,
                                XHz = line.XData[i],
                                YValue = line.YData[i],
                                IsReference = false,
                            });
                        }
                    }
                }
            });
            if (rows.Count == 0)
            {
                AddLog("INFO", "接续临时表：当前无曲线数据可保存");
                return;
            }
            try
            {
                ClsData.Instance.SaveCurveTempData(TestTemplateName, rows);
                AddLog("INFO", $"已把当前 {rows.Count} 个曲线点写入临时表，可在下次接续测试");
            }
            catch (Exception ex)
            {
                AddLog("ERROR", $"写入接续临时表失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 从临时表加载上一未完成测试的曲线数据到本任务，并标记"接续"：
        /// 曲线图表立即显示历史进度；StartTest 时会跳过"清空曲线"，并按最后一个频点裁剪频点队列。
        /// 在 UI 线程调用。
        /// </summary>
        public void LoadAndPrepareResume(string templateName)
        {
            if (string.IsNullOrEmpty(templateName)) return;
            var rows = ClsData.Instance.GainCurveTempData(templateName);
            if (rows == null || rows.Count == 0)
            {
                _pendingResume = false;
                _resumeFromFreqHz = -1;
                return;
            }
            RunOnUI(() =>
            {
                foreach (var kv in CurveChannels)
                {
                    foreach (var line in kv.Value)
                    {
                        if (line.IsYReferenceLine) continue;
                        var lineRows = rows
                            .Where(r => r.ChannelKey == kv.Key && r.LineTitle == (line.Title ?? ""))
                            .OrderBy(r => r.Seq)
                            .ToList();
                        if (lineRows.Count == 0) continue;
                        var xs = new List<double>(lineRows.Count);
                        var ys = new List<double>(lineRows.Count);
                        foreach (var r in lineRows)
                        {
                            xs.Add(r.XHz);
                            ys.Add(r.YValue);
                        }
                        line.XData = new ObservableCollection<double>(xs);
                        line.YData = new ObservableCollection<double>(ys);
                    }
                }
            });
            _resumeFromFreqHz = rows.Count > 0 ? rows.Max(r => r.XHz) : -1;
            _pendingResume = true;
            AddLog("INFO", $"已加载上一未完成测试数据（末频点 {_resumeFromFreqHz / 1e6:0.###} MHz），点击【开始】将从该频点继续");
        }

        /// <summary>
        /// 接续测试：把频点队列裁剪到"上一未完成测试的最后一个频点"之后，
        /// 使本轮从断点继续（跨频段逐段生效，因为 FrequencyQueue 每个频段都会重新填充）。
        /// </summary>
        protected virtual void TrimQueueToResumePoint()
        {
            if (_resumeFromFreqHz < 0) return;
            double lastFreqMHz = _resumeFromFreqHz / 1e6;
            // 保留严格大于末频点的频点（已测点不再重复测）
            var remaining = FrequencyQueue.Where(f => f > lastFreqMHz + 1e-9).ToList();
            FrequencyQueue = new Queue<double>(remaining);
            if (remaining.Count == 0)
            {
                // 所有频点都已测完（理论上 UserBandIsComplete 已先触发），保险：清空续测标记
                _resumeFromFreqHz = -1;
            }
        }
        #endregion

        public SockConnect? GetSockByType(DeviceType type)
        {
            int index = MapToDeviceType.FindIndex(t => t == type);
            if (index >= 0 && index < Socks.Count)
                return Socks[index];

            return null;
        }
        public List<SockConnect?> GetAllSocksByType(DeviceType type)
        {
            List<SockConnect?> result = new List<SockConnect?>();
            for (int i = 0; i < MapToDeviceType.Count; i++)
            {
                if (MapToDeviceType[i] == type && i < Socks.Count)
                    result.Add(Socks[i]);
            }
            return result;
        }
        /// <summary>
        /// 连接所有通信设备
        /// </summary>
        /// <returns></returns>
        private bool ConnectAllHardDevice()
        {
            bool allConnected = true;
            try
            {
                //切换频段会重新进入本方法：先释放上一频段的连接，避免 Socks/设备映射重复累积
                foreach (var old in Socks)
                {
                    try { old?.Close(); } catch { /* 释放失败不阻断重连 */ }
                }
                Socks.Clear();
                MapToDeviceType.Clear();

                var DeviceListInfo = CommonFreAndHardParms[TestContext.CurrentBandIndex].HardModel.ListDeviceInfos;
                for (int i = 0; i < DeviceListInfo.Count; i++)
                {
                    if (string.IsNullOrEmpty(DeviceListInfo[i].Name) || DeviceListInfo[i].Name == "No Device") continue;

                    SockConnect? sock = null;
                    bool isOk = ConnectSingleHardDevice(DeviceListInfo[i], info => sock = info);
                    if (isOk)
                    {
                        Socks.Add(sock);
                        SetIndex(Socks.Count - 1, DeviceListInfo[i].Type);
                        //连接信息上屏：操作员需要知道每台设备的连接结果                      
                    }
                    else
                    {
                        AddLog("ERROR", $"设备【{DeviceListInfo[i].Name}】({DeviceListInfo[i].Type}) 连接失败，请检查设备配置与网络");
                    }
                    allConnected &= isOk;
                }
                return allConnected;
            }
            catch (Exception ex)
            {
                sendMessagelog($"设备连接异常：{ex.Message}");
                return false;
            }
        }
        /// <summary>
        /// 连接单个设备
        /// </summary>
        /// <param name="deviceInfo"></param>
        /// <param name="assignConnection"></param>
        /// <returns></returns>
        private bool ConnectSingleHardDevice(DeviceInfo deviceInfo, Action<SockConnect> assignConnection)
        {
            if (deviceInfo == null)
            {

                return false;
            }
            try
            {
                // 创建连接对象 - 固定逻辑
                var connection = ClsFactory.GainSock(deviceInfo.Model);
                if (connection == null)
                {

                    return false;
                }
                connection.AddShowLog += Connection_AddShowLog;
                if (deviceInfo.IsActive)
                {
                    // 初始化连接 - 固定逻辑
                    connection.InitConnection(deviceInfo.DevicePara);

                    if (!connection.Initialed)
                    {
                        return false;
                    }
                    AddLog("INFO", $"设备【{deviceInfo.Name}】({deviceInfo.Type}) 连接成功");
                }
                connection.Name = deviceInfo.Name;
                // 赋值连接对象
                assignConnection(connection);

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public void Connection_AddShowLog(string arg1, int arg2)
        {
            ClassLibrary.LogManager.WriteLog("Device", arg1);
        }

        /// <summary>
        /// 发送日志（连接/初始化过程的消息统一走这里上屏 + 落文件）
        /// </summary>
        /// <param name="message"></param>
        public void sendMessagelog(string message)
        {
            //旧实现是空方法——连接失败等信息全被吞掉，操作员完全看不到。
            //统一路由到 AddLog（内部已切 UI 线程 + 落文件）
            AddLog("WARN", message);
        }
        /// <summary>
        /// 初始化测试硬件与扫频参数：连接设备、刷新频点循环队列、初始化子任务
        /// </summary
        private bool InitTestHardwareAndFreqParam()
        {
            //1连接所有的设备
            DisconnectAllSocks();
            if (!ConnectAllHardDevice())
                return false;
            OnLineCount = Socks.Where(s => s.Initialed).Count();
            //2.刷新内外侧循环队列
            RefreshInOrOutLoopQueue();
            //3.分发子模块任务
            AsiggnBandSubTask();
            return true;
        }
        /// <summary>
        /// 开始测试
        /// </summary>

        /// <summary>
        /// 频段切换定时器事件：自动切换测试频段 + 初始化设备 + 启动测试
        /// </summary>
        private void BandSwitchTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            BandSwitchTimer.Stop();
            try
            {
                // 1. 检测所有频段是否已经遍历完成，完成则直接退出
                if (UserBandIsComplete())
                {
                    DisconnectAllSocks();
                    return;
                }
                AddLog("INFO", "开始进行设备连接");

                // 2. 初始化测试硬件 频点参数（连接设备可能耗时数秒，期间用户可能已点 停止/暂停）
                if (!InitTestHardwareAndFreqParam())
                {
                    // 修复：此前初始化失败只记日志就 return，状态仍停在 PRERUN/RUNNING，
                    // 必须手动点“停止”才能恢复。现在自动回滚到 STOPPED（等同“测试开始前状态”），
                    // 释放可能已部分连接的设备，并让“开始”按钮重新可用。
                    AddLog("WARN", "开始测试失败：设备初始化/连接未通过，已自动回滚到停止状态（请检查设备配置与网络后重试）");
                    try { DisconnectAllSocks(); } catch { }
                    TestContext.currentTestState = TestState.STOPPED;
                    RefreshRunState();
                    return;
                }
                AddLog("INFO", "设备连接成功");

                // 2.5 仅历史恢复模式，执行一次队列剪裁
                if (TestContext.IsRestoreFromHistory)
                {
                    TrimQueueByHistoryBreakPoint();
                    TestContext.IsRestoreFromHistory = false;
                }

                // 2.6 通用"接续测试"：从断点频点继续（每个频段填充队列后裁剪一次，跨频段逐段生效）
                if (_resumeFromFreqHz >= 0)
                    TrimQueueToResumePoint();

                // 3. 初始化期间用户已暂停：保留设备连接，恢复时从本频段继续
                if (TestContext.currentTestState == TestState.PAUSED)
                    return;
                // 4. 初始化期间用户已停止（或其它非启动状态）：放弃本轮启动，释放刚连上的设备
                if (TestContext.currentTestState != TestState.PRERUN)
                {
                    try { ReleaseDevice(); } catch { }
                    DisconnectAllSocks();
                    return;
                }
                TestContext.currentTestState = TestState.RUNNING;
                RefreshRunState();
                //频段切换信息上屏：操作员能看出当前测到第几个频段、覆盖范围
                var bandFre = CommonFreAndHardParms[TestContext.CurrentBandIndex].FreqBase;
                AddLog("INFO", $"开始测试频段 {TestContext.CurrentBandIndex + 1}/{CommonFreAndHardParms.Count}：" +
                    $"{GenericHelper.ConvertToMhzData(bandFre.StartFre, bandFre.StartFreUnit)} MHz ~ " +
                    $"{GenericHelper.ConvertToMhzData(bandFre.EndFre, bandFre.EndFreUnit)} MHz");
                //频点队列刚刷新好 → 刷一次进度（本段 0%，总进度 = 前面频段已完成的占比）
                UpdateTestProgress();
                // 5. 记录本循环线程的运行代次，再执行外层循环任务
                BeginLoopCycle();
                ExcuteOutsideLoopEvent();
            }
            catch (Exception ex)
            {
                RunOnUI(() => AddLog("ERROR", $"测试循环异常：{ex.Message}"));
                TestContext.currentTestState = TestState.STOPPED;
                RefreshRunState();
                try { ReleaseDevice(); } catch { }
                DisconnectAllSocks();
            }
        }

        public virtual void CompleteBand()
        {

        }
        /// <summary>
        /// 开始测试按钮
        /// </summary>
        public void StartTest()
        {
            //暂停中点开始 = 继续测试
            if (TestContext.currentTestState == TestState.PAUSED)
            {
                ResumeTest();
                return;
            }
            //运行中/准备中不允许重复启动
            if (TestContext.currentTestState is TestState.RUNNING or TestState.PRERUN)
                return;

            // ===== 前置条件校验：不满足则自动回滚，绝不停留在“运行中/准备中” =====
            // 修复：此前若开始条件不满足（如未配置频段、设备初始化失败），任务会卡在
            // PRERUN/RUNNING，必须手动点“停止”才能恢复到空闲；现在检测不通过立即回滚到 IDLE。
            if (!CheckStartPreconditions(out var preReason))
            {
                AddLog("WARN", $"开始测试被取消：{preReason}（已自动回滚到空闲状态）");
                TestContext.currentTestState = TestState.IDLE;
                RefreshRunState();
                return;
            }

            //IDLE / STOPPED / COMPLETED 都算全新开始：重置频段索引、计时归零
            TestContext.CurrentBandIndex = 0;
            _elapsedWatch.Reset();
            ResetTestProgress();   //强制归零：此时队列里可能还留着上一轮的剩余频点，推导会得出脏值

            // 接续测试：保留上一轮已测曲线（图表直接显示历史进度），不清空
            if (!_pendingResume)
            {
                //新一轮开始 = 覆盖式测试：清空上一轮的曲线点位，否则新旧两轮的点混画在一张图上
                //（AddChannelData 是 Append 语义，不清就会一直往下追加）。
                //参考线（limit 限值折线）是配置数据不是测试数据，跳过不清。
                RunOnUI(() =>
                {
                    foreach (var lines in CurveChannels.Values)
                        foreach (var line in lines)
                        {
                            if (line.IsYReferenceLine) continue;
                            line.XData = new ObservableCollection<double>();
                            line.YData = new ObservableCollection<double>();
                        }
                });
            }

            //开启新一轮运行：旧代次立即作废（残留的旧循环线程下一圈检查时退出，且不会被本轮 RUNNING 复活）
            Volatile.Write(ref _runGeneration, Volatile.Read(ref _runGeneration) + 1);

            TestContext.currentTestState = TestState.PRERUN;
            StartElapsedTimer();
            RefreshRunState();
            // 接续测试：跳过清空临时数据（保留已测进度）；否则全新开始，清空该模板接续临时数据
            if (_pendingResume)
            {
                AddLog("INFO", "【开始测试】接续上次未完成的测试，将从断点频点继续");
            }
            else
            {
                try { ClsData.Instance.ClearCurveTemp(TestTemplateName); } catch { }
            }
            _pendingResume = false;   // 接续标记已消费（_resumeFromFreqHz 保留用于逐频段裁剪队列）
            AddLog("INFO", $"【开始测试】准备连接设备，共 {CommonFreAndHardParms.Count} 个频段");
            BandSwitchTimer.Start();
        }

        /// <summary>
        /// 开始测试前置条件校验：任一不满足都应“自动回滚到测试开始前”，而不是停在运行中/准备中。
        /// 当前校验项：必须至少配置一个测试频段（否则设备即便连上也无事可做）。
        /// 后续若需增加“校准数据已导入”“设备在线数 &gt; 0”等条件，在此累加即可。
        /// </summary>
        private bool CheckStartPreconditions(out string reason)
        {
            reason = string.Empty;
            if (CommonFreAndHardParms.Count == 0)
            {
                reason = "未配置任何测试频段，请先在配置页设置频率范围";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 终止测试【强制停止，清空进度】
        /// </summary>
        public void StopTest()
        {
            bool wasRunning = TestContext.currentTestState
                is TestState.RUNNING or TestState.PAUSED or TestState.PRERUN;
            BandSwitchTimer.Stop();
            //旧代次立即作废：正在跑的循环线程下一圈检查时退出，也不会再被复活
            Volatile.Write(ref _runGeneration, Volatile.Read(ref _runGeneration) + 1);
            TestContext.currentTestState = TestState.STOPPED;
            StopElapsedTimer();
            _elapsedWatch.Reset();
            ElapsedText = "00:00:00";
            RefreshRunState();
            if (wasRunning)
            {
                //下发指令关射频输出/功放（子类 ReleaseDevice），再断开所有设备连接
                AddLog("WARN", "【停止测试】已停止测试并断开设备连接");
                // 先把已测曲线快照写入接续临时表（Req 2：中途停止可续测）
                try { SaveCurveTempToDatabase(); }
                catch (Exception ex) { AddLog("ERROR", $"写入接续临时表异常：{ex.Message}"); }
                try { ReleaseDevice(); }
                catch (Exception ex) { AddLog("ERROR", $"停止释放设备异常：{ex.Message}"); }
                DisconnectAllSocks();
            }
            //重置接续标记：下次接续需重新从模板选择弹窗触发
            _resumeFromFreqHz = -1;
            _pendingResume = false;
            //重置频段索引，下次开始从头测试
            TestContext.CurrentBandIndex = 0;
            ResetTestProgress();   //强制停止 = 清空进度，下次开始重新计
        }
        /// <summary>
        /// 断开全部设备连接并清空映射（界面"在线设备"数归零）
        /// </summary>
        private void DisconnectAllSocks()
        {
            foreach (var old in Socks)
            {
                try
                {
                    old.AddShowLog -= Connection_AddShowLog;
                    old?.Close();
                }
                catch { /* 单个关闭失败不阻断其余 */ }
            }
            Socks.Clear();
            MapToDeviceType.Clear();
            //断开信息上屏（测试结束/停止后操作员能看到连接已释放）
            AddLog("INFO", "断开所有设备连接");
        }
        /// <summary>
        /// 暂停测试【保留当前测试进度】
        /// </summary>
        public void PauseTest()
        {
            //运行中或准备中都可以暂停（准备中暂停 = 设备连上后不再进入测试循环）
            if (TestContext.currentTestState is not (TestState.RUNNING or TestState.PRERUN))
                return;
            BandSwitchTimer.Stop();
            TestContext.currentTestState = TestState.PAUSED;
            StopElapsedTimer();
            RefreshRunState();
            AddLog("WARN", "【暂停测试】已关闭射频输出（安全暂停），可点【继续】恢复");
            //安全：暂停期间关闭射频输出，不向 EUT 注入信号
            try { PauseOutput(); }
            catch (Exception ex) { AddLog("ERROR", $"暂停关闭输出异常：{ex.Message}"); }
            //⚠️ 不修改CurrentBandIndex，保存当前频段位置
        }
        /// <summary>
        /// 继续测试【从暂停位置接续执行】
        /// </summary>
        public void ResumeTest()
        {
            //仅暂停状态允许恢复
            if (TestContext.currentTestState != TestState.PAUSED)
                return;
            TestContext.currentTestState = TestState.RUNNING;
            StartElapsedTimer();
            RefreshRunState();
            AddLog("INFO", "【继续测试】已恢复射频输出，从暂停频点接续");
            //安全：恢复测试前先重新打开射频输出（暂停时已关闭）
            try { ResumeOutput(); }
            catch (Exception ex) { AddLog("ERROR", $"恢复打开输出异常：{ex.Message}"); }
            //执行外层循环任务（后台线程：循环内部有阻塞等待读数，不能占 UI 线程）
            Task.Run(() =>
            {
                try
                {
                    BeginLoopCycle();
                    ExcuteOutsideLoopEvent();
                }
                catch (Exception ex)
                {
                    RunOnUI(() => AddLog("ERROR", $"测试循环异常：{ex.Message}"));
                    TestContext.currentTestState = TestState.STOPPED;
                    RefreshRunState();
                }
            });
        }
        #endregion

    }
}
