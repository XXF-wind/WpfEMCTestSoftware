namespace EMCDeviceLibrary
{
    public class Ceyear_NetworkAnalyzer_3672E : SockConnect
    {
        /// <summary>
        /// 初始化连接
        /// </summary>
        /// <param name="Device"></param>
        public override void InitConnection(object Device)
        {
            base.InitConnection(Device);
            if (Initialed)
            {
                SendData("*RST");
                ClassLibrary.ClsDelay.Delay.MicrosecondDelay(100000);
                SendData("*CLS");
                ClassLibrary.ClsDelay.Delay.MicrosecondDelay(100000);
                SendData(":CALC:PAR:DEF 'TEST1', S21");
                ClassLibrary.ClsDelay.Delay.MicrosecondDelay(100000);
                SendData(":DISP:WIND1:TRAC2:FEED 'TEST1'");
                ClassLibrary.ClsDelay.Delay.MicrosecondDelay(100000);
                SendData("CALC:PAR:SEL 'TEST1'");
                ClassLibrary.ClsDelay.Delay.MicrosecondDelay(100000);
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

