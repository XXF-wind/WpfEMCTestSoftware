namespace EMCDeviceLibrary
{
    public class Ceyear_Sight_1435 : SockConnect
    {
        /// <summary>
        /// 初始化连接
        /// </summary>
        /// <param name="Device"></param>
        public override void InitConnection(object Device)
        {
            base.InitConnection(Device);
            if (Status())
            {
                SendData("*RST");
                ClassLibrary.ClsDelay.Delay.MicrosecondDelay(500000);
                //SendData("SENSe1:SPEed 200");
                //ClassLibrary.ClsDelay.Delay.MicrosecondDelay(50000);
                //SendData("SENSe2:SPEed 200");
                //ClassLibrary.ClsDelay.Delay.MicrosecondDelay(50000);
                //SendData("AVERage1:CoUNt 16");
            }
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
                    dataCommand = $":SOUR:FREQ:CW {value}MHz";
                    break;
                case Communication.LEVEL:
                    dataCommand = $":SOUR:POW {value}dBm";
                    break;
                case Communication.SWITCHUNIT:
                    dataCommand = $":UNIT:POW {value}";
                    break;
                case Communication.OUTPUTSTATE:
                    dataCommand = $":OUTP:STAT {value}";
                    break;
                case Communication.ATTENUATION:
                    dataCommand = $":SOUR:POW:ATT {value}dB";
                    break;
                default:
                    return;
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
