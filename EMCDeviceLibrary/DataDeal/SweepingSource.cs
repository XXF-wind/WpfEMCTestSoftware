
namespace EMCDeviceLibrary
{
    /// <summary>
    /// 扫频信号源数据处理
    /// </summary>
    internal class SweepingSource : SendRecDeal
    {
        /// <summary>
        /// 扫频信号源
        /// </summary>
        public SweepingSource()
        {
        }

        #region 读取处理
        /// <summary>
        /// 扫频信号源接收消息处理
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public override List<string> DealRecMsg(Communication comm, byte[] bytes)
        {
            List<string> lst = [];

            return lst;
        }

        ///// <summary>
        ///// 查询2处理
        ///// </summary>
        ///// <param name="bytes"></param>
        ///// <returns></returns>
        //private static List<string> Query2Deal(byte[] bytes)
        //{
        //    List<string> lst;
        //    byte[] bMinFreq = new byte[4];
        //    byte[] bMaxFreq = new byte[4];
        //    byte[] bMaxAtt = new byte[2];
        //    byte[] bStep = new byte[2];
        //    byte[] bPower = new byte[2];
        //    Array.Copy(bytes, 3, bMinFreq, 0, 4);
        //    Array.Copy(bytes, 7, bMaxFreq, 0, 4);
        //    Array.Copy(bytes, 11, bMaxAtt, 0, 2);
        //    Array.Copy(bytes, 13, bStep, 0, 2);
        //    Array.Copy(bytes, 16, bPower, 0, 2);
        //    //信号源最小频率值
        //    string minFreq = ((bMinFreq[0] << 24 | bMinFreq[1] << 16 | bMinFreq[2] << 8 | bMinFreq[3]) * 0.001).ToString();
        //    //信号源最大频率值
        //    string maxFreq = ((bMaxFreq[0] << 24 | bMaxFreq[1] << 16 | bMaxFreq[2] << 8 | bMaxFreq[3]) * 0.001).ToString();
        //    //衰减器最大衰减值
        //    string maxAtt = ((bMaxAtt[0] << 8 | bMaxAtt[1]) * 0.5).ToString();
        //    //衰减步进值
        //    string step = (bStep[0] << 8 | bStep[1]).ToString();
        //    //符号位
        //    string signBit = bytes[15].ToString();
        //    //功率值
        //    string power = ((bPower[0] << 8 | bPower[1]) * 0.01).ToString();
        //    lst = [Communication.Query2.ToString(), minFreq, maxFreq, maxAtt, step, signBit, power];
        //    return lst;
        //}

        ///// <summary>
        ///// 查询1处理
        ///// </summary>
        ///// <param name="bytes"></param>
        ///// <returns></returns>
        //private static List<string> Query1Deal(byte[] bytes)
        //{
        //    List<string> lst;
        //    byte[] bStart = new byte[4];
        //    byte[] bStop = new byte[4];
        //    byte[] bStay = new byte[2];
        //    byte[] bOpen = new byte[2];
        //    byte[] bClose = new byte[2];
        //    Array.Copy(bytes, 3, bStart, 0, 4);
        //    Array.Copy(bytes, 7, bStop, 0, 4);
        //    Array.Copy(bytes, 11, bStay, 0, 2);
        //    Array.Copy(bytes, 13, bOpen, 0, 2);
        //    Array.Copy(bytes, 15, bClose, 0, 2);
        //    //扫频起始频率值
        //    string start = ((bStart[0] << 24 | bStart[1] << 16 | bStart[2] << 8 | bStart[3]) * 0.001).ToString();
        //    //扫频终止频率值
        //    string stop = ((bStop[0] << 24 | bStop[1] << 16 | bStop[2] << 8 | bStop[3]) * 0.001).ToString();
        //    //扫频驻留时间
        //    string stay = (bStay[0] << 8 | bStay[1]).ToString();
        //    //扫频开启时间
        //    string open = ((bOpen[0] << 8 | bOpen[1]) * 0.001).ToString();
        //    //扫频关闭时间
        //    string close = ((bClose[0] << 8 | bClose[1]) * 0.001).ToString();
        //    string unit = bytes[17].ToString();
        //    lst = [Communication.Query1.ToString(), start, stop, stay, open, close, unit];
        //    return lst;
        //}

        ///// <summary>
        ///// 读取校验处理
        ///// </summary>
        ///// <param name="bytes"></param>
        ///// <returns></returns>
        //private static List<string> ReadCalibrationDeal(byte[] bytes)
        //{
        //    List<string> lst;
        //    //频段，高位字节在后
        //    string Band = (bytes[4] << 8 | bytes[3]).ToString();
        //    lst = [Communication.readCalibration.ToString(), Band];
        //    byte[] bs = new byte[256];
        //    Array.Copy(bytes, 5, bs, 0, 256);
        //    for (int i = 0; i < bs.Length; i += 4)
        //    {
        //        byte[] b = new byte[4];
        //        Array.Copy(bs, i, b, 0, 4);
        //        string s = (b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]).ToString();
        //        lst.Add(s);
        //    }
        //    return lst;
        //}

