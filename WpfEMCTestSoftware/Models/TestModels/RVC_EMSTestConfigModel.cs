using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;
using WpfEMCTestSoftware.Enums;
using WpfEMCTestSoftware.Models.HardwareModels;

namespace WpfEMCTestSoftware.Models.TestModels
{
    /// <summary>
    ///EMS共用参数：起止频率、单位、步进参数
    /// </summary>
    public class CommonFreqStepBase
    {
        public double StartFre { get; set; }
        public FrequencyUnit StartFreUnit { get; set; }
        public double EndFre { get; set; }
        public FrequencyUnit EndFreUnit { get; set; }
        public double StepSize { get; set; }
        public STEPMODE MStepMode { get; set; }

        public FrequencyUnit StepSizeUnit { get; set; }

        public double FreqDwellTime { get; set; }

    }
    //抗扰度限制值
    public class EmsImmunityLimit
    {
        public double ConstlimitLevel { get; set; }                // 限制值（定值模式）                                    
        public double UpperLimit { get; set; }         //幅值上限（功率/场强最大值）                                    
        public double LowerLimit { get; set; }         // 幅值下限（功率/场强最小值）                                    
        public ModulationType ModulationMode { get; set; }
        public LimitType LimitMode { get; set; }
        public string ImmunityTableAddr { get; set; }   // 抗扰度限值表文件路径
        public List<(double Freq, double Limit)> FreqLimitTable { get; set; } // 频率点→限值的列表
        public EmsImmunityLimit()
        {
            FreqLimitTable = new List<(double Freq, double Limit)>();
        }
    }

    public partial class RVC_EMSTestConfigModel : ObservableObject
    {
        // ==================== 通用参数 ====================
        [ObservableProperty]
        private EMCStandard _curEMSStandard;                    // 执行标准
        [ObservableProperty]
        private ReverbTestMode _curReverbTestMode;              //测试模式
        [ObservableProperty]
        private TestStageType _curTestStageType;                //测试阶段
        [ObservableProperty]
        private string _preCalFileName = "";                         //预校准文件名称
        //========================测试频段参数====================
        [ObservableProperty]
        private ObservableCollection<RVC_EMSFreBand> _listRvcFreBands = new();
    }
    public partial class RVC_EMSFreBand : ObservableObject
    {
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
            }
        }

        public FrequencyUnit StepUnit
        {
            get => FreqCommon.StepSizeUnit;
            set
            {
                FreqCommon.StepSizeUnit = value;
                OnPropertyChanged();
            }
        }
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
        #region 抗扰度独有字段
        public EmsImmunityLimit ImmunityLimit = new EmsImmunityLimit();

        [DataGridColumnSetting("限制值类型", Width = "130", HorizontalAlignment = HorizontalAlignment.Center)]

        public LimitType IimitType
        {
            get => ImmunityLimit.LimitMode;
            set
            {
                ImmunityLimit.LimitMode = value;
                OnPropertyChanged();
            }
        }
        [DataGridColumnSetting("限制值", Width = "130", HorizontalAlignment = HorizontalAlignment.Center)]
        public double ConstlimitLevel
        {
            get => ImmunityLimit.ConstlimitLevel;
            set
            {
                ImmunityLimit.ConstlimitLevel = value;
                OnPropertyChanged();
            }
        }
        [DataGridColumnSetting("上限裕度", Width = "130", HorizontalAlignment = HorizontalAlignment.Center)]
        public double UpperLimit
        {
            get => ImmunityLimit.UpperLimit;
            set
            {
                ImmunityLimit.UpperLimit = value;
                OnPropertyChanged();
            }
        }
        [DataGridColumnSetting("下限裕度", Width = "130", HorizontalAlignment = HorizontalAlignment.Center)]
        public double LowerLimit
        {
            get => ImmunityLimit.LowerLimit;
            set
            {
                ImmunityLimit.LowerLimit = value;
                OnPropertyChanged();
            }
        }

        public string ImmunityTableAddr
        {
            get => ImmunityLimit.ImmunityTableAddr;
            set
            {
                ImmunityLimit.ImmunityTableAddr = value;
                OnPropertyChanged();
            }
        }
        [DataGridColumnSetting("调制类型", Width = "130", HorizontalAlignment = HorizontalAlignment.Center)]
        public ModulationType ModulationType
        {
            get => ImmunityLimit.ModulationMode;
            set
            {
                ImmunityLimit.ModulationMode = value;
                OnPropertyChanged();
            }
        }


        #endregion
        #region 硬件模板
        [ObservableProperty]
        private RVC_EMSHardModel _rVCHardModel = new();
        #endregion
    }
}
