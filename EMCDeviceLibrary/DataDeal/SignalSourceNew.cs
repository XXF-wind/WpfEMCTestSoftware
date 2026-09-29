
namespace EMCDeviceLibrary
{
    /// <summary>
    /// 8G～18G信号源数据处理
    /// </summary>
    internal class SignalSourceNew : SendRecDeal
    {
        /// <summary>
        /// 8G～18G信号源
        /// </summary>
        public SignalSourceNew()
        {
        }

        #region 读取处理
        /// <summary>
        /// 8G～18G信号源接收消息处理
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public override List<string> DealRecMsg(Communication comm, byte[] bytes)
        {
            List<string> lst = [];
            switch (comm)
            {
                //case Communication.Query0:
                //    lst = Query0Deal(bytes);
                //    break;
                default:
                    break;
            }
            return lst;
        }

        ///// <summary>
        ///// 查询0处理
        ///// </summary>
        ///// <param name="bytes"></param>
        ///// <param name="msg"></param>
        ///// <returns></returns>
        //private static List<string> Query0Deal(byte[] bytes)
        //{
        //    List<string> strings = [];
        //    if (bytes.Length == 20)
        //    {
        //        byte[] bfreq = new byte[4];
        //        byte[] bpulse = new byte[4];
        //        byte[] bCycle = new byte[4];
        //        Array.Copy(bytes, 3, bfreq, 0, 4);
        //        Array.Copy(bytes, 8, bCycle, 0, 4);
        //        Array.Copy(bytes, 12, bpulse, 0, 4);
        //        //频率
        //        string frequency = (((bfreq[0] << 24) | (bfreq[1] << 16) | (bfreq[2] << 8) | bfreq[3]) * 0.001).ToString();
        //        //衰减
        //        string att = (bytes[7] * 0.5).ToString();
        //        //周期
        //        string HighCycle = (((bCycle[0] << 24) | (bCycle[1] << 16) | (bCycle[2] << 8 | bCycle[3])) * 0.1).ToString();
        //        //脉宽
        //        string pulsewidth = ((bpulse[0] << 24 | bpulse[1] << 16 | bpulse[2] << 8 | bpulse[3]) * 0.1).ToString();
        //        //工作模式
        //        string model = bytes[16].ToString();
        //        //放大器开关
        //        string AmplifierSwitch = bytes[17].ToString();
        //        strings = [Communication.Query0.ToString(), frequency, att, HighCycle, pulsewidth, model, AmplifierSwitch];
        //    }

        //    return strings;
        //}
        #endregion

        #region 发送处理
        /// <summary>
        /// 8G～18G信号源发送消息处理
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="comm"></param>
        /// <returns></returns>
        public override byte[] DealSendMsg(List<string> msg, Communication comm, out bool isset)
        {
            isset = false;
            if (msg.Count < 1)
            {
                return [];
            }
            byte[] bytes = [];
            byte address = Convert.ToByte(msg[0]);
            switch (comm)
            {
                case Communication.SPEED:               //设置扫频速度
                    byte speed = Convert.ToByte(msg[1]);
                    bytes = [0xF5, address, 0x07, speed, 0x00, 0x00];
                    isset = true;
                    break;
                case Communication.FREQUENCY:           //设置工作频率
                    int freq = Convert.ToInt32(msg[1]) * 1000;
                    bytes = [0xF5, address, 0x0A, (byte)((freq >> 24)), (byte)((freq >> 16)), (byte)((freq >> 8)), (byte)freq];
                    isset = true;
                    break;
                case Communication.ATTENUATION:         //设置衰减
                    byte att = (byte)(Convert.ToInt16(msg[1]) * 2);
                    bytes = [0xF5, address, 0x0B, att, 0x00, 0x00];
                    isset = true;
                    break;
                case Communication.PERIODVALUE:       //设置周期值0.1us
                    int period = Convert.ToInt32(msg[1]) * 10;
                    bytes = [0xF5, address, 0x0C, (byte)((period >> 24) & 0xFF), (byte)((period >> 16) & 0xFF), (byte)((period >> 8) & 0xFF), (byte)(period & 0xFF)];
                    isset = true;
                    break;
                case Communication.PULSEWIDTH:          //设置脉宽0.1us
                    int pulseW = Convert.ToInt32(msg[1]) * 10;
                    bytes = [0xF5, address, 0x0D, (byte)((pulseW >> 24) & 0xFF), (byte)((pulseW >> 16) & 0xFF), (byte)((pulseW >> 8) & 0xFF), (byte)(pulseW & 0xFF)];
                    isset = true;
                    break;
                case Communication.WORKINGMODE:         //设置工作模式
                    byte mode = (byte)(msg[1] == "CW" ? 0x01 : 0x00);
                    bytes = [0xF5, address, 0x0E, mode, 0x00, 0x00];
                    isset = true;
                    break;
                case Communication.SWITCHINGTIME:      //设置放大器开关
                    byte _switch = (byte)(msg[1] == "ON" ? 0x01 : 0x00);
                    bytes = [0xF5, address, 0x0F, _switch, 0x00, 0x00];
                    isset = true;
                    break;
            }
            return bytes;
        }
        #endregion
    }
}
