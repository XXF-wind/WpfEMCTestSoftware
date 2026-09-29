namespace EMCDeviceLibrary
{
    public class EP408 : SockConnect
    {
        /// <summary>
        /// 设置频率命令
        /// </summary>
        private readonly string setFreqCmd = "#00k";
        /// <summary>
        /// 查询命令
        /// </summary>
        private readonly string queryFieldCmds = "#00?T*";
        /// <summary>
        /// 初始化连接
        /// </summary>
        /// <param name="Device"></param>
        public override void InitConnection(object Device)
        {
            base.InitConnection(Device);
            if (Status())
            {
                SendData("#00w4*");   //设置40hz滤波,延时250ms 80hz 150ms
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
                double value = ExtractValue(recData.Trim());
                Task.Run(() =>
                {
                    Push(value.ToString());
                });
            }
        }

        /// <summary>
        /// 读取信息
        /// </summary>
        /// <returns></returns>
        public override string? ReadData()
        {
            if (CommunicationType == CommunicationMode.VISA)
            {
                if (visa != null)
                {
                    string result = visa.ReadResponse();
                    return ExtractValue(result).ToString();
                }
            }
            return "";
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
        /// 批量发送
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

        /// <summary>
        /// 从输入字符串中提取指定格式的数值（转换自原C++逻辑）
        /// </summary>
        /// <param name="xmlData">输入字符串</param>
        /// <returns>提取并转换后的double数值，失败返回0.0</returns>
        public double ExtractValue(string xmlData)
        {
            // 对应原C++的xmlData.contains("#")&&xmlData.contains("*")
            // 注：C# string.Contains区分大小写，与Qt QString::contains默认行为一致
            if (xmlData != null && xmlData.Contains('#') && xmlData.Contains('*'))
            {
                // 对应原C++的int posA = xmlData.indexOf("#");
                int posA = xmlData.IndexOf('#');
                // 对应原C++的int posB = xmlData.indexOf("*");
                int posB = xmlData.IndexOf('*');

                // 对应原C++的if(posB>posA)
                if (posB > posA)
                {
                    // 对应原C++的xmlData.mid(posA+1,posB-posA-1)
                    // 说明：Qt mid(起始索引, 截取长度) ≡ C# Substring(起始索引, 截取长度)
                    string targetSubStr = xmlData.Substring(posA + 1, posB - posA - 1);

                    // 对应原C++的toDouble()，使用TryParse提高安全性，避免转换异常
                    if (double.TryParse(targetSubStr, out double result))
                    {
                        return result;
                    }
                    else
                    {
                        return 0.0;
                    }
                }
                else
                {
                    return 0.0;
                }
            }
            else
            {
                return 0.0;
            }
        }

        /// <summary>
        /// 从字节数组中解析数值（转换自parseByteValue方法）
        /// </summary>
        /// <param name="bytArry">输入的字节数组</param>
        /// <returns>解析并开方后的double数值，失败返回0.0</returns>
        public double ParseByteValue(byte[] bytArry)
        {
            double data = 0.0; // 对应原C++的qreal data = 0.0;

            // 对应原C++的if(bytArry.size()<5)
            if (bytArry == null || bytArry.Length < 5)
            {
                return data; // 字节数不匹配或数组为空，返回0.0
            }

            // 对应原C++的int flagBit = bytArry.indexOf(0x54);
            int flagBit = Array.IndexOf(bytArry, (byte)0x54); // 查找标志位0x54的索引

            // 对应原C++的if(flagBit == -1 || (flagBit + 4) >= bytArry.size())
            if (flagBit == -1 || (flagBit + 4) >= bytArry.Length)
            {
                return data; // 未找到标志位或后续字节不足，返回0.0
            }

            // 对应原C++的qint32 unitData = 字节按内存顺序组合（小端序，低位在前）
            int unitData = bytArry[flagBit + 1]
                           + (bytArry[flagBit + 2] << 8)
                           + (bytArry[flagBit + 3] << 16)
                           + (bytArry[flagBit + 4] << 24);

            // 对应原C++的memcpy(&data,&unitData,sizeof (unitData));
            // 说明：将int类型的unitData内存数据拷贝到double类型的data，C#使用BitConverter实现字节转换
            byte[] unitDataBytes = BitConverter.GetBytes(unitData);
            // 若系统字节序与原C++环境不一致，可在此调整字节序（如unitDataBytes = unitDataBytes.Reverse().ToArray()）
            if (unitDataBytes.Length >= sizeof(double))
            {
                // 拷贝4字节（int长度）到8字节（double长度），剩余字节补0（与memcpy行为一致）
                byte[] dataBytes = new byte[sizeof(double)];
                Buffer.BlockCopy(unitDataBytes, 0, dataBytes, 0, unitDataBytes.Length);
                data = BitConverter.ToDouble(dataBytes, 0);
            }
            else
            {
                // 异常情况：int字节数组长度不足，直接返回0.0
                return 0.0;
            }

            // 对应原C++的data = sqrt(static_cast<qreal>(data));
            data = Math.Sqrt(data);

            return data;
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

        public EP408()
        {
        }
    }
}
