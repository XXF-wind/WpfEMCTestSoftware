using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Enums;
using WpfEMCTestSoftware.Models;
using WpfEMCTestSoftware.Models.HardwareModels;
using static WpfEMCTestSoftware.GenericHelper;

namespace WpfEMCTestSoftware.ViewModels
{
    public partial class EMSHardViewModel : BaseViewModel, IDisposable
    {

        [ObservableProperty]
        private ObservableCollection<string> _signalSourceNames = ["No Device"];   //信号源
        [ObservableProperty]
        private ObservableCollection<string> _amplifierNames = ["No Device"];      //功放
        [ObservableProperty]
        private ObservableCollection<string> _powerMeterames = ["No Device"];      //功率计
        [ObservableProperty]
        private ObservableCollection<string> _switchMatrixNames = ["No Device"];   //开关
        [ObservableProperty]
        private ObservableCollection<string> _fieldStrengthNames = ["No Device"];  //场强探头
        [ObservableProperty]
        private ObservableCollection<string> _switch1 = ["1-1", "1-2", "1-3", "1-4"];
        [ObservableProperty]
        private ObservableCollection<string> _switch2 = ["2-1", "2-2", "2-3", "2-4"];
        [ObservableProperty]
        private ObservableCollection<string> _switch3 = ["3-1", "3-2", "3-3", "3-4"];
        [ObservableProperty]
        private ObservableCollection<string> _switch4 = ["4-1", "4-2", "4-3", "4-4"];
        /// <summary>
        /// 加载分类仪器
        /// </summary>
        private void LoadSortDeviceNames()
        {
            foreach (var device in DeviceCacheStore.SignalSources)
                SignalSourceNames.Add(device.Name);
            foreach (var device in DeviceCacheStore.Amplifiers)
                AmplifierNames.Add(device.Name);
            foreach (var device in DeviceCacheStore.PowerMeters)
                PowerMeterames.Add(device.Name);
            foreach (var device in DeviceCacheStore.FieldStrengthMeters)
                FieldStrengthNames.Add(device.Name);
            foreach (var device in DeviceCacheStore.SwitchMatrixs)
                SwitchMatrixNames.Add(device.Name);
        }
        /// <summary>
        /// EMSModel参数模型
        /// </summary>
        [ObservableProperty]
        private RVC_EMSHardModel _emsModel = new RVC_EMSHardModel();
        /// <summary>
        /// 当前选择的频段硬件模板,用来在界面上绑定数据
        /// </summary>
        //[ObservableProperty]
        //private EMSSubBandHardConfigItem _curSubBandHardConfigItem; 
        /// <summary>
        /// 起始频率
        /// </summary>
        [ObservableProperty]
        private double _startFreq = 0.0;
        /// <summary>
        /// 终止频率    
        /// </summary>
        [ObservableProperty]
        private double _endFreq = 0.0;
        /// <summary>
        /// 起始频率单位
        /// </summary>
        [ObservableProperty]
        private FrequencyUnit _startFreqUnit = 0;
        /// <summary>
        /// 终止频率单位
        /// </summary>
        [ObservableProperty]
        private FrequencyUnit _endFreqUnit = 0;
        /// <summary>
        /// 频率单位选项组
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _freqUnits = [.. Enum.GetNames(typeof(FreqUnit))];

        public partial class Bands : ObservableObject
        {
            [ObservableProperty]
            public string? _bandName;
        }
        /// <summary>
        /// 频段标题列表
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<Bands> _titleBands = [];
        /// <summary>
        /// 当前选择频段序号
        /// </summary>
        [ObservableProperty]
        private int _selBandIndex = -1;

        public EMSHardViewModel()
        {
            //// 初始化时，主动添加第一个频段
            //var firstBand = new EMSSubBandHardConfigItem();
            //EmsModel?.EMSSubBandHardConfig?.Add(firstBand);         
            //CurSubBandHardConfigItem = firstBand;
            //UpdateTitleBands();
            //LoadSortDeviceNames();
            //Messenger.UnregisterAll(this);
            //Messenger.Register<SaveHardConfigMessage, int>(this, 203, (rec, msg) =>
            //{
            //    EmsModel = msg.Value;
            //    UpdateTitleBands();
            //});
            //Messenger.Register<SaveHardConfigMessage, int>(this, 202, (rec, msg) =>
            //{
            //    for (int i = 0; i < EmsModel.EMSSubBandHardConfig.Count; i++)
            //    {
            //        EMSSubBandHardConfigItem? item = EmsModel.EMSSubBandHardConfig[i];
            //        item.LossDesToAMPData = GenericHelper.ReadcsvToDictory(item.LossDesToAMP);
            //        item.LossDesToFWData = GenericHelper.ReadcsvToDictory(item.LossDesToFW);
            //        item.LossDesToRWData = GenericHelper.ReadcsvToDictory(item.LossDesToRW);
            //    }
            //    Messenger.Send(new SaveHardConfigMessage(EmsModel), 201);
            //});
        }
        public void UnregisterMessages()
        {
            Messenger.UnregisterAll(this);

        }

