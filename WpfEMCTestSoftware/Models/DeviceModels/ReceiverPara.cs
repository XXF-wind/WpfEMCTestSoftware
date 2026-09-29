using System.Collections.ObjectModel;

namespace WpfEMCTestSoftware.Models
{
    #region 枚举类型    
    /// <summary>
    /// 测试方式
    /// </summary>
    public enum TestMethod
    {
        扫频
    }
    /// <summary>
    /// 输入端口
    /// </summary>
    public enum InputPort
    {
        端口1,
        端口2
    }

    /// <summary>
    /// 轨迹显示
    /// </summary>
    public enum TrackDisplay
    {
        刷新显示
    }

    /// <summary>
    /// 耦合方式
    /// </summary>
    public enum CouplingMethod
    {
        交流
    }

    /// <summary>
    /// 底噪扩展
    /// </summary>
    public enum BottomNoiseExpan
    {
        关闭,
        开启
    }

    /// <summary>
    /// 前置预放
    /// </summary>
    public enum PreAmplifier
    {
        关闭,
        开启
    }

    /// <summary>
    /// 预选器
    /// </summary>
    public enum PreSelector
    {
        关闭,
        开启
    }

    /// <summary>
    /// 步进类型
    /// </summary>
    public enum StepType
    {
        线性步进,
    }

    /// <summary>
    /// 检波器
    /// </summary>
    public enum Cymoscope
    {
        无,
        _MIL_峰值
    }

    /// <summary>
    /// 频率单位枚举
    /// </summary>
    public enum FreqUnit
    {
        Hz,
        kHz,
        MHz,
        GHz
    }
    #endregion

    public class ReceiverPara : DeviceInfo
    {
        private TestMethod _testMethod = TestMethod.扫频;// 测试方式
        private InputPort _inputPort = InputPort.端口1;// 输入端口
        private TrackDisplay _trackDisplay = TrackDisplay.刷新显示;// 轨迹显示
        private CouplingMethod _couplingMethod = CouplingMethod.交流;// 耦合方式
        private BottomNoiseExpan _bottomNoiseExpan = BottomNoiseExpan.关闭;// 底噪扩展
        private bool _isTDS = false;// 是否使用TDS功能
        private double _referenceAmplitude = 0.0;// 参考幅度
        private double _builtInAtt = 0.0;// 内置衰减
        private PreAmplifier _preAmplifier = PreAmplifier.关闭;// 前置预放
        private PreSelector _preSelector = PreSelector.关闭;// 预选器
        private ObservableCollection<Cymoscope> _cymoscopes = [Cymoscope._MIL_峰值, Cymoscope.无, Cymoscope.无, Cymoscope.无];// 检波器组
        private double _rbw = 0.0;// 分辨率带宽
        private StepType _stepType = StepType.线性步进;//步进类型
        private int _measuretime = 0;//测量时间
        private int _frequencystep = 0;//频率步进
        private FreqUnit _freqUnit = FreqUnit.Hz; //频率单位
        /// <summary>
        /// 测试方式
        /// </summary>
        public TestMethod TestMethod
        {
            get => _testMethod;
            set => SetProperty(ref _testMethod, value);
        }
        /// <summary>
        /// 输入端口
        /// </summary>
        public InputPort InputPort
        {
            get => _inputPort;
            set => SetProperty(ref _inputPort, value);
        }
        /// <summary>
        /// 轨迹显示
        /// </summary>
        public TrackDisplay TrackDisplay
        {
            get => _trackDisplay;
            set => SetProperty(ref _trackDisplay, value);
        }
        /// <summary>
        /// 耦合方式
        /// </summary>
        public CouplingMethod CouplingMethod
        {
            get => _couplingMethod;
            set => SetProperty(ref _couplingMethod, value);
        }
        /// <summary>
        /// 底噪扩展
        /// </summary>
        public BottomNoiseExpan BottomNoiseExpan
        {
            get => _bottomNoiseExpan;
            set => SetProperty(ref _bottomNoiseExpan, value);
        }
        /// <summary>
        /// 是否使用TDS功能
        /// </summary>
        public bool IsTDS
        {
            get => _isTDS;
            set => SetProperty(ref _isTDS, value);
        }
        /// <summary>
        /// 参考幅度
        /// </summary>
        public double ReferenceAmplitude
        {
            get => _referenceAmplitude;
            set => SetProperty(ref _referenceAmplitude, value);
        }

        /// <summary>
        /// 内置衰减
        /// </summary>
        public double BuiltInAtt
        {
            get => _builtInAtt;
            set => SetProperty(ref _builtInAtt, value);
        }
        /// <summary>
        /// 前置预放
        /// </summary>
        public PreAmplifier PreAmplifier
        {
            get => _preAmplifier;
            set => SetProperty(ref _preAmplifier, value);
        }
        /// <summary>
        /// 预选器
        /// </summary>
        public PreSelector PreSelector
        {
            get => _preSelector;
            set => SetProperty(ref _preSelector, value);
        }
        /// <summary>
        /// 检波器组
        /// </summary>
        public ObservableCollection<Cymoscope> Cymoscopes
        {
            get => _cymoscopes;
            set => SetProperty(ref _cymoscopes, value);
        }
        /// <summary>
        /// 分辨率带宽
        /// </summary>
        public double Rbw
        {
            get => _rbw;
            set => SetProperty(ref _rbw, value);
        }
        /// <summary>
        /// 步进类型
        /// </summary>
        public StepType StepType
        {
            get => _stepType;
            set => SetProperty(ref _stepType, value);
        }
        /// <summary>
        /// 测量时间
        /// </summary>
        public int Measuretime
        {
            get => _measuretime;
            set => SetProperty(ref _measuretime, value);
        }
        /// <summary>
        /// 频率步进
        /// </summary>
        public int Frequencystep
        {
            get => _frequencystep;
            set => SetProperty(ref _frequencystep, value);
        }
        /// <summary>
        /// 频率单位
        /// </summary>
        public FreqUnit FreqUnit
        {
            get => _freqUnit;
            set => SetProperty(ref _freqUnit, value);
        }
    }
}
