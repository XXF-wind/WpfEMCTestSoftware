using ClassLibrary.Generic;
using Equipment;
using Peak.Can.Basic.BackwardCompatibility;
using System.IO.Ports;
using System.Text;
using System.Timers;

namespace EMCDeviceLibrary
{
    /// <summary>
    /// 设备通讯统一管理类（总入口）
    /// 功能：集成 网口、串口、CAN、VISA 四种通讯方式，对外提供统一收发接口
    /// 继承：SerialPortCommon（串口通用基类）
    /// </summary>
    public class SockConnect : SerialPortCommon
    {
        static SockConnect()
        {
            // net8 默认不包含 GB2312/GBK 代码页，注册 CodePages 提供器后才能按 GBK 解码设备的中文响应
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// 智能解码：先按 UTF-8 解码；若出现乱码占位符（U+FFFD，说明原始字节不是 UTF-8，
        /// 例如设备用 GBK 编码返回的中文错误消息），改用 GB2312（GBK）重新解码
        /// </summary>
        private static string DecodeSmart(byte[] msg)
        {
            string text = Encoding.Default.GetString(msg);
            if (text.Contains('\uFFFD'))
            {
                try { text = Encoding.GetEncoding("GB2312").GetString(msg); }
                catch { /* 保持 UTF-8 结果 */ }
            }
            return text;
        }
        /// <summary>
        /// 通讯类型枚举：LAN/Serial/CAN/VISA
        /// </summary>
        public CommunicationMode CommunicationType;

        /// <summary>
        /// CAN 通讯实例
        /// </summary>
        protected PCanBasicConnect? PCanBasic = null;

        /// <summary>
        /// VISA 仪器通讯实例
        /// </summary>
        protected VisaCommunicator? visa = null;

        /// <summary>
        /// TCP 客户端通讯实例
        /// </summary>
        protected ClientSocket? socket = null;

        protected Communication ReadType = 0;
        protected string RecData = "";
        protected string finish = "\n";
        /// <summary>
        /// 定时轮询读取计时器（周期读取设备参数）
        /// </summary>
        private readonly System.Timers.Timer tmrRead = new(500);

        /// <summary>
        /// 单次读取计时器（只执行一次读取）
        /// </summary>
        private readonly System.Timers.Timer tmrSingleRead = new(100);

        /// <summary>
        /// 单次读取的命令列表
        /// </summary>
        private List<string> SingleReadPara = [];

        /// <summary>
        /// 周期读取的命令列表
        /// </summary>
        private List<string> ReadPara = [];

        /// <summary>
        /// 字符串类型数据接收事件（推送给上层UI）
        /// </summary>
        public event Action<string>? RecInfoPush;

        /// <summary>
        /// 字节数组类型数据接收事件
        /// </summary>
        public event Action<byte[]>? RecInfosPush;

        /// <summary>
        /// CAN 消息接收事件
        /// </summary>
        public event Action<TPCANMsg>? RecPCANPush;

        /// <summary>
        /// 发送锁：防止重复发送
        /// </summary>
        private bool IsSend = false;

        /// <summary>
        /// 设备参数结构体（IP、端口、串口、CAN通道等）
        /// </summary>
        internal DevicePara device;

        /// <summary>
        /// 设备名称
        /// </summary>
        private string _name = "";
        public string Name
        {
            set { _name = value; }
            get { return _name; }
        }

        /// <summary>
        /// 读取完成标志
        /// </summary>
        private bool _IsFinish = false;
        public new bool IsFinish
        {
            set { _IsFinish = value; }
            get { return _IsFinish; }
        }

        /// <summary>
        /// 发送成功标志
        /// </summary>
        private bool _sendSuccess = false;
        public bool sendSuccess
        {
            set { _sendSuccess = value; }
            get { return _sendSuccess; }
        }

        /// <summary>
        /// 初始化成功标志
        /// </summary>
        private bool _Initialed;
        public bool Initialed
        {
            set { _Initialed = value; }
            get { return _Initialed; }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public SockConnect()
        {

        }

        /// <summary>
        /// 设置读取参数（周期读取 + 单次读取）
        /// </summary>
        /// <param name="reads">周期读取命令列表</param>
        /// <param name="singleReads">单次读取命令列表</param>
        public void SetReadPara(List<string> reads, List<string> singleReads)
        {
            ReadPara = reads;
            SingleReadPara = singleReads;

            // 单次计时器执行一次
            tmrSingleRead.AutoReset = false;
            tmrSingleRead.Elapsed += TmrSingleRead_Elapsed;

            // 周期计时器执行一次后自动重启
            tmrRead.AutoReset = false;
            tmrRead.Elapsed += TmrRead_Elapsed;
        }

        /// <summary>
        /// 启动周期读取计时器
        /// </summary>
        public void StartReadTimer()
        {
            tmrRead.Start();
        }

        /// <summary>
        /// 周期读取计时器回调：循环发送读取指令
        /// </summary>
        private void TmrRead_Elapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                // 未在发送中，才执行发送
                if (!IsSend)
                {
                    foreach (string para in ReadPara)
                    {
                        SendData(para);
                        ClassLibrary.ClsDelay.Delay.MicrosecondDelay(5000);
                    }
                }
            }
            finally
            {
                // 无论是否异常，都重启计时器，保证持续轮询
                tmrRead.Start();
            }
        }

