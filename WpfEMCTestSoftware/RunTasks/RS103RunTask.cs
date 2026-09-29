using ClassLibrary;
using ClassLibrary.Generic;
using Company.UI.Controls.UseControls;
using EMCDeviceLibrary;
using ScottPlot;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using WpfEMCTestSoftware.Enums;
using WpfEMCTestSoftware.Models;
using WpfEMCTestSoftware.Models.DisplayModules;
using WpfEMCTestSoftware.Models.TestModels;

namespace WpfEMCTestSoftware.RunTasks
{
    /// <summary>
    /// RS103 电场辐射敏感度（辐射安全裕度）：
    /// 信号源 + 功放 + 发射天线逐频点施加电场，场强计回读闭环逼近限值场强，
    /// 到位后驻留调制，监视 EUT；实测场强与限值的差即辐射安全裕度。
    /// 测试流程与 CS114RunTask 同构（Qt testworkcs114.cpp 框架移植）：
    ///   外层循环 = 频点队列；内层循环 = 功率逼近（大步进快速/精调小步进、
    ///   正式测试校准功率封顶、信号源输出上限约束、异常退出集中判定）。
    ///   量纲差异：CS114 注入电平 dBμA → RS103 场强 dBμV/m（场强计回读）。
    /// 校准阶段：逐频点记录"频率-前向功率"（达到限值场强时的馈入功率），
    /// 正式测试导入后作为功率上限封顶（天线/场耦合关系由校准数据隐含）。
    /// 配置/模板复用 EMS_CS114ConfigModel（频段 + 限值 + 硬件 + 通用参数）。
    /// </summary>
    public class RS103RunTask : BaseRunTask
    {
        public CS114DeviceChannel DeviceChannel { get; } = new();
        public CS114RuntimeParam RuntimeParam { get; } = new();

        AmpifierPara AmpPara = new AmpifierPara();

        //==================== 曲线通道（与 ModuleProfileRegistry["RS103"] 的 Charts 一致）====================
        private readonly ObservableCollection<ChartLineModel> _signalOutCurves = [];
        private readonly ObservableCollection<ChartLineModel> _fieldCurves = [];
        private readonly ObservableCollection<ChartLineModel> _powerCurves = [];

        /// <summary>场强实测曲线（保存数据用它）</summary>
        protected ChartLineModel FieldLine { get; }

        /// <summary>场强限值参考线（BuildLimitCurve 整组写入）</summary>
        protected ChartLineModel LimitLine { get; }

        public RS103RunTask()
        {
            FieldLine = new ChartLineModel
            {
                Title = "实测场强",
                LineColor = ScottPlot.Colors.Purple,
                LineWidth = 1.5f,
                XData = [],
                YData = [],
            };
            LimitLine = new ChartLineModel
            {
                Title = "limit",
                IsYReferenceLine = true,
                LineColor = ScottPlot.Colors.Red,
                LineStyle = LinePattern.Dashed,
                LineWidth = 1.5f,
                XData = [],
                YData = [],
            };
            _signalOutCurves.Add(new ChartLineModel
            {
                Title = "信号源输出",
                LineColor = ScottPlot.Colors.DodgerBlue,
                LineWidth = 1.5f,
                XData = [],
                YData = [],
            });
            _fieldCurves.Add(FieldLine);
            _fieldCurves.Add(LimitLine);
            _powerCurves.Add(new ChartLineModel
            {
                Title = "前向功率",
                LineColor = ScottPlot.Colors.Green,
                LineWidth = 1.5f,
                XData = [],
                YData = [],
            });
            _powerCurves.Add(new ChartLineModel
            {
                Title = "反向功率",
                LineColor = ScottPlot.Colors.Yellow,
                LineWidth = 1.5f,
                XData = [],
                YData = [],
            });
            RegisterChannel("Chart.Signal", _signalOutCurves);   // Y轴：输出/dBm
            RegisterChannel("Chart.Field", _fieldCurves);        // Y轴：场强/dBμV/m（含 limit 参考线）
            RegisterChannel("Chart.Power", _powerCurves);        // Y轴：W（前向+反向两条）
        }

        // ==================== 频段参数（与 CS114 同构） ====================
        protected CS114BandParms? CurrentBandParms
        {
            get
            {
                int i = TestContext.CurrentBandIndex;
                return RuntimeParam.ImmunityLimit.Count > 0 && i >= 0 && i < RuntimeParam.ImmunityLimit.Count
                    ? RuntimeParam.ImmunityLimit[i]
                    : null;
            }
        }

        protected bool HasBandParms => CurrentBandParms != null;

        // ==================== 设备校验与初始化 ====================
        private bool CheckRS103SockInstrumentsValid()
        {
            DeviceChannel.SignalSock = GetSockByType(DeviceType.信号源);
            DeviceChannel.ReadLevelSock = GetSockByType(DeviceType.接收机);   //场强计/场探头（或带场强读数的接收机）
            DeviceChannel.ForwardPowerSock = GetAllSocksByType(DeviceType.功率计).FirstOrDefault();
            if (!CheckMustChoseDevice(DeviceChannel.SignalSock, "信号源") ||
                !CheckMustChoseDevice(DeviceChannel.ReadLevelSock, "场强计/接收机") ||
                !CheckMustChoseDevice(DeviceChannel.ForwardPowerSock, "前向功率计"))
                return false;
            DeviceChannel.AmplifierSock = GetSockByType(DeviceType.功放);
            DeviceChannel.SwitchSock = GetSockByType(DeviceType.开关矩阵);
            var powerMeters = GetAllSocksByType(DeviceType.功率计);
            DeviceChannel.ReservePowerSock = powerMeters.Count >= 2 ? powerMeters[1] : null;

            DeviceChannel.ReadLevelSock.RecInfoPush += ReadLevelSock_RecInfoPush;
            DeviceChannel.ForwardPowerSock.RecInfoPush += ForwardPowerSock_RecInfoPush;
            if (DeviceChannel.ReservePowerSock != null)
                DeviceChannel.ReservePowerSock.RecInfoPush += ReservePowerSock_RecInfoPush;
            return true;
        }

