using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EMCDeviceLibrary;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using WpfEMCTestSoftware.Models;
using WpfEMCTestSoftware.ViewModels.PageViewModels.TestConfigViewModels;

namespace WpfEMCTestSoftware.ViewModels
{
    /// <summary>开关矩阵通道项（4×6 拓扑中一个节点）</summary>
    public partial class SwitchChannelItem : ObservableObject
    {
        public int SwitchNumber { get; set; }
        public int ChannelNumber { get; set; }
        public string ChannelLabel => $"CH{ChannelNumber}";

        [ObservableProperty]
        private bool _isSelected;
    }

    /// <summary>自动测试范围筛选项（开关 / 通道多选）</summary>
    public partial class FilterOptionItem : ObservableObject
    {
        public int Value { get; set; }
        public string Label { get; set; } = "";

        [ObservableProperty]
        private bool _isSelected = true;
    }

    /// <summary>自动测试结果行</summary>
    public partial class PhaseTestRow : ObservableObject
    {
        public int Index { get; set; }
        public int LoopNumber { get; set; }
        public int SwitchNumber { get; set; }
        public int ChannelNumber { get; set; }
        public double FrequencyMHz { get; set; }        // 频点（MHz）
        public string ChannelLabel => $"S{SwitchNumber}-CH{ChannelNumber}";
        public string LoopLabel => $"第{LoopNumber}轮";

        [ObservableProperty]
        private string _phaseValue = "—";

        [ObservableProperty]
        private string _status = "待测";
    }

    /// <summary>日志条目</summary>
    public class LogEntry
    {
        public string Timestamp { get; set; } = DateTime.Now.ToString("HH:mm:ss.fff");
        public string Message { get; set; } = "";
        public bool IsError { get; set; }
    }

    /// <summary>
    /// 相位测试页 ViewModel — 手动控制（4×6 开关拓扑 + 读取 VNA 相位）/ 自动循环测试（按所选开关与通道组合、循环次数遍历）
    /// </summary>
    public partial class PhaseTestViewModel : BaseTestBaseViewModel
    {

        // ═══════════════ 设备连接 ═══════════════
        [ObservableProperty] private bool _isSwitchConnected;
        [ObservableProperty] private bool _isVNAConnected;
        [ObservableProperty] private string _switchConnectBtnText = "连接开关矩阵";
        [ObservableProperty] private string _vnaConnectBtnText = "连接矢量网络分析仪";

        // ═══════════════ 模式切换 ═══════════════
        [ObservableProperty] private bool _isManualMode = false;
        [ObservableProperty] private bool _isAutoMode = true;

        // ═══════════════ 手动控制 · 开关拓扑（4 开关 × 6 通道） ═══════════════
        public ObservableCollection<SwitchChannelItem> Switch1Channels { get; } = [];
        public ObservableCollection<SwitchChannelItem> Switch2Channels { get; } = [];
        public ObservableCollection<SwitchChannelItem> Switch3Channels { get; } = [];
        public ObservableCollection<SwitchChannelItem> Switch4Channels { get; } = [];

        [ObservableProperty] private SwitchChannelItem? _selectedChannel;
        [ObservableProperty] private string _phaseValue = "—";
        [ObservableProperty] private string _phaseReadStatus = "请选择通道";

        // ═══════════════ 自动测试 · 范围筛选（开关 / 通道多选） ═══════════════
        public ObservableCollection<FilterOptionItem> SwitchFilterItems { get; } = [];
        public ObservableCollection<FilterOptionItem> ChannelFilterItems { get; } = [];

        // ═══════════════ 自动测试 ═══════════════
        [ObservableProperty] private ObservableCollection<PhaseTestRow> _testResults = [];
        [ObservableProperty] private bool _isTestRunning;
        [ObservableProperty] private bool _isTestPaused;
        [ObservableProperty] private string _testStatus = "就绪";
        [ObservableProperty] private int _currentStep;
        [ObservableProperty] private int _totalSteps = 24;
        [ObservableProperty] private int _completedSteps;
        [ObservableProperty] private string _startBtnText = "开始测试";
        [ObservableProperty] private string _pauseBtnText = "暂停";

