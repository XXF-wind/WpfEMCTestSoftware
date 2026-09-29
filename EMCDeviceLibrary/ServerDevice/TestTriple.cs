namespace EMCDeviceLibrary
{
    internal class TestTriple : SubSocket
    {
        public TestTriple()
        {
            Linsten(5088);
        }

        public override void dealMsg(byte[] bytes)
        {
            if (bytes.Length > 0)
            {
                if (bytes[0] == 0xF5)
                {
                    Task.Run(() =>
                    {
                        Push(bytes);
                    });
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
            }
            catch
            {
                throw;
            }
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
    }
}
