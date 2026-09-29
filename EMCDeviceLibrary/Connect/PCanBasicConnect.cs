using ClassLibrary.Generic;
using Peak.Can.Basic.BackwardCompatibility;
using System.Text;
using TPCANHandle = System.UInt16;

namespace EMCDeviceLibrary
{
    public class PCanBasicConnect
    {
        public delegate void myInvoke(TPCANMsg msg);//定义一个自己的委托
        public event myInvoke? ReceiveMsg;            //转移信息事件
        private TPCANHandle m_PcanHandle = 81; // 根据实际使用的CAN接口选择
        private TPCANBaudrate baudRate = TPCANBaudrate.PCAN_BAUD_500K; // 设置波特率为500 kbps
        private System.Timers.Timer tmrRead = new System.Timers.Timer(1000);
        public bool m_IsConnected = false;

        /// <summary>
        /// 初始化连接
        /// </summary>
        /// <param name="para"></param>
        public void Connect(TPCANHandle _pcanHandle, TPCANBaudrate _baudRate)
        {
            m_PcanHandle = _pcanHandle;
            baudRate = _baudRate;

            TPCANStatus status = PCANBasic.Initialize(m_PcanHandle, baudRate);
            if (status != TPCANStatus.PCAN_ERROR_OK)
            {
                m_IsConnected = false;
                if (status != TPCANStatus.PCAN_ERROR_CAUTION)
                {
                    throw (new Exception($"{GenericParameter.Language.PCanInitializeFailedMsg}: {GetErrorText(status)}"));
                }
                else
                {
                    throw (new Exception(GenericParameter.Language.PCanBitrateFailedMsg));
                }
            }
            status = PCANBasic.FilterMessages(m_PcanHandle, 0, 2047, TPCANMode.PCAN_MODE_STANDARD);
            m_IsConnected = status == TPCANStatus.PCAN_ERROR_OK;
            tmrRead.AutoReset = false;
            tmrRead.Elapsed += (s, e) =>
            {
                try
                {
                    if (!m_IsConnected)
                        return;
                    ReadMessages();
                }
                finally
                {
                    tmrRead.Start();
                }
            };
            tmrRead.Start();
            SendData(0x400, [0XAA, 0X00, 0X00, 0X00, 0X00, 0X00, 0X00, 0X00, 0X00]);
        }

        #region 收

        /// <summary>
        /// 用于读取 PCAN-Basic 消息的函数
        /// </summary>
        private void ReadMessages()
        {
            TPCANStatus stsResult;

            // 至少会读取一次队列来查找消息。
            // 如果找到了一条消息，会再次查找，尝试找到更多消息。
            // 如果队列为空或发生错误，就跳出
            //do-while 语句。
            do
            {
                stsResult = ReadMessage();
                if (stsResult == TPCANStatus.PCAN_ERROR_ILLOPERATION)
                    break;
            } while ((!Convert.ToBoolean(stsResult & TPCANStatus.PCAN_ERROR_QRCVEMPTY)));
        }

        /// <summary>
        /// 用于在普通 CAN 设备上读取 CAN 消息的函数
        /// </summary>
        /// <returns>一个 TPCANStatus 错误代码</returns>
        private TPCANStatus ReadMessage()
        {
            TPCANMsg CANMsg;
            TPCANTimestamp CANTimeStamp;
            TPCANStatus stsResult;

            // 执行 PCANBasic 的 “读取” 功能                      
            stsResult = PCANBasic.Read(m_PcanHandle, out CANMsg, out CANTimeStamp);
            if (stsResult != TPCANStatus.PCAN_ERROR_QRCVEMPTY && CANMsg.LEN > 0)
            {   //推送收到的消息
                Task.Run(() =>
                {
                    ReceiveMsg?.Invoke(CANMsg);
                });
            }
            return stsResult;
        }

        ///// <summary>
        ///// 处理收到的消息之后推送
        ///// </summary>
        ///// <param name="theMsg">The received PCAN-Basic message</param>
        ///// <returns>True if the message must be created, false if it must be modified</returns>
        //private void ProcessMessage(TPCANMsg theMsg, TPCANTimestamp itsTimeStamp)
        //{
        //    TPCANMsgFD newMsg;
        //    TPCANTimestampFD newTimestamp;

        //    newMsg = new TPCANMsgFD
        //    {
        //        DATA = new byte[64],
        //        ID = theMsg.ID,
        //        DLC = theMsg.LEN
        //    };
        //    for (int i = 0; i < ((theMsg.LEN > 8) ? 8 : theMsg.LEN); i++)
        //        newMsg.DATA[i] = theMsg.DATA[i];
        //    newMsg.MSGTYPE = theMsg.MSGTYPE;

