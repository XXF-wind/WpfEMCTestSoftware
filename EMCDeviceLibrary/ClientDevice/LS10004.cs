namespace EMCDeviceLibrary
{
    /// <summary>
    /// LS10004 信号源模块 通讯子类
    /// 协议依据：《LS10004信号源模块控制指令说明V2.0.pdf》
    ///   - 第1节 通讯方式：串口 RS232（DB9：2/3/5），115200-8-N-1，无流控
    ///   - 第2节 帧格式：纯 ASCII 文本指令（类 SCPI），无帧头/地址/校验；
    ///     设置指令 = 指令字 + 空格(0x20) + 参数 + \n(0x0A)；查询指令以 ?\n 结尾；
    ///     应答：Succeed\n（成功）/ Defeated\n（失败）/ Syntactic Error\n（无法识别）/ ASCII 数据 + \n
    ///   - 第3节 指令表：OUT:FREQ / OUT:AMPL / OUT:RF / MOD:TYPE / MOD:PARA / MOD:STATU? / SG:REF
    ///   - 第4节 调制参数 MOD:PARA（Puls/AM/FM/PM 四组参数）
    ///   - 第5节 调制参数查询 MOD:STATU? → AAAA,BBBB,CCCC,DDDD,EEEE\n
    /// 数值编码：全部 ASCII 十进制字符串（频率隐含单位 MHz，幅度隐含单位 dBm），无字节序问题
    /// 注意：指令间隔须 > 1ms；协议无复位/初始化指令
    /// 类名必须与 DeviceMode 枚举名一致（ClsFactory 按枚举名反射创建实例）
    /// </summary>
    public class LS10004 : SockConnect
    {
        /// <summary>
        /// 初始化连接（通讯方式由 DevicePara.Mode 决定：Serial 串口 115200-8-N-1）
        /// </summary>
        /// <param name="Device">设备参数（DevicePara：Mode=Serial，ComPort，ComBaud=115200）</param>
        public override void InitConnection(object Device)
        {
            base.InitConnection(Device);
        }

        /// <summary>
        /// 接收到消息处理（第2节：应答为 ASCII 字符串 + \n 结尾；
        /// Succeed=设置成功，Defeated=设置失败，Syntactic Error=指令无法识别，查询返回数据+\n）
        /// </summary>
        /// <param name="recData">接收到的字符串</param>
        public override void dealMsg(string recData)
        {
            //读取成功标记
            const string finish = "\n";
            ShowLog($"Rec message:{recData}To COM{device?.ComPort ?? "-"}", 1);
            if (recData.Contains(finish, StringComparison.CurrentCultureIgnoreCase))
            {
                Task.Run(() =>
                {
                    Push(recData);
                });
            }
        }

        /// <summary>
        /// 字节数组消息处理（串口直收模式）
        /// </summary>
        /// <param name="Msg">接收到的字节</param>
        public override void dealMsg(byte[] Msg)
        {
            base.dealMsg(Msg);
        }

        /// <summary>
        /// 按参数类型发送指令（对应文档第3节指令表）
        /// 单位约定：频率 MHz（0.004~400，步进0.1Hz）、幅度 dBm（-60~10，步进0.25）
        /// </summary>
        /// <param name="ID">Communication 枚举值</param>
        /// <param name="value">参数值</param>
        public override void SendData(int ID, string value)
        {
            Communication communication = (Communication)ID;
            Thread.Sleep(1000);
            string dataCommand = string.Empty;
            switch (communication)
            {
                case Communication.FREQUENCY:
                    // OUT:FREQ <f>\n  设置频率（MHz，0.004~400）
                    dataCommand = $"OUT:FREQ {value}";
                    break;
                case Communication.LEVEL:
                    // OUT:AMPL <a>\n  设置幅度（dBm，-60~10，步进0.25）
                    dataCommand = $"OUT:AMPL {value}";
                    break;
                case Communication.OUTPUTSTATE:
                    // OUT:RF <x>\n  输出开关（1=开 0=关）
                    dataCommand = $"OUT:RF {value}";
                    break;
                case Communication.MODENABLE:
                    // MOD:TYPE <m>\n  调制模式（0=CW 1=Puls脉冲 3=AM 4=FM 5=PM）
                    dataCommand = $"MOD:TYPE {value}";
                    break;
                case Communication.SGREF:
                    // SG:REF <x>\n  外参考使能（1=开启 0=关闭）
                    dataCommand = $"SG:REF {value}";
                    break;
                case Communication.OUTPUTPOWER:
                    // 文档无实际输出功率监测指令（OUT:AMPL? 只回设定值，非实测功率）；
                    // 幅度设置由 LEVEL（OUT:AMPL）承担，如需查询设定值直接透传 "OUT:AMPL?"
                    ShowLog("LS10004 unsupported command:OUTPUTPOWER(No output power monitor command; amplitude set via LEVEL/OUT:AMPL, query via \"OUT:AMPL?\")", 2);
                    return;
                case Communication.初始化:
                    // 协议无复位/初始化指令（文档未提供 reset 类命令），仅记录日志
                    ShowLog("LS10004 unsupported command:初始化(No reset command in the protocol, ignored)", 2);
                    return;
                case Communication.SWITCHUNIT:
                    // 协议无单位切换指令（幅度固定 dBm），仅记录日志
                    ShowLog("LS10004 unsupported command:SWITCHUNIT(No unit switch command in the protocol, ignored)", 2);
                    return;
                case Communication.AM_DEPT:
                case Communication.AM_STATe:
                case Communication.AM_TYPE:
                case Communication.INPUTPOWER:
                case Communication.PERIODVALUE:
                case Communication.PULSEWIDTH:
                    // AM 深度/开关/类型、脉冲周期/脉宽在 LS10004 中由 MOD:TYPE + MOD:PARA 四元组统一控制，
                    // 无独立指令对应，请改用 SendData(MODENABLE,"3") + SendModPara(...)，此处仅记录日志
                    ShowLog($"LS10004 unsupported command:{communication}(Controlled by MOD:TYPE + MOD:PARA, use SendModPara instead)", 2);
                    return;
                default:
                    break;
            }
            SendData(dataCommand);
        }

        /// <summary>
        /// 设置调制参数 MOD:PARA（文档第4节）
        /// 格式：MOD:PARA AAAA,BBBB,CCCC,DDDD\n（4 个 ASCII 十进制整数，逗号 0x2C 分隔）
        /// Puls(MOD:TYPE=1)：参数1=0(无效)，参数2=脉宽系数N(1~1000)，参数3=周期系数M(1~1000)，参数4=速率系数K(1~65535,τ=4ns×K)
        /// AM(MOD:TYPE=3)：参数1=波形(0正弦/1锯齿/2三角/3方波)，参数2=深度系数N(0~10000,深度=N×0.01%)，参数3=速率系数M(1~65535)，参数4=倍速系数K(0~6)
        /// FM(MOD:TYPE=4)：参数1=波形(0~3)，参数2=最大频偏N(1~50000,频偏=N×200Hz)，参数3=速率系数M，参数4=倍速系数K(0~6)
        /// PM(MOD:TYPE=5)：参数1=0(无效)，参数2=相移系数φ(0~10000,相移=0.036°×φ)，参数3=速率系数M，参数4=倍速系数K(0~6)
        /// </summary>
        /// <param name="p1">参数1</param>
        /// <param name="p2">参数2</param>
        /// <param name="p3">参数3</param>
        /// <param name="p4">参数4</param>
        public void SendModPara(string p1, string p2, string p3, string p4)
        {
            SendData($"MOD:PARA {p1},{p2},{p3},{p4}");
        }

        /// <summary>
        /// 查询调制参数 MOD:STATU?（文档第5节）
        /// 应答：AAAA,BBBB,CCCC,DDDD,EEEE\n（AAAA=调制方式 0=CW/1=Puls/3=AM/4=FM/5=PM，BBBB~EEEE=参数1~4）
        /// </summary>
        public void QueryModStatus()
        {
            SendData("MOD:STATU?");
        }

        /// <summary>
        /// 发送原始指令（第2节：指令以 \n 结尾；文档全部指令均可经此透传，保证指令集无遗漏）
        /// </summary>
        /// <param name="value">指令（如 OUT:FREQ? / SG:REF?）</param>
        public override void SendData(string value)
        {
            try
            {
                string str = value + "\n";
                SendMsg(str);
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        public override void Close()
        {
            //释放现有连接
            try
            {
                Initialed = false;
                ClearData();
                base.Close();
            }
            catch { }
            finally { }
        }
    }
}
