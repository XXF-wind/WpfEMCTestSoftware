using System.Collections.Concurrent;
using System.Timers;

namespace EMCDeviceLibrary
{
    public class Sinpel_Ampifier : SockConnect
    {
        private System.Timers.Timer tmrSend = new System.Timers.Timer(100);
        private ConcurrentQueue<SendInfo> sendInfos = new();

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
            //读取成功标记
            const string finish = "\n";
            ShowLog($"Rec message:{recData}To {device.IP}", 1);

            if (recData.Contains(finish, StringComparison.CurrentCultureIgnoreCase))
            {
                Task.Run(() =>
                {
                    Push(recData.Trim("\r\n     ".ToCharArray()));
                });
            }
        }

        /// <summary>
        /// 排队发送计时器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TmrSend_Elapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                if (sendInfos.TryDequeue(out var info))
                {
                    SendMsg(info.SendComm);
                }
            }
            finally
            {
                tmrSend.Start();
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
                string data = name + " " + value + "\n";
                sendInfos.Enqueue(new SendInfo()
                {
                    SendComm = data,
                    IsSet = true,
                    comm = name
                });
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
                string str = value + "?\n";
                if (value.Contains('?'))
                {
                    str = value + "\n";
                }
                sendInfos.Enqueue(new SendInfo()
                {
                    SendComm = str,
                    IsSet = false,
                    comm = value.Replace("?", "")
                });
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
        public override void SendData(int ID, string value)
        {
            Communication communication = (Communication)ID;
            string dataCommand = string.Empty;
            switch (communication)
            {
                case Communication.ENABlE_RFOUT:
                    dataCommand = $"STAT OPERATE";
                    break;
                case Communication.DISABlE_RFOUT:
                    dataCommand = $"STAT STANDBY";
                    break;

                default:
                    break;
            }
            SendData(dataCommand);
        }

        /// <summary>
        /// 清理数据
        /// </summary>
        public override void ClearData()
        {
            try
            {
                sendInfos.Clear();
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

        public Sinpel_Ampifier()
        {
            tmrSend.Elapsed += TmrSend_Elapsed;
            tmrSend.AutoReset = false;
            tmrSend.Start();
        }
    }
}
