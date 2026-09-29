using System.Net;
using System.Net.Sockets;

namespace EMCDeviceLibrary
{
    public class ServerSocket
    {
        private Thread threadWatch = null;
        private Socket SocketWatch = null;
        public event Action<Socket> NewConnect;

        /// <summary>
        /// 侦听
        /// </summary>
        /// <param name="post">端口号</param>
        public bool Listen(int port)
        {
            //协议地址
            IPAddress localaddr = IPAddress.Parse("0.0.0.0");
            IPEndPoint endPoint = new(localaddr, port);
            SocketWatch = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                // 将负责监听的套接字绑定到唯一的ip和端口上；
                SocketWatch.Bind(endPoint);
            }
            catch (SocketException)
            {
                return false;
            }
            SocketWatch.Listen(100);
            //新建线程侦听
            threadWatch = new Thread(Connecting)
            {
                IsBackground = true
            };
            threadWatch.Start();
            return true;
        }

        /// <summary>
        /// 新线程执行过程
        /// </summary>
        private void Connecting()
        {
            try
            {
                while (true) // 持续不断的监听客户端的连接请求；
                {
                    Thread.Sleep(100);
                    // 开始监听客户端连接请求，Accept方法会阻断当前的线程；
                    Socket sckConnection = SocketWatch.Accept();    //一旦监听到一个客户端的请求，就返回一个与该客户端通信的 套接字；
                    NewConnect(sckConnection);
                }
            }
            catch
            {

            }
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        public void Close()
        {
            try
            {
                if (SocketWatch != null)
                {
                    SocketWatch.Close();
                    SocketWatch.Dispose();
                    SocketWatch = null;
                }
            }
            catch
            {

            }
        }
    }
}