        /// <summary>自动测试循环次数（最小 1，输入小于 1 自动修正为 1）</summary>
        [ObservableProperty] private int _loopCount = 1;

        partial void OnLoopCountChanged(int value)
        {
            if (value < 1)
            {
                _loopCount = 1;
                OnPropertyChanged(nameof(LoopCount));
                return;
            }
            RefreshAutoPlan();
        }

        /// <summary>自动测试起始频率（MHz）</summary>
        [ObservableProperty] private double _startFreqMHz = 1000;

        /// <summary>自动测试终止频率（MHz）</summary>
        [ObservableProperty] private double _stopFreqMHz = 18000;

        [ObservableProperty] private bool _isFixedMode = true;
        [ObservableProperty] private bool _isSweepMode = false;




        /// <summary>自动测试频率点数（≥2，起始→终止线性分布）</summary>
        [ObservableProperty] private int _freqPointCount = 10;
        [ObservableProperty] private string _selectMeasureMode = "定点测量";

        partial void OnStartFreqMHzChanged(double value) => RefreshAutoPlan();
        partial void OnStopFreqMHzChanged(double value) => RefreshAutoPlan();
        partial void OnFreqPointCountChanged(int value)
        {

            if (value < 2)
            {
                _freqPointCount = 2;
                OnPropertyChanged(nameof(FreqPointCount));
                return;
            }
            RefreshAutoPlan();
        }
        partial void OnSelectMeasureModeChanged(string value) => RefreshAutoPlan();


        /// <summary>CSV 保存通道筛选：全部通道 = 保存所有；选指定 CHn = 只保存该通道</summary>
        [ObservableProperty] private string _selectedSaveChannel = "全部通道";
        public ObservableCollection<string> SaveChannelOptions { get; } = ["全部通道", "CH1", "CH2", "CH3", "CH4", "CH5", "CH6"];
        public ObservableCollection<string> MeasureModesOptions { get; } = ["定点测量", "扫频测试"];


        // ═══════════════ 日志 ═══════════════

        [ObservableProperty] private ObservableCollection<LogEntry> _logEntries = [];
        //==============================设备绑定名称==========================
        [ObservableProperty]
        private string _switchMatrixName;
        [ObservableProperty]
        private string _networkName;

        // ═══════════════ 内部字段 ═══════════════
        private SockConnect? _switchSock;
        private SockConnect? _vnaSock;
        private SwitchMatrixPara? _switchMatrixParainfo;
        private WpfEMCTestSoftware.Models.DeviceInfo? _deviceInfo;
        private DispatcherTimer? _autoTimer;
        private int _autoStepIndex;
        private List<(SwitchChannelItem Item, double FreqMHz)> _autoQueue = [];
        private double? _lastFreqMHz;   // 上一步频点（MHz）：用于判断是否需要切换 CW 频率
        private readonly Random _rand = new();
        private Queue<double> ReceiveDataQueue = [];

        public PhaseTestViewModel()
        {
            LoadSortDeviceNames();

            //手动控制 开关拓扑图
            for (int ch = 1; ch <= 6; ch++)
            {
                Switch1Channels.Add(new SwitchChannelItem { SwitchNumber = 1, ChannelNumber = ch });
                Switch2Channels.Add(new SwitchChannelItem { SwitchNumber = 2, ChannelNumber = ch });
                Switch3Channels.Add(new SwitchChannelItem { SwitchNumber = 3, ChannelNumber = ch });
                Switch4Channels.Add(new SwitchChannelItem { SwitchNumber = 4, ChannelNumber = ch });
            }

            // 自动控制 开关拓扑图
            for (int sw = 1; sw <= 4; sw++)
                SwitchFilterItems.Add(new FilterOptionItem { Value = sw, Label = $"开关{sw}" });
            for (int ch = 1; ch <= 6; ch++)
                ChannelFilterItems.Add(new FilterOptionItem { Value = ch, Label = $"CH{ch}" });

            // 根据默认筛选生成自动测试结果表
            RefreshAutoPlan();
            AddLog("相位测试系统就绪", false);
        }



