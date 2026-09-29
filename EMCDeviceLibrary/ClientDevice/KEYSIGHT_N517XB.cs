namespace EMCDeviceLibrary
{
    /// <summary>
    /// 是德科技 KEYSIGHT N517XB 系列信号源 设备驱动类
    /// 继承自：SockConnect（通用通讯基类）
    /// 功能：通过 SCPI 指令控制信号源（设置频率、功率、开关输出、调制等）
    /// 通讯方式：LAN / VISA （SCPI 标准指令）
    /// </summary>
    public class KEYSIGHT_N517XB : SockConnect
    {
        /// <summary>
        /// 默认功率单位 DBM（毫瓦分贝）
        /// </summary>
        private readonly string genertorUnit = "DBM";

        /// <summary>
        /// 重写初始化连接
        /// 功能：连接成功后 → 自动复位设备（*RST）
        /// </summary>
        public override void InitConnection(object Device)
        {
            // 调用父类初始化（网口/VISA连接）
            base.InitConnection(Device);

            // 如果连接成功，发送复位指令，让设备恢复出厂状态
            if (Status())
            {
                SendData("*RST");
            }
        }

        /// <summary>
        /// 开启 AM 调制（幅度调制）的一组配置指令
        /// 配置：调制类型、深度、输入、使能
        /// </summary>
        private void AMEnable()
        {
            SendData(22, "LIN");     // 设置AM调制类型为线性
            SendData(20, "80");      // 设置AM调制深度 80%
            SendData(4, "1");        // 设置调制输入功率 1KHz
            SendData(21, "ON");      // 打开AM调制输出
        }

        /// <summary>
        /// 重写字符串数据接收处理
        /// 功能：接收信号源返回的 SCPI 应答数据 → 推送给上层UI
        /// </summary>
        public override void dealMsg(string recData)
        {
            // 读取完成的结束符（换行表示一帧结束）
            const string finish = "\n";
            // 输出接收日志
            ShowLog($"Rec message:{recData}To {device.IP}", 1);

            // 如果收到完整帧（包含换行）
            if (recData.Contains(finish, StringComparison.CurrentCultureIgnoreCase))
            {
                // 异步推送数据到界面
                Task.Run(() =>
                {
                    Push(recData);
                });
            }
        }

        /// <summary>
        /// 重写：根据指令ID + 参数值 组 SCPI 指令
        /// 功能：将业务指令（如设置频率）→ 翻译成标准 SCPI 指令
        /// </summary>
        public override void SendData(int ID, string value)
        {
            // 将数字ID转为枚举指令类型
            Communication communication = (Communication)ID;
            string dataCommand = string.Empty;

            // 根据不同指令，拼接对应的 SCPI 命令
            switch (communication)
            {
                case Communication.FREQUENCY:          // 设置频率
                    dataCommand = $":FREQ {value} MHz";
                    break;
                case Communication.LEVEL:              // 设置功率
                    dataCommand = $":POW {value} {genertorUnit}";
                    break;
                case Communication.OUTPUTSTATE:        // 输出开关
                    dataCommand = $":OUTP:STAT {value}";
                    break;
                case Communication.SWITCHUNIT:          // 切换功率单位
                    dataCommand = $"UNIT:POWer {value}";
                    break;
                case Communication.PERIODVALUE:        // 脉冲周期
                    dataCommand = $":PULM:INT:PER {value} MS";
                    break;
                case Communication.PULSEWIDTH:          // 脉冲宽度
                    dataCommand = $":PULM:INT:PWIDth {value} US";
                    break;
                case Communication.MODOUTSTATE:        // 调制输出开关
                    dataCommand = $"OUTP:MOD:STAT {value}";
                    break;
                case Communication.MODENABLE:          // 脉冲调制使能
                    dataCommand = $":PULM:STATe {value}";
                    break;
                case Communication.AM_DEPT:             // AM调制深度
                    dataCommand = $":AM1:DEPTh:LIN {value}";
                    break;
                case Communication.INPUTPOWER:          // AM调制输入频率
                    dataCommand = $":AM1:INT:FUNC1:FREQ {value}KHz";
                    break;
                case Communication.AM_STATe:            // AM调制使能
                    dataCommand = $":AM1:STATe {value}";
                    break;
                case Communication.AM_TYPE:             // AM调制类型
                    dataCommand = $":AM1:TYPE {value}";
                    break;
                default:
                    break;
            }

            // 发送拼接好的 SCPI 指令
            SendData(dataCommand);
        }

        /// <summary>
        /// 重写：发送字符串指令（SCPI指令）
        /// 功能：给指令加换行符 → 调用底层 SendMsg 发送
        /// </summary>
        public override void SendData(string value)
        {
            try
            {
                // SCPI 指令必须以 \n 结尾
                string str = value + "\n";
                // 调用底层发送（网口/VISA）
                SendMsg(str);
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// 重写：关闭设备连接
        /// 功能：先关闭射频输出 → 再断开连接
        /// </summary>
        public override void Close()
        {
            try
            {
                // 安全关闭输出，防止一直发射信号
                SendData(21, "OFF");
                Initialed = false;
                ClearData();
                // 调用父类关闭连接
                base.Close();
            }
            catch { }
            finally { }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public KEYSIGHT_N517XB()
        {
        }
    }
}