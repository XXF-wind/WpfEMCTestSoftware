using ClassLibrary;
using ClassLibrary.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Models;
using WpfEMCTestSoftware.Models.EventMessages;

namespace WpfEMCTestSoftware.ViewModels
{
    public partial class FieldStrengthMeterParaViewModel : BaseViewModel
    {
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
        /// 场强仪参数信息
        /// </summary>
        [ObservableProperty]
        private FieldStrengthMeterPara _mfieldStrengthMeterPara = new();

        public FieldStrengthMeterParaViewModel()
        {
            IsActive = true;
            //保存回传：设备参数页 -> 本设备子页（把界面值回写到参数对象）
            Messenger.Register<SaveClickEventMessage, int>(this, (int)DeviceSaveMessageToken.DeviceParaToSubPage, (r, m) => OnSaveClick(m));
            //设备信息下发：设备参数页 -> 本设备子页（场强仪参数页）
            Messenger.Register<SetDeviceEventMessage, int>(this, (int)DeviceParaMessageToken.DeviceParaToSubPage, (r, m) => RecDeviceInfo(m));
        }

        /// <summary>
        /// 接收设置设备事件消息
        /// </summary>
        /// <param name="message"></param>
        public void RecDeviceInfo(SetDeviceEventMessage message)
        {
            if (message.Value is FieldStrengthMeterPara para)
            {
                MfieldStrengthMeterPara = para;
            }

        }

        /// <summary>
        /// 接收保存设备参数按钮点击的消息
        /// </summary>
        /// <param name="message">事件消息</param>
        public void OnSaveClick(SaveClickEventMessage message)
        {
            MfieldStrengthMeterPara.calibrationfactorData = GenericHelper.ReadcsvToDictory(MfieldStrengthMeterPara.CalibrationFactor);
            //参数回写完毕：本设备子页 -> 设备参数页（由设备参数页统一落库）
            Messenger.Send(new SaveClickEventMessage(true), (int)DeviceSaveMessageToken.SubPageToDevicePara);
        }

        /// <summary>
        /// 文件导入
        /// </summary>
        /// <param name="obj"></param>
        [RelayCommand]
        private void FileInput(object obj)
        {
            OpenFileDialog openFile = new()
            {
                Title = "请选择要导入的文件",
                Filter = "CSV文件|*.csv",
                InitialDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "System", "参数管理", "探头校准因子")
            };
            if (openFile.ShowDialog() != true)
            {
                return;
            }
            MfieldStrengthMeterPara.CalibrationFactor = openFile.FileName;
        }
    }
}