        private bool CheckRS103DeviceInfoInstrumentsValid()
        {
            var device = GenericHelper.globalDevices?.FirstOrDefault(d => d != null && d.Name == DeviceChannel.AmplifierSock?.Name);
            if (device is AmpifierPara para)
            {
                AmpPara = para;
                return true;
            }
            return false;
        }

        //每设备独立的接收队列（防互相顶掉读数，与 CS114 相同）
        private readonly Queue<double> _forwardPowerQueue = [];
        private readonly Queue<double> _reversePowerQueue = [];
        private readonly Queue<double> _readLevelQueue = [];

        private void ReservePowerSock_RecInfoPush(string obj) => EnqueueTo(_reversePowerQueue, obj);
        private void ForwardPowerSock_RecInfoPush(string obj) => EnqueueTo(_forwardPowerQueue, obj);
        private void ReadLevelSock_RecInfoPush(string obj) => EnqueueTo(_readLevelQueue, obj);

        private static void EnqueueTo(Queue<double> queue, string obj)
        {
            if (!double.TryParse(obj, out double value)) return;
            queue.Enqueue(value);
            while (queue.Count > 1) queue.Dequeue();   //防积压：只保留最新一条
        }

        private bool CheckMustChoseDevice(SockConnect? sock, string name)
        {
            if (sock == null)
            {
                sendMessagelog($"{name}通信sock为null");
                return false;
            }
            return true;
        }

        // ==================== 基类流程钩子 ====================
        protected override void RefreshInOrOutLoopQueue()
        {
            FrequencyQueue.Clear();
            FrequencyQueue = CalculateAllFreqPoints(TestContext.CurrentBandIndex);
        }

        protected override void RefreshLimitLinesByFreqRange() { }

        /// <summary>频段子任务初始化：设备校验 → 功放开输出 → 信号源配置（调制/起始功率/开输出）→ 场强计初始化</summary>
        protected override bool AsiggnBandSubTask()
        {
            try
            {
                if (!CheckRS103SockInstrumentsValid())
                {
                    sendMessagelog("设备通信校验失败！");
                    return false;
                }
                if (!CheckRS103DeviceInfoInstrumentsValid())
                {
                    sendMessagelog("功放设备参数校验失败（无法读取最大输出表）！");
                    return false;
                }
                SetAmplifierOut(true, DeviceChannel.AmplifierSock);
                SetupSignalForBand();
                SetupProbeParams();
                sendMessagelog("模块初始化成功！");
                return true;
            }
            catch (Exception ex)
            {
                sendMessagelog($"模块初始化异常：{ex.Message}");
                return false;
            }
        }

        protected override void ReleaseDevice()
        {
            try
            {
                if (DeviceChannel.ReadLevelSock != null)
                    DeviceChannel.ReadLevelSock.RecInfoPush -= ReadLevelSock_RecInfoPush;
                if (DeviceChannel.ForwardPowerSock != null)
                    DeviceChannel.ForwardPowerSock.RecInfoPush -= ForwardPowerSock_RecInfoPush;
                if (DeviceChannel.ReservePowerSock != null)
                    DeviceChannel.ReservePowerSock.RecInfoPush -= ReservePowerSock_RecInfoPush;
                //先关信号源输出，再关功放（与 CS114 相同的释放顺序）
                DeviceChannel.SignalSock?.SendData((int)Communication.OUTPUTSTATE, "OFF");
                SetAmplifierOut(false, DeviceChannel.AmplifierSock);
                ReceiveDataQueue.Clear();
            }
            catch (Exception ex)
            {
                sendMessagelog($"释放设备异常：{ex.Message}");
            }
        }

        /// <summary>暂停：关信号源射频输出（暂停期间不得向 EUT 辐照）</summary>
        protected override void PauseOutput()
        {
            try
            {
                DeviceChannel.SignalSock?.SendData((int)Communication.OUTPUTSTATE, "OFF");
                AddLog("INFO", "已暂停：信号源射频输出关闭");
            }
            catch (Exception ex)
            {
                AddLog("ERROR", $"暂停关闭射频输出失败：{ex.Message}");
            }
        }

        /// <summary>恢复：重新打开信号源射频输出（与 PauseOutput 成对）</summary>
        protected override void ResumeOutput()
        {
            try
            {
                DeviceChannel.SignalSock?.SendData((int)Communication.OUTPUTSTATE, "ON");
                AddLog("INFO", "已恢复：信号源射频输出打开");
            }
            catch (Exception ex)
            {
                AddLog("ERROR", $"恢复打开射频输出失败：{ex.Message}");
            }
        }

        // ==================== 逼近循环状态（与 CS114 同构） ====================
        private double _curSignalPower;
        /// <summary>信号源当前输出 dBm（低于闭环初始功率时钳到初始功率）</summary>
        public double CurSignalPower
        {
            get => _curSignalPower;
            set => _curSignalPower = value < RuntimeParam.GeneralParms.GjbInitPowerDBm
                ? RuntimeParam.GeneralParms.GjbInitPowerDBm : value;
        }

        private int _adjustStepCount;      //本频点已调整步数
        private double _maxSignalOutDbm = 10;

        private const int MaxNormalSteps = 50;              //动态大步进阶段最大步数，之后精调
        private const double SignalPowerBackoffDbm = 6.0;   //频点结束回退量，保护 EUT/功放

