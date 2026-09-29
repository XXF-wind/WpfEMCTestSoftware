using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;
using WpfEMCTestSoftware.Enums;
using WpfEMCTestSoftware.Models.HardwareModels;


namespace WpfEMCTestSoftware.Models.TestModels
{
    public class RVC_FUParam
    {
        public double StirrerDwellTime { get; set; }
        public double TxAntennaEfficiency { get; set; }
        public double RxAntennaEfficiency { get; set; }
        public double InterferenceLevelLimit { get; set; }
        public double UpperOffsetDb { get; set; }
        public double LowerOffsetDb { get; set; }
        public double NextPointLevelAdjustDb { get; set; }
        public bool IsOffsetLog { get; set; }     //场强限值精度Log类型
        public bool IsPowerAmpLinearCheckEnabled { get; set; }
        public double LinearAdjustRangeDb { get; set; }
        public string HarmonicTestMethod { get; set; } = string.Empty;
        public bool TestSecondHarmonic { get; set; }
        public bool TestThirdHarmonic { get; set; }

    }
    public partial class RVC_FieldUniformityConfigModel : ObservableObject
    {
        // ==================== 全局测试参数 ====================
        [ObservableProperty]
        private EMCStandard _curEMSStandard;       // EMC标准
        [ObservableProperty]
        private int _minPositionPoints;             //最少位置点数
        [ObservableProperty]
        private int _currentTestPoint;              //当前测试点
        [ObservableProperty]
        private AdjustPriority _adjustmentPriority; //调整优先级       
        // ==================== 测试频段模板 ====================
        [ObservableProperty]
        private ObservableCollection<RVC_FUFreBand> _listFreBand = [];
    }
    public partial class RVC_FUFreBand : ObservableObject
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
        #region 硬件模板
        [ObservableProperty]
        private RVC_EMSHardModel _rVCHardModel = new();
        #endregion

        public RVC_FUParam rvc_FuParam = new();
        #region 以下独有字段 搅拌器设置

        [DataGridColumnSetting("搅拌器驻留时间", Width = "150", HorizontalAlignment = HorizontalAlignment.Center)]
        public double StirrerDwellTime
        {
            get => rvc_FuParam.StirrerDwellTime;
            set
            {
                rvc_FuParam.StirrerDwellTime = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region 天线效率设置
        private double _txAntennaEfficiency; // 发射天线效率（%）
        [DataGridColumnSetting("发射天线效率", Width = "150", HorizontalAlignment = HorizontalAlignment.Center)]
        public double TxAntennaEfficiency
        {
            get => _txAntennaEfficiency;
            set
            {
                _txAntennaEfficiency = value;
                OnPropertyChanged();
            }
        }

        private double _rxAntennaEfficiency; // 接收天线效率（%）
        [DataGridColumnSetting("接收天线效率", Width = "150", HorizontalAlignment = HorizontalAlignment.Center)]
        public double RxAntennaEfficiency
        {
            get => _rxAntennaEfficiency;
            set
            {
                _rxAntennaEfficiency = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region 电平设置       
        [DataGridColumnSetting("干扰电平限值", Width = "150", HorizontalAlignment = HorizontalAlignment.Center)]
        public double InterferenceLevelLimit
        {
            get => rvc_FuParam.InterferenceLevelLimit;
            set
            {
                rvc_FuParam.InterferenceLevelLimit = value;
                OnPropertyChanged();
            }
        }
        [DataGridColumnSetting("上偏移", Width = "130", HorizontalAlignment = HorizontalAlignment.Center)]
        public double UpperOffsetDb
        {
            get => rvc_FuParam.UpperOffsetDb;
            set
            {
                rvc_FuParam.UpperOffsetDb = value;
                OnPropertyChanged();
            }
        }
        [DataGridColumnSetting("下偏移", Width = "130", HorizontalAlignment = HorizontalAlignment.Center)]
        public double LowerOffsetDb
        {
            get => rvc_FuParam.LowerOffsetDb;
            set
            {
                rvc_FuParam.LowerOffsetDb = value;
                OnPropertyChanged();
            }
        }


        [DataGridColumnSetting("下一频点电平调整", Width = "170", HorizontalAlignment = HorizontalAlignment.Center)]
        public double NextPointLevelAdjustDb
        {
            get => rvc_FuParam.NextPointLevelAdjustDb;
            set
            {
                rvc_FuParam.NextPointLevelAdjustDb = value;
                OnPropertyChanged();
            }
        }

        public bool IsOffsetLog      //场强限值精度Log类型
        {
            get => rvc_FuParam.IsOffsetLog;
            set
            {
                rvc_FuParam.IsOffsetLog = value;
                OnPropertyChanged();
            }
        }

        [DataGridColumnSetting("功放线性度检查", Width = "150", HorizontalAlignment = HorizontalAlignment.Center)]
        public bool IsPowerAmpLinearCheckEnabled
        {
            get => rvc_FuParam.IsPowerAmpLinearCheckEnabled;
            set
            {
                rvc_FuParam.IsPowerAmpLinearCheckEnabled = value;
                OnPropertyChanged();
            }
        }


        [DataGridColumnSetting("线性度调整范围", Width = "150", HorizontalAlignment = HorizontalAlignment.Center)]
        public double LinearAdjustRangeDb
        {
            get => rvc_FuParam.LinearAdjustRangeDb;
            set
            {
                rvc_FuParam.LinearAdjustRangeDb = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region 谐波测试设置

        [DataGridColumnSetting("谐波测试方法", Width = "150", HorizontalAlignment = HorizontalAlignment.Center)]
        public string HarmonicTestMethod
        {
            get => rvc_FuParam.HarmonicTestMethod;
            set
            {
                rvc_FuParam.HarmonicTestMethod = value;
                OnPropertyChanged();
            }
        }

        [DataGridColumnSetting("二次谐波", Width = "130", HorizontalAlignment = HorizontalAlignment.Center)]
        public bool TestSecondHarmonic
        {
            get => rvc_FuParam.TestSecondHarmonic;
            set
            {
                rvc_FuParam.TestSecondHarmonic = value;
                OnPropertyChanged();
            }
        }


        [DataGridColumnSetting("三次谐波", Width = "130", HorizontalAlignment = HorizontalAlignment.Center)]
        public bool TestThirdHarmonic
        {
            get => rvc_FuParam.TestThirdHarmonic;
            set
            {
                rvc_FuParam.TestThirdHarmonic = value;
                OnPropertyChanged();
            }
        }
        #endregion


    }
}
