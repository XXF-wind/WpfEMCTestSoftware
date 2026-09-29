using System.Diagnostics;

namespace EMCDeviceLibrary
{
    public class Baluelec_POWER : SockConnect
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
        /// 接收到消息处理
        /// </summary>
        /// <param name="recData">接收到的字符串</param>
        public override void dealMsg(string recData)
        {
            //读取成功标记
            const string finish = "\n";
            ShowLog($"Rec message:{recData}To {device.IP}", 1);
            if (recData.Contains(finish, StringComparison.CurrentCultureIgnoreCase))
            {
                Task.Run(() =>
                {
                    recData = recData.Trim();
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
                    dataCommand = $":FREQ {value}";
                    break;
                case Communication.读取数据:
                    dataCommand = ":AMPL?";   //读取数据
                    if (CommunicationType == CommunicationMode.VISA)
                    {
                        var sw = Stopwatch.StartNew();
                        string? re = QueryResult(dataCommand)?.Trim('\0');
                        Debug.WriteLine($"耗时 {sw.ElapsedMilliseconds} ms");
                        if (!string.IsNullOrEmpty(re))
                        {
                            Push(re);
                        }
                        return;
                    }
                    break;
                default:
                    break;
            }
            SendData(dataCommand);
        }

        /// <summary>
        /// 发送命令
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