        // ==================== 设备连接 ====================
        /// <summary>
        /// 匹配设备
        /// </summary>
        [RelayCommand]
        private void OnSwitchMatrixChange()
        {
            if (string.IsNullOrEmpty(SwitchMatrixName))
                return;
            if (SwitchMatrixName != "No Device")
                _switchMatrixParainfo = (SwitchMatrixPara)GenericHelper.globalDevices.FirstOrDefault(d => d.Name == SwitchMatrixName);
        }
        [RelayCommand]
        private void OnNetworkChange()
        {
            if (string.IsNullOrEmpty(NetworkName)) return;
            if (NetworkName != "No Device")
                _deviceInfo = GenericHelper.globalDevices.FirstOrDefault(d => d.Name == NetworkName);
        }

        /// <summary>
        /// 连接开关
        /// </summary>
        [RelayCommand]
        private void ConnectSwitch()
        {
            if (IsSwitchConnected)
            {
                DisconnectSwitch();
                AddLog($"断开开关", false);
                IsSwitchConnected = false;
                return;
            }
            try
            {
                // TODO: 从设备列表获取实际 DevicePara
                _switchSock = ClsFactory.GainSock(_switchMatrixParainfo.Model);
                if (_switchSock == null)
                {
                    AddLog("无法创建开关对象", true);
                    return;
                }
                _switchSock.Name = "开关矩阵";
                _switchSock.InitConnection(_switchMatrixParainfo.DevicePara);
                if (!_switchSock.Initialed)
                {
                    AddLog($"{_switchMatrixParainfo.Address}开关矩阵通信连接失败", true);
                    return;
                }
                SwitchConnectBtnText = "断开开关矩阵";
                IsSwitchConnected = true;
                _switchSock.AddShowLog += _vnaSock_AddShowLog;
                AddLog($"{_switchMatrixParainfo.Address}开关矩阵通信连接成功", false);
            }
            catch (Exception ex)
            {
                AddLog($"开关矩阵连接失败: {ex.Message}", true);
            }
        }
        /// <summary>
        /// 断开开关
        /// </summary>
        private void DisconnectSwitch()
        {
            _switchSock.AddShowLog -= _vnaSock_AddShowLog;
            _switchSock?.Close();
            _switchSock = null;
            IsSwitchConnected = false;
            SwitchConnectBtnText = "连接开关矩阵";
            AddLog("开关矩阵已断开", false);
        }
        /// <summary>
        /// 连接矢量网络分析仪
        /// </summary>
        [RelayCommand]
        private void ConnectVNA()
        {
            if (IsVNAConnected) { DisconnectVNA(); IsVNAConnected = false; return; }
            try
            {
                // TODO: 从设备列表获取 VNA 设备参数
                _vnaSock = ClsFactory.GainSock(_deviceInfo.Model);
                if (_vnaSock == null)
                {
                    AddLog("无法创建矢量网络分析仪对象", true);
                    return;
                }
                _vnaSock.Name = "矢量网络分析仪";
                _vnaSock.InitConnection(_deviceInfo.DevicePara);
                if (!_vnaSock.Initialed)
                {
                    AddLog($"{_deviceInfo.Address}矢量网络分析仪通信连接失败", true);
                    return;
                }
                IsVNAConnected = true;
                //切换到相位模式
                _vnaSock.SendData("CALC1:FORM PHAS");
                ClassLibrary.ClsDelay.Delay.MicrosecondDelay(100000);

                _vnaSock.RecInfoPush += _vnaSock_RecInfoPush;
                _vnaSock.AddShowLog += _vnaSock_AddShowLog;
                VnaConnectBtnText = "断开矢量网络分析仪";
                AddLog($"{_deviceInfo.Address}矢量网络分析仪通信连接成功", false);

            }
            catch (Exception ex)
            {
                AddLog($"VNA 连接失败: {ex.Message}", true);
            }
        }

        private void _vnaSock_AddShowLog(string arg1, int arg2)
        {
            ClassLibrary.LogManager.WriteLog("Device", arg1);
        }

        //矢量网络分析仪
        private void _vnaSock_RecInfoPush(string obj)
        {
            string[] parts = obj.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var s in parts)
            {
                string txt = s.Trim();
                if (double.TryParse(txt, out double value))
                {
                    ReceiveDataQueue.Enqueue(value);
                }
            }
        }