        // ==================== 外层循环：消耗频点队列 ====================
        protected override void ExcuteOutsideLoopEvent()
        {
            if (TestContext.currentTestState == TestState.PAUSED)
                return;

            while (FrequencyQueue.Count > 0)
            {
                if (!IsLoopActive)
                    return;

                //1. 取频点 + 更新界面
                TestContext.CurrentFre = FrequencyQueue.Dequeue();
                TestContext.LastFrequency = TestContext.CurrentFre;
                RunOnUI(() =>
                {
                    RefreshCurrentFrequency();
                    UpdateTestProgress();
                });

                //2. 信号源设频率、场强计设中心频率
                SetSignalFrequency();
                ClsDelay.Delay.MicrosecondDelay(200000);
                SetProbeFrequency();

                //3. 内层循环：本频点场强逼近
                ExcuteInsideLoopEvent();

                //4. 校准阶段：记录"频率-前向功率"校准点（与 CS114 相同的校准数据结构）
                if (RuntimeParam.CurTestStageType == TestStageType.CalibrationTest
                    && !double.IsNaN(TestContext.LastForwardPower_W)
                    && TestContext.LastForwardPower_W > 0)
                {
                    RuntimeParam.CalCollectList.Add(new CS114CalPoint
                    {
                        FreqMHz = TestContext.CurrentFre,
                        ForwardPowerW = TestContext.LastForwardPower_W,
                    });
                }
            }
            NotifyOutsideLoopComplete();
        }

        protected override void ExcuteInsideLoopEvent()
        {
            if (TestContext.currentTestState == TestState.PAUSED)
                return;
            RunFrequencyApproach();
        }

        // ==================== 内层循环：单频点场强逼近 ====================
        /// <summary>
        /// 单频点场强逼近主循环（对应 CS114 的 RunFrequencyApproach，量纲换为场强 dBμV/m）：
        /// 设功率 → 读前向/反向功率 → 场强计回读场强 → 步进逼近限值场强；
        /// 正式测试同时受校准功率上限封顶（两个步进取较小值）。
        /// </summary>
        private void RunFrequencyApproach()
        {
            var bp = CurrentBandParms;
            if (bp == null)
            {
                AddLog("WARN", $"频段 {TestContext.CurrentBandIndex + 1} 参数缺失，跳过 {TestContext.CurrentFre:0.###} MHz");
                return;
            }
            bool isFormalTest = RuntimeParam.CurTestStageType == TestStageType.FormalTest;

            //1. 本频点目标限值（dBμV/m）
            TestContext.LevelLimit = GetFieldLimit(TestContext.CurrentFre, bp);
            RunOnUI(RefreshLevelCard);

            //2. 信号源输出上限（功放最大输出 + 信号线线损）
            double? signalLimitOut = GetSignalLimitOut(TestContext.CurrentFre);
            if (signalLimitOut == null)
            {
                AddLog("WARN", $"频段 {TestContext.CurrentBandIndex + 1} 未配置功放最大输出或信号线线损表，本频点不施加信号源上限约束！");
                _maxSignalOutDbm = MaxSignalOutFallbackDbm;
            }
            else
            {
                _maxSignalOutDbm = signalLimitOut.Value;
            }

            //3. 正式测试：校准表插值出功率上限（W→dBm）
            double? powerLimitDbm = isFormalTest ? GetCalPowerLimitDbm(TestContext.CurrentFre) : null;
            if (isFormalTest && powerLimitDbm == null)
                AddLog("WARN", "未加载校准数据（CalPowerTable 为空），本频点不做功率约束！");

            //4. 重置逼近状态
            _adjustStepCount = 0;

            //5. 逼近主循环
            while (IsLoopActive)
            {
                SetSignalPower(CurSignalPower);
                ClsDelay.Delay.MicrosecondDelay(500000);

                //前向/反向功率
                double fwdDbm = ReadForwardPowerDBm();
                if (!double.IsNaN(fwdDbm))
                {
                    TestContext.LastForwardPower_W = ClassLibrary.Generic.GenericMethods.PowerValue(fwdDbm, PowerUnit.W);
                    RunOnUI(RefreshForwardPower);
                }
                else
                {
                    AddLog("WARN", $"[{TestContext.CurrentFre:F6} MHz] 前向功率读数超时");
                }
                if (DeviceChannel.ReservePowerSock != null)
                {
                    double revDbm = ReadReverseDBm();
                    if (!double.IsNaN(revDbm))
                    {
                        TestContext.LastReversePower_W = ClassLibrary.Generic.GenericMethods.PowerValue(revDbm, PowerUnit.W);
                        RunOnUI(RefreshReversePower);
                    }
                }

                //场强计回读场强
                double fieldDbuVm = ReadFieldLevel();
                if (double.IsNaN(fieldDbuVm))
                {
                    if (!IsLoopActive) return;
                    AddLog("ERROR", $"[{TestContext.CurrentFre:F6} MHz] 场强读数无效，终止本频点");
                    return;
                }
                TestContext.LastInjectLevel_dBuA = fieldDbuVm;   //复用字段：当前场强
                RunOnUI(RefreshLevelCard);

                //步进：场强步进与功率步进取较小（功率被校准值封顶）
                double step = CalcAdjustStep(_adjustStepCount < MaxNormalSteps,
                                             TestContext.LevelLimit, powerLimitDbm,
                                             TestContext.LastForwardPower_W, bp, out bool isLevel);

                _adjustStepCount++;

                ApproachExitReason exitReason = CheckApproachExit(step, isLevel);
                if (exitReason != ApproachExitReason.Continue)
                {
                    HandleApproachExit(exitReason);
                    break;
                }

                CurSignalPower = Math.Round(CurSignalPower + step, 2);
            }
        }

