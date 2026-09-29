using EMCDeviceLibrary;
using Peak.Can.Basic.BackwardCompatibility;
using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using WpfEMCTestSoftware.Enums;
using WpfEMCTestSoftware.Models;
using WpfEMCTestSoftware.Models.DeviceModels;

namespace WpfEMCTestSoftware
{
    public class DeviceSerAndDeser
    {
        /// <summary>
        /// 序列化功放参数对象为JSON字符串
        /// </summary>
        /// <param name="para"></param>
        /// <returns></returns>
        public static string DeviceParaSer<T>(T para)
        {
            string jsonString = System.Text.Json.JsonSerializer.Serialize(para);
            return jsonString;
        }

        /// <summary>
        /// 从JSON字符串反序列化功放参数对象
        /// </summary>
        /// <param name="jsonString"></param>
        /// <returns></returns>
        public static T DeviceParaDeSer<T>(string jsonString) where T : new()
        {
            // 先判断：如果是空/空白字符串，直接返回默认实例
            if (string.IsNullOrWhiteSpace(jsonString))
            {
                return new T();
            }
            // 关键：添加枚举转换器 + 忽略大小写
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() }, // 解决枚举报错
                PropertyNameCaseInsensitive = true              // 兼容大小写
            };

            // 关键：必须把 options 传入 Deserialize 方法
            return JsonSerializer.Deserialize<T>(jsonString, options) ?? new T();
        }

        /// <summary>
        /// 获取设备信息列表，从DataTable中读取数据并根据设备类型创建相应的设备对象，最后返回设备信息列表
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        public static List<DeviceInfo> GetDeviceInfos(DataTable table)
        {
            List<DeviceInfo> deviceInfos = [];
            foreach (DataRow row in table.Rows)
            {
                string T = row["DeviceType"].ToString() ?? "未知设备";
                DeviceType deviceType = (DeviceType)Enum.Parse(typeof(DeviceType), T);
                DeviceInfo device = deviceType switch
                {
                    DeviceType.功放 => DeviceParaDeSer<AmpifierPara>(row["OtherPara"].ToString() ?? string.Empty),
                    DeviceType.接收机 => DeviceParaDeSer<ReceiverPara>(row["OtherPara"].ToString() ?? string.Empty),
                    DeviceType.场强仪 => DeviceParaDeSer<FieldStrengthMeterPara>(row["OtherPara"].ToString() ?? string.Empty),
                    DeviceType.功率计 => DeviceParaDeSer<PowerMeterPara>(row["OtherPara"].ToString() ?? string.Empty),
                    DeviceType.开关矩阵 => DeviceParaDeSer<SwitchMatrixPara>(row["OtherPara"].ToString() ?? string.Empty),
                    DeviceType.频谱仪 => DeviceParaDeSer<OscilloscopePara>(row["OtherPara"].ToString() ?? string.Empty),
                    DeviceType.信号源 => DeviceParaDeSer<SignalSourcePara>(row["OtherPara"].ToString() ?? string.Empty),
                    DeviceType.衰减器 => DeviceParaDeSer<PhaseAttPara>(row["OtherPara"].ToString() ?? string.Empty),
                    DeviceType.倍频器 => DeviceParaDeSer<MultiplierFreqPara>(row["OtherPara"].ToString() ?? string.Empty),
                    DeviceType.示波器 => DeviceParaDeSer<OscilloscopePara>(row["OtherPara"].ToString() ?? string.Empty),
                    DeviceType.搅拌器 => DeviceParaDeSer<StirrerPara>(row["OtherPara"].ToString() ?? string.Empty),
                    DeviceType.天线 => DeviceParaDeSer<AntennaPara>(row["OtherPara"].ToString() ?? string.Empty),
                    DeviceType.天线塔 => DeviceParaDeSer<AntennaTowerPara>(row["OtherPara"].ToString() ?? string.Empty),
                    DeviceType.矢量网络分析仪 => DeviceParaDeSer<DeviceInfo>(row["OtherPara"].ToString() ?? string.Empty),
                };
                device.Id = Convert.ToInt32(row["Id"]);
                device.Name = row["Name"].ToString() ?? string.Empty;
                device.Type = deviceType;
                device.Model = (DeviceMode)Enum.Parse(typeof(DeviceMode), row["DeviceModel"].ToString() ?? string.Empty);
                device.Communicationmode = (CommunicationMode)Enum.Parse(typeof(CommunicationMode), row["Communicationmode"].ToString() ?? string.Empty);

                device.IsActive = Convert.ToBoolean(row["IsActive"] ?? "false");
                device.Time = Convert.ToInt32(row["CommunicationTime"] ?? "0");
                device.DevicePara = new DevicePara
                {
                    IP = row["IP"].ToString(),
                    Port = Convert.ToInt32(row["Port"] ?? "0"),
                    ComPort = row["ComPort"].ToString(),
                    ComBaud = row["ComBaud"].ToString(),
                    CANChannel = Convert.ToUInt16(row["CANChannel"] ?? "0"),
                    CANBaudRate = (TPCANBaudrate)Enum.Parse(typeof(TPCANBaudrate), row["CANBaudRate"].ToString() ?? string.Empty),
                    Mode = (CommunicationMode)Enum.Parse(typeof(CommunicationMode), row["Communicationmode"].ToString() ?? string.Empty),
                    Address = device.Communicationmode == CommunicationMode.VISA ? row["Address"].ToString() ?? string.Empty : string.Empty
                };
                device.Address = device.Communicationmode switch
                {
                    CommunicationMode.LAN => $"{device.DevicePara.IP}:{device.DevicePara.Port}",
                    CommunicationMode.Serial => $"{device.DevicePara.ComPort}:{device.DevicePara.ComBaud}",
                    CommunicationMode.CAN => $"{device.DevicePara.CANChannel}:{device.DevicePara.CANBaudRate}",
                    CommunicationMode.VISA => device.DevicePara.Address,
                    _ => ""
                };

                deviceInfos.Add(device);
            }
            return deviceInfos;
        }
    }
}
