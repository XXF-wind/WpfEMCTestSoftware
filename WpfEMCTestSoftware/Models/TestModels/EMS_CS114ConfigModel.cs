using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;
using WpfEMCTestSoftware.Enums;
using WpfEMCTestSoftware.Models.HardwareModels;

namespace WpfEMCTestSoftware.Models.TestModels
{

    // ==================== CS114通用参数====================
    public partial class CS114GeneralParms : ObservableObject
    {
        /// <summary>反馈控制模式：禁用 / 启用前向功率 / 闭环监测</summary>
        [ObservableProperty]
        private string _feedbackMode = "禁用";
        /// <summary>调节次数</summary>
        [ObservableProperty]
        private double _adjustCount = 50;
        /// <summary>反馈限值 上限[dB]</summary>
        [ObservableProperty]
        private double _feedbackUpperDB;
        /// <summary>反馈限值 下限[dB]</summary>
        [ObservableProperty]
        private double _feedbackLowerDB;
        /// <summary>GJB151B 闭环测试法 上限[dB]</summary>
        [ObservableProperty]
        private double _gjbUpperDB;
        /// <summary>GJB151B 闭环测试法 容差[dB]</summary>
        [ObservableProperty]
        private double _gjbToleranceDB;
        /// <summary>GJB151B 闭环测试法 初始功率[dBm]</summary>
        [ObservableProperty]
        private double _gjbInitPowerDBm = -60;

        /// <summary>启用功率回馈控制</summary>
        [ObservableProperty]
        private bool _enablePowerFeedback;
        /// <summary>上升延时[ms]</summary>
        [ObservableProperty]
        private double _riseDelayMs = 1000;
        /// <summary>下降延时[ms]</summary>
        [ObservableProperty]
        private double _fallDelayMs = 1000;
        /// <summary>上升次数</summary>
        [ObservableProperty]
        private double _riseCount = 5;
        /// <summary>下降次数</summary>
        [ObservableProperty]
        private double _fallCount = 5;
        /// <summary>下降[dB]（左）</summary>
        [ObservableProperty]
        private double _fallDbLeft = 3;
        /// <summary>下降[dB]（右）</summary>
        [ObservableProperty]
        private double _fallDbRight = 3;

        /// <summary>调制参数组（调制_1/2/3）</summary>
        [ObservableProperty]
        private ObservableCollection<CS114_ModGroup> _modGroups = new()
        {
            new CS114_ModGroup { Name = "调制_1" },
            new CS114_ModGroup { Name = "调制_2" },
            new CS114_ModGroup { Name = "调制_3" },
        };

        /// <summary>上电时间[ms]</summary>
        [ObservableProperty]
        private double _powerOnMs = 12000;
        /// <summary>下电时间[ms]</summary>
        [ObservableProperty]
        private double _powerOffMs = 4000;
        /// <summary>偏置电压[V]</summary>
        [ObservableProperty]
        private double _biasVoltageV = 2;
        /// <summary>输出电压[V]</summary>
        [ObservableProperty]
        private double _outputVoltageV = 2;
        /// <summary>功率单位（W / dBm）</summary>
        [ObservableProperty]
        private string _powerUnit = "W";
        /// <summary>测试等级Y轴（Linear / Log）</summary>
        [ObservableProperty]
        private string _levelAxisMode = "Linear";
        /// <summary>Y轴最小</summary>
        [ObservableProperty]
        private double _levelAxisMin = -50;
        /// <summary>Y轴最大</summary>
        [ObservableProperty]
        private double _levelAxisMax = 200;
        /// <summary>报告图片宽度[px]</summary>
        [ObservableProperty]
        private double _reportImgWidth = 470;
        /// <summary>报告图片高度[px]</summary>
        [ObservableProperty]
        private double _reportImgHeight = 200;
        /// <summary>报告模板路径</summary>
        [ObservableProperty]
        private string _reportTemplatePath = string.Empty;

    }

    //=====================BandingSetting参数==============
    public partial class CS114BandParms : ObservableObject
    {
        #region 线损（5 条，标明每条从哪到哪）
        /// <summary>线损列表：固定 5 条，标明每条从哪到哪；Points 为频率-损耗数据</summary>
        [ObservableProperty]
        private ObservableCollection<CS114CableLoss> _cableLosses = new();

