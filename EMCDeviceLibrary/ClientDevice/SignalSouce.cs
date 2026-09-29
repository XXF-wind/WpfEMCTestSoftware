using System.Collections.Concurrent;
using System.Timers;

namespace EMCDeviceLibrary
{
    /// <summary>
    /// 信号源设备驱动类
    /// 继承自：SockConnect（通用通讯基类）
    /// 功能：实现与自定义协议信号源的通讯、指令解析、自动校验、队列发送
    /// </summary>
    public class SignalSouce : SockConnect
    {
        /// <summary>
        /// 信号源类型枚举（根据设备返回的协议头自动识别）
        /// </summary>
        public enum SignalSouceType
        {
            None = 0,           // 未识别
            SweepingSource = 1, // 扫频信号源
            SignalSourceNew = 2, // 新型信号源
            SignalSourceOld = 3  // 旧型信号源
        }

        /// <summary>
        /// 信号源支持的【设置类指令】集合
        /// 用于判断返回的指令是否是设置指令
        /// </summary>
        private byte[] SendSetCommand = [0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x1A, 0x2A, 0x1B, 0x5A];

        /// <summary>
        /// 指令处理类（根据信号源类型自动切换：新/旧/扫频）
        /// 负责组包发送、解析返回
        /// </summary>
        private SendRecDeal sendRec;

        /// <summary>
        /// 当前正在发送的指令信息
        /// </summary>
        private SendInfo sendInfo = new();

        /// <summary>
        /// 发送队列轮询定时器（100ms 检查一次是否有指令要发）
        /// </summary>
        private System.Timers.Timer tmrSend = new System.Timers.Timer(100);

        /// <summary>
        /// 线程安全的发送指令队列（多线程调用不会冲突）
        /// 所有要发给信号源的指令先进队列，再依次发送
        /// </summary>
        private ConcurrentQueue<SendInfo> sendInfos = new();