        /// <summary>本圈调整步进：场强步进 = f(限值 − 当前场强)；正式测试叠加功率步进，取较小值</summary>
        private double CalcAdjustStep(bool fineMode, double limitLevel, double? powerLimitDbm,
                                      double fwdW, CS114BandParms bp, out bool isLevel)
        {
            isLevel = true;
            double fieldToLower = limitLevel - TestContext.LastInjectLevel_dBuA;
            double fieldStep = StepByDistance(fieldToLower, bp.UpperLimit, fineMode);

            if (RuntimeParam.CurTestStageType != TestStageType.FormalTest || powerLimitDbm == null)
                return fieldStep;

            if (fwdW <= 0 || double.IsNaN(fwdW)) return fieldStep;
            double fwdDbm = 10.0 * Math.Log10(fwdW * 1000.0);
            double powerToLower = powerLimitDbm.Value - fwdDbm;
            double powerStep = StepByDistance(powerToLower, Math.Abs(powerLimitDbm.Value) * 0.05, fineMode);
            isLevel = fieldStep < powerStep;
            return Math.Min(fieldStep, powerStep);
        }

        /// <summary>按"目标-当前"距离选步进：差得多走大步，接近了走小步，进了容差窗口返回 0（与 CS114 同一公式）</summary>
        private static double StepByDistance(double dBToLower, double dBWidth, bool fineMode)
        {
            if (dBToLower <= 0 && dBToLower >= -dBWidth)
                return 0.0;

            if (dBToLower > 0)   //低于目标 → 加功率
            {
                if (!fineMode)
                {
                    if (dBToLower >= 10) return 6.0;
                    if (dBToLower >= 3) return 3.0;
                    if (dBToLower >= 1) return dBToLower + dBWidth / 5;
                    if (dBToLower > 0.3) return dBToLower / 2.0;
                    if (dBToLower > 0.2) return 0.1;
                    return dBToLower + dBWidth / 10;
                }
                if (dBToLower >= 10) return 6.0;
                if (dBToLower >= 3) return 1.0;
                if (dBToLower >= 1) return 0.5;
                if (dBToLower >= 0.2) return 0.1;
                return dBToLower + dBWidth / 10;
            }

            //高于目标 → 减功率
            if (!fineMode)
            {
                if (dBToLower <= -10) return -6.0;
                if (dBToLower <= -3) return -3.0;
                if (dBToLower <= -1) return dBToLower - dBWidth / 5;
                if (dBToLower <= -0.3) return dBToLower / 2.0;
                if (dBToLower <= -0.2) return -0.1;
                return dBToLower + dBWidth / 10;
            }
            if (dBToLower <= -10) return -6.0;
            if (dBToLower <= -3) return -1.0;
            if (dBToLower <= -1) return -0.5;
            if (dBToLower <= -0.2) return -0.1;
            return dBToLower + dBWidth / 10;
        }

        /// <summary>逼近退出条件判定（与 CS114 相同：正常=到位驻留；异常=超次数/超输出上限）</summary>
        private ApproachExitReason CheckApproachExit(double step, bool isLevel)
        {
            if (Math.Abs(step) < 1e-6)
                return isLevel ? ApproachExitReason.ReachedLevel : ApproachExitReason.ReachedPower;
            if (_adjustStepCount > RuntimeParam.GeneralParms.AdjustCount)
                return ApproachExitReason.AdjustCountExceeded;
            if (CurSignalPower > _maxSignalOutDbm)
                return ApproachExitReason.SignalOverLimit;
            return ApproachExitReason.Continue;
        }

        /// <summary>按退出原因收尾本频点：日志 →（正常）驻留调制 → 刷新图表 → 功率回退</summary>
        private void HandleApproachExit(ApproachExitReason reason)
        {
            switch (reason)
            {
                case ApproachExitReason.ReachedLevel:
                    DwellWithModulation();
                    AddLog("INFO", $"[{TestContext.CurrentFre:F6} MHz] 场强 {TestContext.LastInjectLevel_dBuA:F3} dBμV/m " +
                        $"已达限值 {TestContext.LevelLimit:F3} dBμV/m，驻留调制完成");
                    break;
                case ApproachExitReason.ReachedPower:
                    DwellWithModulation();
                    AddLog("INFO", $"[{TestContext.CurrentFre:F6} MHz] 当前功率 {TestContext.LastForwardPower_W:F3} W " +
                        $"已达校准限值 {TestContext.PowerLimitW:F3} W，驻留调制完成");
                    break;
                case ApproachExitReason.AdjustCountExceeded:
                    AddLog("WARN", $"[{TestContext.CurrentFre:F6} MHz] 调整步数超限（{RuntimeParam.GeneralParms.AdjustCount} 次），强制结束本频点");
                    break;
                case ApproachExitReason.SignalOverLimit:
                    AddLog("WARN", $"[{TestContext.CurrentFre:F6} MHz] 信号源输出 {CurSignalPower:F3} dBm 超过上限 {_maxSignalOutDbm:F3} dBm，强制结束本频点");
                    break;
                case ApproachExitReason.AmplifierOverRated:
                    AddLog("WARN", $"[{TestContext.CurrentFre:F6} MHz] 功放输出超过额定功率，强制结束本频点");
                    break;
                case ApproachExitReason.VswrOverLimit:
                    AddLog("WARN", $"[{TestContext.CurrentFre:F6} MHz] 驻波比超限，强制结束本频点");
                    break;
            }

            RefreshChartData();
            CurSignalPower -= SignalPowerBackoffDbm;
        }

        /// <summary>
        /// 驻留调制：场强到位后按频段配置的驻留时间保持 AM 输出（分段睡眠，可被暂停/停止打断）。
        /// 调制已在 SetupSignalForBand 打开，这里只做时间驻留。
        /// </summary>
        private void DwellWithModulation()
        {
            double dwellMs = CommonFreAndHardParms.Count > TestContext.CurrentBandIndex
                ? CommonFreAndHardParms[TestContext.CurrentBandIndex].FreqBase.FreqDwellTime : 1000;
            if (dwellMs <= 0) return;
            int slept = 0;
            while (slept < dwellMs && IsLoopActive)
            {
                Thread.Sleep(20);
                slept += 20;
            }
        }

