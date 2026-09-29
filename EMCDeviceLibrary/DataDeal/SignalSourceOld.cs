
namespace EMCDeviceLibrary
{
    /// <summary>
    /// 100M～6G信号源数据处理
    /// </summary>
    internal class SignalSourceOld : SendRecDeal
    {
        /// <summary>
        /// 100M～6G信号源
        /// </summary>
        public SignalSourceOld()
        {
        }

        #region 读取处理
        /// <summary>
        /// 100M～6G信号源接收消息处理
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
        //        byte[] bfreq = new byte[3];
        //        byte[] bpulse = new byte[3];
        //        byte[] bCycle = new byte[3];
        //        Array.Copy(bytes, 3, bfreq, 0, 3);
        //        Array.Copy(bytes, 7, bCycle, 0, 3);
        //        Array.Copy(bytes, 10, bpulse, 0, 3);
        //        //频率
        //        string frequency = (((bfreq[0] << 16) | (bfreq[1] << 8) | bfreq[2])*0.001).ToString();
        //        //衰减
        //        string att = (bytes[6] * 0.5).ToString();
        //        //周期
        //        string HighCycle = ((bCycle[0] << 16 | bCycle[1] << 8 | bCycle[2]) * 0.1).ToString();
        //        //脉宽
        //        string pulsewidth = ((bpulse[0] << 16 | bpulse[1] << 8 | bpulse[2]) * 0.1).ToString();
        //        //工作模式
        //        string model = bytes[13].ToString();
        //        //放大器开关
        //        string AmplifierSwitch = bytes[14].ToString();
        //        strings = [Communication.Query0.ToString(), frequency, att, HighCycle, pulsewidth, model, AmplifierSwitch];
        //    }

        //    return strings;
        //}
        #endregion

        #region 发送处理
        /// <summary>
        /// 100M～6G信号源发送消息处理
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
            byte address = Convert.ToByte(msg[0], 16);
            switch (comm)
            {
                case Communication.SPEED:               //设置通讯速率
                    byte speed = Convert.ToByte(msg[1], 16);
                    bytes = [0xF5, address, 0x07, speed, 0x00, 0x00];
                    isset = true;
                    break;
                case Communication.FREQUENCY:           //设置工作频率
                    int freq = Convert.ToInt32(msg[1]) * 1000;
                    bytes = [0xF5, address, 0x0A, (byte)((freq >> 16) & 0xFF), (byte)((freq >> 8) & 0xFF), (byte)(freq & 0xFF)];
                    isset = true;
                    break;
                case Communication.ATTENUATION:         //设置衰减
                    byte att = (byte)(Convert.ToInt16(msg[1], 16) * 2);
                    bytes = [0xF5, address, 0x0B, att, 0x00, 0x00];
                    isset = true;
                    break;
                case Communication.PERIODVALUE:       //设置周期值1us
                    int period = Convert.ToInt32(msg[1]);
                    bytes = [0xF5, address, 0x0C, (byte)((period >> 16) & 0xFF), (byte)((period >> 8) & 0xFF), (byte)(period & 0xFF)];
                    isset = true;
                    break;
                case Communication.PULSEWIDTH:          //设置脉宽1us
                    int pulseW = Convert.ToInt32(msg[1]);
                    bytes = [0xF5, address, 0x0D, (byte)((pulseW >> 16) & 0xFF), (byte)((pulseW >> 8) & 0xFF), (byte)(pulseW & 0xFF)];
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
                case Communication.SINGLESWITCH:      //设置单路功放开关
                    byte s = Convert.ToByte(msg[1], 2);
                    bytes = [0xF5, address, 0x09, s, 0x00, 0x00];
                    isset = true;
                    break;
                    //case Communication.Query0:
                    //    bytes = [0xF5, address, 0xAA, 0x00, 0x00, 0x00];
                    //    break;
            }
            return bytes;
        }
        #endregion
    }
}