        /// <summary>按顺序补齐 5 条线损（只在缺失时填充）</summary>
        public void EnsureCableLosses()
        {
            (string From, string To)[] defaults =
            {
                ("信号源", "功率放大器"),
                ("功率放大器", "双定向耦合器"),
                ("耦合器前向口", "前向功率计"),
                ("耦合器反向口", "反向功率计"),
                ("注入探头", "校准夹具"),
            };
            for (int i = 0; i < defaults.Length; i++)
            {
                if (i >= CableLosses.Count)
                    CableLosses.Add(new CS114CableLoss(defaults[i].From, defaults[i].To));
            }
        }
        #endregion

        #region 其他分频段参数（等级 / 限值 / 暂停 / 调制）
        /// <summary>起始等级 [dBμA]（信号源限制值 limit）</summary>
        [ObservableProperty]
        private double _startLevel = 60;

        partial void OnStartLevelChanged(double value) => OnPropertyChanged(nameof(LimitDisplayText));

        /// <summary>终止等级 [dBμA]</summary>
        [ObservableProperty]
        private double _endLevel = 60;

        /// <summary>限值类型：定值/文件</summary>
        [ObservableProperty]
        private LimitType _limitMode = LimitType.Const;

        partial void OnLimitModeChanged(LimitType value) => OnPropertyChanged(nameof(LimitDisplayText));

        /// <summary>上限裕度 [dB]</summary>
        [ObservableProperty]
        private double _upperLimit = 6;



        /// <summary>限值表文件路径（文件导入模式）</summary>
        [ObservableProperty]
        private string _immunityTableAddr = string.Empty;

        /// <summary>限值偏置[dB]：导入的传导安全裕度数据 + 偏置 = 限值等级（默认 +6）</summary>
        [ObservableProperty]
        private double _limitOffsetDB = 6;

        /// <summary>限值曲线点集合（频率MHz → 等级dBμA，导入裕度数据+偏置后生成）</summary>
        [ObservableProperty]
        private List<CS114LimitPoint> _limitTable = new();

        /// <summary>限值点数文字（“导入” / “N点·查看”）</summary>
        public string LimitPointCountText => LimitTable.Count > 0 ? $"{LimitTable.Count}点·查看" : "导入";

        /// <summary>连接图 limit 徽标显示文字（定值显示数值，文件模式显示点数）</summary>
        public string LimitDisplayText => LimitMode == LimitType.File && LimitTable.Count > 0
            ? $"文件·{LimitTable.Count}点"
            : StartLevel.ToString();

        /// <summary>限值表格变动后刷新状态显示</summary>
        public void RefreshLimitStatus()
        {
            OnPropertyChanged(nameof(LimitPointCountText));
            OnPropertyChanged(nameof(LimitDisplayText));
            OnPropertyChanged(nameof(LimitTable));
        }

        /// <summary>频点暂停时间 [ms]</summary>
        [ObservableProperty]
        private double _pauseTime = 10;

        /// <summary>调制类型（CW 连续波 / AM_1 / FM_1 / Pulse_1 ...）</summary>
        [ObservableProperty]
        private string _modulationText = "CW 连续波";
        #endregion

        /// <summary>复制一份频段参数（添加频段时用，线损/限值点深拷贝、互不影响）</summary>
        public CS114BandParms Clone()
        {
            var copy = new CS114BandParms
            {
                StartLevel = StartLevel,
                EndLevel = EndLevel,
                LimitMode = LimitMode,
                UpperLimit = UpperLimit,

                ImmunityTableAddr = ImmunityTableAddr,
                LimitOffsetDB = LimitOffsetDB,
                LimitTable = new List<CS114LimitPoint>(LimitTable),
                PauseTime = PauseTime,
                ModulationText = ModulationText,
                CableLosses = new ObservableCollection<CS114CableLoss>(
                    CableLosses.Select(c => new CS114CableLoss(c.FromName, c.ToName)
                    {
                        DisplayName = c.DisplayName,
                        Points = c.Points.Select(p => new CS114CoeffPoint { FreqMHz = p.FreqMHz, CoefDB = p.CoefDB }).ToList(),
                        Source = c.Source,
                        LastModified = c.LastModified,
                    })),
            };
            foreach (var c in copy.CableLosses) c.RefreshStatus();
            return copy;
        }
    }

    // ==================== 弹窗/页面配套数据契约（保持现有弹窗与页面可用）====================
    /// <summary>
    /// CS114 系数点：频率(MHz) → 损耗/系数(dB)
    /// </summary>
    public partial class CS114CoeffPoint : ObservableObject
    {
        [ObservableProperty]
        private string _freqMHz;

        [ObservableProperty]
        private string _coefDB;
    }

    /// <summary>
    /// CS114 限值点：频率(MHz) → 限值等级(dBμA)
    /// 由导入的传导安全裕度测试数据 + 偏置（默认 +6dB）计算得出
    /// </summary>
    public partial class CS114LimitPoint : ObservableObject
    {
        [ObservableProperty]
        private string _freqMHz;