        /// <summary>
        /// 断开矢量网络分析仪
        /// </summary>
        private void DisconnectVNA()
        {
            _vnaSock.AddShowLog -= _vnaSock_AddShowLog;
            _vnaSock?.Close();
            _vnaSock = null;
            IsVNAConnected = false;
            VnaConnectBtnText = "连接矢量网络分析仪";
            AddLog("矢量网络分析仪已断开", false);
        }

        // ==================== 模式切换 ====================

        [RelayCommand]
        private void SwitchToManual() { IsManualMode = true; IsAutoMode = false; }
        [RelayCommand]
        private void SwitchToAuto() { IsManualMode = false; IsAutoMode = true; }

        // ==================== 手动控制 ====================

        /// <summary>选中一个通道（取消旧选中，高亮新选中）</summary>
        [RelayCommand]
        private void SelectChannel(SwitchChannelItem? item)
        {
            if (item == null) return;
            var allChannelsList = AllChannels().ToList();
            int startIndex = (item.SwitchNumber - 1) * 6;
            int stopIndex = item.SwitchNumber * 6;

            if (item.IsSelected)
            {
                //关闭通道
                item.IsSelected = false;
                _switchSock?.SendData($"SWITCH:DISABLE<B{item.SwitchNumber}><A{item.ChannelNumber}>");
                AddLog($"关闭通道: {item.ChannelLabel}", false);
            }
            else
            {
                while (startIndex < stopIndex)
                {
                    allChannelsList[startIndex].IsSelected = false;
                    startIndex++;
                }
                //打开通道
                item.IsSelected = true;
                _switchSock?.SendData(item.SwitchNumber.ToString(), item.ChannelNumber.ToString());
                PhaseReadStatus = "已选中，等待读取";
                AddLog($"已切换开关: {item.ChannelLabel}", false);
            }
        }

        /// <summary>从 VNA 读取当前通道相位</summary>
        [RelayCommand]
        private void ReadPhase()
        {
            _vnaSock?.SendData("CALC:MARK1:Y?");
            double mockPhase = GetphaseData();
            PhaseValue = $"{mockPhase}°";
            if (double.IsNaN(mockPhase))
            {
                AddLog($"读取相位超时", true);
            }
            else
            {
                PhaseReadStatus = "读取成功";
            }

        }
        private double GetphaseData()
        {

            int totalWaitMs = 3000;
            int spinSleepMs = 20;
            DateTime endTime = DateTime.Now.AddMilliseconds(totalWaitMs);

            while (DateTime.Now < endTime)
            {
                if (ReceiveDataQueue.TryDequeue(out var data))
                {
                    //取到目标数据，清空队列中多余残余数据
                    ReceiveDataQueue.Clear();
                    return data;
                }
                Thread.Sleep(spinSleepMs);
            }
            //超时无数据，同样清空旧残留
            ReceiveDataQueue.Clear();
            return double.NaN;

        }
        private List<double> GetSCANPhaseData()
        {
            List<double> resultList = new List<double>();
            int totalWaitMs = 3000;
            int spinSleepMs = 20;
            DateTime endTime = DateTime.Now.AddMilliseconds(totalWaitMs);

            while (DateTime.Now < endTime)
            {
                //尽可能把现有队列全部取出来
                while (ReceiveDataQueue.TryDequeue(out var d))
                {
                    resultList.Add(d);
                }
                if (resultList.Count > 0)
                {
                    return resultList;
                }
                Thread.Sleep(spinSleepMs);
            }

            //超时没有收到任何数据，返回空列表
            return resultList;

        }

        // ==================== 自动测试 · 范围筛选 ====================

        /// <summary>点击筛选按钮切换选中状态（测试运行中不允许修改）</summary>
        [RelayCommand]
        private void ToggleFilter(FilterOptionItem? item)
        {
            if (item == null) return;
            if (IsTestRunning)
            {
                AddLog("测试运行中，请先停止测试再修改范围", true);
                return;
            }
            item.IsSelected = !item.IsSelected;
            RefreshAutoPlan();
        }

