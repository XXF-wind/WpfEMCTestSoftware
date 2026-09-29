namespace EMCDeviceLibrary
{
    /// <summary>
    /// 白鹭（Baluelec）MSA 系列频谱分析仪 通讯子类
    /// 协议依据：《频谱分析仪_SA用户编程手册》（MSA-B.01.03，2026-02-03）
    ///   - 第2章 仪器程控端口的连接和设置（P11-13）：仅 LAN 网口，SICL-LAN / Sockets 协议，不支持 DHCP
    ///   - 第3章 命令子系统（P13-36）：3.1 IEEE488.2；3.2 CALCulate；3.3 CONFigure；3.4 DISPlay；
    ///     3.5 INITiate；3.6 INSTrument；3.7 SENSe；3.8 SYStem；3.9 TRIGger；3.10 TRACe
    ///   - 第4章 编程实例 + 附录（P36-37）：频率计数使用示例
    /// 通讯方式：LAN（Sockets 协议）；迹线数据请通过 10068 端口发送 :TRACe?（3.10）
    /// 命令结束符：命令以分号 ; 结尾（手册命令格式均带 ;）；查询应答为 ASCII 字符串
    /// 类名必须与 DeviceMode 枚举名一致（ClsFactory 按枚举名反射创建实例）
    /// </summary>
    public class Baluelec_SpectrumAnalyzer_MSA : SockConnect
    {
        /// <summary>
        /// 初始化连接（通讯方式由 DevicePara.Mode 决定：LAN/Serial/VISA）
        /// </summary>
        /// <param name="Device">设备参数（DevicePara）</param>
        public override void InitConnection(object Device)
        {
            base.InitConnection(Device);
        }

        /// <summary>
        /// 接收到消息处理（第3章：查询应答为 ASCII 字符串）
        /// </summary>
        /// <param name="recData">接收到的字符串</param>
        public override void dealMsg(string recData)
        {
            ShowLog($"Rec message:{recData}To {device.IP}", 1);
            RecData += recData;
            if (RecData.Contains(finish, StringComparison.CurrentCultureIgnoreCase))
            {
                Task.Run(() =>
                {
                    recData = RecData[..(RecData.IndexOf(finish) + finish.Length)];
                    RecData = RecData[(RecData.IndexOf(finish) + finish.Length)..];
                    if (recData.Split(',').Length > 2)
                    {
                        Push(GainPeak(recData).ToString());
                    }
                    else
                    {
                        Push(recData);
                    }
                });
            }
        }
        /// <summary>
        /// 获取峰值
        /// </summary>
        /// <returns></returns>
        private double GainPeak(string trace)
        {
            string[] values = trace.Split(',');
            double peak = values.Select(v => double.TryParse(v, out double d) ? d : double.MinValue).Max();
            switch (ReadType)
            {
                case Communication.READ_DBUA:
                    peak = ClassLibrary.Generic.GenericMethods.ConvertDBmToDbuA(peak, 50);
                    break;
                case Communication.READ_DBUV:
                    peak = ClassLibrary.Generic.GenericMethods.ConvertDBmToDbuv(peak, 50);
                    break;
                default: break;
            }
            return peak;
        }
        /// <summary>
        /// 字节数组消息处理（Socket 直收模式）
        /// </summary>
        /// <param name="Msg">接收到的字节</param>
        public override void dealMsg(byte[] Msg)
        {
            base.dealMsg(Msg);
        }

        /// <summary>
        /// 按参数类型发送命令（对应手册第3章命令子系统）
        /// </summary>
        /// <param name="ID">Communication 枚举值</param>
        /// <param name="value">参数值</param>
        public override void SendData(int ID, string value)
        {
            Communication communication = (Communication)ID;
            ReadType = communication;
            string dataCommand = string.Empty;
            switch (communication)
            {
                case Communication.FREQUENCY:
                    // 3.7(15) [:SENSe]:FREQuency:CENTer <freq>;  单位默认Hz
                    dataCommand = $":FREQ:CENT {value}MHz;";
                    break;
                case Communication.视频带宽:
                    // 3.7(8) [:SENSe]:BANDwidth:VIDeo <freq>;  取值见手册列表
                    dataCommand = $":BAND:VID {value}KHz;";
                    break;
                case Communication.分辨带宽:
                    // 3.7(6) [:SENSe]:BANDwidth[:RESolution] <freq>;  取值见手册列表
                    dataCommand = $":BAND {value}KHz;";
                    break;
                case Communication.耦合方式:
                    // 手册无输入耦合命令；该型号输入为固定 50Ω，仅记录日志
                    ShowLog($"Baluelec unsupported command:{communication}(No input coupling command in the manual, ignored)", 2);
                    return;
                case Communication.SPAN:
                    // 3.7(20) [:SENSe]:FREQuency:SPAN <freq>;
                    dataCommand = $":FREQ:SPAN {value}MHz;";
                    break;
                case Communication.MARKFREQ:
                    // 3.2(5) :CALCulate:WINDow:TRACe:MARKer<n>:XFREquence <value>;  单位Hz
                    dataCommand = $":CALC:WIND:TRAC:MARK1:XFREQ {value}Hz;";   //设置标记频率
                    break;

                case Communication.INPUTPOWER:
                    // 信道功率：本手册（扫频频谱版）未提供 :READ:CHP? 类信道功率命令，
                    // 使用 3.2(6) 标记Y值（当前峰值）作为功率读数，与 N9020A/SHA851A 的 :READ:CHP? 语义对齐
                    dataCommand = ":CALC:WIND:TRAC:MARK1:Y?;";    // 读取标记峰值（功率）
                    break;
                case Communication.参考电平:
                    // 3.4(1) :DISPlay:WINDow:TRACe:Y[:SCALe]:RLEVel <value>;
                    dataCommand = $":DISP:WIND:TRAC:Y:RLEV {value}dBm;";
                    break;
                case Communication.初始化:
                    // 3.1(5) *RST; 复位
                    // 3.6(2) :INSTrument[:SELect] SA; 进入频谱分析仪模式
                    // 3.2(1) :CALCulate:MARKer1:CPSEarch:STATe 1; 峰值跟踪
                    dataCommand = "*RST;";
                    SendData(dataCommand);
                    Thread.Sleep(100);
                    dataCommand = ":INST:SEL SA;"; //进入频谱分析仪模式
                    SendData(dataCommand);
                    Thread.Sleep(100);
                    dataCommand = ":CALC:MARK1:CPSE:STATe 1;";   //开始搜索峰值（峰值跟踪开）
                    break;
                case Communication.READ_DBM:
                case Communication.读取数据:
                case Communication.READ_DBUA:
                case Communication.READ_DBUV:
                    dataCommand = ":TRACE:DATA? TRACE1";   //读取迹线数据
                    break;
                default:
                    break;
            }
            SendData(dataCommand);
        }

        /// <summary>
        /// 发送原始 SCPI 命令（第3章：命令以 ; 结尾）
        /// </summary>
        /// <param name="value">SCPI 命令</param>
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
        /// 读取迹线数据（3.10 :TRACe:[DATA]?; 请通过 10068 端口发送该指令；
        /// 应答格式：value1,value2,...value(n)，n 为当前测量点数）
        /// </summary>
        /// <returns>迹线应答字符串（VISA 模式返回原始应答）</returns>
        public string? QueryTraceData()
        {
            const string dataCommand = ":TRACe?;";
            if (CommunicationType == CommunicationMode.VISA)
            {
                return QueryResult(dataCommand);
            }
            SendData(dataCommand);
            return null;
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        public override void Close()
        {
            //释放现有Socket连接
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