        [ObservableProperty]
        private string _levelDB;
    }

    /// <summary>
    /// 调制参数组（调制_1/2/3，每组含 AM/FM/Pulse 参数）
    /// </summary>
    public partial class CS114_ModGroup : ObservableObject
    {
        /// <summary>调制组名称（如 调制_1）</summary>
        public string Name { get; set; } = "调制_1";

        /// <summary>AM 深度 [%]</summary>
        [ObservableProperty]
        private double _amDepth = 80;

        /// <summary>AM 速率 [kHz]</summary>
        [ObservableProperty]
        private double _amRateKHz = 1;

        /// <summary>FM 偏移 [kHz]</summary>
        [ObservableProperty]
        private double _fmOffsetKHz = 1;

        /// <summary>FM 速率 [kHz]</summary>
        [ObservableProperty]
        private double _fmRateKHz = 1;

        /// <summary>Pulse 周期 [ms]</summary>
        [ObservableProperty]
        private double _pulsePeriodMs = 1;

        /// <summary>Pulse 宽度 [ms]</summary>
        [ObservableProperty]
        private double _pulseWidthMs = 0.5;
    }

    // ==================== 线损 ====================
    /// <summary>
    /// 线损条目：一段电缆的损耗，标明从哪到哪（如：信号源→功率放大器）。
    /// Points 为该线损的频率-损耗数据，支持手动添加/导入/导出，随配置保存下次使用
    /// </summary>
    public partial class CS114CableLoss : ObservableObject
    {
        public CS114CableLoss() { }

        /// <summary>线损构造：标明从哪到哪</summary>
        public CS114CableLoss(string fromName, string toName)
        {
            FromName = fromName;
            ToName = toName;
            DisplayName = $"{fromName}→{toName}";
        }

        /// <summary>线损起点（从）</summary>
        [ObservableProperty]
        private string _fromName = string.Empty;

        /// <summary>线损终点（到）</summary>
        [ObservableProperty]
        private string _toName = string.Empty;

        /// <summary>线损显示名称（哪到哪）</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>线损点集合（频率MHz → 损耗dB）</summary>
        public List<CS114CoeffPoint> Points { get; set; } = new();

        /// <summary>来源：手动 / 导入 / 空</summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>最近更新时间</summary>
        public string LastModified { get; set; } = string.Empty;

        /// <summary>是否已添加线损</summary>
        [ObservableProperty]
        private bool _isConfigured;

        /// <summary>状态文字：已添加 / 未添加</summary>
        public string StatusText => IsConfigured ? "已添加" : "未添加";

        /// <summary>线损点数文字</summary>
        public string PointCountText => Points.Count.ToString();

