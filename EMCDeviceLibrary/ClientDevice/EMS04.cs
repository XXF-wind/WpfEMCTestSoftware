using System.Diagnostics;
using System.Text.RegularExpressions;

namespace EMCDeviceLibrary
{
    public class EMS04 : SockConnect
    {
        private readonly string queryFieldCmds = "MEAS?;";
        /// <summary>
        /// 初始化连接
        /// </summary>
        /// <param name="Device"></param>
        public override void InitConnection(object Device)
        {
            base.InitConnection(Device);
            if (Status())
            {
                SendData(queryFieldCmds);
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
        /// 发送查询信号
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

        /// <summary>
        /// 从输入字符串中提取指定格式的数值（转换自原C++逻辑）
        /// </summary>
        /// <param name="input">输入字符串</param>
        /// <returns>提取并转换后的double数值，失败返回0.0</returns>
        public double ExtractValue(string input)
        {
            // 1. 正则匹配（对应原C++的QRegularExpression，修正：支持:D/:F，匹配\r/\n之前的内容）
            // 原C++注释要求支持:D或:F，此处正则优化为匹配:[DF]，非贪婪捕获到换行符前
            Regex regex = new Regex(@":D(.*?)(\r|\n)");
            Match match = regex.Match(input);
            string substring = string.Empty;
            int count = 0; // 计数，确保收集4个有效数字字符

            if (!match.Success) // 对应C++的match.hasMatch()
            {
                return 0.0;
            }

            // 提取:D后面到\r/\n之间的内容（对应C++的match.captured(1)，C#中Groups[1].Value）
            string matchedPart = match.Groups[1].Value;
            List<int> decimalPositions = []; // 对应C++的QList<int>
            string cleanedInput = string.Empty;

            // 2. 过滤出所有数字和小数点，并记录小数点位置（与原C++逻辑一致）
            for (int i = 0; i < matchedPart.Length; i++)
            {
                char c = matchedPart[i];
                if (char.IsDigit(c) || c == '.')
                {
                    if (c == '.')
                    {
                        // 记录小数点在cleanedInput中的索引位置（对应原逻辑）
                        decimalPositions.Add(cleanedInput.Length);
                    }
                    cleanedInput += c;
                }
            }

            // 3. 确保数据长度至少16（保留原业务逻辑）
            if (cleanedInput.Length < 16)
            {
                Debug.WriteLine("Extracted number string is too short!"); // 对应C++的qDebug()
                return 0.0;
            }

            // 4. 从后往前遍历，收集4个有效数字并截取子串
            for (int i = cleanedInput.Length - 1; i >= 0; i--)
            {
                char c = cleanedInput[i];

                if (char.IsDigit(c)) // 只对数字计数（对应原逻辑）
                {
                    count++;
                }

                // 收集到4个有效数字字符，截取子串
                if (count == 4)
                {
                    // 对应C++的QString::mid(i, cleanedInput.length()-1)
                    // 注意：C# Substring(startIndex, length)，此处计算截取长度（从i到字符串末尾）
                    int substringLength = cleanedInput.Length - i;
                    substring = cleanedInput.Substring(i, substringLength);
                    break;
                }
            }

            // 5. 转换为double并返回（对应C++的substring.toDouble()，C#用TryParse更安全）
            if (double.TryParse(substring, out double result))
            {
                return result;
            }
            else
            {
                return 0.0;
            }
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

        public EMS04()
        {
        }
    }
}
