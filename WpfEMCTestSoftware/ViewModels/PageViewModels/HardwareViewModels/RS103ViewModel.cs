using WpfEMCTestSoftware.Core;

namespace WpfEMCTestSoftware.ViewModels
{
    public partial class RS103ViewModel : BaseViewModel
    {
        ///// <summary>
        ///// RS103参数模型
        ///// </summary>
        //[ObservableProperty]
        //private RS103Model? _rs103Config = new();

        ///// <summary>
        ///// 当前选择的频段硬件模板
        ///// </summary>
        //[ObservableProperty]
        //private SubBandHardConfigItem _curSubBandHardConfigItem = new();

        ///// <summary>
        ///// 起始频率
        ///// </summary>
        //[ObservableProperty]
        //private double _startFreq = 0.0;
        ///// <summary>
        ///// 终止频率
        ///// </summary>
        //[ObservableProperty]
        //private double _endFreq = 0.0;

        ///// <summary>
        ///// 起始频率单位
        ///// </summary>
        //[ObservableProperty]
        //private int _startFreqUnit = 0;
        ///// <summary>
        ///// 终止频率单位
        ///// </summary>
        //[ObservableProperty]
        //private int _endFreqUnit = 0;
        ///// <summary>
        ///// 频率单位选项组
        ///// </summary>
        //[ObservableProperty]
        //private ObservableCollection<string> _freqUnits = [.. Enum.GetNames(typeof(FreqUnit))];

        //public class Bands
        //{
        //    public string? BandName { get; set; }
        //}

        ///// <summary>
        ///// 频段标题列表
        ///// </summary>
        //[ObservableProperty]
        //private ObservableCollection<Bands> _titleBands = [];

        ///// <summary>
        ///// 当前选择频段序号
        ///// </summary>
        //[ObservableProperty]
        //private int _selBandIndex;

        //private int StartFreqBase;
        //private int EndFreqBase;
        //public RS103ViewModel()
        //{
        //    Messenger.Register<SaveClickEventMessage, int>(this, 1, (r, m) => OnSaveClick(m));
        //    Messenger.Register<TransmitDataEventMessage, int>(this, 0, (r, m) => OnTransmitDataEventMessageReceived(m));
        //}

        ///// <summary>
        ///// 接收保存模板参数按钮点击的消息
        ///// </summary>
        ///// <param name="message">事件消息</param>
        //public void OnSaveClick(SaveClickEventMessage message)
        //{
        //    Messenger.Send(new SaveClickEventMessage(true), 2);
        //}

        ///// <summary>
        ///// 获取到传递数据消息
        ///// </summary>
        ///// <param name="message"></param>
        //private void OnTransmitDataEventMessageReceived(TransmitDataEventMessage message)
        //{
        //    if (message.Value is string config)
        //    {
        //        string Json = GenericHelper.StripFreqRangePrefix(config);
        //        // 4. JSON解析配置（忽略大小写）
        //        var jsonOptions = new JsonSerializerOptions
        //        {
        //            PropertyNameCaseInsensitive = true,
        //            WriteIndented = true
        //        };

        //        // 5. 解析JSON为强类型对象
        //        Rs103Config = JsonSerializer.Deserialize<RS103Model>(Json, jsonOptions);
        //        if (Rs103Config != null)
        //        {
        //            InitFreq();
        //        }
        //    }
        //}

        ///// <summary>
        ///// 初始化频率
        ///// </summary>
        //private void InitFreq()
        //{
        //    if (Rs103Config?.SubBandHardConfig?.Count > 0)
        //    {
        //        for (int i = Rs103Config.SubBandHardConfig.Count - 1; i >= 0; i--)
        //        {
        //            var Config = Rs103Config.SubBandHardConfig[i];
        //            StartFreqBase = Convert.ToInt32(Config.StartFre);
        //            EndFreqBase = Convert.ToInt32(Config.StopFre);
        //            BaseToValue(StartFreqBase, ref _startFreq, ref _startFreqUnit);
        //            BaseToValue(EndFreqBase, ref _endFreq, ref _endFreqUnit);
        //            OnPropertyChanged(nameof(StartFreq));
        //            OnPropertyChanged(nameof(EndFreq));
        //            OnPropertyChanged(nameof(StartFreqUnit));
        //            OnPropertyChanged(nameof(EndFreqUnit));

        //            string band = string.Concat(StartFreq, Enum.GetName(typeof(FreqUnit), StartFreqUnit), "~", EndFreq, Enum.GetName(typeof(FreqUnit), EndFreqUnit));
        //            TitleBands.Insert(0, new Bands { BandName = band });
        //        }
        //        SelBandIndex = 0;
        //        CurSubBandHardConfigItem = Rs103Config.SubBandHardConfig[0];
        //    }
        //}

        ///// <summary>
        ///// 基准转值
        ///// </summary>
        ///// <param name="baseValue"></param>
        ///// <param name="value"></param>
        ///// <param name="unit"></param>
        //private void BaseToValue(int baseValue, ref double value, ref int unit)
        //{
        //    if (GenericMethods.GetDigitCount(baseValue) > 3)
        //    {
        //        value = baseValue / 1000.0;
        //        unit = (int)FreqUnit.GHz;
        //    }
        //    else
        //    {
        //        value = baseValue;
        //        unit = (int)FreqUnit.MHz;
        //    }
        //}

        ///// <summary>
        ///// 保存模板参数
        ///// </summary>
        ///// <param name="obj"></param>
        //[RelayCommand]
        //private void SaveTemplate(object obj)
        //{

        //}

        ///// <summary>
        ///// 频段选中事件
        ///// </summary>
        ///// <param name="e"></param>
        //[RelayCommand]
        //private void OnSelectionChanged(SelectionChangedEventArgs e)
        //{
        //    var Config = Rs103Config.SubBandHardConfig[SelBandIndex];
        //    StartFreqBase = Convert.ToInt32(Config.StartFre);
        //    EndFreqBase = Convert.ToInt32(Config.StopFre);
        //    BaseToValue(StartFreqBase, ref _startFreq, ref _startFreqUnit);
        //    BaseToValue(EndFreqBase, ref _endFreq, ref _endFreqUnit);
        //    OnPropertyChanged(nameof(StartFreq));
        //    OnPropertyChanged(nameof(EndFreq));
        //    OnPropertyChanged(nameof(StartFreqUnit));
        //    OnPropertyChanged(nameof(EndFreqUnit));

        //    CurSubBandHardConfigItem = Config;

        //}

    }
}