        [RelayCommand]
        private void SelectAllSwitches() => SetSwitchFilterAll(true);

        [RelayCommand]
        private void ClearSwitches() => SetSwitchFilterAll(false);

        [RelayCommand]
        private void SelectAllChannels() => SetChannelFilterAll(true);

        [RelayCommand]
        private void ClearChannels() => SetChannelFilterAll(false);

        private void SetSwitchFilterAll(bool selected)
        {
            if (IsTestRunning)
            {
                AddLog("测试运行中，请先停止测试再修改范围", true);
                return;
            }
            foreach (var item in SwitchFilterItems)
                item.IsSelected = selected;
            RefreshAutoPlan();
        }

        private void SetChannelFilterAll(bool selected)
        {
            if (IsTestRunning)
            {
                AddLog("测试运行中，请先停止测试再修改范围", true);
                return;
            }
            foreach (var item in ChannelFilterItems)
                item.IsSelected = selected;
            RefreshAutoPlan();
        }

        /// <summary>按所选开关 × 所选通道生成单轮测试队列</summary>
        private List<SwitchChannelItem> BuildAutoQueue()
        {
            //第一行 选出用户勾选了哪些开关
            var switches = new HashSet<int>(SwitchFilterItems.Where(f => f.IsSelected).Select(f => f.Value));
            //第二行 选出用户勾选了哪些通道
            var channels = new HashSet<int>(ChannelFilterItems.Where(f => f.IsSelected).Select(f => f.Value));
            return AllChannels()
                .Where(c => switches.Contains(c.SwitchNumber) && channels.Contains(c.ChannelNumber))
                .ToList();
        }

        /// <summary>按当前筛选、循环次数与频率点数重建结果表、展开执行队列并刷新总步数（频率为最外侧循环）</summary>
        private void BuildAutoPlan()
        {
            if (SelectMeasureMode == "定点测量")
            {
                IsFixedMode = true;
                IsSweepMode = false;
            }
            else
            {
                IsFixedMode = false;
                IsSweepMode = true;
            }
            var queue = BuildAutoQueue();
            TestResults.Clear();
            _autoQueue = [];
            var freqList = BuildFrequencyList();
            // 三层循环：频率（最外侧） → 循环轮次 → 开关通道          
            int index = 1;
            if (SelectMeasureMode == "定点测量")
            {
                foreach (double freq in freqList)
                {
                    for (int loop = 1; loop <= LoopCount; loop++)
                    {
                        foreach (var item in queue)
                        {
                            _autoQueue.Add((item, freq));
                            TestResults.Add(new PhaseTestRow
                            {
                                Index = index++,
                                FrequencyMHz = freq,
                                LoopNumber = loop,
                                SwitchNumber = item.SwitchNumber,
                                ChannelNumber = item.ChannelNumber
                            });
                        }
                    }
                }

            }
            else
            {
                for (int loop = 1; loop <= LoopCount; loop++)
                {
                    foreach (var item in queue)
                    {
                        foreach (double freq in freqList)
                        {
                            _autoQueue.Add((item, freq));
                            TestResults.Add(new PhaseTestRow
                            {
                                Index = index++,
                                FrequencyMHz = freq,
                                LoopNumber = loop,
                                SwitchNumber = item.SwitchNumber,
                                ChannelNumber = item.ChannelNumber
                            });
                        }
                    }
                }
            }

            TotalSteps = _autoQueue.Count;
            CompletedSteps = 0;
            CurrentStep = 0;
            TestStatus = "就绪";
        }

        /// <summary>生成频点列表（MHz，起始→终止线性分布，频点数不足 2 时按 2 处理）</summary>
        private List<double> BuildFrequencyList()
        {
            double start = StartFreqMHz;
            double stop = StopFreqMHz;
            if (stop < start) stop = start;              // 防御：终止小于起始时退化为单点
            int n = Math.Max(2, FreqPointCount);
            var list = new List<double>();
            if (Math.Abs(stop - start) < 1e-9)
            {
                list.Add(start);
                return list;
            }
            double step = (stop - start) / (n - 1);
            for (int i = 0; i < n; i++)
                list.Add(start + i * step);
            return list;
        }
        /// <summary>根据当前筛选与循环次数刷新测试计划（测试运行中不刷新）</summary>
        private void RefreshAutoPlan()
        {
            if (IsTestRunning) return;
            BuildAutoPlan();
        }

