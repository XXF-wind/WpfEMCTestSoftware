using ClassLibrary;

namespace EMCDeviceLibrary
{
    public class RIGOL_DG1000Z : SockConnect
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
            //读取成功标记
            const string finish = "\n";
            ShowLog($"Rec message:{recData}To {device.IP}", 1);

            if (recData.Contains(finish, StringComparison.CurrentCultureIgnoreCase))
            {
                Task.Run(() =>
                {
                    Push(recData);
                });
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ID"></param>
        /// <param name="value"></param>
        public override void SendData(int ID, string value)
        {
            Communication communication = (Communication)ID;
            string dataCommand = string.Empty;
            switch (communication)
            {
                case Communication.FREQUENCY:
                    dataCommand = $":FREQuency {value} MHz";
                    break;
                case Communication.OUTPUTPOWER:
                    dataCommand = $":VOLTage {value}";
                    break;
                case Communication.OUTPUTSTATE:
                    if (value.Contains('1'))
                    {
                        dataCommand = ":OUTP1 ON";
                    }
                    else if (value.Contains('0'))
                    {
                        dataCommand = ":OUTP1 OFF";
                    }
                    else
                    {
                        dataCommand = $":OUTP1 {value}";
                    }
                    break;
                case Communication.MODOUTSTATE:
                    if (value.Contains('1'))
                    {
                        dataCommand = ":SOUR1:AM:STAT ON";
                    }
                    else if (value.Contains('0'))
                    {
                        dataCommand = ":SOUR1:AM:STAT OFF";
                    }
                    else
                    {
                        dataCommand = $":SOUR1:AM:STAT {value}";
                    }
                    break;
                case Communication.初始化:
                    dataCommand = "*RST";
                    SendData(dataCommand);
                    ClsDelay.Delay.MicrosecondDelay(50 * 1000);
                    dataCommand = "*CLS";
                    //SendData(dataCommand);
                    //ClsDelay.Delay.MicrosecondDelay(50 * 1000);
                    //dataCommand = $":SOUR1:MOD:TYPE {value}";
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
