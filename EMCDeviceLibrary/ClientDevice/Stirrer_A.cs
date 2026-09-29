namespace EMCDeviceLibrary.ClientDevice
{
    /// <summary>
    /// 搅拌器
    /// </summary>
    public class Stirrer_A : SockConnect
    {

        /// <summary>
        /// 初始化连接
        /// 连接设备成功后，做一些初始化参数
        /// </summary>
        /// <param name="Device"></param>
        public override void InitConnection(object Device)
        {
            base.InitConnection(Device);
            if (Status())
            {
                //列如复位到0°

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

        public override void SendData(int ID, string value)
        {
            Communication communication = (Communication)ID;
            string dataCommand = string.Empty;
            switch (communication)
            {
                case Communication.SET_STIRRER_ANGLE:
                    dataCommand = $"待填充";
                    break;
                case Communication.SET_STIRRER_SPEED:
                    dataCommand = $"待填充";
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