        /// <summary>把本频点最终读数打到曲线上（x=Hz；功率 W / 场强 dBμV/m / 信号源 dBm）</summary>
        private void RefreshChartData()
        {
            double xHz = TestContext.CurrentFre * 1_000_000;
            if (TestContext.LastForwardPower_W > 0)
                AddChannelData("Chart.Power", xHz, TestContext.LastForwardPower_W, "前向功率");
            if (TestContext.LastReversePower_W > 0)
                AddChannelData("Chart.Power", xHz, TestContext.LastReversePower_W, "反向功率");
            if (!double.IsNaN(TestContext.LastInjectLevel_dBuA))
                AddChannelData("Chart.Field", xHz, TestContext.LastInjectLevel_dBuA);
            AddChannelData("Chart.Signal", xHz, CurSignalPower);

            //结果表一行：裕度 = 实测场强 − 限值（≥0 达到规定场强 = 合格）
            double margin = double.IsNaN(TestContext.LastInjectLevel_dBuA) || TestContext.LevelLimit == 0
                ? double.NaN : TestContext.LastInjectLevel_dBuA - TestContext.LevelLimit;
            string verdict = double.IsNaN(margin) ? "-" : (margin >= 0 ? "合格" : "欠场强");
            AddResultRow(TestContext.CurrentFre, TestContext.LevelLimit, TestContext.LastInjectLevel_dBuA, margin, verdict);
        }

        // ==================== 设备控制与读数 ====================
        private void SetSignalFrequency()
            => DeviceChannel.SignalSock?.SendData((int)Communication.FREQUENCY, TestContext.CurrentFre.ToString("F6"));

        private void SetProbeFrequency()
        {
            DeviceChannel.ReadLevelSock?.SendData((int)Communication.FREQUENCY, TestContext.CurrentFre.ToString("F6"));
            ClsDelay.Delay.MicrosecondDelay(200000);
            DeviceChannel.ReadLevelSock?.SendData((int)Communication.MARKFREQ, TestContext.CurrentFre.ToString("F6"));
            ClsDelay.Delay.MicrosecondDelay(200000);
            double span = TestContext.CurrentFre switch
            {
                > 200 => 200,
                _ => TestContext.CurrentFre
            };
            DeviceChannel.ReadLevelSock?.SendData((int)Communication.SPAN, span.ToString("F6"));
        }

        private void SetSignalPower(double powerDbm)
        {
            DeviceChannel.SignalSock?.SendData((int)Communication.LEVEL, powerDbm.ToString("0.##"));
            TestContext.LastSignalOut = powerDbm;
            RunOnUI(RefreshSignalOut);
        }

        private double ReadForwardPowerDBm()
        {
            double originalDb = WaitSockValue(DeviceChannel.ForwardPowerSock, _forwardPowerQueue, 3000, (int)Communication.读取数据);
            double fwCouple = GetCouplingValue(AmpPara.FCoupling, AmpPara.FCouplingData, TestContext.CurrentFre, "前向耦合");
            double fwLoss = InterpCoeff(ToFowrdCableLoss?.Points, TestContext.CurrentFre);
            return originalDb + fwCouple + fwLoss;
        }

        private double ReadReverseDBm()
        {
            double originalDb = WaitSockValue(DeviceChannel.ReservePowerSock, _reversePowerQueue, 3000, (int)Communication.读取数据);
            double rwCouple = GetCouplingValue(AmpPara.RCoupling, AmpPara.RCouplingData, TestContext.CurrentFre, "反向耦合");
            double rwLoss = InterpCoeff(ToReverseCableLoss?.Points, TestContext.CurrentFre);
            return originalDb + rwCouple + rwLoss;
        }

        /// <summary>
        /// 读场强并归一到 dBμV/m。
        /// 场强计若回线性值 V/m（&lt;10000 视为线性域）按 20·log10(v)+120 换算；
        /// 已是对数值则直接采用（TODO：接入具体场强计型号后在配置里明确单位约定）。
        /// </summary>
        private double ReadFieldLevel()
        {
            double raw = WaitSockValue(DeviceChannel.ReadLevelSock, _readLevelQueue, 3000, (int)Communication.读取数据);
            if (double.IsNaN(raw) || raw <= 0) return double.NaN;
            return raw < 10000 ? 20.0 * Math.Log10(raw) + 120.0 : raw;
        }

        /// <summary>等待某台设备回数（队列模式同步等，超时 NaN；可被暂停/停止打断）</summary>
        private double WaitSockValue(SockConnect? sock, Queue<double> queue, int timeoutMs, int readtype)
        {
            if (sock == null) return double.NaN;
            sock.SendData(readtype, "");
            DateTime start = DateTime.Now;
            while (queue.Count == 0)
            {
                if (!IsLoopActive) return double.NaN;
                if ((DateTime.Now - start).TotalMilliseconds > timeoutMs) return double.NaN;
                Thread.Sleep(10);
            }
            return queue.Dequeue();
        }

        private double GetCouplingValue(string setting, Dictionary<double, double> table, double freqMHz, string name)
        {
            if (double.TryParse(setting, NumberStyles.Float, CultureInfo.InvariantCulture, out double fixedDb))
                return fixedDb;
            if (table != null && table.Count > 0)
                return InterpDictionary(table, freqMHz);
            AddLog("WARN", $"{name}配置既不是有效数值，耦合数据表也为空，按 0 dB 处理！");
            return 0.0;
        }