        // ==================== 自动测试 ====================

        [RelayCommand]
        private void StartAutoTest()
        {
            if (IsTestRunning)
            {
                StopAutoTest();
                return;
            }
            ClassLibrary.Generic.GenericParameter.IsTestRun = true;
            //开始测试
            BeginAutoLoop();
        }
        [RelayCommand]
        private void PauseAutoTest()
        {
            if (!IsTestRunning) return;
            IsTestPaused = !IsTestPaused;
            if (IsTestPaused)
            {
                _autoTimer?.Stop();
                TestStatus = "已暂停";
                PauseBtnText = "继续";
                AddLog("测试已暂停", false);
            }
            else
            {
                _autoTimer?.Start();
                TestStatus = "运行中";
                PauseBtnText = "暂停";
                AddLog("测试已恢复", false);
            }
        }


        private void BeginAutoLoop()
        {
            //开工前先检查：一个都没选就直接报错返回，不动任何状态、不启动定时器
            var queue = BuildAutoQueue();
            if (queue.Count == 0)
            {
                AddLog("请至少选择一个开关和一个通道", true);
                return;
            }
            //真正开工：重建结果表、展开执行队列、更新总步数
            BuildAutoPlan();
            // 进入点频（CW）模式：测试循环中每个频点通过 SENS:FREQ:CW 切换
            // _vnaSock?.SendData("SENS1:FREQ:MODE CW");
            _lastFreqMHz = null;
            _autoStepIndex = 0;
            foreach (var row in TestResults)
            {
                row.PhaseValue = "—";
                row.Status = "待测";
            }
            CompletedSteps = 0;
            CurrentStep = 0;
            IsTestRunning = true;
            IsTestPaused = false;
            TestStatus = "运行中";
            StartBtnText = "停止";
            PauseBtnText = "暂停";

            _autoTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            if (SelectMeasureMode == "定点测量")
            {
                // CALC: MARK1 ON
                _vnaSock?.SendData("CALC1:MARK1 ON");
                ClassLibrary.ClsDelay.Delay.MicrosecondDelay(100000);
                _autoTimer.Tick += AutoFixFreTimer_Tick;
                AddLog($"自动测试开始（共 {TotalSteps} 步：频点 → 循环 → 通道）", false);
            }
            else
            {
                //设置为线性扫描
                _vnaSock?.SendData(":SENSe1:SWEep:TYPE LIN");
                ClassLibrary.ClsDelay.Delay.MicrosecondDelay(100000);
                //_vnaSock?.SendData(":SENSe1:BANDwidth 3000\n");

                //设置起始频率
                _vnaSock?.SendData($":SENS:FREQ:STAR {StartFreqMHz * 1000000}");
                ClassLibrary.ClsDelay.Delay.MicrosecondDelay(100000);
                //设置终止频率
                _vnaSock?.SendData($":SENS:FREQ:STOP {StopFreqMHz * 1000000}");
                ClassLibrary.ClsDelay.Delay.MicrosecondDelay(100000);
                //设置扫描点数
                _vnaSock?.SendData($":SENSe1:SWEep:POINts {FreqPointCount}");
                ClassLibrary.ClsDelay.Delay.MicrosecondDelay(100000);

                //  :CALC1: MATH: MEM 归一化
                _vnaSock?.SendData($":CALC1:MATH:MEM");
                ClassLibrary.ClsDelay.Delay.MicrosecondDelay(100000);
                _vnaSock?.SendData($":CALC1:MATH:FUNC DIV");
                ClassLibrary.ClsDelay.Delay.MicrosecondDelay(500000);
                _vnaSock?.SendData(":SENSe1:SWEep:TIME:AUTO ON");
                ClassLibrary.ClsDelay.Delay.MicrosecondDelay(100000);
                _autoTimer.Tick += _autoTimer_Tick;
                AddLog($"自动测试开始（共 {TotalSteps} 步： 循环 → 通道→ 频点）", false);
            }
            _autoTimer.Start();

        }

