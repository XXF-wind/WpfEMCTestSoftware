using System.Diagnostics;

namespace EMCDeviceLibrary
{
    public class EP600 : SockConnect
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
                SendData("#00e 1200*");
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
        /// 从字节数组中解析浮点数值并计算场强（转换自parseByteValue方法）
        /// </summary>
        /// <param name="bytArry">输入的字节数组</param>
        /// <returns>解析并开方后的double数值，失败返回0.0</returns>
        public double ParseByteValue(byte[] bytArry)
        {
            double data = 0.0; // 对应原C++的qreal data = 0.0;

            // 校验字节数组有效性（原逻辑：长度≥5，补充null判断避免空引用）
            if (bytArry == null || bytArry.Length < 5)
            {
                return data;
            }

            // 查找标志位0x54的索引（对应原C++的bytArry.indexOf(0x54)）
            int flagBit = Array.IndexOf(bytArry, (byte)0x54);

            // 校验标志位有效性及后续字节是否充足
            if (flagBit == -1 || (flagBit + 4) >= bytArry.Length)
            {
                return data;
            }

            // 对应原C++的memcpy：从flagBit+1开始拷贝4字节（sizeof(float)）转换为float
            float fieldStrengthSquared;
            try
            {
                // 提取4字节数据（float固定为4字节）
                byte[] floatBytes = new byte[4];
                Buffer.BlockCopy(bytArry, flagBit + 1, floatBytes, 0, 4);

                // 转换为单精度浮点数（对应原C++的float类型）
                fieldStrengthSquared = BitConverter.ToSingle(floatBytes, 0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to convert bytes to float: {ex.Message}");
                return 0.0;
            }

            // 数据验证：判断NaN和无穷大（对应原C++的std::isnan和std::isinf）
            if (float.IsNaN(fieldStrengthSquared) || float.IsInfinity(fieldStrengthSquared))
            {
                Debug.WriteLine("Invalid float value"); // 对应原C++的qDebug输出
                return 0.0;
            }

            // 处理负值：取绝对值（对应原C++的std::abs，C#使用Math.Abs）
            if (fieldStrengthSquared < 0)
            {
                Debug.WriteLine("Negative value, taking absolute value");
                fieldStrengthSquared = Math.Abs(fieldStrengthSquared);
            }

            // 计算场强：开平方（对应原C++的sqrt，C#使用Math.Sqrt）
            data = Math.Sqrt(fieldStrengthSquared);

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

        public EP600()
        {
        }
    }
}
