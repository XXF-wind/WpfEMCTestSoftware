using ClassLibrary;
using ClassLibrary.Generic;

namespace EMCDeviceLibrary
{
    public class Tektronix_TBS1000C : SockConnect
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
            //读取成功标记
            ShowLog($"Rec message:{recData}To {device.IP}", 1);

            RecData += recData;
            if (RecData.Contains(finish, StringComparison.CurrentCultureIgnoreCase))
            {
                Task.Run(() =>
                {
                    recData = RecData[..(RecData.IndexOf(finish) + finish.Length)];
                    RecData = RecData[(RecData.IndexOf(finish) + finish.Length)..];

                    Push(recData);

                });
            }
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
            string dataCommand = string.Empty;
            switch (communication)
            {
                case Communication.FREQUENCY:
                    // 频率单位默认Hz
                    //实际发送时域值=1/value
                    double freqValue = double.Parse(value);
                    dataCommand = $"HORizontal:MAIn:SCAle {1.0 / freqValue}";
                    break;
                case Communication.读取数据:
                    dataCommand = "MEASUrement:IMMed:VALue?";   //读取迹线数据
                    if (CommunicationType == CommunicationMode.VISA)
                    {
                        string? re = QueryResult(dataCommand);
                        if (!string.IsNullOrEmpty(re))
                        {
                            Push(re.ToString());
                        }
                        return;
                    }
                    break;
                case Communication.参考电平:
                    if (double.TryParse(value, out double V))
                    {
                        double vol = GenericMethods.ConvertDBmToVolt(V);
                        // 因为示波器的参考电平是以电压为单位的，而我们通常使用的是功率单位dBm。根据功率和电压的关系，电压值可以通过将功率值转换为电压值来计算。为了确保示波器的显示范围适合信号，我们将示波器上每格的高度设置为电压值的1/6。
                        dataCommand = $"CH1:SCALE {vol / 6}";
                    }
                    break;
                case Communication.初始化:
                    dataCommand = "*RST";
                    SendData(dataCommand);
                    ClsDelay.Delay.MicrosecondDelay(50 * 1000);
                    dataCommand = "CH1:POSITION 0"; //偏移设置为0
                    SendData(dataCommand);
                    ClsDelay.Delay.MicrosecondDelay(50 * 1000);
                    dataCommand = "CH1:PROBE:GAIN 1"; //设置探头增益（输出/输入传输比），即衰减倍数的倒数
                    SendData(dataCommand);
                    ClsDelay.Delay.MicrosecondDelay(50 * 1000);
                    dataCommand = "MEASUrement:IMMed:SOUrce1 CH1"; //指定测量源通道（或 MATH）
                    SendData(dataCommand);
                    ClsDelay.Delay.MicrosecondDelay(50 * 1000);
                    dataCommand = "MEASUrement:IMMed:TYPe RMS"; //设测量类型为真有效值（整个波形 RMS）
                    break;
                default:
                    break;
            }
            SendData(dataCommand);
        }

        /// <summary>
        /// 发送原始 SCPI 命令
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