        /// <summary>
        /// 刷新 ListView 显示的频段标题
        /// </summary>
        private void UpdateTitleBands()
        {
            TitleBands.Clear();
            //foreach (var config in EmsModel.EMSSubBandHardConfig)
            //{         
            //    //起始转化
            //    var(startFreq, startUnit) = GenericHelper.ConvertMhzToAppropriateUnit(config.StartFre??0);
            //    var (stopFreq, stopUnit) = GenericHelper.ConvertMhzToAppropriateUnit(config.StopFre ?? 0);
            //    //终止转化
            //    GenericHelper.ConvertMhzToAppropriateUnit(config?.StopFre??0);
            //    // 拼接成：80MHz ~ 1GHz
            //    string bandText = $"{startFreq}{Enum.GetName(typeof(FreqUnit), startUnit)} ~ {stopFreq}{Enum.GetName(typeof(FreqUnit), stopUnit)}";
            //    TitleBands.Add(new Bands { BandName = bandText });
            //}
            SelBandIndex = 0;
            OnSelectionChanged(null);
        }
        /// <summary>
        /// 添加频段事件 只添加
        /// </summary>
        [RelayCommand]
        public void AddFreBand()
        {
            //var newBand = new EMSSubBandHardConfigItem(); 
            //EmsModel?.EMSSubBandHardConfig?.Add(newBand);
            //CurSubBandHardConfigItem = newBand;
            //TitleBands.Add(new Bands { BandName=$"{newBand.StartFre}MHz~{newBand.StopFre}MHz" });
        }

        /// <summary>
        /// 删除频段事件
        /// </summary>
        [RelayCommand]
        public void DeleteFreBand()
        {
            //if (CurSubBandHardConfigItem == null)
            //    return;

            //// 删除数据
            //EmsModel?.EMSSubBandHardConfig?.Remove(CurSubBandHardConfigItem);

            //// 删除标题
            //if (SelBandIndex >= 0 && SelBandIndex < TitleBands.Count)
            //    TitleBands.RemoveAt(SelBandIndex);

            //// 删完重新赋值
            //if (EmsModel.EMSSubBandHardConfig.Any())
            //{
            //    // 还有 → 选第一个
            //    CurSubBandHardConfigItem = EmsModel.EMSSubBandHardConfig.First();
            //    SelBandIndex = 0;
            //}
            //else
            //{
            //    // 没了 → 置空
            //    CurSubBandHardConfigItem = null;
            //    SelBandIndex = -1;
            //}
        }
        //保存频段
        [RelayCommand]
        public void SaveFreBand()
        {
            //string bandText = $"{StartFreq}{Enum.GetName(typeof(FreqUnit), StartFreqUnit)} ~ {EndFreq}{Enum.GetName(typeof(FreqUnit), EndFreqUnit)}";
            //// 安全判断：索引有效才更新
            //if (SelBandIndex < 0 || SelBandIndex >= TitleBands.Count)
            //    return;
            //TitleBands[SelBandIndex].BandName = bandText;   
            //var bandConfig = EmsModel?.EMSSubBandHardConfig[SelBandIndex];
            //if (bandConfig == null)
            //    return;
            //bandConfig.StartFre = GenericHelper.ConvertToMhzData(StartFreq, StartFreqUnit);
            //bandConfig.StopFre = GenericHelper.ConvertToMhzData(EndFreq, EndFreqUnit);        
        }

        [RelayCommand]
        public void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            //if (SelBandIndex < 0) return;
            //var Config = EmsModel?.EMSSubBandHardConfig?[SelBandIndex];
            //if (Config == null) return;
            //var (StartFreq, StartFreqUnit) = GenericHelper.ConvertMhzToAppropriateUnit(Config.StartFre ?? 0);
            //var(EndFreq, EndFreqUnit) = GenericHelper.ConvertMhzToAppropriateUnit(Config.StopFre ?? 0);
            //this.StartFreq = StartFreq;
            //this.StartFreqUnit = StartFreqUnit;
            //this.EndFreq = EndFreq;
            //this.EndFreqUnit = EndFreqUnit;
            //OnPropertyChanged(nameof(StartFreq));
            //OnPropertyChanged(nameof(EndFreq));
            //OnPropertyChanged(nameof(StartFreqUnit));
            //OnPropertyChanged(nameof(EndFreqUnit));
            //CurSubBandHardConfigItem = Config;
        }


        [RelayCommand]
        public void FileInput(Object obj)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                Title = "请选择要导入的csv文件",
                InitialDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "System", "参数管理", "插损"),
                Filter = "csv文件|*.csv",
            };
            if (openFileDialog.ShowDialog() != true)
                return;
            string fileName = openFileDialog.FileName;
            //if (obj.ToString() == "A")     
            //{
            //    CurSubBandHardConfigItem.LossDesToAMP = fileName;
            //}
            //else if (obj.ToString() == "F")  
            //{
            //    CurSubBandHardConfigItem.LossDesToFW = fileName;
            //}
            //else if (obj.ToString() == "R")  
            //{
            //    CurSubBandHardConfigItem.LossDesToRW = fileName;
            //}


        }
        public void Dispose()
        {

        }
    }
}