        //    Task.Run(() =>
        //    {
        //        Push(newMsg.DATA);
        //    });

        //    newTimestamp = itsTimeStamp.micros + (1000UL * itsTimeStamp.millis) + (0x100_000_000UL * 1000UL * itsTimeStamp.millis_overflow);
        //    //ProcessMessage(newMsg, newTimestamp);
        //}
        #endregion

        /// <summary>
        /// 发送从上位机接收到的数据
        /// </summary>
        /// <param name="bytes"></param>
        public void SendData(uint address, byte[] bytes)
        {
            TPCANStatus stsResult;
            stsResult = WriteFrame(address, bytes);
            if (stsResult != TPCANStatus.PCAN_ERROR_OK)
            {
                throw (new Exception(GetFormatedError(stsResult)));
            }
        }

        /// <summary>
        /// 以指定的数据字节向配置的硬件接口发送CAN数据帧。
        /// </summary>
        /// <remarks>CAN帧以标准消息类型和8 
        ///字节的固定数据长度发送。确保在调用此方法之前正确初始化硬件接口。</remarks>
        /// <param name="datas">包含要在CAN帧中传输的数据有效载荷的字节数组。数组必须包含至少8个元素；</param>
        /// <returns>一个TPCANStatus值，表示写操作的结果。返回TPCANStatus。PCAN_错误_如果
        ///帧发送成功，则返回OK；否则，返回描述失败的错误代码。</returns>
        private TPCANStatus WriteFrame(uint address, byte[] datas)
        {
            TPCANMsg CANMsg;

            // We create a TPCANMsg message structure 
            //
            CANMsg = new TPCANMsg();
            CANMsg.DATA = new byte[8];

            // 配置消息。其包含 ID、数据长度、消息类型以及数据
            CANMsg.ID = address;
            CANMsg.LEN = 8;
            CANMsg.MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD;
            // 如果要发送远程帧，数据字节并不重要.
            //
            // 获取数据并填充到消息结构中
            //
            for (int i = 0; i < GetLengthFromDLC(CANMsg.LEN, true); i++)
            {
                CANMsg.DATA[i] = datas[i];
            }
            // 消息发送到已配置的硬件
            //
            return PCANBasic.Write(m_PcanHandle, ref CANMsg);
        }

        /// <summary>
        /// Convert a CAN DLC value into the actual data length of the CAN/CAN-FD frame.
        /// </summary>
        /// <param name="dlc">A value between 0 and 15 (CAN and FD DLC range)</param>
        /// <param name="isSTD">A value indicating if the msg is a standard CAN (FD Flag not checked)</param>
        /// <returns>The length represented by the DLC</returns>
        public static int GetLengthFromDLC(int dlc, bool isSTD)
        {
            if (dlc <= 8)
                return dlc;

            if (isSTD)
                return 8;

            switch (dlc)
            {
                case 9: return 12;
                case 10: return 16;
                case 11: return 20;
                case 12: return 24;
                case 13: return 32;
                case 14: return 48;
                case 15: return 64;
                default: return dlc;
            }
        }


        /// <summary>
        /// 获取异常信息
        /// </summary>
        /// <param name="error"></param>
        /// <returns></returns>
        private string GetErrorText(TPCANStatus error)
        {
            StringBuilder buffer = new(256);
            uint bufferLength = (uint)buffer.Capacity;
            PCANBasic.GetValue(m_PcanHandle, TPCANParameter.PCAN_LOG_TEXT, buffer, bufferLength);
            return buffer.ToString();
        }

        /// <summary>
        /// 帮助函数用于将异常以文本形式获取
        /// </summary>
        /// <param name="error">待翻译的异常代码</param>
        /// <returns>代码对应异常的文本</returns>
        private string GetFormatedError(TPCANStatus error)
        {
            StringBuilder strTemp;

            // 创建一个足够大的缓冲区来容纳错误文本        
            strTemp = new StringBuilder(256);
            // 使用 GetErrorText API 函数获取文本
            // 如果函数成功，将返回经过转换的错误。如果失败，
            // 返回一段描述当前错误的文本。
            return PCANBasic.GetErrorText(error, 0, strTemp) != TPCANStatus.PCAN_ERROR_OK
                ? $"{GenericParameter.Language.PCanErrorMsg}:0x{error:X}"
                : strTemp.ToString();
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Close()
        {
            PCANBasic.Uninitialize(m_PcanHandle);
            tmrRead.Enabled = false;
        }

        public PCanBasicConnect()
        { }
    }
}
