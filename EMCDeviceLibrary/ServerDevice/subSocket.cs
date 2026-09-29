using System.Text;

namespace EMCDeviceLibrary
{
    public class SubSocket : SockConnect
    {
        private ServerSocket server = new();
        public List<string> _lstCode = [];
        private byte StartByte = 0x02;
        private byte EndByte = 0x03;

        /// <summary>
        /// 开始侦听
        /// </summary>
        /// <returns>侦听是否正确开启</returns>
        public override bool Linsten(int port)
        {
            if (!server.Listen(port))
                return false;
            server.NewConnect += Server_NewConnect;
            return true;
        }

        /// <summary>
        /// 有新的连接请求
        /// </summary>
        /// <param name="client"></param>
        private void Server_NewConnect(System.Net.Sockets.Socket client)
        {
            Connect(client);
        }

        private List<byte> lstByte = new List<byte>();
        public override void dealMsg(byte[] Msg)
        {
            lstByte.AddRange(Msg);
            ShowLog("接收到字节序:" + Msg, 1);
            int startIndex = lstByte.IndexOf(StartByte);
            int endIndex = lstByte.IndexOf(EndByte, startIndex + 1);
            if (startIndex == -1 || endIndex == -1) return;
            byte[] validBytes = [.. lstByte.GetRange(startIndex + 1, endIndex - startIndex - 1)];
            lstByte.RemoveRange(0, endIndex + 1);
            Push(validBytes);
        }

        private string? _readstr;
        /// <summary>
        /// 接收到数据
        /// </summary>
        /// <param name="Msg"></param>
        public override void dealMsg(string Msg)
        {
            _readstr += Msg;
            if (_readstr == "") return;
            //_lstCode.Add(msg);
            if (_readstr.Contains("\r\n"))
            {
                string temp = _readstr.Split('\r')[0];
                Push(temp);
                _readstr = _readstr.Replace(temp + "\r\n", "");
            }
        }

        /// <summary>
        /// 拆分接收的套接字发送来的消息
        /// </summary>
        /// <param name="Msg">接收到的消息</param>
        /// <returns>拆分后的有效消息</returns>
        public override string MySplit(byte[] Msg)
        {
            string info = Encoding.UTF8.GetString(Msg);  //将byte[]转为字符串
            if (info == null || info == "") return "";
            string[] str = info.Split('[');         //以'['分割接收的信息
            if (str.Length < 2 || str[1] == "") return "";
            string[] arr = str[1].Split(']');       //arr[0]为拆分后的指令
            return arr.Length > 1 ? arr[0] : "";
        }

        /// <summary>
        /// 移除码列表中的指定元素
        /// </summary>
        /// <param name="str"></param>
        public void removeList(string str)
        {
            _lstCode.Remove(str);
        }

        /// <summary>
        /// 移除码列表中的首个元素
        /// </summary>
        public void RemoveList()
        {
            _lstCode.RemoveAt(0);
        }

        /// <summary>
        /// 重置列表中数据元素
        /// </summary>
        /// <param name="lst"></param>
        public void addList(List<string> lst)
        {
            _lstCode.Clear();
            _lstCode.AddRange(lst.ToArray());
        }

        public override void SendData(string value)
        {
            base.SendMsg("[" + value + "]");
        }

        /// <summary>
        /// 发送字节数组数据
        /// </summary>
        /// <param name="value"></param>
        public override void SendData(byte[] value)
        {
            byte[] sendbytes = new byte[value.Length + 2];
            sendbytes[0] = StartByte;
            Array.Copy(value, 0, sendbytes, 1, value.Length);
            sendbytes[sendbytes.Length - 1] = EndByte;
            base.SendMsg(sendbytes);
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        public override void Close()
        {
            base.Close();
            if (server != null)
            {
                server.Close();
                server = new ServerSocket();
            }
        }
    }
}