        ///// <summary>
        ///// 查询0处理
        ///// </summary>
        ///// <param name="bytes"></param>
        ///// <param name="msg"></param>
        ///// <returns></returns>
        //private static List<string> query0Deal(byte[] bytes)
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
        //        //衰减量
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
        /// 扫频信号源发送消息处理
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
                case Communication.SPEED:               //设置扫频速度
                    byte speed = Convert.ToByte(msg[1], 16);
                    bytes = [0xF5, address, 0x07, speed, 0x00, 0x00, 0x00];
                    isset = true;
                    break;
                case Communication.OUTPUTPOWER:         //设置输出功率
                    int power = (int)Math.Abs(Convert.ToDecimal(msg[1]) * 100);
                    bytes = [0xF5, address, 0x08, (byte)(msg[1].Contains('-') ? 0x01 : 0x00), (byte)((power >> 8) & 0xFF), (byte)(power & 0xFF), 0x00];
                    isset = true;
                    break;
                case Communication.FREQUENCY:           //设置工作频率
                    int freq = (int)(Convert.ToDecimal(msg[1]) * 1000);
                    bytes = [0xF5, address, 0x0A, (byte)((freq >> 24) & 0xFF), (byte)((freq >> 16) & 0xFF), (byte)((freq >> 8) & 0xFF), (byte)(freq & 0xFF)];
                    isset = true;
                    break;
                case Communication.ATTENUATION:         //设置衰减
                    byte att = (byte)(Convert.ToDecimal(msg[1]) * 2);
                    bytes = [0xF5, address, 0x0B, att, 0x00, 0x00, 0x00];
                    isset = true;
                    break;
                case Communication.PERIODVALUE:       //设置周期值0.1us
                    int period = (int)(Convert.ToDecimal(msg[1]) * 10);
                    bytes = [0xF5, address, 0x0C, (byte)((period >> 24) & 0xFF), (byte)((period >> 16) & 0xFF), (byte)((period >> 8) & 0xFF), (byte)(period & 0xFF)];
                    isset = true;
                    break;
                case Communication.PULSEWIDTH:          //设置脉宽0.1us
                    int pulseW = (int)(Convert.ToDecimal(msg[1]) * 10);
                    bytes = [0xF5, address, 0x0D, (byte)((pulseW >> 24) & 0xFF), (byte)((pulseW >> 16) & 0xFF), (byte)((pulseW >> 8) & 0xFF), (byte)(pulseW & 0xFF)];
                    isset = true;
                    break;
                case Communication.WORKINGMODE:         //设置工作模式
                    byte mode = (byte)(msg[1] == "CW" ? 0x01 : 0x00);
                    bytes = [0xF5, address, 0x0E, mode, 0x00, 0x00, 0x00];
                    isset = true;
                    break;
                case Communication.SWITCHINGTIME:      //设置从机开关时间
                    int Starttime = Convert.ToInt32(msg[1]);
                    int Stoptime = Convert.ToInt32(msg[2]);
                    bytes = [0xF5, address, 0x0F, (byte)((Starttime >> 8) & 0xFF), (byte)(Starttime & 0xFF), (byte)((Stoptime >> 8) & 0xFF), (byte)(Stoptime & 0xFF)];
                    isset = true;
                    break;
                case Communication.STARTFREQ:          //设置起始频率
                    int startF = (int)(Convert.ToDecimal(msg[1]) * 1000);
                    bytes = [0xF5, address, 0x1A, (byte)((startF >> 24) & 0xFF), (byte)((startF >> 16) & 0xFF), (byte)((startF >> 8) & 0xFF), (byte)(startF & 0xFF)];
                    isset = true;
                    break;
                case Communication.STOPFREQ:          //设置终止频率
                    int stopF = (int)(Convert.ToDecimal(msg[1]) * 1000);
                    bytes = [0xF5, address, 0x2A, (byte)((stopF >> 24) & 0xFF), (byte)((stopF >> 16) & 0xFF), (byte)((stopF >> 8) & 0xFF), (byte)(stopF & 0xFF)];
                    isset = true;
                    break;
                case Communication.DWELLTIME:          //设置扫频频率驻留时间
                    int dwellT = Convert.ToInt32(msg[1]);
                    bytes = [0xF5, address, 0x1B, (byte)((dwellT >> 8) & 0xFF), (byte)(dwellT & 0xFF), 0x00, 0x00];
                    isset = true;
                    break;
                case Communication.CALIBRATION:    //设置校验数据
                    int BandNo = Convert.ToInt32(msg[1]);
                    List<byte> lstb = [0xF5, address, 0x5A, (byte)(BandNo & 0xFF), (byte)((BandNo >> 8) & 0xFF)];
                    for (int i = 2; i < msg.Count; i++)
                    {
                        int v = Convert.ToInt32(msg[i]);
                        lstb.Add((byte)((v >> 24) & 0xFF));
                        lstb.Add((byte)((v >> 16) & 0xFF));
                        lstb.Add((byte)((v >> 8) & 0xFF));
                        lstb.Add((byte)(v & 0xFF));
                    }
                    bytes = [.. lstb];
                    isset = true;
                    break;
                //case Communication.readCalibration: //读取校验数据
                //    BandNo = Convert.ToInt32(msg[1]);
                //    bytes = [0xF5, address, 0x5B, (byte)(BandNo & 0xFF), (byte)((BandNo >> 8) & 0xFF), 0x00, 0x00];
                //    break;
                //case Communication.Query0:
                //    bytes = [0xF5, address, 0xAA, 0x00, 0x00, 0x00, 0x00];
                //    break;
                //case Communication.Query1:
                //    bytes = [0xF5, address, 0xAA, 0x01, 0x00, 0x00, 0x00];
                //    break;
                //case Communication.Query2:
                //    bytes = [0xF5, address, 0xAA, 0x02, 0x00, 0x00, 0x00];
                //    break;
                default:
                    break;
            }
            return bytes;
        }
        #endregion
    }
}
