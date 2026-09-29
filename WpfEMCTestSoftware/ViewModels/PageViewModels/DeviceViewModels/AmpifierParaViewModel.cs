using ClassLibrary;
using ClassLibrary.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Models;
using WpfEMCTestSoftware.Models.EventMessages;

namespace WpfEMCTestSoftware.ViewModels
{
    // 功放参数视图模型
    public partial class AmpifierParaViewModel : BaseViewModel
    {
        #region 变量/属性定义
        public ObservableCollection<string> PowerUnits { get; set; } = ["W", "dBm"];

        /// <summary>
        /// 使用语言
        /// </summary>
        public SwitchLanguage Language
        {
            get => GenericParameter.Language;
            set
            {
                GenericParameter.Language = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 功放参数
        /// </summary>
        [ObservableProperty]
        private AmpifierPara _ampifierPara = new();

        /// <summary>
        /// 正向耦合值
        /// </summary>
        [ObservableProperty]
        private double _fCouplvalue;

        /// <summary>
        /// 正向耦合文件路径
        /// </summary>
        [ObservableProperty]
        private string _fCouplFile = string.Empty;

        /// <summary>
        /// 反向耦合值
        /// </summary>
        [ObservableProperty]
        private double _rCouplvalue;

        /// <summary>
        /// 反向耦合文件路径
        /// </summary>
        [ObservableProperty]
        private string _rCouplFile = string.Empty;

        /// <summary>
        /// 最大输出值文件路径
        /// </summary>
        [ObservableProperty]
        private string _maxInputValueFile = string.Empty;

        /// <summary>
        /// 功率单位
        /// </summary>
        [ObservableProperty]
        private string _powerUnit = "W";

        /// <summary>
        /// 驻波保护起始功率
        /// </summary>
        [ObservableProperty]
        private double _swpStartPower = 0;

        [ObservableProperty]
        private bool _isFixdF;      //正向 :固定值选中
        [ObservableProperty]
        private bool _isFileF;      //正向：文件选中
        [ObservableProperty]
        private bool _isFixdR;      //反向：固定值选中
        [ObservableProperty]
        private bool _isFileR;      //反向：文件选中


        #endregion

        public AmpifierParaViewModel()
        {
            IsActive = true;
            //保存回传：设备参数页 -> 本设备子页（把界面值回写到参数对象）
            Messenger.Register<SaveClickEventMessage, int>(this, (int)DeviceSaveMessageToken.DeviceParaToSubPage, (r, m) => OnSaveClick(m));
            //设备信息下发：设备参数页 -> 本设备子页（功放参数页）
            Messenger.Register<SetDeviceEventMessage, int>(this, (int)DeviceParaMessageToken.DeviceParaToSubPage, (r, m) => RecDeviceInfo(m));
        }
        /// <summary>
        /// 接收设置设备事件消息
        /// </summary>
        /// <param name="message"></param>
        public void RecDeviceInfo(SetDeviceEventMessage message)
        {
            // 初始化功放参数.
            Init(message.Value);
        }

        /// <summary>
        /// 接收保存设备参数按钮点击的消息
        /// </summary>
        /// <param name="message">事件消息</param>
        public void OnSaveClick(SaveClickEventMessage message)
        {
            if (IsFixdF)
            {
                AmpifierPara.FCoupling = FCouplvalue.ToString();
            }
            else if (IsFileF)
            {
                AmpifierPara.FCoupling = FCouplFile;
                AmpifierPara.FCouplingData = GenericHelper.ReadcsvToDictory(AmpifierPara.FCoupling);
            }

            if (IsFixdR)
            {
                AmpifierPara.RCoupling = RCouplvalue.ToString();
            }
            else if (IsFileR)
            {
                AmpifierPara.RCoupling = RCouplFile;
                AmpifierPara.RCouplingData = GenericHelper.ReadcsvToDictory(AmpifierPara.RCoupling);
            }
            AmpifierPara.MaxInValue = MaxInputValueFile;
            AmpifierPara.MaxinValueData = GenericHelper.ReadcsvToDictory(AmpifierPara.MaxInValue);
            //参数回写完毕：本设备子页 -> 设备参数页（由设备参数页统一落库）
            Messenger.Send(new SaveClickEventMessage(true), (int)DeviceSaveMessageToken.SubPageToDevicePara);
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="device"></param>
        private void Init(DeviceInfo device)
        {
            if (device is AmpifierPara para)
            {
                AmpifierPara = para;
                MaxInputValueFile = AmpifierPara.MaxInValue;
                if (double.TryParse(AmpifierPara.FCoupling, out double _fvalue))
                {
                    FCouplvalue = _fvalue;
                    FCouplFile = string.Empty;
                    IsFixdF = true;
                    IsFileF = false;
                }
                else
                {
                    FCouplvalue = 0;
                    FCouplFile = AmpifierPara.FCoupling;
                    IsFixdF = false;
                    IsFileF = true;
                }
                if (double.TryParse(AmpifierPara.RCoupling, out double _rvalue))
                {
                    RCouplvalue = _rvalue;
                    RCouplFile = string.Empty;
                    IsFixdR = true;
                    IsFileR = false;
                }
                else
                {
                    RCouplvalue = 0;
                    RCouplFile = AmpifierPara.RCoupling;
                    IsFixdR = false;
                    IsFileR = true;
                }
            }

        }



        /// <summary>
        /// 文件导入
        /// </summary>
        /// <param name="obj"></param>
        [RelayCommand]
        private void FileInput(object obj)
        {
            OpenFileDialog openFile = new OpenFileDialog()
            {
                Title = "请选择要导入的文件",
                Filter = "csv文件|*.csv",
            };
            if (obj.ToString() == "F" || obj.ToString() == "R")
                openFile.InitialDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "System", "参数管理", "耦合系数");
            else if (obj.ToString() == "M")
                openFile.InitialDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "System", "参数管理", "功放最大输入");
            else
                return;
            if (openFile.ShowDialog() != true)
            {
                return;
            }
            string fileName = openFile.FileName;
            if (obj.ToString() == "F")      //正向耦合文件
            {
                FCouplFile = fileName;
            }
            else if (obj.ToString() == "R")  //反向耦合文件
            {
                RCouplFile = fileName;
            }
            else if (obj.ToString() == "M")  //最大输入值文件
            {
                MaxInputValueFile = fileName;
            }
        }

    }
}
