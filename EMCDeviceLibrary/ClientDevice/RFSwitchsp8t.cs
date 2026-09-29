namespace EMCDeviceLibrary
{
    public class RFSwitchsp8t : SockConnect
    {
        /// <summary>
        /// 初始化功放连接
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
            ShowLog($"接收到{Name}消息：{recData}", 2);

            //读取成功标记
            const string finish = "\n";
            if (recData.Contains(finish, StringComparison.CurrentCultureIgnoreCase))
            {
                Task.Run(() =>
                {
                    string key = device.Mode == CommunicationMode.LAN ? device.IP : device.ComPort;
                    Push($"{key}:{recData.Trim()}");
                });
            }
        }

        /// <summary>
        /// 发送设置信号
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public override void SendData(string name, string value)
        {
            try
            {
                SendMsg($"{name} {value}\r\n");
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// 发送查询信号
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
        /// 批量发送查询信号
        /// </summary>
        /// <param name="lst"></param>
        public override void SendData(List<string> lst)
        {
            try
            {
                foreach (string value in lst)
                {
                    SendData(value);
                }
            }
            catch
            {
                throw;
            }
        }

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