        /// <summary>每个频段开始时配置信号源：调制（缺省 1kHz 80% AM）→ 起始功率 → 开射频输出</summary>
        private void SetupSignalForBand()
        {
            var sg = DeviceChannel.SignalSock;
            if (sg == null) return;

            var mod = CurrentModGroup;
            sg.SendData((int)Communication.AM_TYPE, "LIN");
            ClsDelay.Delay.MicrosecondDelay(100000);
            sg.SendData((int)Communication.AM_DEPT, (mod?.AmDepth ?? 80).ToString("0.#"));
            ClsDelay.Delay.MicrosecondDelay(100000);
            sg.SendData((int)Communication.INPUTPOWER, (mod?.AmRateKHz ?? 1).ToString("0.###"));
            ClsDelay.Delay.MicrosecondDelay(100000);
            sg.SendData((int)Communication.AM_STATe, "ON");
            ClsDelay.Delay.MicrosecondDelay(100000);

            CurSignalPower = RuntimeParam.GeneralParms.GjbInitPowerDBm;
            sg.SendData((int)Communication.LEVEL, CurSignalPower.ToString("0.##"));
            TestContext.LastSignalOut = CurSignalPower;
            RunOnUI(RefreshSignalOut);

            ClsDelay.Delay.MicrosecondDelay(100000);
            sg.SendData((int)Communication.OUTPUTSTATE, "ON");
            ClsDelay.Delay.MicrosecondDelay(100000);
        }

        private void SetupProbeParams()
        {
            DeviceChannel?.ReadLevelSock?.SendData((int)Communication.初始化, "200");
            ClsDelay.Delay.MicrosecondDelay(200000);
            double? level = HasBandParms ? (CurrentBandParms?.StartLevel + 10) : 100;
            DeviceChannel?.ReadLevelSock?.SendData((int)Communication.参考电平, level?.ToString(CultureInfo.InvariantCulture) ?? "100");
        }

        // -------------------- 调制组（与 CS114 同一选择逻辑） --------------------
        /// <summary>按调制类型编号取调制参数组（"AM_2" → ModGroups[1]；无编号用第 1 组）</summary>
        private CS114_ModGroup? CurrentModGroup
        {
            get
            {
                var groups = RuntimeParam.GeneralParms.ModGroups;
                if (groups.Count == 0) return null;
                int idx = 0;
                var text = CurrentBandParms?.ModulationText ?? string.Empty;
                int us = text.LastIndexOf('_');
                if (us >= 0 && us + 1 < text.Length
                    && int.TryParse(text[(us + 1)..], out int n) && n >= 1 && n <= groups.Count)
                    idx = n - 1;
                return groups[idx];
            }
        }

        // -------------------- 线损表 --------------------
        private CS114CableLoss? SignalCableLoss =>
            CurrentBandParms?.CableLosses.FirstOrDefault(c => c.ToName == "功率放大器");
        private CS114CableLoss? ToFowrdCableLoss =>
            CurrentBandParms?.CableLosses.FirstOrDefault(c => c.ToName == "前向功率计");
        private CS114CableLoss? ToReverseCableLoss =>
            CurrentBandParms?.CableLosses.FirstOrDefault(c => c.ToName == "反向功率计");

        // -------------------- 限值 / 校准表插值（与 CS114 同一套公式） --------------------
        /// <summary>本频点场强限值（dBμV/m）：文件模式 log 域插值；定值起止不同按频段起止频率 log 域连线</summary>
        protected double GetFieldLimit(double freqMHz, CS114BandParms bp, int bandIndex = -1)
        {
            if (bp.LimitMode == LimitType.File && bp.LimitTable.Count > 0)
            {
                var pts = bp.LimitTable
                    .Select(p => (double.TryParse(p.FreqMHz, NumberStyles.Float, CultureInfo.InvariantCulture, out double db) ? db : 0.0,
                                  double.TryParse(p.LevelDB, NumberStyles.Float, CultureInfo.InvariantCulture, out double ldb) ? ldb : 0.0))
                    .ToList();
                return InterpLog(freqMHz, pts);
            }
            if (bp.StartLevel == bp.EndLevel)
                return bp.StartLevel;
            var fre = CommonFreAndHardParms[bandIndex >= 0 ? bandIndex : TestContext.CurrentBandIndex].FreqBase;
            double startFreq = GenericHelper.ConvertToMhzData(fre.StartFre, fre.StartFreUnit);
            double endFreq = GenericHelper.ConvertToMhzData(fre.EndFre, fre.EndFreUnit);
            if (startFreq <= 0 || endFreq <= startFreq)
                return bp.StartLevel;
            return InterpLog(freqMHz, [(startFreq, bp.StartLevel), (endFreq, bp.EndLevel)]);
        }

        /// <summary>校准表插值出当前频率的前向功率上限（W→dBm）；无校准数据返回 null</summary>
        private double? GetCalPowerLimitDbm(double freqMHz)
        {
            var table = RuntimeParam.CalPowerTable;
            if (table.Count == 0) return null;
            double powerW = Interp(freqMHz, table.OrderBy(p => p.FreqMHz)
                                                  .Select(p => (p.FreqMHz, p.ForwardPowerW)).ToList());
            if (powerW <= 0) return null;
            return 10.0 * Math.Log10(powerW * 1000.0);
        }

        /// <summary>信号源输出限制值 = 功放最大输出 + 信号线线损（log 频率域插值）</summary>
        private double? GetSignalLimitOut(double freqMHz)
        {
            var amplifierOutTable = AmpPara.MaxinValueData;
            var cableLossTable = SignalCableLoss?.Points;
            if (amplifierOutTable == null || amplifierOutTable.Count == 0) return null;
            if (cableLossTable == null || cableLossTable.Count == 0) return null;
            double powerDbm = InterpDictionaryLog(amplifierOutTable, freqMHz);
            double cableLossDb = InterpCoeffLog(cableLossTable, freqMHz);
            return powerDbm + cableLossDb;
        }

        private static double Interp(double x, List<(double X, double Y)> pts)
        {
            if (pts.Count == 0) return 0;
            if (pts.Count == 1 || x <= pts[0].X) return pts[0].Y;
            if (x >= pts[^1].X) return pts[^1].Y;
            for (int i = 1; i < pts.Count; i++)
            {
                if (x <= pts[i].X)
                {
                    double t = (x - pts[i - 1].X) / (pts[i].X - pts[i - 1].X);
                    return pts[i - 1].Y + t * (pts[i].Y - pts[i - 1].Y);
                }
            }
            return pts[^1].Y;
        }