        private void _autoTimer_Tick(object? sender, EventArgs e)
        {
            _autoTimer?.Stop();
            try
            {
                if (_autoStepIndex >= _autoQueue.Count)
                {
                    CompleteAutoTest();
                    return;
                }
                var (item, freqMHz) = _autoQueue[_autoStepIndex];
                var row = TestResults[_autoStepIndex];
                int currentIndex = _autoStepIndex;


                _switchSock?.SendData(item.SwitchNumber.ToString(), item.ChannelNumber.ToString());
                Thread.Sleep(1000);
                _vnaSock?.SendData(":CALCulate:DATA? FDATA");
                List<double> scanList = GetSCANPhaseData();

                if (scanList.Count == 0)
                {
                    //扫描超时：只标记当前这一行，继续步进（也可以批量标记本组全部超时，看你的需要）
                    row.PhaseValue = "超时";
                    row.Status = "超时";

                    AddLog($"#{row.Index} [第{row.LoopNumber}轮] {row.ChannelLabel} @ {freqMHz:F1}MHz → {row.PhaseValue}", false);

                }
                else
                {
                    AddLog($"【轨迹扫描成功】获得{scanList.Count}个数据点，开始批量填充结果", false);
                    //批量连续填充多条结果，有多少个数据就填充多少行
                    for (int i = 0; i < scanList.Count; i++)
                    {
                        if (currentIndex >= _autoQueue.Count)
                            break;

                        var curRow = TestResults[currentIndex];
                        double val = scanList[i];
                        curRow.PhaseValue = $"{val:F4}°";
                        curRow.Status = "完成";
                        //CompletedSteps = CurrentStep + 1;
                        AddLog($"#{curRow.Index} [第{curRow.LoopNumber}轮] {curRow.ChannelLabel} @ {curRow.FrequencyMHz:F1}MHz → {curRow.PhaseValue}", false);
                        currentIndex++;
                    }
                }
                CompletedSteps = _autoStepIndex + FreqPointCount;
                _autoStepIndex = _autoStepIndex + FreqPointCount;

            }
            catch (Exception ex)
            {
                AddLog($"测试步骤异常：{ex.Message}", true);
            }
            finally
            {
                //无论正常结束/异常崩溃，最后一定重启定时器（除非全部测试完成）
                if (_autoStepIndex < _autoQueue.Count)
                {
                    _autoTimer?.Start();
                }
                else
                {
                    CompleteAutoTest();
                }
            }
        }

        private void AutoFixFreTimer_Tick(object? sender, EventArgs e)
        {
            _autoTimer?.Stop();
            try
            {
                if (_autoStepIndex >= _autoQueue.Count)
                {
                    CompleteAutoTest();
                    return;
                }
                var (item, freqMHz) = _autoQueue[_autoStepIndex];
                var row = TestResults[_autoStepIndex];
                CurrentStep = _autoStepIndex + 1;
                if (_lastFreqMHz == null || Math.Abs(_lastFreqMHz.Value - freqMHz) > 1e-9)
                {
                    _vnaSock?.SendData($"CALC1:MARK1:X {freqMHz * 1e6}");
                    AddLog($"切换频点: {freqMHz:F1} MHz", false);
                    _lastFreqMHz = freqMHz;
                    Thread.Sleep(200);
                }
                _switchSock?.SendData(item.SwitchNumber.ToString(), item.ChannelNumber.ToString());
                Thread.Sleep(5000);
                _vnaSock?.SendData("CALC:MARK1:Y?");
                double mockPhase = GetphaseData();
                row.PhaseValue = double.IsNaN(mockPhase) ? "超时" : $"{mockPhase}°";
                row.Status = double.IsNaN(mockPhase) ? "超时" : "完成";
                CompletedSteps = _autoStepIndex + 1;
                AddLog($"#{row.Index} [第{row.LoopNumber}轮] {row.ChannelLabel} @ {freqMHz:F1}MHz → {row.PhaseValue}", false);
                _autoStepIndex++;
            }
            catch (Exception ex)
            {
                AddLog($"测试步骤异常：{ex.Message}", true);
            }
            finally
            {
                //无论正常结束/异常崩溃，最后一定重启定时器（除非全部测试完成）
                if (_autoStepIndex < _autoQueue.Count)
                {
                    _autoTimer?.Start();
                }
                else
                {
                    CompleteAutoTest();
                }
            }
        }

