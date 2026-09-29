namespace WpfEMCTestSoftware.Models.EventMessages
{
    /// <summary>
    /// 请求指定类型设备的消息
    /// </summary>
    public class RequestDevicesByTypeMessage
    {
        public int DeviceTypeID { get; }
        public RequestDevicesByTypeMessage(int deviceTypeID) { DeviceTypeID = deviceTypeID; }

    }
}
