using Company.UI.Controls.UseControls;
using EMCDeviceLibrary;
using System.Collections.ObjectModel;
using WpfEMCTestSoftware.Enums;
using WpfEMCTestSoftware.Models.TestModels;

namespace WpfEMCTestSoftware.RunTasks
{
    public class TempHistoryInfo
    {
        public int HistoryStandard { get; set; }
        public string HistoryTestName { get; set; }
    }
    /// <summary>
    /// D0160测试时所需的设备
    /// </summary>
    public class DO160DeviceChannel
    {
        // 必选设备
        public SockConnect? SignalSock { get; set; }         // 信号源
        public SockConnect? FieldStrengthSock { get; set; }  // 接收机或者频谱仪或者示波器，读取电流的设备就行
        public SockConnect? ForwardPowerSock { get; set; }   // 正向功率计

        // 可选设备
        public SockConnect? ReservePowerSock { get; set; }  // 反向功率计
        public SockConnect? AmplifierSock { get; set; }     // 功放
        public SockConnect? SwitchSock { get; set; }         // 开关
    }

    public class DO160RuntimeParam
    {
        private EMCStandard _standard;
        public EMCStandard Standard
        {
            get => _standard;
            set => _standard = value;
        }
        private ReverbTestMode _curReverbTestMode;
        public ReverbTestMode CurReverbTestMode
        {
            get => _curReverbTestMode;
            set => _curReverbTestMode = value;
        }

        private TestStageType _curTestStageType;
        public TestStageType CurTestStageType
        {
            get => _curTestStageType;
            set => _curTestStageType = value;
        }

        private string _preCalFileName = string.Empty;
        public string PreCalFileName
        {
            get => _preCalFileName;
            set => _preCalFileName = value;
        }
        public List<EmsImmunityLimit> ImmunityLimit { get; set; } = [];

    }
    public partial class DO160RunTask : BaseRunTask
    {
        //==================== 曲线通道（基类不配默认曲线，全部由子类自己注册）====================//
        // 对应 ModuleProfileRegistry 的 ReverbChamber 预设：Chart.Signal / Chart.Field / Chart.Power
        /// <summary>Chart.Signal 通道：信号源输出曲线</summary>
        private readonly ObservableCollection<ChartLineModel> _signalCurves = [];
        /// <summary>Chart.Field 通道：场强幅值 + 限值两条曲线</summary>
        private readonly ObservableCollection<ChartLineModel> _fieldCurves = [];
        /// <summary>Chart.Power 通道：前向功率 + 反向功率两条曲线</summary>
        private readonly ObservableCollection<ChartLineModel> _powerCurves = [];

