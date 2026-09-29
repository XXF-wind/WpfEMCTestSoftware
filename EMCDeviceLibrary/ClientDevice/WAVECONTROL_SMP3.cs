using System.Diagnostics;
using System.Xml.Linq;

namespace EMCDeviceLibrary
{
    public class WAVECONTROL_SMP3 : SockConnect
    {
        /// <summary>
        /// 设置频率命令
        /// </summary>
        private readonly string setFreqCmd = "#00k";
        /// <summary>
        /// 查询命令
        /// </summary>
        private readonly string queryFieldCmds = "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n<cmd>\r\n  <instruction>GET_SAMPLE</instruction>\r\n<md5>e774bbdabb350a4c22d963355f1884c7</md5></cmd>";
        /// <summary>
        /// 初始化连接
        /// </summary>
        /// <param name="Device"></param>
        public override void InitConnection(object Device)
        {
            base.InitConnection(Device);
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
                //double value = ParseXmlValue(recData.Trim(),);
                Task.Run(() =>
                {
                    Push(recData.ToString());
                });
            }
        }

        /// <summary>
        /// 读取信息
        /// </summary>
        /// <returns></returns>
        public override string? ReadData(string para)
        {
            if (CommunicationType == CommunicationMode.VISA)
            {
                if (visa != null)
                {
                    string result = visa.ReadResponse();
                    byte type = para switch
                    {
                        "x" => 0,
                        "y" => 1,
                        "z" => 2,
                        "c" => 3,
                        _ => 0
                    };
                    return ParseXmlValue(result, type).ToString();
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
        /// 从XML字符串中截取有效片段并解析对应节点值（转换自parseXmlValue方法）
        /// </summary>
        /// <param name="xmlData">输入的原始XML字符串</param>
        /// <param name="axisType">轴类型（对应原quint8，C#用byte）</param>
        /// <returns>解析后的对应节点double数值，失败返回0.0</returns>
        public double ParseXmlValue(string xmlData, byte axisType)
        {
            // 1. 查找XML开始标签"<?xml"的索引（对应原C++的startIndex）
            int startIndex = xmlData?.IndexOf("<?xml", StringComparison.Ordinal) ?? -1;

            // 如果未找到开始标签，直接返回0.0
            if (startIndex == -1)
            {
                return 0.0;
            }

            // 2. 查找结束标签"</reply>"的索引（从startIndex开始查找，对应原C++的重载indexOf）
            string endTag = "</reply>";
            int endIndex = xmlData.IndexOf(endTag, startIndex, StringComparison.Ordinal);

            // 如果未找到结束标签，直接返回0.0
            if (endIndex == -1)
            {
                return 0.0;
            }

            // 3. 截取有效的XML数据（对应原C++的mid方法）
            // 计算截取长度：结束标签索引 - 开始标签索引 + 结束标签自身长度
            int validXmlLength = endIndex - startIndex + endTag.Length;
            string validXmlData;
            try
            {
                validXmlData = xmlData.Substring(startIndex, validXmlLength);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Debug.WriteLine($"Failed to substring valid XML data: {ex.Message}");
                return 0.0;
            }

            // 4. 解析有效XML，获取x/y/z/value节点值（对应原C++的QXmlStreamReader）
            double x = 0.0, y = 0.0, z = 0.0, c = 0.0;
            try
            {
                // 加载有效XML片段（忽略XML声明的格式警告，兼容更多场景）
                XDocument xDoc = XDocument.Parse(validXmlData, LoadOptions.PreserveWhitespace);

                // 遍历所有子节点，提取对应值（贴近原C++的流式解析逻辑）
                foreach (XElement element in xDoc.Descendants())
                {
                    string elementName = element.Name.LocalName; // 获取节点本地名称（忽略命名空间）
                    string elementValue = element.Value.Trim(); // 去除首尾空白，避免转换失败

                    // 安全转换为double，失败则保持默认值0.0
                    switch (elementName)
                    {
                        case "x":
                            double.TryParse(elementValue, out x);
                            break;
                        case "y":
                            double.TryParse(elementValue, out y);
                            break;
                        case "z":
                            double.TryParse(elementValue, out z);
                            break;
                        case "value":
                            double.TryParse(elementValue, out c);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to parse XML data: {ex.Message}");
                return 0.0;
            }

            // 5. 根据AxisType返回对应值（对应原C++的switch逻辑）
            switch (axisType)
            {
                case 0:
                    return x;
                case 1:
                    return y;
                case 2:
                    return z;
                case 3:
                    return c;
                default:
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

        public WAVECONTROL_SMP3()
        {
        }
    }
}