        /// <summary>对数域插值（锚点在 log10(f) 轴上连线，范围外钳到端点值）</summary>
        private static double InterpLog(double x, List<(double X, double Y)> pts)
        {
            var valid = pts.Where(p => p.X > 0).ToList();
            if (valid.Count == 0) return 0;
            if (valid.Count == 1 || x <= valid[0].X) return valid[0].Y;
            if (x >= valid[^1].X) return valid[^1].Y;
            double logX = Math.Log10(x);
            double prevLog = Math.Log10(valid[0].X);
            for (int i = 1; i < valid.Count; i++)
            {
                double curLog = Math.Log10(valid[i].X);
                if (logX <= curLog)
                {
                    double t = (logX - prevLog) / (curLog - prevLog);
                    return valid[i - 1].Y + t * (valid[i].Y - valid[i - 1].Y);
                }
                prevLog = curLog;
            }
            return valid[^1].Y;
        }

        private static double InterpDictionary(Dictionary<double, double> table, double x)
            => Interp(x, table.OrderBy(p => p.Key).Select(p => (p.Key, p.Value)).ToList());

        private static double InterpDictionaryLog(Dictionary<double, double> table, double x)
            => InterpLog(x, table.OrderBy(p => p.Key).Select(p => (p.Key, p.Value)).ToList());

