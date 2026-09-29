namespace EMCDeviceLibrary
{
    /// <summary>
    /// 普源 RIGOL DSG815 信号源 设备驱动类
    /// 继承自：SockConnect（通用通讯基类）
    /// 功能：通过 SCPI 指令控制普源 DSG815 信号源
    /// 支持：频率、功率、输出开关、脉冲调制、AM调制 等
    /// </summary>
    public class RIGOL_DSG815 : SockConnect
    {
        /// <summary>
        /// 默认功率单位 dBm（毫瓦分贝）
        /// </summary>
        private readonly string genertorUnit = "dBm";


        /// <summary>
        /// 重写初始化连接
        /// 功能：连接成功后 → 自动复位设备（恢复出厂设置）
        /// </summary>
        /// <param name="Device">设备参数</param>
        public override void InitConnection(object Device)
        {
            // 调用父类初始化（网口/VISA 连接）
            base.InitConnection(Device);

            // 如果连接成功，发送预设指令复位信号源
            if (Status())
            {
                SendData(":SYSTem:PRESet");
            }
        }

        /// <summary>
        /// 配置并开启 AM 幅度调制
        /// 设置：调制深度、调制频率、打开调制输出
        /// </summary>
        private void AMEnable()
        {
            SendData(20, "80");      // 设置 AM 调制深度 80%
            SendData(4, "1");        // 设置调制信号频率 1kHz
            SendData(21, "ON");      // 打开 AM 调制输出
        }

        /// <summary>
        /// 重写字符串接收处理
        /// 功能：处理信号源返回的 SCPI 应答数据 → 推送给上层UI
        /// </summary>
        /// <param name="recData">设备返回的字符串</param>
        public override void dealMsg(string recData)
        {
            // 帧结束符：换行符表示一条完整消息
            const string finish = "\n";
            // 输出日志
            ShowLog($"Rec message:{recData}To {device.IP}", 1);

            // 如果收到完整帧
            if (recData.Contains(finish, StringComparison.CurrentCultureIgnoreCase))
            {
                // 异步推送给界面
                Task.Run(() =>
                {
                    Push(recData);
                });
            }
        }

        /// <summary>
        /// 重写：根据指令ID + 参数 拼接 SCPI 指令
        /// 功能：上层传入指令ID → 自动生成对应普源 SCPI 命令
        /// </summary>
        /// <param name="ID">指令ID（对应Communication枚举）</param>
        /// <param name="value">参数值</param>
        public override void SendData(int ID, string value)
        {
            Communication communication = (Communication)ID;
            string dataCommand = string.Empty;

            // 根据不同功能拼接 SCPI 指令
            switch (communication)
            {
                case Communication.FREQUENCY:          // 设置频率
                    dataCommand = $":FREQ {value}MHz";
                    break;
                case Communication.LEVEL:              // 设置功率
                    dataCommand = $":LEV {value}{genertorUnit}";
                    break;
                case Communication.OUTPUTSTATE:        // 输出开关
                    dataCommand = $":OUTP:STAT {value}";
                    break;
                case Communication.SWITCHUNIT:         // 切换功率单位
                    dataCommand = $"UNIT:POWer {value}";
                    break;
                case Communication.PERIODVALUE:        // 脉冲周期
                    dataCommand = $":PULM:PERiod {value}ms";
                    break;
                case Communication.PULSEWIDTH:         // 脉冲宽度
                    dataCommand = $":PULM:WIDTh {value}us";
                    break;
                case Communication.MODOUTSTATE:        // 调制输出开关
                    dataCommand = $"OUTP:MOD:STAT {value}";
                    break;
                case Communication.MODENABLE:          // 脉冲调制使能
                    dataCommand = $":PULM:STATe {value}";
                    break;
                case Communication.AM_DEPT:             // AM调制深度
                    dataCommand = $":AM:DEPT {value}";
                    break;
                case Communication.INPUTPOWER:          // AM调制频率
                    dataCommand = $":AM:FREQ {value}kHz";
                    break;
                case Communication.AM_STATe:            // AM调制开关
                    dataCommand = $":AM:STATe {value}";
                    break;
                default:
                    break;
            }

            // 发送最终拼接好的 SCPI 指令
            SendData(dataCommand);
        }

        /// <summary>
        /// 重写：发送字符串指令
        /// 功能：给指令加换行符 → 调用底层 SendMsg 发送
        /// </summary>
        /// <param name="value">SCPI指令</param>
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
        /// 功能：先安全关闭输出 → 再释放连接 → 防止一直发射
        /// </summary>
        public override void Close()
        {
            try
            {
                SendData(21, "OFF"); // 关闭射频输出
                Initialed = false;
                ClearData();
                base.Close(); // 调用父类关闭通讯
            }
            catch { }
            finally { }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public RIGOL_DSG815()
        {
        }
    }
}