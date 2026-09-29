using CommunityToolkit.Mvvm.ComponentModel;
using EMCDeviceLibrary;
using System.Windows;
using WpfEMCTestSoftware.Enums;

namespace WpfEMCTestSoftware.Models
{
    // 这是一个设备信息类，用于表示和管理设备的数据
    // 继承自 ObservableObject（来自 CommunityToolkit.Mvvm），实现了 INotifyPropertyChanged 接口
    // 当属性值改变时，会自动通知UI进行更新（这是WPF MVVM模式的核心）
    public class DeviceInfo : ObservableObject
    {
        // -----------------------------
        // 私有字段（Private Fields）
        // 对应C++中的私有成员变量，用于存储数据
        // -----------------------------
        private int _id;
        private string _name = string.Empty;
        private DeviceType _type;
        private DeviceMode _model;
        private CommunicationMode _communicationmode;
        private string _address = string.Empty;
        private bool _isActive; // 是否激活
        private int _time; // 通讯时间
        private DevicePara _devicePara = new();

        // -----------------------------
        // 公共属性（Public Properties）
        // C#的属性相当于C++的getter/setter方法，但语法更简洁
        // -----------------------------
        public DevicePara DevicePara
        {
            get => _devicePara;
            set => SetProperty(ref _devicePara, value);
        }
        //        这是元数据，附加到属性上

        //运行时可以通过反射读取这些信息

        //类似于C++的注解或装饰器模式
        [DataGridColumnSetting("序号", Width = "80", HorizontalAlignment = HorizontalAlignment.Center)]
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value); // 设置_id的值，并触发通知
        }

        [DataGridColumnSetting("名称")]
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        [DataGridColumnSetting("设备")]
        public DeviceType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        [DataGridColumnSetting("型号")]
        public DeviceMode Model
        {
            get => _model;
            set => SetProperty(ref _model, value);
        }

        [DataGridColumnSetting("通讯方式")]
        public CommunicationMode Communicationmode
        {
            get => _communicationmode;
            set => SetProperty(ref _communicationmode, value);
        }

        [DataGridColumnSetting("通讯地址")] // 初始隐藏
        public string Address
        {
            get => _address;
            set => SetProperty(ref _address, value);
        }
        [DataGridColumnSetting("状态", Width = "80", HorizontalAlignment = HorizontalAlignment.Center)]
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }



        [DataGridColumnSetting("通讯时间", IsVisible = true)]
        public int Time
        {
            get => _time;
            set => SetProperty(ref _time, value);
        }


    }
}