        /// <summary>根据点数刷新状态（添加/导入/清除后调用）</summary>
        public void RefreshStatus()
        {
            IsConfigured = Points.Count > 0;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(PointCountText));
        }
    }

    /// <summary>
    /// CS114 配置模板
    /// </summary>
    public partial class EMS_CS114ConfigModel : ObservableObject
    {
        // ==================== 全局测试参数 ====================
        [ObservableProperty]
        private EMCStandard _curEMSStandard = EMCStandard.GJB151B;   // EMC标准
        [ObservableProperty]
        private TestStageType _curTestStageType = TestStageType.FormalTest;  // 测试阶段
        [ObservableProperty]
        private CS114GeneralParms _generalCS114Parms = new CS114GeneralParms();
        /// <summary>校准功率表（正式测试用：由校准阶段导出的 CSV 导入，按频率插值出功率上限；空表 = 不做功率约束）</summary>
        [ObservableProperty]
        private List<CS114CalPoint> _calPowerTable = [];
        /// <summary>校准功率表来源文件路径（仅用于界面显示与追溯）</summary>
        [ObservableProperty]
        private string _calPowerTablePath = "";
        // ==================== 测试频段模板 ====================
        /// <summary>测试频段列表（分频段参数：频率 / 硬件设备 / 线损）</summary>
        [ObservableProperty]
        private ObservableCollection<EMS_CS114FreBand> _listFreBands = [];
        /// <summary>
        /// 初始化默认频段
        /// </summary>
        public void EnsureDefaults()
        {
            if (ListFreBands.Count == 0)
                ListFreBands.Add(new EMS_CS114FreBand());
        }
    }

    /// <summary>CS114 校准点：频率(MHz) → 校准阶段记录的前向功率(W)。
    /// 校准阶段逐频点记录，导出成 CSV；正式测试导入后按频率插值出功率上限</summary>
    public class CS114CalPoint
    {
        public double FreqMHz { get; set; }
        public double ForwardPowerW { get; set; }
    }

    /// <summary>
    /// 分频段参数
    /// 频率
    /// 硬件设备
    /// 线损
    /// </summary>
    public partial class EMS_CS114FreBand : ObservableObject
    {
        public EMS_CS114FreBand()
        {
            // 默认频段：GJB151B CS114 电缆束注入传导敏感度 10kHz ~ 400MHz
            FreqCommon.StartFre = 10;
            FreqCommon.StartFreUnit = FrequencyUnit.KHz;
            FreqCommon.EndFre = 400;
            FreqCommon.EndFreUnit = FrequencyUnit.MHz;
            FreqCommon.StepSize = 1;
            FreqCommon.StepSizeUnit = FrequencyUnit.MHz;
            FreqCommon.MStepMode = STEPMODE.LIN;
            FreqCommon.FreqDwellTime = 1000;    // 驻留 ms
            _bandParms.EnsureCableLosses();

        }

        public CommonFreqStepBase FreqCommon = new CommonFreqStepBase();
        #region 界面绑定包装器
        [DataGridColumnSetting("起始频率", Width = "130", HorizontalAlignment = HorizontalAlignment.Center)]
        public double StartFre
        {
            get => FreqCommon.StartFre;
            set
            {
                FreqCommon.StartFre = value;
                OnPropertyChanged();
            }
        }
        [DataGridColumnSetting("起始单位", Width = "130", HorizontalAlignment = HorizontalAlignment.Center)]
        public FrequencyUnit StartFreUnit
        {
            get => FreqCommon.StartFreUnit;
            set
            {
                FreqCommon.StartFreUnit = value;
                OnPropertyChanged();
            }
        }


        [DataGridColumnSetting("截止频率", Width = "130", HorizontalAlignment = HorizontalAlignment.Center)]
        public double EndFreq
        {
            get => FreqCommon.EndFre;
            set
            {
                FreqCommon.EndFre = value;
                OnPropertyChanged();
            }
        }

        [DataGridColumnSetting("截止单位", Width = "130", HorizontalAlignment = HorizontalAlignment.Center)]
        public FrequencyUnit EndFreUnit
        {
            get => FreqCommon.EndFreUnit;
            set
            {
                FreqCommon.EndFreUnit = value;
                OnPropertyChanged();
            }
        }

        [DataGridColumnSetting("步进", Width = "130", HorizontalAlignment = HorizontalAlignment.Center)]
        public double Stepsize
        {
            get => FreqCommon.StepSize;
            set
            {
                FreqCommon.StepSize = value;
                OnPropertyChanged();
            }
        }

        [DataGridColumnSetting("步进模式", Width = "130", HorizontalAlignment = HorizontalAlignment.Center)]
        public STEPMODE Mstepmode
        {
            get => FreqCommon.MStepMode;
            set
            {
                FreqCommon.MStepMode = value;
                OnPropertyChanged(); // 通知界面刷新
                // 步进模式切换 → 步进单位的显示随之切换（LIN=频率单位，LOG=百分比）
                OnPropertyChanged(nameof(IsLogStepMode));
                OnPropertyChanged(nameof(StepUnitText));
            }
        }

        public FrequencyUnit StepUnit
        {
            get => FreqCommon.StepSizeUnit;
            set
            {
                FreqCommon.StepSizeUnit = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StepUnitText));
            }
        }

        /// <summary>
        /// 是否为百分比步进：步进模式 = LOG 时，步进值按百分比递增（如 10 表示 10%），单位固定为 %
        /// </summary>
        public bool IsLogStepMode => FreqCommon.MStepMode == STEPMODE.LOG;

        /// <summary>
        /// 步进单位显示文字：LIN 显示频率单位（Hz/kHz/MHz/GHz），LOG 显示百分比（%）
        /// </summary>
        public string StepUnitText => IsLogStepMode ? "%" : StepUnit.GetDescription();

        [DataGridColumnSetting("频点驻留时间", Width = "130", HorizontalAlignment = HorizontalAlignment.Center)]
        public double FreqDwellTime
        {
            get => FreqCommon.FreqDwellTime;
            set
            {
                FreqCommon.FreqDwellTime = value;
                OnPropertyChanged();
            }

        }
        #endregion

        #region 硬件模板
        [ObservableProperty]
        private RVC_EMSHardModel _rVCHardModel = new();
        #endregion

        #region 独有频段参数 
        [ObservableProperty]
        private CS114BandParms _bandParms = new CS114BandParms();
        #endregion


    }
}