        // 设备通道
        public DO160DeviceChannel DeviceChannel { get; }
        // 运行参数
        public DO160RuntimeParam RuntimeParam { get; }
        public DO160RunTask()
        {
            DeviceChannel = new DO160DeviceChannel();
            RuntimeParam = new DO160RuntimeParam();
            //在自己的构造函数体里注册通道：此时本类字段初始化器已执行完，集合可用；
            //注册一定早于运行页的 ModuleHost.BuildCharts → Bind（绑定时机见 RunConfigViewModel.CreateTaskByConfig）
            RegisterDO160ChartChannels();
        }
        /// <summary>
        /// 注册混响室要显示的曲线通道（与 ReverbChamber 配置一一对应）：
        /// 信号源(dBm) / 场强+限值(v/m) / 前向+反向功率(W)
        /// </summary>
        private void RegisterDO160ChartChannels()
        {
            _signalCurves.Add(new ChartLineModel
            {
                Title = "幅值",
                LineColor = ScottPlot.Colors.Blue,
                XData = [],
                YData = [],
            });
            _powerCurves.Add(new ChartLineModel
            {
                Title = "前向功率",
                LineColor = ScottPlot.Colors.Green,
                XData = [],
                YData = [],
            });
            _powerCurves.Add(new ChartLineModel
            {
                Title = "反向功率",
                LineColor = ScottPlot.Colors.Yellow,
                XData = [],
                YData = [],
            });
            _fieldCurves.Add(new ChartLineModel
            {
                Title = "幅值",
                LineColor = ScottPlot.Colors.Purple,
                XData = [],
                YData = [],
            });
            _fieldCurves.Add(new ChartLineModel
            {
                Title = "限值",
                LineColor = ScottPlot.Colors.Red,
                XData = [],
                YData = [],
            });

            RegisterChannel("Chart.Signal", _signalCurves);   // Y轴：输出/dBm
            RegisterChannel("Chart.Field", _fieldCurves);     // Y轴：极限电平监测/v/m
            RegisterChannel("Chart.Power", _powerCurves);     // Y轴：输出/W
        }
        /// <summary>
        /// 校验DO160标准测试所需仪器是否在线有效，绑定全局设备通道，注册数据接收回调事件
        /// </summary>
        private bool CheckDO160InstrumentsValid()
        {
            // 必选仪器通道赋值
            DeviceChannel.SignalSock = GetSockByType(DeviceType.信号源);
            DeviceChannel.FieldStrengthSock = GetSockByType(DeviceType.场强仪);
            DeviceChannel.ForwardPowerSock = GetAllSocksByType(DeviceType.功率计).FirstOrDefault();
            //必选设备有效性监测
            if (!CheckMustChoseDevice(DeviceChannel.SignalSock, "信号源") ||
             !CheckMustChoseDevice(DeviceChannel.FieldStrengthSock, "场强仪") ||
             !CheckMustChoseDevice(DeviceChannel.ForwardPowerSock, "前向功率计"))
                return false;
            // 可选设备通道赋值
            DeviceChannel.AmplifierSock = GetSockByType(DeviceType.功放);
            DeviceChannel.SwitchSock = GetSockByType(DeviceType.开关矩阵);
            var PowerMeters = GetAllSocksByType(DeviceType.功率计);
            DeviceChannel.ReservePowerSock = PowerMeters.Count >= 2 ? PowerMeters[1] : null;

            DeviceChannel.FieldStrengthSock.RecInfoPush += FieldStrengthSock_RecInfoPush;
            DeviceChannel.ForwardPowerSock.RecInfoPush += ForwardPowerSock_RecInfoPush;
            if (DeviceChannel.ReservePowerSock != null)
                DeviceChannel.ReservePowerSock.RecInfoPush += ReservePowerSock_RecInfoPush;
            return true;

        }
        private void ReservePowerSock_RecInfoPush(string obj) => EnqueueData(obj);
        private void ForwardPowerSock_RecInfoPush(string obj) => EnqueueData(obj);
        private void FieldStrengthSock_RecInfoPush(string obj) => EnqueueData(obj);
        /// <summary>
        /// 校验必选设备
        /// </summary>
        /// <param name="sock">设备通信端</param>
        /// <param name="name">设备名称</param>
        /// <returns></returns>
        private bool CheckMustChoseDevice(SockConnect? sock, string name)
        {
            if (sock == null)
            {
                sendMessagelog($"{name}通信sock为null");
                return false;
            }
            return true;
        }
        /// <summary>
        /// 接收数据入队（共用逻辑）
        /// </summary>
        private void EnqueueData(string obj)
        {
            if (ReceiveDataQueue.Count > 0) return;
            if (double.TryParse(obj, out double value))
                ReceiveDataQueue.Enqueue(value);
        }
        //更新内外循环队列数据
        protected override void RefreshInOrOutLoopQueue()
        {

        }
        protected override void RefreshLimitLinesByFreqRange()
        {
            //取本频段的"限值"曲线行（限值线随频段范围刷新时从这里拿引用）
            var Limitline = _fieldCurves.FirstOrDefault(lineModel => lineModel.Title == "限值");
        }
        protected override void ReleaseDevice()
        {
            try
            {
                // 1. 先取消事件订阅
                if (DeviceChannel.FieldStrengthSock != null)
                    DeviceChannel.FieldStrengthSock.RecInfoPush -= FieldStrengthSock_RecInfoPush;

                if (DeviceChannel.ForwardPowerSock != null)
                    DeviceChannel.ForwardPowerSock.RecInfoPush -= ForwardPowerSock_RecInfoPush;

                if (DeviceChannel.ReservePowerSock != null)
                    DeviceChannel.ReservePowerSock.RecInfoPush -= ReservePowerSock_RecInfoPush;

                // 2. 先关闭信号源输出
                if (DeviceChannel.SignalSock != null)
                    DeviceChannel.SignalSock.SendData((int)Communication.OUTPUTSTATE, "OFF");
                // 3. 再关闭功放
                SetAmplifierOut(false, DeviceChannel.AmplifierSock);

                // 4. 清空队列（释放内存）
                ReceiveDataQueue.Clear();
            }
            catch (Exception ex)
            {
                sendMessagelog($"释放设备异常：{ex.Message}");
            }
        }
        //分配频段子任务
        protected override bool AsiggnBandSubTask()
        {
            try
            {
                //1.必选设备校验,绑定通信事件
                if (!CheckDO160InstrumentsValid())
                {
                    sendMessagelog("设备校验失败！");
                    return false;
                }
                //2.打开功放输出
                SetAmplifierOut(true, DeviceChannel.AmplifierSock);
                //3.设置开关通道
                //4.设置信号源参数

                sendMessagelog("模块初始化成功！");
                return true;
            }
            catch (Exception ex)
            {
                sendMessagelog($"模块初始化异常：{ex.Message}");
                return false;
            }
        }



    }
}
