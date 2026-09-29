
namespace EMCDeviceLibrary
{
    internal class SendRecDeal
    {
        public SendRecDeal()
        {
        }

        /// <summary>
        /// 处理接收到的消息，默认按ASCII编码处理，并去除回车换行符
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public virtual List<string> DealRecMsg(Communication communication, byte[] bytes)
        {
            return [];
        }

        /// <summary>
        /// 处理发送的消息，默认按ASCII编码，并在末尾添加换行符
        /// </summary>
        /// <param name="msgs"></param>
        /// <param name="communication"></param>
        /// <param name="IsSet">是否为设置指令</param>
        /// <returns></returns>
        public virtual byte[] DealSendMsg(List<string> msgs, Communication communication, out bool IsSet)
        {
            IsSet = false;
            return [];
        }

    }
}
