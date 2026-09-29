using ClassLibrary;
using ClassLibrary.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Models;
using WpfEMCTestSoftware.Models.EventMessages;

namespace WpfEMCTestSoftware.ViewModels
{
    public partial class SwitchMatrixParaViewModel : BaseViewModel
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
        /// 开关矩阵参数信息
        /// </summary>
        [ObservableProperty]
        private SwitchMatrixPara _switchMatrixPara = new()
        {
            SwitchState = [0, 0, 0, 0]
        };

        public SwitchMatrixParaViewModel()
        {
            IsActive = true;
            //保存回传：设备参数页 -> 本设备子页（把界面值回写到参数对象）
            Messenger.Register<SaveClickEventMessage, int>(this, (int)DeviceSaveMessageToken.DeviceParaToSubPage, (r, m) => OnSaveClick(m));
            //设备信息下发：设备参数页 -> 本设备子页（开关矩阵参数页）
            Messenger.Register<SetDeviceEventMessage, int>(this, (int)DeviceParaMessageToken.DeviceParaToSubPage, (r, m) => RecDeviceInfo(m));
        }

        /// <summary>
        /// 接收设置设备事件消息
        /// </summary>
        /// <param name="message"></param>
        public void RecDeviceInfo(SetDeviceEventMessage message)
        {
            //注意：这里必须接住设备列表传下来的"真实对象"（和数据库中那条记录是同一个引用），
            //      页面开关的 TwoWay 绑定才会写进这条记录，再由设备参数页统一保存入库。
            if (message.Value is SwitchMatrixPara para)
            {
                //页面的 4 个开关分别绑定 SwitchState[0]~[3]，历史数据为空时补足 4 位，
                //否则绑定取不到下标，点开关也写不回数据库
                para.SwitchState ??= [];
                while (para.SwitchState.Count < 4)
                {
                    para.SwitchState.Add(0);
                }
                SwitchMatrixPara = para;
            }
        }

        /// <summary>
        /// 接收保存设备参数按钮点击的消息
        /// </summary>
        /// <param name="message">事件消息</param>
        public void OnSaveClick(SaveClickEventMessage message)
        {
            //参数回写完毕：本设备子页 -> 设备参数页（由设备参数页统一落库）
            Messenger.Send(new SaveClickEventMessage(true), (int)DeviceSaveMessageToken.SubPageToDevicePara);
        }

        /// <summary>
        /// 切换开关
        /// </summary>
        [RelayCommand]
        private void SwitchToggle(object obj)
        {

        }
    }
}