        private static double InterpCoeff(List<CS114CoeffPoint>? pts, double freqMHz)
        {
            if (pts == null || pts.Count == 0) return 0.0;
            return Interp(freqMHz, pts.OrderBy(p => double.TryParse(p.FreqMHz, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : 0.0)
                                  .Select(p => (double.TryParse(p.FreqMHz, NumberStyles.Float, CultureInfo.InvariantCulture, out double d2) ? d2 : 0.0,
                                  double.TryParse(p.CoefDB, NumberStyles.Float, CultureInfo.InvariantCulture, out double c) ? c : 0.0)).ToList());
        }

        private static double InterpCoeffLog(List<CS114CoeffPoint>? pts, double freqMHz)
        {
            if (pts == null || pts.Count == 0) return 0.0;
            return InterpLog(freqMHz, pts.OrderBy(p => double.TryParse(p.FreqMHz, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : 0.0)
                                  .Select(p => (double.TryParse(p.FreqMHz, NumberStyles.Float, CultureInfo.InvariantCulture, out double d2) ? d2 : 0.0,
                                  double.TryParse(p.CoefDB, NumberStyles.Float, CultureInfo.InvariantCulture, out double c) ? c : 0.0)).ToList());
        }

        // ==================== 参数卡（当前频段 + 电平检测） ====================
        public override IEnumerable<ParamGroup> GetParamGroups()
        {
            foreach (var g in base.GetParamGroups())
                yield return g;
            WriteLevelCard();
            yield return LevelGroup;
        }

        private ParamGroup LevelGroup => Group("Card.Level", "电平检测");

        private string LevelLimitModeText => CurrentBandParms == null
            ? "-" : CurrentBandParms.LimitMode == LimitType.Const ? "定值" : "文件";

        private string LimitText => HasBandParms ? $"{TestContext.LevelLimit:F3} dBμV/m" : "-";

        private string FieldText => HasBandParms
            ? (double.IsNaN(TestContext.LastInjectLevel_dBuA) ? "-" : $"{TestContext.LastInjectLevel_dBuA:0.###} dBμV/m")
            : "-";

        private string UpperMarginText => HasBandParms ? $"{CurrentBandParms!.UpperLimit} dB" : "-";

        private void WriteLevelCard() => LevelGroup.Set(
            ("限值类型", LevelLimitModeText),
            ("限值场强", LimitText),
            ("当前场强", FieldText),
            ("上限裕度", UpperMarginText));

        /// <summary>刷新"当前场强"一行（UI 线程调用）</summary>
        public void RefreshLevelCard()
        {
            LevelGroup.Set(("当前场强", FieldText), ("限值场强", LimitText));
        }

        // ==================== 限值参考线 ====================
        /// <summary>
        /// 配置映射后整组写入场强限值折线（画法与 CS114.BuildLimitCurve 一致）：
        /// 每频段一段——定值模式两点连线，文件模式按限值表逐点；x 单位 Hz。
        /// </summary>
        public void BuildLimitCurve()
        {
            LimitLine.XData.Clear();
            LimitLine.YData.Clear();
            for (int b = 0; b < CommonFreAndHardParms.Count && b < RuntimeParam.ImmunityLimit.Count; b++)
            {
                var fre = CommonFreAndHardParms[b].FreqBase;
                double fMinMHz = GenericHelper.ConvertToMhzData(fre.StartFre, fre.StartFreUnit);
                double fMaxMHz = GenericHelper.ConvertToMhzData(fre.EndFre, fre.EndFreUnit);
                if (fMaxMHz <= fMinMHz) continue;
                var bp = RuntimeParam.ImmunityLimit[b];

                if (bp.LimitMode == LimitType.File && bp.LimitTable.Count > 0)
                {
                    var pts = bp.LimitTable
                        .Select(p => (double.TryParse(p.FreqMHz, NumberStyles.Float, CultureInfo.InvariantCulture, out double db) ? db : 0.0,
                                      double.TryParse(p.LevelDB, NumberStyles.Float, CultureInfo.InvariantCulture, out double ldb) ? ldb : 0.0))
                        .Where(p => p.Item1 >= fMinMHz && p.Item1 <= fMaxMHz)
                        .OrderBy(p => p.Item1).ToList();
                    foreach (var (fMHz, lv) in pts)
                    {
                        LimitLine.XData.Add(fMHz * 1_000_000);
                        LimitLine.YData.Add(lv);
                    }
                }
                else
                {
                    LimitLine.XData.Add(fMinMHz * 1_000_000);
                    LimitLine.YData.Add(bp.StartLevel);
                    LimitLine.XData.Add(fMaxMHz * 1_000_000);
                    LimitLine.YData.Add(bp.EndLevel);
                }
            }
        }

        // ==================== 结果表 ====================
        /// <summary>往 Table.Result 追加一行（行集合与界面共享引用）</summary>
        private void AddResultRow(double freqMHz, double limit, double field, double margin, string verdict)
        {
            var t = Table("Table.Result", ModuleCatalog.Get("Table.Result").Title, ModuleCatalog.Get("Table.Result").Columns);
            var row = new ResultRow(
                $"{freqMHz:F6}",
                double.IsNaN(limit) || limit == 0 ? "-" : $"{limit:0.###} dBμV/m",
                double.IsNaN(field) ? "-" : $"{field:0.###} dBμV/m",
                "-",
                double.IsNaN(margin) ? "-" : $"{margin:F2} dB",
                verdict);
            row.No = t.Rows.Count + 1;
            row.IsOverLimit = verdict == "欠场强";
            row.IsCritical = !double.IsNaN(margin) && margin >= 0 && margin < 3;
            t.Rows.Add(row);
        }

        // ==================== 校准数据导出（对应 Qt generateCalibFile） ====================
        /// <summary>
        /// 把校准阶段采集的"频率-前向功率"导出为 CSV（达到限值场强时的馈入功率）。
        /// 目录：System\校准文件\RS103；正式测试导入后按频率插值出功率上限。
        /// </summary>
        public string ExportCalibrationData()
        {
            try
            {
                if (RuntimeParam.CalCollectList.Count == 0)
                {
                    AddLog("WARN", "没有校准数据可导出");
                    return string.Empty;
                }
                string dir = Path.Combine(GenericParameter.SysPath, "System", "校准文件", "RS103");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir,
                    $"{SanitizeFileName(TestTemplateName)}_校准_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

                var sb = new StringBuilder();
                sb.AppendLine("Frequency(MHz),ForwardPower(W)");
                foreach (var p in RuntimeParam.CalCollectList.OrderBy(p => p.FreqMHz))
                    sb.AppendLine($"{p.FreqMHz:F6},{p.ForwardPowerW:0.######}");
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

                AddLog("INFO", $"RS103 校准文件已保存：{path}");
                return path;
            }
            catch (Exception ex)
            {
                AddLog("ERROR", $"导出校准文件失败：{ex.Message}");
                return string.Empty;
            }
        }

        // ==================== 保存数据（辐射安全裕度报告 CSV） ====================
        /// <summary>
        /// 保存测试数据：Frequency(MHz),Limit(dBuV/m),Field(dBuV/m),Margin(dB),Verdict。
        /// 裕度 = 实测场强 − 限值（≥0 表示施加场强达到规定值，敏感度裕度满足）。
        /// 目录：System\数据文件\RS103。
        /// </summary>
        public override void SaveData()
        {
            if (RuntimeParam.CurTestStageType == TestStageType.CalibrationTest)
            {
                string calPath = ExportCalibrationData();
                if (string.IsNullOrEmpty(calPath)) return;
            }
            SaveFormalResult();
        }

        private void SaveFormalResult()
        {
            int n = Math.Min(FieldLine.XData.Count, FieldLine.YData.Count);
            if (n == 0)
            {
                string stageName = RuntimeParam.CurTestStageType == TestStageType.FormalTest ? "正式测试" : "校准测试";
                System.Windows.MessageBox.Show($"{stageName}暂无数据，测试结束后再保存", "提示");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Frequency(MHz),Limit(dBuV/m),Field(dBuV/m),Margin(dB),Verdict");
            for (int i = 0; i < n; i++)
            {
                double freqMHz = FieldLine.XData[i] / 1e6;
                double field = FieldLine.YData[i];
                int b = BandIndexOf(freqMHz);
                double limit = double.NaN;
                if (b >= 0 && b < RuntimeParam.ImmunityLimit.Count)
                    limit = GetFieldLimit(freqMHz, RuntimeParam.ImmunityLimit[b], b);

                string limitText = double.IsNaN(limit) ? "-" : limit.ToString("0.###", CultureInfo.InvariantCulture);
                string marginText = double.IsNaN(limit) ? "-" : (field - limit).ToString("0.###", CultureInfo.InvariantCulture);
                string verdict = double.IsNaN(limit) ? "-" : (field - limit >= 0 ? "合格" : "欠场强");
                sb.AppendLine($"{freqMHz.ToString("F6", CultureInfo.InvariantCulture)},{limitText}," +
                    $"{field.ToString("0.###", CultureInfo.InvariantCulture)},{marginText},{verdict}");
            }

            string dir = Path.Combine(GenericParameter.SysPath, "System", "数据文件", "RS103");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir,
                $"{SanitizeFileName(TestTemplateName)}_{DateTime.Now:yyyyMMddHHmmss}.csv");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

            LastSavedReportPath = path;   // 供界面把曲线图保存到报告同目录
            AddLog("INFO", $"【保存数据】RS103 辐射敏感度测试数据已保存：{path}（共 {n} 个频点）");
            System.Windows.MessageBox.Show($"测试数据保存到{path}完成", "提示");
        }

        /// <summary>按频率(MHz)找所属频段下标；不在任何频段范围内返回 -1</summary>
        private int BandIndexOf(double freqMHz)
        {
            for (int b = 0; b < CommonFreAndHardParms.Count; b++)
            {
                var fre = CommonFreAndHardParms[b].FreqBase;
                double start = GenericHelper.ConvertToMhzData(fre.StartFre, fre.StartFreUnit);
                double end = GenericHelper.ConvertToMhzData(fre.EndFre, fre.EndFreUnit);
                if (freqMHz >= start - 1e-9 && freqMHz <= end + 1e-9)
                    return b;
            }
            return -1;
        }

        /// <summary>文件名清洗：模板名可能带路径非法字符，统一换成下划线</summary>
        private static string SanitizeFileName(string name)
            => string.Join("_", (name ?? "未命名").Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
    }
}
