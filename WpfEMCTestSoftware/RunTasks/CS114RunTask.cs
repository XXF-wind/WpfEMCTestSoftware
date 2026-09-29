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
    public class CS114RuntimeParam
    {
        private EMCStandard _standard;
        public EMCStandard Standard
        {
            get => _standard;
            set => _standard = value;
        }

        private TestStageType _curTestStageType;
        public TestStageType CurTestStageType
        {
            get => _curTestStageType;
            set => _curTestStageType = value;
        }
        /// <summary>通用参数快照（反馈控制 / 调制组 / 上下电 / Y轴 / 报告）</summary>
        public CS114GeneralParms GeneralParms { get; set; } = new();
        public List<CS114BandParms> ImmunityLimit { get; set; } = [];

        /// <summary>校准阶段实时采集的"频率-前向功率"数据（跑完后 ExportCalibrationData() 导出 CSV）</summary>
        public List<CS114CalPoint> CalCollectList { get; } = [];

        /// <summary>正式测试用的校准功率表（由校准阶段导出的 CSV 导入；空表 = 不做功率约束）</summary>
        public List<CS114CalPoint> CalPowerTable { get; set; } = [];
    }

    //CS114CalPoint 已迁至 Models/TestModels/EMS_CS114ConfigModel.cs（随 .cs114 配置序列化）
    /// <summary>
    /// D0160测试时所需的设备
    /// </summary>
    public class CS114DeviceChannel
    {
        // 必选设备
        public SockConnect? SignalSock { get; set; }         // 信号源
        public SockConnect? ReadLevelSock { get; set; }     // 读取电平设备：接收机或者频谱仪或者示波器
        public SockConnect? ForwardPowerSock { get; set; }   // 正向功率计

        // 可选设备
        public SockConnect? ReservePowerSock { get; set; }  // 反向功率计
        public SockConnect? AmplifierSock { get; set; }     // 功放
        public SockConnect? SwitchSock { get; set; }         // 开关
    }

    public partial class CS114RunTask : BaseRunTask
    {
        // 设备通道
        public CS114DeviceChannel DeviceChannel { get; }
        // 运行参数
        public CS114RuntimeParam RuntimeParam { get; }

        AmpifierPara AmpPara = new AmpifierPara();

        //==================== 曲线通道（Key 必须与 ModuleProfileRegistry["CS114"] 的 Charts 一致）====================//
        // 运行页按 ModuleKey 从 CurveChannels 取集合（见 ChannelChartModule.ProvideLines），
        // 键不一致 → 模块取到 null → 图永远是空的。基类默认注册的是 Chart.Signal/Field/Power，
        // 而 CS114 配置要的是 Signal/FwdPower/RevPower/InjectLevel，所以必须在这里补齐。
        private readonly ObservableCollection<ChartLineModel> _signalOutCurves = [];
        private readonly ObservableCollection<ChartLineModel> _powerCurves = [];

        private readonly ObservableCollection<ChartLineModel> _injectLevelCurves = [];
        // Chart.Power
        public CS114RunTask()
        {
            DeviceChannel = new CS114DeviceChannel();
            RuntimeParam = new CS114RuntimeParam();
            RegisterCS114ChartChannels();
        }

        /// <summary>
        /// 注册本测试项要显示的曲线通道（与 EMS_Conducted 配置一一对应）：
        /// 信号源输出(dBm) / 功率-前向+反向(W，同通道两曲线) / 注入电平(dBμA)
        /// </summary>
        private void RegisterCS114ChartChannels()
        {
            _signalOutCurves.Add(new ChartLineModel
            {
                Title = "信号源输出",
                LineColor = ScottPlot.Colors.DodgerBlue,
                LineWidth = 1.5f,
                XData = [],
                YData = [],
            });
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
            _injectLevelCurves.Add(new ChartLineModel
            {
                Title = "注入电平",
                LineColor = ScottPlot.Colors.Purple,
                LineWidth = 1.5f,
                XData = [],
                YData = [],
            });


            //第二条曲线"limit"：配置限值折线（数据来自 RuntimeParam.ImmunityLimit，见 BuildLimitCurve）：
            //每个频段一段——定值模式 (频段起点,起始等级)-(频段终点,终止等级) 两点（起始=终止即为水平线）；
            //文件模式按导入的限值表逐点连成折线。所有频段首尾相接铺满整个测试频率范围。
            //★ IsYReferenceLine=true：ChannelChartModule 按这条线的 Y 最大值推算注入电平图纵轴
            //  （上限 = max(limit) + 10dB、下限 = 0）。
            //  ⚠ 查找一律用 IsYReferenceLine 标记，别按标题字符串找（标题改了就会静默失联）
            //  ⚠ 这条线只在配置映射后由 BuildLimitCurve 整组写入一次，测试过程中不要动它
            //    （它不是实测跟随线——别把实时读数写进来覆盖配置限值）
            _injectLevelCurves.Add(new ChartLineModel
            {
                Title = "limit",
                IsYReferenceLine = true,
                LineColor = ScottPlot.Colors.Red,
                LineStyle = LinePattern.Dashed,
                LineWidth = 1.5f,
                XData = [],
                YData = [],
            });
            //RegisterChannel 是"曲线数据”和“界面图表”之间唯一的桥。
            //不执行这一行，你的 _signalOutCurves 就是一个没有任何人引用的私有集合——数据加进去，图上永远一片空白
            RegisterChannel("Chart.Signal", _signalOutCurves);          // Y轴：输出/dBm
            RegisterChannel("Chart.Power", _powerCurves);               // Y轴：W（前向+反向两条曲线，打点用 lineTitle 区分）
            RegisterChannel("Chart.InjectLevel", _injectLevelCurves);   // Y轴：dBμA
        }
        #region card 右侧状态栏数据更新

        //==================== CS114 专用参数卡（电平检测 / 调制方式 / 反馈控制 / 参考校准）====================//
        // 这 4 张卡在 EMS_Conducted 配置里注册（ModuleProfile.cs:191），但基类 GetParamGroups() 不产出
        // 它们 —— 不覆写的话 Bind() 会走"未配置"分支。这里覆写：先产出基类的两组，再补本测试项的 4 组。
        // 刷新约定与基类"当前频段"卡一致：谁变了刷谁；实时值改完 TestContext 字段后调对应 RefreshXxx()。
        // ⚠ 所有刷新方法都必须在 UI 线程调用（首次遇到新键会走 Items.Add()）。
        // 静态配置项不用单独刷 —— 改配置后走 RefreshPanelGroups()（会重跑本覆写方法）即可整卡更新。
        /// <summary>测试项专有参数组的产出：基类两组（当前频段 / EUT 状态）+ 本项 4 组</summary>
        public override IEnumerable<ParamGroup> GetParamGroups()
        {
            foreach (var g in base.GetParamGroups())
                yield return g;
            WriteLevelCard(); yield return LevelGroup;
            WriteModulationCard(); yield return ModulationGroup;
            WriteFeedbackCard(); yield return FeedbackGroup;
            WriteRefCalCard(); yield return RefCalGroup;
        }

        /// <summary>当前频段的 CS114 分频段参数（无频段 / 索引越界 / 未映射配置时返回 null）。
        /// ImmunityLimit 与 CommonFreAndHardParms 按频段下标一一对应（见 MapEmsToCS114Param）</summary>
        private CS114BandParms? CurrentBandParms
        {
            get
            {
                int i = TestContext.CurrentBandIndex;
                return RuntimeParam.ImmunityLimit.Count > 0 && i >= 0 && i < RuntimeParam.ImmunityLimit.Count
                    ? RuntimeParam.ImmunityLimit[i]
                    : null;
            }
        }

        /// <summary>有可用频段参数时才显示实时数值，否则一律显示 "-"</summary>
        private bool HasBandParms => CurrentBandParms != null;

        // -------------------- 电平检测 Card.Level --------------------
        private ParamGroup LevelGroup => Group("Card.Level", "电平检测");

        /// <summary>限值类型：定值 / 文件</summary>
        private string LevelLimitModeText
        {
            get
            {
                var bp = CurrentBandParms;
                return bp == null ? "-" : bp.LimitMode == LimitType.Const ? "定值" : "文件";
            }
        }

        /// <summary>限值等级：定值显示 dBμA，文件模式显示点数</summary>
        private string LevelLimitText
        {
            get
            {
                var bp = CurrentBandParms;
                if (bp == null) return "-";
                return $"{TestContext.LevelLimit:F3} dBμA";
            }
        }

        private string UpperMarginText
        {
            get
            {
                var bp = CurrentBandParms;
                return bp == null ? "-" : $"{bp.UpperLimit} dB";
            }
        }

        /// <summary>当前注入电平显示文本（测试循环里改 TestContext.LastInjectLevel_dBuA 后刷）</summary>
        private string InjectLevelText => HasBandParms ? $"{TestContext.LastInjectLevel_dBuA:0.###} dBμA" : "-";

        /// <summary>整卡初建（GetParamGroups 里调用，键顺序即卡片显示顺序）</summary>
        private void WriteLevelCard() => LevelGroup.Set(
            ("限值类型", LevelLimitModeText),
            ("限值等级", LevelLimitText),
            ("上限裕度", UpperMarginText),
            ("当前注入电平", InjectLevelText));

        /// <summary>
        /// 刷新"当前注入电平"一项：写完 TestContext.LastInjectLevel_dBuA 后调用，卡上其他读数不动。
        /// ⚠ 必须在 UI 线程调用
        /// </summary>
        public void RefreshInjectLevel()
        {
            LevelGroup.Set(("当前注入电平", InjectLevelText));
        }
        public void RefreshReceiveLimit()
        {
            LevelGroup.Set(("限值等级", LevelLimitText));
        }
        /// <summary>
        /// 把配置限值铺到"注入电平"图的 limit 红虚线上（配置映射完成后调用一次）：
        /// 遍历所有频段（ImmunityLimit 与 CommonFreAndHardParms 按下标一一对应），
        /// 用户分频段测试是因为功放频率范围不够，图上要的是整个频段完整的限值：
        /// <list type="bullet">
        /// <item>定值模式：每频段两点 (起点,StartLevel)-(终点,EndLevel)，起止等级相同即为水平线，
        ///       与 GetReceiverLimit 的频段内对数插值语义严格一致；</item>
        /// <item>文件模式：LimitTable（MHz→dBμA）各锚点在 log 频率轴上连折线；表为空时退回定值画法兜底。</item>
        /// </list>
        /// 限值插值统一在 log10(f) 域进行（GJB151C-2024 CS114 限值曲线按 log 频率轴给点连线；
        /// 若按 MHz 线性插值，锚点之间的曲线在对数坐标里会鼓包/凹陷，偏离标准限值线形状）。
        /// 各频段首尾相接（前一频段终点 = 下一频段起点），最终连成覆盖全频段的折线。
        /// 更新方式与参考线约定一致：整组替换 XData/YData（setter 发 PropertyChanged，图表全量重绘）。
        /// </summary>
        public void BuildLimitCurve()
        {
            RunOnUI(() =>
            {
                //按参考线标记找（别按标题字符串找，标题改了会静默失联）
                var line = _injectLevelCurves.FirstOrDefault(l => l.IsYReferenceLine);
                if (line == null)
                    return;
                var xs = new List<double>();
                var ys = new List<double>();
                int bandCount = Math.Min(RuntimeParam.ImmunityLimit.Count, CommonFreAndHardParms.Count);

                //★ 画线必须与算值同空间（2026-09-22 修复"画线与频点算出的限值不一样"；
                //  2026-09-23 起限值插值统一为 log10(f) 域——GJB151C-2024 CS114 限值曲线本身就是
                //  log 频率轴上的折线，MHz 线性插值画/算都会偏离标准形状）：
                //  按对数频率均匀加密采样，每个采样点的 y 都用与测试判定完全相同的对数插值函数算出，
                //  画出的折线与算值处处重合（每十倍频程 100 点，视觉上已是平滑曲线）。
                const int SamplesPerDecade = 100;

                void AddSegment(double f1MHz, double f2MHz, Func<double, double> limitAt)
                {
                    if (f2MHz <= f1MHz)
                        return;   //频段起止倒挂/无效：跳过该段，不影响其他频段
                    double log1 = Math.Log10(f1MHz), log2 = Math.Log10(f2MHz);
                    int n = Math.Max(2, (int)Math.Ceiling((log2 - log1) * SamplesPerDecade) + 1);
                    for (int k = 0; k < n; k++)
                    {
                        double fMHz = Math.Pow(10, log1 + (log2 - log1) * k / (n - 1));
                        xs.Add(fMHz * 1e6);   //★ 曲线坐标系是 Hz
                        ys.Add(limitAt(fMHz));
                    }
                }

                for (int i = 0; i < bandCount; i++)
                {
                    var bp = RuntimeParam.ImmunityLimit[i];
                    var CommonFre = CommonFreAndHardParms[i].FreqBase;
                    //配置值是 MHz+单位，先统一换算成 MHz
                    double fMinMHz = GenericHelper.ConvertToMhzData(CommonFre.StartFre, CommonFre.StartFreUnit);
                    double fMaxMHz = GenericHelper.ConvertToMhzData(CommonFre.EndFre, CommonFre.EndFreUnit);

                    if (bp.LimitMode == LimitType.File && bp.LimitTable.Count > 0)
                    {
                        //文件模式：限值表（MHz→dBμA，解析失败丢点）。
                        //插值用 InterpLog——log10(f) 域连线、表范围外钳到端点值，与 GetReceiverLimit 逐字节同源
                        var pts = bp.LimitTable
                            .Select(p => (double.TryParse(p.FreqMHz, out double f) ? f : double.NaN,
                                          double.TryParse(p.LevelDB, out double lv) ? lv : double.NaN))
                            .Where(p => !double.IsNaN(p.Item1) && !double.IsNaN(p.Item2))
                            .OrderBy(p => p.Item1)
                            .ToList();
                        if (pts.Count > 0)
                        {
                            AddSegment(fMinMHz, fMaxMHz, f => InterpLog(f, pts));
                            continue;
                        }
                        //限值表解析后为空：退回定值画法兜底
                    }

                    //定值模式（及文件表为空的兜底）：与 GetReceiverLimit 第 3 步完全同一公式——
                    //频段内 StartLevel→EndLevel 在 log 频率轴上连线（StartLevel==EndLevel 即水平线）
                    AddSegment(fMinMHz, fMaxMHz,
                        f => InterpLog(f, [(fMinMHz, bp.StartLevel), (fMaxMHz, bp.EndLevel)]));
                }
                //整组替换触发 UserChart 全量重绘 + ChannelChartModule 参考线联动（按 max 推算 Y 轴）
                line.XData = new ObservableCollection<double>(xs);
                line.YData = new ObservableCollection<double>(ys);
            });
            if (RuntimeParam.CurTestStageType == TestStageType.FormalTest)
                BuildPowerLimitCurve();
        }
        public void BuildPowerLimitCurve()
        {

            _powerCurves.Add(new ChartLineModel
            {
                IsYReferenceLine = true,
                Title = "limit",
                LineColor = ScottPlot.Colors.Red,
                LineStyle = LinePattern.Dashed,
                LineWidth = 1.5f,
                XData = [],
                YData = [],
            });

            RunOnUI(() =>
            {
                //按参考线标记找（别按标题字符串找，标题改了会静默失联）
                var line = _powerCurves.FirstOrDefault(l => l.IsYReferenceLine);
                if (line == null)
                    return;
                var xs = new List<double>();
                var ys = new List<double>();

                var pts = RuntimeParam.CalPowerTable.Select(p => (p.FreqMHz, p.ForwardPowerW))
                            .Where(p => !double.IsNaN(p.Item1) && !double.IsNaN(p.Item2))
                            .OrderBy(p => p.Item1)
                            .ToList();
                if (pts.Count > 0)
                {
                    //★ 与注入电平限值线同一修复（2026-09-22）：算值（GetCalPowerLimitDbm）是 MHz 线性插值，
                    //  LOG 图上锚点直连直线是 log 线性——按对数频率加密采样（每十倍频程 100 点），
                    //  每个采样点 y 用同一个 Interp 算出，画线与算值处处重合
                    const int SamplesPerDecade = 100;
                    double f1MHz = pts[0].Item1, f2MHz = pts[^1].Item1;
                    double log1 = Math.Log10(f1MHz), log2 = Math.Log10(f2MHz);
                    int n = Math.Max(2, (int)Math.Ceiling((log2 - log1) * SamplesPerDecade) + 1);
                    for (int k = 0; k < n; k++)
                    {
                        double fMHz = Math.Pow(10, log1 + (log2 - log1) * k / (n - 1));
                        xs.Add(fMHz * 1e6);   //★ 曲线坐标系是 Hz
                        ys.Add(Interp(fMHz, pts));
                    }
                }
                line.XData = new ObservableCollection<double>(xs);
                line.YData = new ObservableCollection<double>(ys);
            });
        }





        // -------------------- 调制方式 Card.Modulation --------------------
        private ParamGroup ModulationGroup => Group("Card.Modulation", "调制方式");

        /// <summary>调制类型文字（如 CW 连续波 / AM_1 / FM_2 / Pulse_1）</summary>
        private string ModTypeText => CurrentBandParms?.ModulationText ?? "-";

        /// <summary>按调制类型编号取调制参数组（"AM_2" → ModGroups[1]；无编号或解析失败用第 1 组）</summary>
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

        private bool IsAmMod => ModTypeText.StartsWith("AM", StringComparison.OrdinalIgnoreCase);
        private bool IsFmMod => ModTypeText.StartsWith("FM", StringComparison.OrdinalIgnoreCase);
        private bool IsPulseMod => ModTypeText.StartsWith("Pulse", StringComparison.OrdinalIgnoreCase);

        // 与当前调制类型无关的参数显示 "—"（区别于无数据的 "-"）
        private string AmDepthText => IsAmMod && CurrentModGroup != null ? $"{CurrentModGroup.AmDepth} %" : "—";
        private string AmRateText => IsAmMod && CurrentModGroup != null ? $"{CurrentModGroup.AmRateKHz} kHz" : "—";
        private string FmOffsetText => IsFmMod && CurrentModGroup != null ? $"{CurrentModGroup.FmOffsetKHz} kHz" : "—";
        private string FmRateText => IsFmMod && CurrentModGroup != null ? $"{CurrentModGroup.FmRateKHz} kHz" : "—";
        private string PulsePeriodText => IsPulseMod && CurrentModGroup != null ? $"{CurrentModGroup.PulsePeriodMs} ms" : "—";
        private string PulseWidthText => IsPulseMod && CurrentModGroup != null ? $"{CurrentModGroup.PulseWidthMs} ms" : "—";

        private void WriteModulationCard() => ModulationGroup.Set(
            ("调制类型", ModTypeText),
            ("AM 深度", AmDepthText),
            ("AM 速率", AmRateText),
            ("FM 偏移", FmOffsetText),
            ("FM 速率", FmRateText),
            ("脉冲周期", PulsePeriodText),
            ("脉冲宽度", PulseWidthText));

        // -------------------- 反馈控制 Card.Feedback --------------------
        private ParamGroup FeedbackGroup => Group("Card.Feedback", "反馈控制");

        private string FeedbackModeText => RuntimeParam.GeneralParms.FeedbackMode;
        private string PowerFeedbackText => RuntimeParam.GeneralParms.EnablePowerFeedback ? "启用" : "禁用";
        private string AdjustCountText => $"{RuntimeParam.GeneralParms.AdjustCount} 次";
        private string FeedbackUpperText => $"{RuntimeParam.GeneralParms.FeedbackUpperDB} dB";
        private string FeedbackLowerText => $"{RuntimeParam.GeneralParms.FeedbackLowerDB} dB";
        private string GjbInitPowerText => $"{RuntimeParam.GeneralParms.GjbInitPowerDBm} dBm";

        /// <summary>当前反馈功率显示文本（测试循环里改 TestContext.LastFeedbackPower_dBm 后刷）</summary>
        private string FeedbackPowerText => HasBandParms ? $"{TestContext.LastFeedbackPower_dBm} dBm" : "-";

        private void WriteFeedbackCard() => FeedbackGroup.Set(
            ("反馈模式", FeedbackModeText),
            ("功率回馈", PowerFeedbackText),
            ("调节次数", AdjustCountText),
            ("反馈上限", FeedbackUpperText),
            ("反馈下限", FeedbackLowerText),
            ("闭环初始功率", GjbInitPowerText),
            ("当前反馈功率", FeedbackPowerText));

        /// <summary>
        /// 刷新"当前反馈功率"一项：写完 TestContext.LastFeedbackPower_dBm 后调用，卡上其他读数不动。
        /// ⚠ 必须在 UI 线程调用
        /// </summary>
        public void RefreshFeedbackPower() => FeedbackGroup.Set(("当前反馈功率", FeedbackPowerText));
        // -------------------- 信号源到功率放大器线损 --------------------
        /// <summary> 信号源线损 = 到功率放大器的损耗
        private CS114CableLoss? SignalCableLoss =>
            CurrentBandParms?.CableLosses.FirstOrDefault(c => c.ToName == "功率放大器");
        // -------------------- 到正向功率计的损耗--------------------
        private CS114CableLoss? ToFowrdCableLoss =>
            CurrentBandParms?.CableLosses.FirstOrDefault(c => c.ToName == "前向功率计");

        // -------------------- 到反向功率计的损耗--------------------
        private CS114CableLoss? ToReverseCableLoss =>
          CurrentBandParms?.CableLosses.FirstOrDefault(c => c.ToName == "反向功率计");
        // -------------------- 参考校准 Card.RefCal --------------------
        private ParamGroup RefCalGroup => Group("Card.RefCal", "参考校准");

        /// <summary>参考校准数据 = 线损表里"注入探头 → 校准夹具"那条（校准夹具的传输阻抗数据）</summary>
        private CS114CableLoss? CalCableLoss
            => CurrentBandParms?.CableLosses.FirstOrDefault(c => c.ToName == "校准夹具");

        private string CalStatusText
        {
            get
            {
                var cl = CalCableLoss;
                return cl == null ? "-" : cl.StatusText;
            }
        }
        private string CalPointText
        {
            get
            {
                var cl = CalCableLoss;
                return cl == null ? "-" : $"{cl.Points.Count} 点";
            }
        }
        private string CalSourceText
        {
            get
            {
                var cl = CalCableLoss;
                return cl == null || string.IsNullOrEmpty(cl.Source) ? "—" : cl.Source;
            }
        }
        private string CalModifiedText
        {
            get
            {
                var cl = CalCableLoss;
                return cl == null || string.IsNullOrEmpty(cl.LastModified) ? "—" : cl.LastModified;
            }
        }

        /// <summary>校准电平显示文本（测试循环里改 TestContext.LastCalLevel_dBuA 后刷）</summary>
        private string CalLevelText => HasBandParms ? $"{TestContext.LastCalLevel_dBuA} dBμA" : "-";

        private void WriteRefCalCard() => RefCalGroup.Set(
            ("校准线损", CalStatusText),
            ("校准点数", CalPointText),
            ("数据来源", CalSourceText),
            ("最近更新", CalModifiedText),
            ("校准电平", CalLevelText));

        /// <summary>
        /// 刷新"校准电平"一项：写完 TestContext.LastCalLevel_dBuA 后调用，卡上其他读数不动。
        /// ⚠ 必须在 UI 线程调用
        /// </summary>
        public void RefreshCalLevel() => RefCalGroup.Set(("校准电平", CalLevelText));
        #endregion
        #region chart图表数据更新


        #endregion
        #region 测试流程
        /// <summary>
        /// 校验DO160标准测试所需仪器是否在线有效，绑定全局设备通道，注册数据接收回调事件
        /// </summary>
        private bool CheckCS114SockInstrumentsValid()
        {
            // 必选仪器通道赋值
            DeviceChannel.SignalSock = GetSockByType(DeviceType.信号源);
            DeviceChannel.ReadLevelSock = GetSockByType(DeviceType.接收机);
            DeviceChannel.ForwardPowerSock = GetAllSocksByType(DeviceType.功率计).FirstOrDefault();
            //必选设备有效性监测
            if (!CheckMustChoseDevice(DeviceChannel.SignalSock, "信号源") ||
             !CheckMustChoseDevice(DeviceChannel.ReadLevelSock, "接收机") ||
             !CheckMustChoseDevice(DeviceChannel.ForwardPowerSock, "前向功率计"))
                return false;
            // 可选设备通道赋值
            DeviceChannel.AmplifierSock = GetSockByType(DeviceType.功放);
            DeviceChannel.SwitchSock = GetSockByType(DeviceType.开关矩阵);
            var PowerMeters = GetAllSocksByType(DeviceType.功率计);
            DeviceChannel.ReservePowerSock = PowerMeters.Count >= 2 ? PowerMeters[1] : null;

            DeviceChannel.ReadLevelSock.RecInfoPush += ReadLevelSock_RecInfoPush;
            DeviceChannel.ForwardPowerSock.RecInfoPush += ForwardPowerSock_RecInfoPush;
            if (DeviceChannel.ReservePowerSock != null)
                DeviceChannel.ReservePowerSock.RecInfoPush += ReservePowerSock_RecInfoPush;
            return true;

        }

        private bool CheckCS114DeviceInfoInstrumentsValid()
        {
            var device = GenericHelper.globalDevices?.FirstOrDefault(d => d != null && d.Name == DeviceChannel.AmplifierSock?.Name);
            if (device is AmpifierPara para)
            {
                AmpPara = para;
                return true;
            }
            return false;


        }
        //每设备独立的接收队列（三个设备共用一个队列会互相顶掉读数，必须按设备分开）
        private readonly Queue<double> _forwardPowerQueue = [];
        private readonly Queue<double> _reversePowerQueue = [];
        private readonly Queue<double> _readLevelQueue = [];

        private void ReservePowerSock_RecInfoPush(string obj) => EnqueueTo(_reversePowerQueue, obj);
        private void ForwardPowerSock_RecInfoPush(string obj) => EnqueueTo(_forwardPowerQueue, obj);
        private void ReadLevelSock_RecInfoPush(string obj) => EnqueueTo(_readLevelQueue, obj);
        /// <summary>
        /// 接收数据入队（按设备分队列）
        /// </summary>
        private static void EnqueueTo(Queue<double> queue, string obj)
        {
            if (!double.TryParse(obj, out double value)) return;
            queue.Enqueue(value);
            //防积压：设备连续回数时只保留最新1条，旧值没有意义
            while (queue.Count > 1) queue.Dequeue();
        }

        /// <summary>
        /// 校验必选设备
        /// </summary>
        /// <param name="sock">设备通信端</param>
        /// <param name="name">设备名称</param>
        /// <returns></returns>
        private bool CheckMustChoseDevice(SockConnect? sock, string name)
        {
            if (sock == null)
            {
                sendMessagelog($"{name}通信sock为null");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 外层循环里边的频率点队列
        /// </summary>
        protected override void RefreshInOrOutLoopQueue()
        {
            FrequencyQueue.Clear();
            FrequencyQueue = CalculateAllFreqPoints(TestContext.CurrentBandIndex);
        }

        /// <summary>
        /// RefreshLimitLinesByFreqRange 不需要覆写：CS114 的四张曲线（信号源/前向/反向/注入电平）
        ///都没有限值线，基类空实现即可。
        /// </summary>
        protected override void ReleaseDevice()
        {
            try
            {
                // 1. 先取消事件订阅
                if (DeviceChannel.ReadLevelSock != null)
                {
                    DeviceChannel.ReadLevelSock.RecInfoPush -= ReadLevelSock_RecInfoPush;
                }

                if (DeviceChannel.ForwardPowerSock != null)
                {
                    DeviceChannel.ForwardPowerSock.RecInfoPush -= ForwardPowerSock_RecInfoPush;

                }
                if (DeviceChannel.ReservePowerSock != null)
                {
                    DeviceChannel.ReservePowerSock.RecInfoPush -= ReservePowerSock_RecInfoPush;
                }

                // 2. 先关闭信号源输出
                if (DeviceChannel.SignalSock != null)
                    DeviceChannel.SignalSock.SendData((int)Communication.OUTPUTSTATE, "OFF");
                // 3. 再关闭功放
                SetAmplifierOut(false, DeviceChannel.AmplifierSock);

                // 4. 清空队列（释放内存）
                ReceiveDataQueue.Clear();
            }
            catch (Exception ex)
            {
                sendMessagelog($"释放设备异常：{ex.Message}");
            }
        }
        /// <summary>
        /// 暂停：关闭信号源射频输出（安全要求：暂停期间不向 EUT 注入信号）
        /// </summary>
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

        /// <summary>
        /// 恢复：重新打开信号源射频输出（与 PauseOutput 成对）
        /// </summary>
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

        /// <summary>
        /// 分配频段子任务
        /// </summary>
        /// <returns></returns>
        protected override bool AsiggnBandSubTask()
        {
            try
            {
                //1.必选通信设备校验,绑定通信事件
                if (!CheckCS114SockInstrumentsValid())
                {
                    sendMessagelog("设备通信校验失败！");
                    return false;
                }
                if (!CheckCS114DeviceInfoInstrumentsValid())
                {
                    sendMessagelog("设备参数校验失败！");
                    return false;
                }
                //2.打开功放输出
                SetAmplifierOut(true, DeviceChannel.AmplifierSock);
                //3.设置信号源参数（调制 / 起始功率 / 开输出，对应 Qt setupCS114SignalGenerator）
                SetupSignalForBand();
                //4.初始化接收设备参数
                SetupReceiveParams();
                sendMessagelog("模块初始化成功！");
                return true;
            }
            catch (Exception ex)
            {
                sendMessagelog($"模块初始化异常：{ex.Message}");
                return false;
            }
        }
        //==================== CS114 测试流程（移植自 Qt testworkcs114.cpp）====================//
        // 结构对应关系：
        //   Qt performCS114MainTestLoop 的 while 循环   → RunFrequencyApproach（内层逼近）
        //   Qt CalculateDynamic/DiscorderCS114Step      → CalcAdjustStep(fineMode: false/true)
        //   Qt TransmissionValuessigl(...,"CurrentXxx") → TestContext.XXX = v + RunOnUI(RefreshXxx)
        //   Qt CollectionTestData → generateCalibFile() → CalCollectList → ExportCalibrationData()
        //
        // ⚠ 整条循环跑在 BandSwitchTimer 的线程池线程上：
        //   - 碰界面（参数卡/图表/可观察属性）必须包 RunOnUI(...)
        //   - 暂停/停止 = TestContext.currentTestState 变化，循环每一圈都会检查


        private double _curSignalPower;    
        /// <summary>
        /// 信号源当前输出 dBm
        /// </summary>
        public double CurSignalPower
        {
            get { return _curSignalPower; }
            set
            {
                _curSignalPower = (value < RuntimeParam.GeneralParms.GjbInitPowerDBm) ? RuntimeParam.GeneralParms.GjbInitPowerDBm : value;
            }
        }

        private int _adjustStepCount;      // 本频点已调整步数（Qt adjustStepCount）

        /// <summary>信号源输出上限 dBm（TODO：应按 功放最大安全输入 + 信号源→功放线损 计算，对应 Qt limitSignalOut）</summary>
        private double _maxSignalOutDbm = 10;

        /// <summary>动态大步进阶段的最大步数，超过后换精调小步进（TODO：对应 Qt maxNormalSteps，可挪进通用参数配置）</summary>
        private const int MaxNormalSteps = 50;

        /// <summary>调整次数超限倍率：本频点允许的最大调整次数 = 通用参数 AdjustCount × 本系数（Qt handleTerminationConditions）</summary>
        private const int MaxAdjustStepFactor = 1;

        /// <summary>频点结束时信号源输出回退量 dB（保护 EUT / 功放，避免换频时仍处于高电平）</summary>
        private const double SignalPowerBackoffDbm = 6.0;

        // -------------------- 外层循环：消耗频点队列 --------------------
        /// <summary>
        /// 执行外层循环：一次调用测完当前频段的所有频点。
        /// 每个频点：更新界面 → 信号源设频率 → 进内层逼近；
        /// 队列耗尽 → 通知基类切换下一个频段
        /// </summary>
        protected override void ExcuteOutsideLoopEvent()
        {
            // =========【关键】检测暂停，直接截断循环 =========
            if (TestContext.currentTestState == TestState.PAUSED)
                return;

            while (FrequencyQueue.Count > 0)
            {
                //用户暂停/停止/本轮已作废 → 立刻退出，剩余频点留在队列里（恢复时接着测）
                if (!IsLoopActive)
                    return;

                //1. 取一个频点
                TestContext.CurrentFre = FrequencyQueue.Dequeue();

                //2. 更新界面"当前频点"
                TestContext.LastFrequency = TestContext.CurrentFre;

                RunOnUI(() =>
                {
                    RefreshCurrentFrequency();
                    //取走频点后立即刷新进度：整体进度按"已完成频点/总频点"跨频段加权推导
                    //（本段进度 = 段内点数 − 队列剩余数，暂停/恢复不会漂移）
                    UpdateTestProgress();
                });

                //3. 信号源设频率,接收设备中心频率
                SetSignalFrequency();
                ClsDelay.Delay.MicrosecondDelay(200000);
                SetReadLevelFrequency();
                //4. 内层循环：本频点功率逼近（同步执行，跑在当前后台线程）
                ExcuteInsideLoopEvent();

                //5. 频点结束 → 校准阶段记录"频率-前向功率"校准点
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
            //频点队列耗尽 → 通知基类：CurrentBandIndex++ 并重启频段切换定时器
            NotifyOutsideLoopComplete();
        }

        // -------------------- 内层循环：单频点功率逼近 --------------------
        /// <summary>
        /// 执行内层循环：当前频点的功率逼近主循环（对应 Qt performCS114MainTestLoop）
        /// </summary>
        protected override void ExcuteInsideLoopEvent()
        {
            // =========【关键】检测暂停，直接截断循环 =========
            if (TestContext.currentTestState == TestState.PAUSED)
                return;
            RunFrequencyApproach();
        }

        /// <summary>
        /// 单频点功率逼近主循环（对应 Qt performCS114MainTestLoop + performCS114SingleFrequencyTest）。
        /// 校准阶段：只按接收机读数闭环逼近限值，不参考功率；
        /// 正式测试：接收机读数 + 校准功率上限双重约束，取两个步进的较小值（功率被校准值封顶）
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
            //1. 本频点目标限值（接收机读数目标 dBμA）
            TestContext.LevelLimit = GetReceiverLimit(TestContext.CurrentFre, bp);
            RunOnUI(RefreshReceiveLimit);
            //2. 
            double? signalLimitOut = GetSignalLimitOut(TestContext.CurrentFre);
            if (signalLimitOut == null)
            {
                AddLog("WARN", $"频段 {TestContext.CurrentBandIndex + 1} 未配置功放最大输出或信号线线损表，本频点不施加信号源上限约束！");
                _maxSignalOutDbm = MaxSignalOutFallbackDbm;   // 具名常量，不要裸写 10
            }
            else
            {
                _maxSignalOutDbm = signalLimitOut.Value;      // ← 关键：正常分支必须赋值
            }

            //3. 正式测试：校准表插值出功率上限（W→dBm；对应 Qt factors.powerLimit）
            double? powerLimitDbm = isFormalTest ? GetCalPowerLimitDbm(TestContext.CurrentFre) : null;
            if (isFormalTest && powerLimitDbm == null)
            {
                AddLog("WARN", "未加载校准数据（CalPowerTable 为空），本频点不做功率约束！");
            }
            else
            {
                if (powerLimitDbm != null)
                    TestContext.PowerLimitW = ClassLibrary.Generic.GenericMethods.PowerValue((double)powerLimitDbm, PowerUnit.W);
            }

            //4. 重置逼近状态（）
            _adjustStepCount = 0;

            //5. 逼近主循环：测试运行中（代次未过期）就继续逼近。
            //   信号源输出上限不再写在 while 条件里 —— 挪进 CheckApproachExit，
            //   这样超限时能打出"超了多少"的告警，而不是无声退出
            while (IsLoopActive)
            {
                //--- 4.1 设置信号源功率（Qt SetAmplitude(globalpower)）---
                SetSignalPower(CurSignalPower);

                ClsDelay.Delay.MicrosecondDelay(500000);

                //--- 4.2 读前向/反向功率（+线损+耦合值）---
                double fwdDbm = ReadForwardPowerDBm();

                if (!double.IsNaN(fwdDbm))
                {
                    TestContext.LastForwardPower_W = ClassLibrary.Generic.GenericMethods.PowerValue(fwdDbm, PowerUnit.W);
                    RunOnUI(RefreshForwardPower);
                }
                else
                {
                    AddLog("WARN", $"[{TestContext.CurrentFre:f6} MHz] 前向功率读数超时");
                }

                if (DeviceChannel.ReservePowerSock != null)
                {
                    double revDBm = ReadReverseDBm();
                    if (!double.IsNaN(revDBm))
                    {
                        TestContext.LastReversePower_W = ClassLibrary.Generic.GenericMethods.PowerValue(revDBm, PowerUnit.W);
                        RunOnUI(RefreshReversePower);
                    }
                }

                //--- 4.3 读接收机并换算注入电平（dBμA； ）---
                double injectDbuA = ReadReceiverLevel(TestContext.CurrentFre);
                if (double.IsNaN(injectDbuA))
                {
                    //暂停/停止导致的中断不算读数错误，安静退出
                    if (!IsLoopActive) return;
                    AddLog("ERROR", $"[{TestContext.CurrentFre:F6} MHz] 接收机读数无效，终止本频点");
                    return;
                }
                TestContext.LastInjectLevel_dBuA = injectDbuA;
                RunOnUI(RefreshInjectLevel);

                //--- 4.4 计算步进（前 MaxNormalSteps 次动态大步进快速逼近，之后精调小步进）---
                double step = CalcAdjustStep(_adjustStepCount < MaxNormalSteps,
                                             TestContext.LevelLimit, powerLimitDbm, TestContext.LastForwardPower_W, bp, out bool islevel);

                //--- 4.5 本轮累计调整次数（Qt adjustStepCount++）---
                _adjustStepCount++;

                //--- 4.6 退出条件判定：正常=达到电平限值；异常=超次数 / 超输出上限 ---
                //    所有退出判定集中在 CheckApproachExit，后续加"功放额定功率 / 驻波比"检测时不用动主循环
                ApproachExitReason exitReason = CheckApproachExit(step, islevel);
                if (exitReason != ApproachExitReason.Continue)
                {
                    HandleApproachExit(exitReason);
                    break;
                }

                //--- 4.7 调整信号源功率（Qt adjustSignalSource）---
                CurSignalPower = Math.Round(CurSignalPower + step, 2);
            }
        }

        // -------------------- 逼近退出条件（Qt handleTerminationConditions）--------------------
        /// <summary>
        /// 逼近循环的退出条件判定 —— **所有退出条件只在这里判定**。
        /// 正常退出：步进为 0（读数已进入容差窗口，达到电平限值）→ 驻留调制；
        /// 异常退出：调整次数超限（防死循环兜底）、信号源输出超上限。
        /// 扩展方式（功放额定功率 / 驻波比检测）：加一个 if 返回新枚举值，
        /// 再到 HandleApproachExit 的 switch 里补一条日志分支即可，主循环无需改动。
        /// </summary>
        /// <param name="step">本圈算出的调整步进 dB；绝对值 &lt; 1e-6 表示已到位</param>
        private ApproachExitReason CheckApproachExit(double step, bool isLevel)
        {
            //1. 正常：步进为 0 → 读数已在 [限值-容差, 限值] 窗口内，本频点到位
            if (Math.Abs(step) < 1e-6)
            {
                if (isLevel)
                    return ApproachExitReason.ReachedLevel;
                else
                    return ApproachExitReason.ReachedPower;
            }


            //2. 异常：调整次数超过最大值 → 防死循环兜底
            if (_adjustStepCount > RuntimeParam.GeneralParms.AdjustCount)
                return ApproachExitReason.AdjustCountExceeded;

            //3. 异常：信号源输出超过上限（= 功放最大输出 + 信号线线损，见 GetSignalLimitOut）
            if (CurSignalPower > _maxSignalOutDbm)
                return ApproachExitReason.SignalOverLimit;
            return ApproachExitReason.Continue;
        }

        /// <summary>
        /// 按退出原因收尾本频点：打对应日志 →（正常时）驻留调制 → 刷新图表 → 信号源输出回退。
        /// 回退量只改本进程记录值，硬件上由下一圈的 SetSignalPower / 下一频点起步时下发
        /// </summary>
        private void HandleApproachExit(ApproachExitReason reason)
        {
            switch (reason)
            {
                //正常退出：达到电平限值 → 驻留调制
                case ApproachExitReason.ReachedLevel:
                    DwellModulation();
                    AddLog("INFO", $"[{TestContext.CurrentFre:F6} MHz] 注入电平 {TestContext.LastInjectLevel_dBuA:F3} dBμA 已达限值 {GetReceiverLimit(TestContext.CurrentFre, CurrentBandParms!):F3} dBμA，驻留调制完成");
                    break;
                case ApproachExitReason.ReachedPower:
                    DwellModulation();
                    AddLog("INFO", $"[{TestContext.CurrentFre:F6} MHz] 当前功率 {TestContext.LastForwardPower_W:F3} W 已达限值 {TestContext.PowerLimitW:F3} W，驻留调制完成");
                    break;

                //异常退出：调整次数超限
                case ApproachExitReason.AdjustCountExceeded:
                    AddLog("WARN", $"[{TestContext.CurrentFre:F6} MHz] 调整步数超限（{RuntimeParam.GeneralParms.AdjustCount} 次），强制结束本频点");
                    break;

                //异常退出：信号源输出超上限
                case ApproachExitReason.SignalOverLimit:
                    AddLog("WARN", $"[{TestContext.CurrentFre:f6} MHz] 信号源输出 {CurSignalPower:F3} dBm 超过上限 {_maxSignalOutDbm:F3} dBm，强制结束本频点");
                    break;

                //---- 以下为预留扩展的日志分支（CheckApproachExit 接入后自动生效）----
                case ApproachExitReason.AmplifierOverRated:
                    AddLog("WARN", $"[{TestContext.CurrentFre:F6} MHz] 功放输出超过额定功率，强制结束本频点");
                    break;

                case ApproachExitReason.VswrOverLimit:
                    AddLog("WARN", $"[{TestContext.CurrentFre:F6} MHz] 驻波比超限，强制结束本频点");
                    break;
            }

            //刷新图表（记录本频点最终读数）
            RefreshChartData();
            //信号源输出回退（保护 EUT / 功放）
            CurSignalPower -= SignalPowerBackoffDbm;

        }
        /// <summary>
        /// 把本频点的最终读数打到 4 张曲线上（频点结束时调用一次）。
        /// ★ x 必须是 Hz（UserChart 坐标系 / XMinLimit / XMaxLimit 都是 Hz，传 MHz 会画到可视区外且不报错）；
        /// ★ y 单位按 ModuleCatalog 的 YAxisTitle 对齐：前向/反向功率 W、注入电平 dBμA、信号源输出 dBm
        /// </summary>
        private void RefreshChartData()
        {
            double xHz = TestContext.CurrentFre * 1_000_000;
            //前向/反向功率：W，同走 "Chart.Power" 通道（与 RegisterCS114ChartChannels 注册的 Key 一致，
            //打点 Key 不一致只 WARN 不上图）；同通道两条曲线用 lineTitle 区分
            if (TestContext.LastForwardPower_W > 0)
                AddChannelData("Chart.Power", xHz, TestContext.LastForwardPower_W, "前向功率");
            if (TestContext.LastReversePower_W > 0)
                AddChannelData("Chart.Power", xHz, TestContext.LastReversePower_W, "反向功率");
            //注入电平：本身即 dBμA
            if (!double.IsNaN(TestContext.LastInjectLevel_dBuA))
                AddChannelData("Chart.InjectLevel", xHz, TestContext.LastInjectLevel_dBuA);
            //信号源输出：dBm（取回退前的到位值）
            AddChannelData("Chart.Signal", xHz, CurSignalPower);
        }
        /// <summary>
        /// 驻留调制：本频点读数达到电平限值后，在限值上驻留（按频段驻留时间）并保持调制输出。
        /// TODO 待实现：驻留时间取 CommonFreAndHardParms[CurrentBandIndex].FreqDwellTime，
        ///              期间保持 AM 调制（PhaseModulation/AM_STATe 已在 SetupSignalForBand 打开）
        /// </summary>
        private void DwellModulation()
        {

        }
        // -------------------- 步进计算（移植 Qt CalculateDynamic/DiscorderCS114Step）--------------------
        /// <summary>
        /// 计算本圈调整步进。
        /// 接收机步进 = f(限值 - 当前注入电平)；
        /// 正式测试再算功率步进 = f(校准功率上限 - 当前前向功率)，取两者较小值（Qt qMin）
        /// </summary>
        /// <param name="fineMode">false=动态大步进（6/3/1dB 快速逼近）；true=精调小步进（1/0.5/0.1dB）</param>
        private double CalcAdjustStep(bool fineMode, double limitLevel, double? powerLimitDbm,
                                      double fwdW, CS114BandParms bp, out bool isLevel)
        {
            isLevel = true;
            //1. 接收机步进（Qt RECVdBToLower / RECVdBWidth）
            double recvToLower = limitLevel - TestContext.LastInjectLevel_dBuA;
            double recvStep = StepByDistance(recvToLower, bp.UpperLimit, fineMode);

            //2. 校准阶段：功率不参与步进（Qt 里 if(CS114testProcedure == "测试阶段") 不成立）
            if (RuntimeParam.CurTestStageType != TestStageType.FormalTest || powerLimitDbm == null)
                return recvStep;

            //3. 正式测试：功率步进（前向功率 W→dBm）
            if (fwdW <= 0 || double.IsNaN(fwdW)) return recvStep;   //功率读数无效时不做功率约束
            double fwdDbm = 10.0 * Math.Log10(fwdW * 1000.0);
            double powerToLower = powerLimitDbm.Value - fwdDbm;
            double powerStep = StepByDistance(powerToLower, Math.Abs(powerLimitDbm.Value) * 0.05, fineMode);
            isLevel = recvStep < powerStep;
            //4. 两个步进取较小（Qt qMin(receStep, powerStep)）→ 前向功率被校准值封顶
            return Math.Min(recvStep, powerStep);
        }

        /// <summary>按"目标-当前"距离选步进：差得多走大步，接近了走小步，进了容差窗口返回 0</summary>
        private static double StepByDistance(double dBToLower, double dBWidth, bool fineMode)
        {
            //已在 [目标-容差, 目标] 窗口内 → 到位，不动
            //  71 -75 = -4
            //  -6
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

        // -------------------- 设备控制与读数 --------------------
        private void SetSignalFrequency()
            => DeviceChannel.SignalSock?.SendData((int)Communication.FREQUENCY, TestContext.CurrentFre.ToString("F6"));
        private void SetReadLevelFrequency()
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

            DeviceChannel?.ReadLevelSock?.SendData((int)Communication.SPAN, span.ToString("F6"));
        }
        private void SetSignalPower(double powerDbm)
        {
            DeviceChannel.SignalSock?.SendData((int)Communication.LEVEL, powerDbm.ToString("0.##"));
            TestContext.LastSignalOut = powerDbm;
            RunOnUI(RefreshSignalOut);
        }



        /// <summary>前向功率计读数（W）。TODO：按界面单位设置做 dBm↔W 转换（Qt samplePowerMeasurements 的单位分支）</summary>
        private double ReadForwardPowerDBm()
        {
            double originalDb = WaitSockValue(DeviceChannel.ForwardPowerSock, _forwardPowerQueue, 3000, (int)Communication.读取数据);
            //获取前向耦合值（数值=定值，路径=文件，自动区分）
            double fwCouple = GetCouplingValue(AmpPara.FCoupling, AmpPara.FCouplingData, TestContext.CurrentFre, "前向耦合");
            double fwLoss = InterpCoeff(ToFowrdCableLoss?.Points, TestContext.CurrentFre);
            return originalDb + fwCouple + fwLoss;
        }

        private double ReadReverseDBm()
        {
            double originalDb = WaitSockValue(DeviceChannel.ReservePowerSock, _reversePowerQueue, 3000, (int)Communication.读取数据);
            //获取前向耦合值（数值=定值，路径=文件，自动区分）
            double rwCouple = GetCouplingValue(AmpPara.RCoupling, AmpPara.RCouplingData, TestContext.CurrentFre, "反向耦合");
            double rwLoss = InterpCoeff(ToReverseCableLoss?.Points, TestContext.CurrentFre);
            return originalDb + rwCouple + rwLoss;
        }
        /// <summary>
        /// 获取耦合值：setting 为数值时返回定值；为路径字符串时按频率对 table 插值；两者皆不可用时告警并返回 0 dB
        /// </summary>
        private double GetCouplingValue(string setting, Dictionary<double, double> table, double freqMHz, string name)
        {
            // 1) 数值 → 定值
            if (double.TryParse(setting, NumberStyles.Float, CultureInfo.InvariantCulture, out double fixedDb))
                return fixedDb;

            // 2) 路径 → 已加载的数据表插值
            if (table != null && table.Count > 0)
                return InterpDictionary(table, freqMHz);

            // 3) 兜底：既不是数值，表也是空的
            AddLog("WARN", $"{name}配置既不是有效数值，耦合数据表也为空，按 0 dB 处理！");
            return 0.0;
        }

        /// <summary>
        /// 读接收机并换算注入电平
        /// </summary>
        private double ReadReceiverLevel(double freqMHz)
        {
            // TODO：部分接收机/频谱仪需要先发触发或询问指令才回数，
            //       接入具体型号后在读取前在这里补触发指令（Qt startSingleSweep）
            double dbua = WaitSockValue(DeviceChannel.ReadLevelSock, _readLevelQueue, 3000, (int)Communication.READ_DBUA);
            if (double.IsNaN(dbua)) return double.NaN;
            return dbua + GetInjectCompensation(freqMHz);
        }

        /// <summary>
        /// 注入电平换算补偿量（dB）。
        /// TODO：正式注入电平应使用"注入探头→EUT"传输阻抗；
        /// 现阶段用线损表"注入探头→校准夹具"（参考校准传输阻抗）近似
        /// </summary>
        private double GetInjectCompensation(double freqMHz)
        {
            var cl = CurrentBandParms?.CableLosses.FirstOrDefault(c => c.ToName == "校准夹具");
            return cl == null ? 0 : InterpCoeff(cl.Points, freqMHz);
        }

        /// <summary>等待某台设备的回数（VISA 模式同步读；队列模式等 RecInfoPush 推入该设备的队列）</summary>
        private double WaitSockValue(SockConnect? sock, Queue<double> queue, int timeoutMs, int readtype)
        {
            if (sock == null) return double.NaN;

            sock.SendData(readtype, "");

            //队列模式：阻塞等待设备回数（每 10ms 查一次，超时返回 NaN）
            DateTime start = DateTime.Now;
            while (queue.Count == 0)
            {
                if (!IsLoopActive) return double.NaN;   //等待中用户停止/暂停/本轮已作废
                if ((DateTime.Now - start).TotalMilliseconds > timeoutMs) return double.NaN;
                Thread.Sleep(10);
            }
            return queue.Dequeue();
        }

        /// <summary>
        /// 每个频段开始时配置信号源（移植 Qt setupCS114SignalGenerator + AMEnable）：
        /// 调制参数（缺省 1kHz 80% AM，配置了调制组按配置）→ 起始功率 → 开射频输出
        /// </summary>
        private void SetupSignalForBand()
        {
            var sg = DeviceChannel.SignalSock;
            if (sg == null) return;

            //1. 调制参数
            var mod = CurrentModGroup;
            sg.SendData((int)Communication.AM_TYPE, "LIN");
            ClsDelay.Delay.MicrosecondDelay(100000);
            sg.SendData((int)Communication.AM_DEPT, (mod?.AmDepth ?? 80).ToString("0.#"));
            ClsDelay.Delay.MicrosecondDelay(100000);
            sg.SendData((int)Communication.INPUTPOWER, (mod?.AmRateKHz ?? 1).ToString("0.###"));
            ClsDelay.Delay.MicrosecondDelay(100000);
            sg.SendData((int)Communication.AM_STATe, "ON");
            ClsDelay.Delay.MicrosecondDelay(100000);
            //sg.SendData((int)Communication.MODOUTSTATE, "ON");
            //ClsDelay.Delay.MicrosecondDelay(100000);

            //2. 起始功率（Qt initialLevel；配置里的"闭环初始功率"）
            CurSignalPower = RuntimeParam.GeneralParms.GjbInitPowerDBm;
            sg.SendData((int)Communication.LEVEL, CurSignalPower.ToString("0.##"));
            TestContext.LastSignalOut = CurSignalPower;
            RunOnUI(RefreshSignalOut);

            //3. 开射频输出
            ClsDelay.Delay.MicrosecondDelay(100000);
            sg.SendData((int)Communication.OUTPUTSTATE, "ON");
            ClsDelay.Delay.MicrosecondDelay(100000);

        }

        private void SetupReceiveParams()
        {
            DeviceChannel?.ReadLevelSock?.SendData((int)Communication.初始化, "200");
            ClsDelay.Delay.MicrosecondDelay(200000);
            
            //DeviceChannel?.ReadLevelSock?.SendData((int)Communication.视频带宽, "100");
            //ClsDelay.Delay.MicrosecondDelay(200000);
            //DeviceChannel?.ReadLevelSock?.SendData((int)Communication.分辨带宽, "100");
            //ClsDelay.Delay.MicrosecondDelay(200000);
            double? level = HasBandParms ? (CurrentBandParms?.StartLevel + 10) : 0;
            DeviceChannel?.ReadLevelSock?.SendData((int)Communication.参考电平, level.ToString());
        }

        // -------------------- 限值 / 校准表插值 --------------------
        /// <summary>本频点接收机读数目标限值（dBμA）：文件模式按 LimitTable 在 log10(f) 域插值
        /// （GJB151C-2024 CS114 限值曲线按 log 频率轴给出，锚点之间在 log 轴上连线）</summary>
        /// 定值 StartLevel == EndLevel 就直接取StartLevel
        /// bandIndex：频点所属频段下标。★ 保存数据时必须显式传入——保存时测试已结束、
        /// CurrentBandIndex 已归零，起止限值不同的频段按默认索引插值会算出错误限值
        private double GetReceiverLimit(double freqMHz, CS114BandParms bp, int bandIndex = -1)
        {
            if (bp.LimitMode == LimitType.File && bp.LimitTable.Count > 0)
            {
                var pts = bp.LimitTable.OrderBy(p => double.TryParse(p.FreqMHz, out double db) ? db : 0.0)
                                       .Select(p => (double.TryParse(p.FreqMHz, out double db) ? db : 0.0, double.TryParse(p.LevelDB, out double ldb) ? ldb : 0.0)).ToList();
                return InterpLog(freqMHz, pts);
            }
            if (bp.StartLevel == bp.EndLevel)
                return bp.StartLevel;
            // 3) 起止限值不同：按频段起止频率在 log 频率轴上连线（与 BuildLimitCurve 定值画法同一公式）
            var CommonFre = CommonFreAndHardParms[bandIndex >= 0 ? bandIndex : TestContext.CurrentBandIndex].FreqBase;
            double startFreq = GenericHelper.ConvertToMhzData(CommonFre.StartFre, CommonFre.StartFreUnit);
            double endFreq = GenericHelper.ConvertToMhzData(CommonFre.EndFre, CommonFre.EndFreUnit);
            // 防御：起止频率无效/倒挂/非正（log 域要求频率大于 0），直接用起始限值兜底
            if (startFreq <= 0 || endFreq <= startFreq)
                return bp.StartLevel;
            // 对数域两点插值：锚点在 log10(f) 轴上连直线，范围外钳到端点值
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
            return 10.0 * Math.Log10(powerW * 1000.0);   //W → dBm
        }
        /// <summary>
        /// 获取信号源限制值
        /// </summary>
        /// <param name="freqMHz"></param>
        /// <returns></returns>
        private double? GetSignalLimitOut(double freqMHz)
        {
            var amplifierOutTable = AmpPara.MaxinValueData;      // Dictionary<double, double>：频率(Hz/MHz) → 值
            var cableLossTable = SignalCableLoss?.Points;      // 同样按字典处理

            if (amplifierOutTable == null || amplifierOutTable.Count == 0) return null;
            if (cableLossTable == null || cableLossTable.Count == 0) return null;

            // 1) 功放在该频率下的输出功率（dBm）——log 频率域插值，与限值曲线/图表同一对数空间
            double powerDbm = InterpDictionaryLog(amplifierOutTable, freqMHz);

            // 2) 该频率下的信号线线损（dB）——log 频率域插值
            double cableLossDb = InterpCoeffLog(cableLossTable, freqMHz);

            // 3) 信号源输出限制值 = 功放最大输出 + 线损
            return powerDbm + cableLossDb;

        }

        /// <summary>线性插值（要求 pts 已按 X 升序；点数不足取端点值）</summary>
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

        /// <summary>
        /// 对数域插值：横轴在 log10(f) 上做线性插值（纵轴已是 dB 值，直接线性插值）。
        /// 为什么不用 Interp：GJB151C-2024 CS114 限值曲线按 log 频率轴给出，锚点之间应在
        /// log 轴上连直线——MHz 线性插值会让锚点之间的曲线在对数坐标里鼓包/凹陷，
        /// 限值算值和图上限值线都偏离标准形状。
        /// 非正频点（解析失败产生的 0 值脏数据）不参与插值；范围外钳到端点值，不外推
        /// </summary>
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
            => Interp(x, table
                .OrderBy(p => p.Key)                 // 字典顺序不可依赖，必须排序
                .Select(p => (p.Key, p.Value))       // 用 Key/Value，不是 FreqMHz/ForwardPower
                .ToList());
        /// <summary>字典表对数域插值（GetSignalLimitOut 功放最大输出表用）</summary>
        private static double InterpDictionaryLog(Dictionary<double, double> table, double x)
            => InterpLog(x, table
                .OrderBy(p => p.Key)                 // 字典顺序不可依赖，必须排序
                .Select(p => (p.Key, p.Value))
                .ToList());
        /// <summary>线损表插值（dB）。表为空/未配置返回 0（不参与补偿），避免空引用打断测试循环</summary>
        private static double InterpCoeff(List<CS114CoeffPoint>? pts, double freqMHz)
        {
            if (pts == null || pts.Count == 0) return 0.0;
            return Interp(freqMHz, pts.OrderBy(p => double.TryParse(p.FreqMHz, out double d) ? d : 0.0)
                                  .Select(p => (double.TryParse(p.FreqMHz, out double d) ? d : 0.0,
                                  double.TryParse(p.CoefDB, out double c) ? c : 0.0)).ToList());
        }
        /// <summary>线损表对数域插值（dB，GetSignalLimitOut 用）。表为空/未配置返回 0，避免空引用打断测试循环</summary>
        private static double InterpCoeffLog(List<CS114CoeffPoint>? pts, double freqMHz)
        {
            if (pts == null || pts.Count == 0) return 0.0;
            return InterpLog(freqMHz, pts.OrderBy(p => double.TryParse(p.FreqMHz, out double d) ? d : 0.0)
                                  .Select(p => (double.TryParse(p.FreqMHz, out double d) ? d : 0.0,
                                  double.TryParse(p.CoefDB, out double c) ? c : 0.0)).ToList());
        }

        // -------------------- 校准数据导出（对应 Qt generateCalibFile）--------------------
        /// <summary>
        /// 把校准阶段采集的"频率-前向功率"导出为 CSV。
        /// 目录：System\校准文件\CS114（正式测试在配置页"导入校准数据"时选这个文件，
        /// 解析格式 ParseCalCsv：每行 频率MHz,前向功率W，表头可有可无——两处格式必须保持一致）
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
                string dir = Path.Combine(GenericParameter.SysPath, "System", "校准文件", "CS114");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir,
                    $"{SanitizeFileName(TestTemplateName)}_校准_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

                var sb = new StringBuilder();
                sb.AppendLine("Frequency(MHz),ForwardPower(W)");
                foreach (var p in RuntimeParam.CalCollectList.OrderBy(p => p.FreqMHz))
                    sb.AppendLine($"{p.FreqMHz:F6},{p.ForwardPowerW:0.######}");
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

                AddLog("INFO", $"CS114 校准文件已保存：{path}");
                return path;
            }
            catch (Exception ex)
            {
                AddLog("ERROR", $"导出校准文件失败：{ex.Message}");
                return string.Empty;
            }
        }

        // -------------------- 保存数据（底部【保存】按钮，仅已停止/完成后可点）--------------------
        /// <summary>
        /// 保存测试数据（对照外场抗干扰 FAJTS 的 Save 两分支结构）：
        /// ① 校准模式：**两份都存**——先把"频率-前向功率"单独存一份到 System\校准文件\CS114
        ///    （正式测试导入后做功率上限约束），再存一份本次校准的测试数据到 System\数据文件\CS114；
        ///    校准阶段内层循环同样跑闭环逼近、RefreshChartData 不分阶段打点，所以注入电平曲线有实测数据，
        ///    第二份不是空文件。
        /// ② 正式测试：只存测试数据 → 各频点实测注入电平 + 对应限值 + 余量 + 判定，存 System\数据文件\CS114。
        /// ⚠ 这里**刻意不加 else**：校准模式要的是"校准文件 + 测试数据"两份，加 else 就只剩校准文件。
        /// ⚠ 由 UI 线程的按钮命令调用（RunConfigViewModel.SaveData），弹窗/读曲线都在 UI 线程，无需再切。
        /// </summary>
        public override void SaveData()
        {
            if (RuntimeParam.CurTestStageType == TestStageType.CalibrationTest)
                SaveCalibrationData();
            SaveFormalResult();
        }

        /// <summary>校准模式保存：没有采集到任何校准点时只提示、不生成只带表头的空文件</summary>
        private void SaveCalibrationData()
        {
            if (RuntimeParam.CalCollectList.Count == 0)
            {
                System.Windows.MessageBox.Show("校准测试暂无数据，测试结束后再保存", "提示");
                return;
            }
            string path = ExportCalibrationData();
            if (string.IsNullOrEmpty(path))
                return;   //导出失败的原因已写运行日志
            AddLog("INFO", $"【保存数据】校准文件已保存，正式测试时请在配置页导入：{path}");
            System.Windows.MessageBox.Show(
                $"校准数据保存到{path}完成\n\n正式测试时在配置页点【导入校准数据】选择该文件，" +
                "即可按校准功率约束信号源输出。", "提示");
        }

        /// <summary>
        /// 正式测试保存：数据来自"注入电平"实测曲线（XData=Hz、YData=dBμA，频点结束时打点）。
        /// 限值按"频点所在频段"的配置插值——与测试判定 GetReceiverLimit 同一套逻辑、同一段配置，
        /// 所以存出的判定和测试过程中的判定一致；余量 = 限值 − 实测（≥0 合格）。
        /// 分频段测试只是功放频率范围不够，存档按频点归到所属频段，覆盖整个测试频率范围。
        /// </summary>
        private void SaveFormalResult()
        {
            //注入电平实测曲线 = 非参考线的那条（limit 红虚线是配置限值折线，不是实测值）
            var line = _injectLevelCurves.FirstOrDefault(l => !l.IsYReferenceLine);
            int n = Math.Min(line?.XData.Count ?? 0, line?.YData.Count ?? 0);
            if (line == null || n == 0)
            {
                //提示按当前阶段措辞：校准模式下也会走本方法（SaveData 不加 else），说"正式测试"会误导
                string stageName = RuntimeParam.CurTestStageType == TestStageType.FormalTest ? "正式测试" : "校准测试";
                System.Windows.MessageBox.Show($"{stageName}暂无数据，测试结束后再保存", "提示");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Frequency(MHz),Limit(dBuA),InjectLevel(dBuA),Margin(dB),Verdict");
            for (int i = 0; i < n; i++)
            {
                double freqMHz = line.XData[i] / 1e6;   //曲线坐标系是 Hz（打点约定），存档统一 MHz
                double level = line.YData[i];
                //按频率找所属频段；找不到（频点落在配置频段之外）时限值记 "-"，实测值照存
                int bandIdx = FindBandIndex(freqMHz);
                double limit = double.NaN;
                if (bandIdx >= 0 && bandIdx < RuntimeParam.ImmunityLimit.Count)
                    limit = GetReceiverLimit(freqMHz, RuntimeParam.ImmunityLimit[bandIdx], bandIdx);

                string limitText = double.IsNaN(limit) ? "-" : limit.ToString("0.###", CultureInfo.InvariantCulture);
                string marginText = double.IsNaN(limit) ? "-" : (limit - level).ToString("0.###", CultureInfo.InvariantCulture);
                string verdict = double.IsNaN(limit) ? "-" : (limit - level >= 0 ? "合格" : "超标");
                //★ 数值一律 InvariantCulture：避免本地化把小数点写成逗号，CSV 列错位
                sb.AppendLine($"{freqMHz.ToString("F6", CultureInfo.InvariantCulture)},{limitText}," +
                    $"{level.ToString("0.###", CultureInfo.InvariantCulture)},{marginText},{verdict}");
            }

            string dir = Path.Combine(GenericParameter.SysPath, "System", "数据文件", "CS114");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir,
                $"{SanitizeFileName(TestTemplateName)}_{DateTime.Now:yyyyMMddHHmmss}.csv");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

            LastSavedReportPath = path;   // 供界面把曲线图保存到报告同目录
            AddLog("INFO", $"【保存数据】CS114 测试数据已保存：{path}（共 {n} 个频点）");
            System.Windows.MessageBox.Show($"测试数据保存到{path}完成", "提示");
        }

        /// <summary>按频率(MHz)找所属频段下标；不在任何频段范围内返回 -1（容差 1e-9 吸收浮点误差）</summary>
        private int FindBandIndex(double freqMHz)
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

        /// <summary>文件名清洗：模板名可能带路径非法字符（\/:*?"<>|），统一换成下划线</summary>
        private static string SanitizeFileName(string name)
            => string.Join("_", (name ?? "未命名").Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

        #endregion

    }
}
