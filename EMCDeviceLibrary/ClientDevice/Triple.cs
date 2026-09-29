using System.Text;

namespace EMCDeviceLibrary
{
    /// <summary>
    /// 三频段功放
    /// </summary>
    public class Triple : SockConnect
    {
        /// <summary>
        /// 初始化功放连接
        /// </summary>
        /// <param name="Device"></param>
        public override void InitConnection(object Device)
        {
            try
            {
                base.InitConnection(Device);
            }
            catch (Exception)
            {
                Initialed = false;
            }
        }

        /// <summary>
        /// 接收到消息处理
        /// </summary>
        /// <param name="bytes">接收命令</param>
        public override void dealMsg(byte[] bytes)
        {
            if (bytes.Length > 0)
            {
                if (bytes[0] == 0xF5)
                {

                    Push(bytes);
                }
                else
                {
                    var result = bytes.Select((b, i) => new { Byte = b, Index = i })
                           .FirstOrDefault(x => x.Byte == 0xF5);

                    if (result?.Index > 0)
                    {
                        //读取的有效数据字节序
                        byte[] bs = new byte[bytes.Length - result.Index];
                        Array.Copy(bytes, result.Index, bs, 0, bs.Length);

                        //读取的提示消息字节序
                        byte[] pushBs = new byte[result.Index];
                        Array.Copy(bytes, 0, pushBs, 0, pushBs.Length);
                        Push(bs);
                        Push("收：" + Encoding.ASCII.GetString(pushBs));
                    }
                    else
                    {
                        Push("收：" + Encoding.ASCII.GetString(bytes));
                    }
                }
            }
        }

        /// <summary>
        /// 发送查询指令
        /// </summary>
        /// <param name="address"></param>
        /// <param name="command"></param>
        public override void SendData(byte address, byte command)
        {
            try
            {
                byte[] cmd =
                [
                    0xF5, address,command,0x00,0x00,0x00,0x00,0x00,0x00,
                ];
                CompletionCommand(cmd);
                SendMsg(cmd);
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// 发送指令
        /// </summary>
        /// <param name="data"></param>
        public override void SendData(byte address, byte command, byte[] bytes)
        {
            try
            {
                int len = bytes.Length + 5;
                byte[] cmdHead =
                [
                    0xF5, address,command
                ];
                byte[] cmds = new byte[len];
                //组合字节数组
                Array.Copy(cmdHead, 0, cmds, 0, cmdHead.Length);
                Array.Copy(bytes, 0, cmds, cmdHead.Length, bytes.Length);

                CompletionCommand(cmds);
                SendMsg(cmds);
                if (command != 0xAA)
                    Push("发：" + BitConverter.ToString(cmds));
            }
            catch
            {
                throw;
            }
        }

        public override void SendData(string value)
        {
            string str = value + "\n";
            SendMsg(str);
            Push("发：" + str);
        }

        /// <summary>
        /// 拼接指令，将前部指令相加之和放入后两位
        /// </summary>
        /// <param name="byteArray"></param>
        private void CompletionCommand(byte[] byteArray)
        {
            // 计算前七位的累加和
            byte sumHigh = 0;
            byte sumLow = 0;
            int len = byteArray.Length;
            foreach (byte b in byteArray.Take(len - 2))
            {
                int tempSum = sumLow + b;
                sumLow = (byte)(tempSum & 0xFF);
                sumHigh += (byte)(tempSum >> 8);
            }

            // 将累加和放入后两位
            byteArray[len - 2] = sumHigh;
            byteArray[len - 1] = sumLow;
        }

        /// <summary>
        /// 
        /// </summary>
        public override void ClearData()
        {
            try
            {
                //if (socket != null)
                //{
                //    SendMsg("bufferclear\r\n");
                //}
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


        public Triple(string name)
        {
            this.Name = name;
        }
    }
}
