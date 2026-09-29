namespace EMCDeviceLibrary
{
    public class KEYSIGHT_SpectrumAnalyzer_N9020A : SockConnect
    {
        /// <summary>
        /// 初始化连接
        /// </summary>
        /// <param name="Device"></param>
        public override void InitConnection(object Device)
        {
            base.InitConnection(Device);
        }

        /// <summary>
        /// 接收到消息处理
        /// </summary>
        /// <param name="recData"></param>
        public override void dealMsg(string recData)
        {
            ShowLog($"Rec message:{recData}To {device.IP}", 2);
            RecData += recData;
            if (RecData.Contains(finish, StringComparison.CurrentCultureIgnoreCase))
            {
                Task.Run(() =>
                {
                    recData = RecData[..(RecData.IndexOf(finish) + finish.Length)];
                    RecData = RecData[(RecData.IndexOf(finish) + finish.Length)..];
                    if (double.TryParse(recData, out double peak))
                    {
                        switch (ReadType)
                        {
                            case Communication.READ_DBUA:
                                peak = ClassLibrary.Generic.GenericMethods.ConvertDBmToDbuA(peak, 50);
                                break;
                            case Communication.READ_DBUV:
                                peak = ClassLibrary.Generic.GenericMethods.ConvertDBmToDbuv(peak, 50);
                                break;
                            default:                                 
                                break;
                        }
                        recData = peak.ToString();
                    }
                    Push(recData);
                });
            }
        }

        public override void SendData(int ID, string value)
        {
            Communication communication = (Communication)ID;
            ReadType = communication;
            string dataCommand = string.Empty;
            switch (communication)
            {
                case Communication.FREQUENCY:
                    dataCommand = $":FREQ:CENT {value}MHz";
                    break;
                case Communication.视频带宽:
                    dataCommand = $":BWID:VID {value}KHz";
                    break;
                case Communication.分辨带宽:
                    dataCommand = $":BWIDth {value}KHz";
                    break;
                case Communication.耦合方式:
                    dataCommand = value == "1" ? $"INP:COUP DC" : $"INP:COUP AC";
                    break;
                case Communication.SPAN:
                    dataCommand = $":FREQ:SPAN {value}MHz";
                    break;
                case Communication.MARKFREQ:
                    dataCommand = $":CALC:MARK1:X {value}MHz";   //设置标记频率
                    break;
                case Communication.READ_DBM:
                case Communication.读取数据:
                case Communication.READ_DBUA:
                case Communication.READ_DBUV:
                    dataCommand = $":CALC:MARK1:Y?";   //获取峰值
                    break;
                case Communication.INPUTPOWER:
                    dataCommand = $":READ:CHP?";    // 触发一次测量并读回结果
                    break;
                case Communication.参考电平:
                    dataCommand = $"DISPlay:WINDow:TRACe:Y:RLEVel {value}dBm";
                    break;
                case Communication.初始化:
                    dataCommand = "SYSTem:PRESet";
                    SendData(dataCommand);
                    Thread.Sleep(100);
                    dataCommand = ":INST:SEL SA"; //进入频谱分析仪模式
                    SendData(dataCommand);
                    Thread.Sleep(100);
                    dataCommand = $":CALC:MARK1:MAX";   //开始搜索峰值
                    SendData(dataCommand);
                    Thread.Sleep(100);
                    dataCommand = $"INP:COUP DC";
                    //SendData(dataCommand);
                    //Thread.Sleep(100);
                    //dataCommand = ":CONF:CHP"; //选择信道功率测量
                    break;
                default:
                    break;
            }
            SendData(dataCommand);
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="value"></param>
        public override void SendData(string name, string value)
        {
            try
            {
                string str = $"{name} {value}";
                SendData(str);
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="value"></param>
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