        /// <summary>
        /// 单次读取计时器回调：只执行一次
        /// </summary>
        private void TmrSingleRead_Elapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                foreach (string para in SingleReadPara)
                {
                    SendData(para);
                    ClassLibrary.ClsDelay.Delay.MicrosecondDelay(5000);
                }
            }
            catch
            { }
        }

        #region 连接管理

        /// <summary>
        /// 获取当前设备连接状态
        /// </summary>
        /// <returns>true=已连接，false=未连接</returns>
        public bool Status()
        {
            int result = 0;
            switch (CommunicationType)
            {
                case CommunicationMode.LAN:
                    result = socket == null ? 0 : socket.State();
                    break;
                case CommunicationMode.Serial:
                    result = com.IsOpen ? 1 : 0;
                    break;
                case CommunicationMode.CAN:
                    result = PCanBasic?.m_IsConnected == true ? 1 : 0;
                    break;
                case CommunicationMode.VISA:
                    result = visa?.IsConnected == true ? 1 : 0;
                    break;
            }
            return result == 1;
        }

        /// <summary>
        /// 根据设备参数初始化连接（自动判断通讯类型）
        /// </summary>
        /// <param name="para">设备参数</param>
        public virtual void InitConnection(object para)
        {
            if (para is DevicePara device)
            {
                this.device = device;
                CommunicationType = device.Mode;

                // 根据通讯类型分别连接
                switch (device.Mode)
                {
                    case CommunicationMode.LAN:
                        Connect(device.IP, device.Port);
                        break;
                    case CommunicationMode.Serial:
                        Connect(device.ComPort.ToString(), device.ComBaud);
                        break;
                    case CommunicationMode.CAN:
                        Connect(device.CANChannel, device.CANBaudRate);
                        break;
                    case CommunicationMode.VISA:
                        Connect(device.Address);
                        break;
                }
            }
        }

        /// <summary>
        /// 连接 VISA 仪器
        /// </summary>
        private void Connect(string Address)
        {
            visa ??= new VisaCommunicator();
            if (visa.IsConnected)
                visa.Disconnect();

            visa.Connect(Address);
            Initialed = visa.IsConnected;
        }

        /// <summary>
        /// 连接串口设备
        /// </summary>
        private void Connect(string ComPort, string ComBaud)
        {
            com ??= new SerialPort();
            if (com.IsOpen)
                com.Close();

            // 调用基类初始化串口参数
            string s = InitConnection(ComPort, ComBaud, "8", "1", "None", "");

            if (!com.IsOpen)
                com.Open();

            // 串口读写超时统一 3 秒：避免设备无响应时 Read/Write 默认无限阻塞
            com.ReadTimeout = 3000;
            com.WriteTimeout = 3000;

            if (s == "True")
            {
                com.DataReceived += Com_DataReceived;
                Initialed = true;
            }
            else
            {
                Initialed = false;
            }
        }

        /// <summary>
        /// 连接 CAN 设备
        /// </summary>
        private void Connect(UInt16 _pcanHandle = 81, TPCANBaudrate _baudRate = TPCANBaudrate.PCAN_BAUD_500K)
        {
            PCanBasic ??= new PCanBasicConnect();
            PCanBasic?.Connect(_pcanHandle, _baudRate);

            if (PCanBasic?.m_IsConnected == true)
            {
                PCanBasic.ReceiveMsg += PCanBasic_ReceiveMsg;
                Initialed = true;
            }
            else
            {
                Initialed = false;
            }
        }

        /// <summary>
        /// 连接 TCP 客户端
        /// </summary>
        private void Connect(string? ip, int port)
        {
            try
            {
                socket ??= new ClientSocket();
                socket.Connect(ip, port);

                if (!Status())
                {
                    ShowLog(ip + GenericParameter.Language.LANConnectFailedMsg, 0);
                    Initialed = false;
                    return;
                }
                else
                {
                    ShowLog(ip + GenericParameter.Language.LANConnectSuccessMsg, 1);
                }

                // 绑定接收事件
                socket.ReceiveMsg += Sock_ReceiveMsg;
                Initialed = true;
            }
            catch (Exception e)
            {
                ShowLog($"{GenericParameter.Language.LANConnectError}:{ip},{port}{e.Message}", 0);
                Initialed = false;
            }
        }

        /// <summary>
        /// TCP服务端监听（默认未实现，由子类实现）
        /// </summary>
        public virtual bool Linsten(int port)
        {
            return false;
        }

        /// <summary>
        /// 服务端接受新连接，分配Socket
        /// </summary>
        protected void Connect(System.Net.Sockets.Socket sock)
        {
            if (socket != null)
            {
                socket.ReceiveMsg -= Sock_ReceiveMsg;
                socket.Close();
            }

            CommunicationType = CommunicationMode.LAN;
            socket = new ClientSocket();
            socket.SetSock(sock);
            socket.ReceiveMsg += Sock_ReceiveMsg;
        }

        #endregion

        #region 数据接收

        /// <summary>
        /// CAN 数据接收回调
        /// </summary>
        private void PCanBasic_ReceiveMsg(TPCANMsg msg)
        {
            byte[] bytes = new byte[msg.DATA.Length + 1];
            msg.DATA.CopyTo(bytes, 1);
            bytes[0] = (byte)msg.ID;

            if (RecInfosPush != null)
                dealMsg(bytes);
        }

        /// <summary>
        /// 网口数据接收回调
        /// </summary>
        public void Sock_ReceiveMsg(byte[] msg)
        {
            if (socket == null || socket.State() == 0) return;

            // 触发字符串和字节数组两种接收事件
            if (RecInfoPush != null)
                dealMsg(DecodeSmart(msg));
            if (RecInfosPush != null)
                dealMsg(msg);
        }

        /// <summary>
        /// 串口数据接收回调
        /// </summary>
        public override void Com_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                lock (com)
                {
                    Thread.Sleep(5);
                    int i = com.ReadBufferSize;
                    byte[] bytes = new byte[i];
                    com.Read(bytes, 0, i);

                    // 去掉末尾0
                    byte[] data = [.. bytes.TakeWhile((v, index) => bytes.Skip(index).Any(w => w != 0x00))];
                    dealMsg(data);
                }
            }
            catch (Exception ex)
            {
                ShowLog("Serial port receive data exception:" + ex.Message, 0);
            }
        }

        /// <summary>
        /// VISA 读取设备返回数据
        /// </summary>
        public virtual string? ReadData()
        {
            return visa?.ReadResponse();
        }

        public virtual string? ReadData(string para)
        {
            return visa?.ReadResponse();
        }

        /// <summary>
        /// 字节数组消息处理（虚函数，由子类重写实现解析）
        /// </summary>
        public virtual void dealMsg(byte[] Msg)
        {

        }

        /// <summary>
        /// 数据拆分（虚函数）
        /// </summary>
        public virtual string MySplit(byte[] Msg)
        {
            return "";
        }

        /// <summary>
        /// 字符串消息处理（虚函数）
        /// </summary>
        public virtual void dealMsg(string Msg)
        {

        }

        /// <summary>
        /// 推送字符串数据到上层
        /// </summary>
        public void Push(string msg)
        {
            Task.Run(() =>
            {
                RecInfoPush?.Invoke(msg);
            });
        }

        /// <summary>
        /// 推送字节数组到上层
        /// </summary>
        public void Push(byte[] bytes)
        {
            Task.Run(() =>
            {
                RecInfosPush?.Invoke(bytes);
            });
        }

        /// <summary>
        /// 推送CAN消息到上层
        /// </summary>
        public void Push(TPCANMsg cANMsg)
        {
            Task.Run(() =>
            {
                RecPCANPush?.Invoke(cANMsg);
            });
        }

        #endregion

        /// <summary>
        /// VISA 查询指令（发送+读取）
        /// </summary>
        public virtual string? QueryResult(string command)
        {
            if (CommunicationType == CommunicationMode.VISA)
            {
                return visa?.QueryString(command);
            }
            return "无结果";
        }

        #region 数据发送

        /// <summary>
        /// 发送字符串命令（自动适配通讯方式）
        /// </summary>
        public void SendMsg(string Msg)
        {
            try
            {
                switch (CommunicationType)
                {
                    case CommunicationMode.LAN:
                        if (socket != null && socket.State() == 1)
                        {
                            socket.SendMsg(Msg);
                        }
                        break;
                    case CommunicationMode.Serial:
                        {
                            if (com == null || !com.IsOpen) return;
                            byte[] bytes = Encoding.ASCII.GetBytes(Msg);
                            com.Write(bytes, 0, bytes.Length);
                            break;
                        }
                    case CommunicationMode.VISA:
                        visa?.SendCommand(Msg);
                        break;
                    case CommunicationMode.CAN:
                        {
                            byte[] msgs = Encoding.ASCII.GetBytes(Msg);
                            byte[] bytes = new byte[msgs.Length - 1];
                            msgs.CopyTo(bytes, 1);
                            PCanBasic?.SendData(msgs[0], bytes);
                            break;
                        }
                }

                if (Msg != "")
                    ShowLog($"Send message:{Msg}", 1);
            }
            catch (Exception e)
            {
                Initialed = false;
                Close();
                ShowLog($"Send message:{Msg} exception{e.Message},Disconnect", 0);
            }
        }

        /// <summary>
        /// 发送字节数组
        /// </summary>
        public void SendMsg(byte[] Msg)
        {
            try
            {
                if (Status())
                {
                    if (CommunicationType == CommunicationMode.LAN)
                    {
                        socket?.SendMsg(Msg);
                    }
                    else if (CommunicationType == CommunicationMode.CAN)
                    {
                        byte[] bytes = new byte[Msg.Length - 1];
                        Msg.CopyTo(bytes, 1);
                        PCanBasic?.SendData(Msg[0], bytes);
                    }
                    else
                    {
                        com.Write(Msg, 0, Msg.Length);
                    }
                }
            }
            catch (Exception e)
            {
                ShowLog($"Sending byte array {Msg} exception {e.Message},Disconnect", 0);
            }
        }
        //        上层UI/业务逻辑
        //        ↓
        //调用 SendData(业务指令，虚方法)
        //        ↓
        //（具体设备子类重写 SendData → 组包）
        //        ↓
        //最终调用 SendMsg → 底层硬件发送（网口/串口/CAN/VISA）

        // 以下均为虚方法，发送重载，由具体设备类实现
        public virtual void SendData(List<string> lst) { }
        public virtual void SendData(List<string> lstData, Communication command) { }
        public virtual void SendData(int ID, string value) { }
        public virtual void SendData(string name, string value) { }
        public new virtual void SendData(string value) { }
        public virtual void SendData(byte[] bytes) { }
        public virtual void SendData(byte address, byte command) { }
        public virtual void SendData(byte address, byte command, byte[] lstData) { }
        public virtual void SendData(uint address, byte[] lstData) { }

        #endregion

        #region 关闭与清理

        /// <summary>
        /// 清空数据缓存（虚方法）
        /// </summary>
        public new virtual void ClearData()
        {

        }

        /// <summary>
        /// 关闭所有通讯连接，释放资源
        /// </summary>
        public virtual void Close()
        {
            try
            {
                if (CommunicationType == CommunicationMode.LAN)
                {
                    if (socket != null)
                    {
                        socket.ReceiveMsg -= Sock_ReceiveMsg;
                        socket.Close();
                        socket = null;
                    }
                }
                else if (CommunicationType == CommunicationMode.CAN)
                {
                    if (PCanBasic != null)
                    {
                        PCanBasic.ReceiveMsg -= PCanBasic_ReceiveMsg;
                        PCanBasic.Close();
                        PCanBasic = null;
                    }
                }
                else if (CommunicationType == CommunicationMode.Serial)
                {
                    // 串口关闭
                    base.ClearData();
                    base.Disconnect();
                }
                else
                {
                    visa?.Disconnect();
                }

                ShowLog(Name + "Close connection", 1);
            }
            catch (Exception e)
            {
                ShowLog($"Close connection:{Name} exception:{e.Message}", 0);
            }
        }

        #endregion

        #region 日志系统

        /// <summary>
        /// 日志事件
        /// </summary>
        private event Action<string, int>? AddLog;

        /// <summary>
        /// 外部注册日志输出
        /// </summary>
        public event Action<string, int> AddShowLog
        {
            add
            {
                if (AddLog != null)
                    AddLog = null;
                AddLog += value;
            }
            remove
            {
                if (AddLog != null)
                    AddLog = null;
            }
        }

        /// <summary>
        /// 输出日志
        /// </summary>
        /// <param name="msg">日志内容</param>
        /// <param name="isError">0=错误，1=信息，2=记录</param>
        protected void ShowLog(string msg, int isError)
        {
            AddLog?.Invoke(msg, isError);
        }

        #endregion
    }

    /// <summary>
    /// 发送信息结构体
    /// </summary>
    public class SendInfo
    {
        public required string SendComm;
        public required string comm;
        public bool IsSet;
    }
}