using System.Net;
using System.Net.Sockets;
using System.Text;

namespace EMCDeviceLibrary
{
    public class ClientSocket
    {
        #region 变量定义及声明
        private Socket? sockClient;     //创建套接字
        private bool _closed = false;   //是否已显式关闭（关闭后禁止定时器自动重启）

        public delegate void myInvoke(byte[] msg);//定义一个自己的委托
        public event myInvoke? ReceiveMsg;            //转移信息事件
        private IPEndPoint? EP;
        private int ice = 0;
        private readonly System.Timers.Timer tmrRec = new(15);

        /// <summary>设备连接超时（毫秒）。统一为 3 秒：通讯异常时避免 OS 默认 TCP 建连超时（可达数十秒）造成的长时间阻塞。</summary>
        private const int ConnectTimeoutMs = 3000;
        #endregion

        public ClientSocket()
        {
            tmrRec.Elapsed += TmrRec_Elapsed;
            tmrRec.AutoReset = false;
        }

        /// <summary>
        /// 创建套接字并建立连接
        /// </summary>
        /// <param name="Ip">待连接方IP</param>
        /// <param name="port">待连接端口</param>
        public void Connect(string? Ip, int port)
        {
            IPAddress ip = IPAddress.Parse(Ip);
            EP = new(ip, port);

            Close(); // 覆盖前先关闭旧 socket，防止连接泄漏

            sockClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                SendTimeout = ConnectTimeoutMs,
                ReceiveTimeout = ConnectTimeoutMs
            };
            try
            {
                // 建连超时 3 秒：BeginConnect + WaitOne(3000)，避免 OS 默认 TCP 建连超时过长导致界面长时间阻塞
                var ar = sockClient.BeginConnect(ip, port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(ConnectTimeoutMs, false))
                {
                    sockClient.Close();
                    throw new SocketException((int)SocketError.TimedOut);
                }
                sockClient.EndConnect(ar);
                _closed = false;
                tmrRec.Start();
            }
            catch (SocketException)
            {
                Close();
                throw;
            }
        }

        /// <summary>
        /// 重连
        /// </summary>
        private void ReConnect()
        {
            if (EP == null) return;
            try
            {
                Close(); // 关闭旧 socket（含定时器停止）
                sockClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    SendTimeout = ConnectTimeoutMs,
                    ReceiveTimeout = ConnectTimeoutMs
                };
                // 重连同样受 3 秒建连超时约束：超时当作连接失败，交给外层计数/退出逻辑
                var ar = sockClient.BeginConnect(EP.Address, EP.Port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(ConnectTimeoutMs, false))
                {
                    sockClient.Close();
                    throw new SocketException((int)SocketError.TimedOut);
                }
                sockClient.EndConnect(ar);
                _closed = false;
                tmrRec.Start(); // 重连成功后重启接收定时器
            }
            catch (SocketException)
            {
                // 重连失败：保持关闭状态，等上层下次显式连接，不崩溃
                Close();
            }
        }

        /// <summary>
        /// 读取接收数据定时器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TmrRec_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                if (State() == 1)
                {
                    RecMsg();
                }
                else
                {
                    if (sockClient != null && ice++ > 500)
                    {
                        ice = 0;
                        Close();
                        ReConnect();
                    }
                }
            }
            catch (Exception)
            {
                // 捕获所有异常，防止定时器线程未处理异常导致进程崩溃
            }
            finally
            {
                if (!_closed) tmrRec.Start(); // 已显式关闭时不再重启定时器
            }
        }

        /// <summary>
        /// 连接套接字
        /// </summary>
        /// <param name="sock">待连接的套接字</param>
        public void SetSock(Socket sock)
        {
            _closed = false;
            sockClient = sock;
            tmrRec.Start();
        }

        /// <summary>
        /// 接收数据并将其抛出
        /// </summary>
        private void RecMsg()
        {
            try
            {
                if (sockClient != null)
                {
                    if (sockClient.Poll(-1, SelectMode.SelectRead) != true)
                    {
                        if (sockClient.Available == 0)
                        {
                            Close();
                            throw (new Exception($"服务端断开了连接"));
                        }
                    }
                    int len = sockClient.Available;
                    byte[] arrMsgRec = new byte[len];
                    // 将接收到的数据存入到输入 arrMsgRec中
                    int? length = 0;
                    try
                    {
                        length = sockClient.Receive(arrMsgRec); // 接收数据，并返回数据的长度；
                        ReceiveMsg?.Invoke(arrMsgRec);
                    }
                    catch (SocketException)
                    {
                        Close();
                        ReConnect();
                    }
                }
            }
            catch (Exception ex)
            {

            }
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="Sender">发送者</param>
        /// <param name="Msg">发送信息</param>
        public int SendMsg(string Msg)
        {
            //Encoding gb2312Encoding = Encoding.GetEncoding("GB2312");
            byte[] arrMsg = Encoding.Default.GetBytes(Msg); //转换为字节序
            try
            {
                if (State() == 1)
                {
                    sockClient?.Send(arrMsg); // 发送消息；
                }
                return 1;
            }
            catch (SocketException)
            {
                throw;
            }
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="Sender">发送者</param>
        /// <param name="Msg">发送信息</param>
        public int SendMsg(byte[] bs)
        {
            try
            {
                if (State() == 1)
                {
                    sockClient?.Send(bs); // 发送消息；
                }
                return 1;
            }
            catch (SocketException)
            {
                throw;
            }
        }

        /// <summary>
        /// 套接字状态判断
        /// </summary>
        /// <returns>套接字连接状态</returns>
        public int State()
        {
            try
            {
                if (sockClient == null)         //内存分配判断
                {
                    return 0;
                }
                else
                {
                    return (sockClient.Connected == true ? 1 : 0);  //连接状态判断
                }
            }
            catch (SocketException)
            {
                Close();
                throw;
            }
        }

        /// <summary>
        /// 释放套接字
        /// </summary>
        public void Close()
        {
            try
            {
                _closed = true;
                tmrRec.Stop(); // 先停定时器，杜绝僵尸自动重连
                if (sockClient != null)
                {
                    if (sockClient.Connected)
                    {
                        sockClient.Shutdown(SocketShutdown.Both);
                    }
                    sockClient.Close();
                    sockClient.Dispose();
                    sockClient = null; // 置空，State() 立即返回 0
                }
            }
            catch
            {

            }
        }
    }
}