        /// <summary>
        /// 重写初始化连接
        /// </summary>
        public override void InitConnection(object Device)
        {
            try
            {
                // 调用父类初始化（网口/串口/CAN/VISA自动判断）
                base.InitConnection(Device);
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// 重写：接收数据处理（核心！所有设备返回的数据都走这里）
        /// </summary>
        public override void dealMsg(byte[] bytes)
        {
            if (bytes.Length > 0)
            {
                // 协议头固定为 0xF5 表示有效帧
                if (bytes[0] == 0xF5)
                {
                    // 如果已经初始化了协议解析类
                    if (sendRec != null)
                    {
                        // 当前无指令在处理，直接返回
                        if (sendInfo.comm == Communication.NONE)
                            return;

                        // 如果当前指令是【设置类指令】（如设置频率、功率）
                        if (sendInfo.IsSet)
                        {
                            // 判断返回的命令码是否是合法设置指令
                            if (SendSetCommand.Contains(bytes[2]))
                            {
                                // bytes[3] == 0x01 表示设备回复：设置成功
                                if (bytes[3] == 0x01)
                                {
                                    sendInfos.TryDequeue(out var info); // 出队
                                    Push("设置成功");
                                }
                                else
                                {
                                    Push("设置失败");
                                }
                            }
                            else
                            {
                                Push("设置无效");
                            }
                        }
                        // 是【查询类指令】，需要解析数据并推送给UI
                        else
                        {
                            // 调用协议解析类解析返回数据
                            string sendData = string.Join(',', sendRec.DealRecMsg(sendInfo.comm, bytes));
                            Task.Run(() =>
                            {
                                Push(sendData); // 推送给界面
                            });
                        }
                    }
                    // 第一次接收数据：自动识别信号源类型
                    else
                    {
                        SignalSouceType type = 0;

                        // 根据协议特征码自动识别设备类型
                        if (bytes[2] == 0xBB)
                        {
                            sendRec = new SweepingSource();
                            type = SignalSouceType.SweepingSource;
                        }
                        else if (bytes[2] == 0xAA)
                        {
                            sendRec = new SignalSourceNew();
                            type = SignalSouceType.SignalSourceNew;
                        }
                        else if (bytes[2] == 0x4A)
                        {
                            sendRec = new SignalSourceOld();
                            type = SignalSouceType.SignalSourceOld;
                        }
                        else
                        {
                            sendRec = new();
                        }

                        // 识别失败
                        if (type == 0)
                            Push("未知设备");
                        // 识别成功 → 启动发送定时器
                        else
                        {
                            Initialed = true;
                            Push(type.ToString());
                            tmrSend.Start();
                        }
                    }

                    // 指令处理完成，清空当前指令
                    sendInfo.comm = Communication.NONE;
                }
            }
        }

        /// <summary>
        /// 发送队列定时器回调
        /// 作用：依次从队列中取出指令，发给设备
        /// </summary>
        private void TmrSend_Elapsed(object? sender, ElapsedEventArgs e)
        {
            tmrSend.Stop();
            try
            {
                // 当前没有正在发送的指令
                if (sendInfo.comm == Communication.NONE)
                {
                    // 从队列中取出一条
                    if (sendInfos.TryPeek(out var info))
                    {
                        if (info != null)
                        {
                            // 空指令直接丢弃
                            if (info.comm == Communication.NONE)
                            {
                                _ = sendInfos.TryDequeue(out _);
                            }

                            // 记录当前发送的指令
                            sendInfo = info;

                            // 调用协议类组包
                            byte[] cmd = sendRec.DealSendMsg(info.lst, info.comm, out bool IsSet);
                            info.IsSet = IsSet;

                            // 拼接 CRC16 校验
                            cmd = CompletionCommand(cmd);

                            // 调用底层发送（串口/网口）
                            SendMsg(cmd);
                        }
                    }
                }
            }
            finally
            {
                // 无论如何都重启定时器，保证持续轮询
                tmrSend.Start();
            }
        }

        /// <summary>
        /// 重写：外部调用的发送接口
        /// 把指令丢进队列，不直接发送
        /// </summary>
        public override void SendData(List<string> lstData, Communication command)
        {
            try
            {
                if (sendRec == null)
                    return;

                // 指令入队（线程安全）
                sendInfos.Enqueue(new SendInfo() { lst = lstData, comm = command });
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// 拼接完整指令：数据 + CRC16 校验（2字节）
        /// </summary>
        private byte[] CompletionCommand(byte[] byteArray)
        {
            int len = byteArray.Length;
            byte[] cmd = new byte[len + 2];
            Array.Copy(byteArray, cmd, len);

            // 计算CRC16
            ushort crcValue = CalculateCrc16(cmd, len);

            // 高字节、低字节放入末尾
            cmd[len] = (byte)((crcValue >> 8) & 0xFF);
            cmd[len + 1] = (byte)(crcValue & 0xFF);
            return cmd;
        }

        /// <summary>
        /// CRC16 校验算法（Modbus 标准）
        /// </summary>
        public ushort CalculateCrc16(byte[] arr, int length)
        {
            ushort shift = 0xFFFF;
            for (int i = 0; i < length; i++)
            {
                shift ^= arr[i];
                for (int j = 0; j < 8; j++)
                {
                    if ((shift & 0x01) == 1)
                    {
                        shift >>= 1;
                        shift ^= 0xA001;
                    }
                    else
                    {
                        shift >>= 1;
                    }
                }
            }
            return shift;
        }

        /// <summary>
        /// 清空数据（预留）
        /// </summary>
        public override void ClearData()
        {
            try
            {
                // 可在这里实现清空缓冲区
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// 关闭设备连接
        /// </summary>
        public override void Close()
        {
            try
            {
                Initialed = false;
                ClearData();
                base.Close(); // 调用父类关闭
            }
            catch { }
            finally { }
        }

        /// <summary>
        /// 构造函数：初始化名称、发送定时器
        /// </summary>
        public SignalSouce(string name)
        {
            this.Name = name;
            tmrSend.Elapsed += TmrSend_Elapsed;
            tmrSend.AutoReset = false;
        }

        /// <summary>
        /// 发送指令结构体（存储：参数列表、指令类型、是否为设置指令）
        /// </summary>
        private class SendInfo
        {
            public List<string> lst;    // 参数列表
            public Communication comm;   // 指令类型
            public bool IsSet;          // 是否是设置类指令
        }
    }
}