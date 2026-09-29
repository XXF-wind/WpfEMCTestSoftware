using Peak.Can.Basic.BackwardCompatibility;

namespace EMCDeviceLibrary
{
    /// <summary>
    /// 设备参数类
    /// </summary>
    public class DevicePara
    {
        public DevicePara()
        {

        }

        public DevicePara(CommunicationMode _mode, string _ip, int _port, string _ComNo, string _Baud, string _address, ushort _channel, TPCANBaudrate _baudRate)
        {
            Mode = _mode;
            IP = _ip;
            Port = _port;
            ComPort = _ComNo;
            ComBaud = _Baud;
            Address = _address;
            CANChannel = _channel;
            CANBaudRate = _baudRate;
        }

        /// <summary>
        /// 通讯模式
        /// </summary>
        public CommunicationMode Mode { get; set; } = 0;

        /// <summary>
        /// IP地址
        /// </summary>
        public string? IP { get; set; }

        /// <summary>
        /// 端口号
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// 串口号
        /// </summary>
        public string? ComPort { get; set; }

        /// <summary>
        /// 串口波特率
        /// </summary>
        public string? ComBaud { get; set; }

        /// <summary>
        /// 根据实际使用的CAN接口选择通道
        /// </summary>
        public ushort CANChannel { get; set; } = 0;

        /// <summary>
        /// 连接地址
        /// </summary>
        public string Address { get; set; } = "";

        /// <summary>
        /// CAN接口波特率
        /// </summary>
        public TPCANBaudrate CANBaudRate { get; set; } = TPCANBaudrate.PCAN_BAUD_500K;
    }
}