        private void CompleteAutoTest()
        {
            _autoTimer?.Stop();
            _autoTimer = null;
            IsTestRunning = false;
            IsTestPaused = false;
            TestStatus = "完成";
            StartBtnText = "开始测试";
            PauseBtnText = "暂停";
            ClassLibrary.Generic.GenericParameter.IsTestRun = false;
            AddLog($"自动测试完成（共 {TotalSteps} 步）", false);
        }


        private void ResumeAutoTest() => PauseAutoTestCommand.Execute(null);

        private void StopAutoTest()
        {
            _autoTimer?.Stop();
            _autoTimer = null;
            IsTestRunning = false;
            IsTestPaused = false;
            TestStatus = "已停止";
            StartBtnText = "开始测试";
            PauseBtnText = "暂停";
            ClassLibrary.Generic.GenericParameter.IsTestRun = false;
            AddLog("测试已手动停止", false);
        }

        // ==================== 结果导出 ====================

        /// <summary>把测试结果表保存为 CSV 文件（UTF-8 带 BOM，Excel 可直接打开）</summary>
        [RelayCommand]
        private void SaveCsv()
        {
            if (TestResults.Count == 0)
            {
                MessageBox.Show("暂无测试数据，无法保存", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveFileDialog saveDlg = new SaveFileDialog
            {
                Filter = "CSV表格文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                FileName = $"相位测试结果_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                DefaultExt = ".csv",
                InitialDirectory = AppDomain.CurrentDomain.BaseDirectory,
                Title = "保存相位测试结果"
            };

            if (saveDlg.ShowDialog() != true)
                return;

            try
            {
                // 通道筛选：选了指定通道只保存该通道的数据，"全部通道"保存所有
                IEnumerable<PhaseTestRow> rows = TestResults;
                if (!string.IsNullOrEmpty(SelectedSaveChannel) && SelectedSaveChannel != "全部通道"
                    && int.TryParse(SelectedSaveChannel.Replace("CH", ""), out int filterCh))
                {
                    rows = TestResults.Where(r => r.ChannelNumber == filterCh).ToList();
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("序号,频率(MHz),循环,开关号,通道号,通道,相位值,状态");
                foreach (var row in rows)
                {
                    sb.AppendLine(string.Join(",",
                        row.Index,
                        row.FrequencyMHz.ToString("F1"),
                        row.LoopLabel,
                        row.SwitchNumber,
                        row.ChannelNumber,
                        row.ChannelLabel,
                        row.PhaseValue,
                        row.Status));
                }

                // UTF8 带 BOM，Excel 打开中文不乱码
                File.WriteAllText(saveDlg.FileName, sb.ToString(), new UTF8Encoding(true));
                AddLog($"测试结果已保存: {saveDlg.FileName}", false);
                MessageBox.Show($"保存成功\n路径：{saveDlg.FileName}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddLog($"保存失败: {ex.Message}", true);
                MessageBox.Show($"保存失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==================== 日志 ====================

        private void AddLog(string msg, bool isError)
        {
            LogEntries.Add(new LogEntry { Message = msg, IsError = isError });
            while (LogEntries.Count > 300)
                LogEntries.RemoveAt(0);
            ClassLibrary.LogManager.WriteLog(msg);
        }

        /// <summary>清空日志</summary>
        [RelayCommand]
        private void ClearLog()
        {
            LogEntries.Clear();
            AddLog("日志已清空", false);
        }

        // ==================== 辅助 ====================

        private IEnumerable<SwitchChannelItem> AllChannels()
        {
            foreach (var c in Switch1Channels) yield return c;
            foreach (var c in Switch2Channels) yield return c;
            foreach (var c in Switch3Channels) yield return c;
            foreach (var c in Switch4Channels) yield return c;
        }

        public override void Release()
        {
            _autoTimer?.Stop();
            _autoTimer = null;
            _switchSock?.Close();
            _vnaSock?.Close();
            base.Release();
        }
    }
}
