namespace WpfEMCTestSoftware.Models.EventMessages
{
    /// <summary>
    /// 设备信息下发链路的消息令牌（SetDeviceEventMessage 专用）。
    /// <para>
    /// 说明：这里只是把原来的"裸数字 1 / 2 / 3"换成能一眼看懂流向的名字，
    /// 取值与原代码完全一致（1 / 2 / 3），不改变任何运行行为。
    /// 注册端和发送端必须成对使用同一个令牌，否则消息收不到。
    /// </para>
    /// <para>
    /// 转发链路（每经过一层令牌 +1）：
    /// 设备列表页 →[1]→ 主窗体 →[2]→ 设备参数页 →[3]→ 具体设备子页（功放 / 接收机 / 信号源 / 功率计 ……）
    /// </para>
    /// </summary>
    public enum DeviceParaMessageToken
    {
        /// <summary>
        /// (1) 设备列表页 → 主窗体：请求打开"设备参数页"。
        /// <para>发送方：DeviceListViewModel；接收方：MainViewModel。</para>
        /// </summary>
        DeviceListToMainWindow = 1,

        /// <summary>
        /// (2) 主窗体 → 设备参数页：把设备信息转发给设备参数页。
        /// <para>发送方：MainViewModel；接收方：DeviceParaViewModel。</para>
        /// </summary>
        MainWindowToDevicePara = 2,

        /// <summary>
        /// (3) 设备参数页 → 具体设备子页：把设备信息转发给对应的设备参数子页。
        /// <para>发送方：DeviceParaViewModel；接收方：AmpifierParaViewModel 等各设备子页面。</para>
        /// </summary>
        DeviceParaToSubPage = 3,
    }

    /// <summary>
    /// 设备参数保存回传链路的消息令牌（SaveClickEventMessage 专用）。
    /// <para>取值与原代码一致（1 / 2），仅提升可读性。</para>
    /// <para>
    /// 回传链路：设备参数页 →[1]→ 具体设备子页（子页把界面值写回参数对象）→[2]→ 设备参数页（保存入库）
    /// </para>
    /// </summary>
    public enum DeviceSaveMessageToken
    {
        /// <summary>
        /// (1) 设备参数页 → 具体设备子页：请子页把界面上的值回写到参数对象。
        /// <para>发送方：DeviceParaViewModel；接收方：AmpifierParaViewModel 等各设备子页面。</para>
        /// </summary>
        DeviceParaToSubPage = 1,

        /// <summary>
        /// (2) 具体设备子页 → 设备参数页：参数已回写完成，请求保存入库。
        /// <para>发送方：各设备子页面；接收方：DeviceParaViewModel。</para>
        /// </summary>
        SubPageToDevicePara = 2,
    }

    /// <summary>
    /// 子页面返回主窗体的消息令牌（ExitClickEventMessage 专用）。
    /// <para>
    /// 注意区分两个数字：消息的"值"表示要切换到哪一页（对应 MainViewModel.SwitchPage 的页面索引，
    /// 如 0=快速开始、1=设备列表、0xff/-1=不切换页面），令牌固定为 1，含义是"这条消息发给主窗体处理"。
    /// </para>
    /// </summary>
    public enum PageExitMessageToken
    {
        /// <summary>(1) 子页面 → 主窗体：请求退出当前页，由主窗体按消息值切换页面（MainViewModel 监听）</summary>
        SubPageToMainWindow = 1,
    }
}
