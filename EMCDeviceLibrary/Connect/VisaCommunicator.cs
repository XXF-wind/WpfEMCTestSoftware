using Ivi.Visa;
using System.Text;

namespace EMCDeviceLibrary
{
    /// <summary>
    /// VISA通讯封装类（适配.NET 8 + 无Query方法的NI-VISA接口）
    /// </summary>
    public class VisaCommunicator : IDisposable
    {
        #region 私有字段
        // 可空标记，解决.NET 8空引用检查警告
        private IMessageBasedSession? _visaSession;
        private bool _isConnected;
        // 连接与读写超时统一 3 秒：设备无响应时快速失败，避免连接过程长时间阻塞
        private int _timeoutMs = 3000;
        // 发送命令后的固定等待：默认 0，避免每条命令都白白多花 10ms
        private int _commandDelayMs = 0;
        // 单次读取的目标字节数：设备不发终止符/END，读满即返回。
        // 与 Qt 版验证过的 64 字节保持一致，读取立即返回且数值正确。
        private int _readChunkBytes = 64;

        /// <summary>VISA 连接（Open）阶段超时（毫秒），统一 3 秒</summary>
        private const int ConnectTimeoutMs = 3000;
        #endregion

        #region 公共属性
        /// <summary>
        /// 设备是否已连接
        /// </summary>
        public bool IsConnected => _isConnected;

        /// <summary>
        /// 读写超时时间（毫秒），默认5000ms
        /// </summary>
        public int TimeoutMs
        {
            get => _timeoutMs;
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(TimeoutMs), "超时时间必须大于0");
                _timeoutMs = value;
                if (_visaSession != null)
                    _visaSession.TimeoutMilliseconds = value;
            }
        }

        /// <summary>
        /// 发送命令后的固定等待时间（毫秒），默认0。
        /// 绝大多数设备不需要该延时，设为0可显著加快批量查询；
        /// 仅当设备响应慢、命令被丢弃时才调大（如 5~10ms）。
        /// </summary>
        public int CommandDelayMs
        {
            get => _commandDelayMs;
            set => _commandDelayMs = value < 0 ? 0 : value;
        }

        /// <summary>
        /// 单次读取的目标字节数，默认64（与Qt版一致）。
        /// 本设备不发终止符/END，读满该字节数立即返回；
        /// 调大后若超过设备实际发送量，会一直等到超时。
        /// </summary>
        public int ReadChunkBytes
        {
            get => _readChunkBytes;
            set => _readChunkBytes = value <= 0 ? 64 : value;
        }

        /// <summary>
        /// 当前连接的VISA设备地址
        /// </summary>
        public string? DeviceAddress { get; private set; }
        #endregion

        #region 构造函数
        public VisaCommunicator()
        {
            _visaSession = null;
            _isConnected = false;
            DeviceAddress = string.Empty;
        }

        public VisaCommunicator(string deviceAddress) : this()
        {
            DeviceAddress = deviceAddress;
        }
        #endregion

        #region 核心通讯方法
        /// <summary>
        /// 连接VISA设备
        /// </summary>
        /// <param name="deviceAddress">VISA设备地址（可选，构造时已传入可留空）</param>
        /// <exception cref="ArgumentNullException">设备地址为空</exception>
        /// <exception cref="VisaException">连接失败</exception>
        public void Connect(string? deviceAddress = null)
        {
            if (!string.IsNullOrEmpty(deviceAddress))
                DeviceAddress = deviceAddress;

            if (string.IsNullOrEmpty(DeviceAddress))
                throw new ArgumentNullException(nameof(DeviceAddress), "VISA设备地址不能为空");

            if (_isConnected)
                Disconnect();

            // 环境自检：本机未注册任何厂商 VISA .NET 实现时，Open() 会抛出该特征错误，
            // 这里先拦截并给出可操作的修复提示（检查方式与 VISA.NET 版本无关，避免依赖 8.x 私有 API）
            try
            {
                // 通过GlobalResourceManager打开会话（适配.NET 8新版VISA API）。
                // Open 是原生阻塞调用，本身不受 _timeoutMs 约束；用 Task 包裹并 Wait(3s)，
                // 保证连接阶段最多等待 3 秒，避免设备无响应时长时间阻塞。
                _visaSession = OpenSessionWithTimeout(DeviceAddress, ConnectTimeoutMs);
                if (_visaSession == null)
                    throw new VisaException("无法打开VISA会话，设备不支持消息型VISA通讯");

                // 设置超时时间
                _visaSession.TimeoutMilliseconds = _timeoutMs;
                // 启用终止符：设备返回 '\n' 时 RawIO.Read() 立即结束，不必等到超时
                _visaSession.TerminationCharacterEnabled = true;
                _visaSession.TerminationCharacter = (byte)'\n';
                // 清除设备缓冲区
                _visaSession.Clear();
                _isConnected = true;
            }
            catch (Exception ex) when (HasImplementationMissing(ex))
            {
                throw new VisaException(
                    "本机未检测到厂商 VISA .NET 实现（No vendor-specific VISA .NET implementation is installed）。\n" +
                    "修复方法：打开 NI Package Manager，将 NI-VISA 升级/修复到 2025 Q3 或更新版本（需与本项目的 NationalInstruments.Visa 25.5 NuGet 包匹配），" +
                    "或安装 Keysight IO Libraries Suite 2025，然后重启应用。", ex);
            }
            catch (Exception ex)
            {
                throw new VisaException($"VISA设备连接失败：{ex.Message}", ex);
            }
        }

        /// <summary>
        /// 判断异常链中是否包含"未安装厂商VISA.NET实现"这一环境类错误
        /// </summary>
        private static bool HasImplementationMissing(Exception ex)
        {
            for (Exception? e = ex; e != null; e = e.InnerException)
            {
                if (e.Message.Contains("No vendor-specific VISA .NET implementation is installed",
                        StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 在限定超时内打开 VISA 会话。
        /// GlobalResourceManager.Open 为原生阻塞调用，本身不受 _timeoutMs 约束；
        /// 用 Task 包裹并在超时后返回失败，确保连接阶段最多等待 <paramref name="timeoutMs"/> 毫秒。
        /// </summary>
        /// <exception cref="VisaException">超时或环境缺少厂商 VISA 实现时抛出</exception>
        private static IMessageBasedSession? OpenSessionWithTimeout(string address, int timeoutMs)
        {
            var task = Task.Run(() => GlobalResourceManager.Open(address) as IMessageBasedSession);
            if (!task.Wait(timeoutMs))
            {
                // 超时：放弃本次连接（底层原生线程可能仍在进行，但业务侧已判定连接失败）
                throw new VisaException(
                    $"VISA 设备连接超时（{timeoutMs}ms）：地址 {address} 无响应，请检查设备网络连通性与资源地址。");
            }
            // Open 内部抛异常时 Wait 会封装为 AggregateException，这里展开为原始异常（保留"未安装 VISA 实现"等可读信息）
            try
            {
                return task.Result;
            }
            catch (AggregateException ae)
            {
                throw ae.InnerException ?? ae;
            }
        }

        /// <summary>
        /// 断开VISA设备连接
        /// </summary>
        public void Disconnect()
        {
            if (_visaSession != null)
            {
                try
                {
                    _visaSession.Clear();
                    _visaSession.Dispose();
                }
                catch
                {
                    // 忽略释放过程中的异常，保证资源最终回收
                }
                finally
                {
                    _visaSession = null;
                    _isConnected = false;
                }
            }
        }

        /// <summary>
        /// 向VISA设备发送命令（无返回值）
        /// </summary>
        /// <param name="command">VISA命令（如：SOUR:VOLT 5.0）</param>
        /// <exception cref="InvalidOperationException">设备未连接</exception>
        /// <exception cref="ArgumentNullException">命令为空</exception>
        /// <exception cref="VisaException">命令发送失败</exception>
        public void SendCommand(string command)
        {
            // 校验连接状态
            if (!_isConnected || _visaSession == null)
                throw new InvalidOperationException("VISA设备未连接，无法发送命令");

            // 校验命令
            if (string.IsNullOrEmpty(command))
                throw new ArgumentNullException(nameof(command), "VISA发送命令不能为空");

            try
            {
                // 与 Qt 的 viWrite 保持一致：用 RawIO 明文写入，并显式补 '\n' 结束符。
                // SCPI 命令没有结束符时设备不会解析、也不会返回数据，这是读取 IOTimeoutException 的常见原因。
                string cmd = command.EndsWith('\n') ? command : command + "\n";
                _visaSession.RawIO.Write(Encoding.ASCII.GetBytes(cmd));
                // 默认不再固定等待；设备较慢时可通过 CommandDelayMs 调整
                if (_commandDelayMs > 0)
                    Thread.Sleep(_commandDelayMs);
            }
            catch (Exception ex)
            {
                throw new VisaException($"VISA命令发送失败：{command}，错误信息：{ex.Message}", ex);
            }
        }

        /// <summary>
        /// 从VISA设备读取返回结果（字符串格式）
        /// </summary>
        /// <returns>设备返回的字符串结果</returns>
        /// <exception cref="InvalidOperationException">设备未连接</exception>
        /// <exception cref="VisaException">读取失败</exception>
        public string ReadResponse()
        {
            // 校验连接状态
            if (!_isConnected || _visaSession == null)
                throw new InvalidOperationException("VISA设备未连接，无法读取返回结果");

            try
            {
                // 该设备不发终止符也不发END，读操作要等"装满缓冲区"才返回。
                // RawIO.Read() 不带长度时默认缓冲区很大（数KB），凑满要数秒；
                // 与 Qt 版一致用小缓冲（默认64字节）快速凑满立即返回。
                byte[] buffer = _visaSession.RawIO.Read(ReadChunkBytes);
                string response = Encoding.UTF8.GetString(buffer).TrimEnd('\n', '\r', '\0').Trim();
                return response;
            }
            catch (IOTimeoutException ex)
            {
                throw new VisaException(
                    $"VISA读取超时（{_timeoutMs}ms）：设备未返回数据。请确认查询命令以 '?' 结尾且已发送结束符，或调大 TimeoutMs。", ex);
            }
            catch (Exception ex)
            {
                throw new VisaException($"VISA设备返回结果读取失败：{ex.Message}", ex);
            }
        }

        /// <summary>
        /// 按指定字节数读取返回结果。
        /// 适用于已知返回长度、且设备不发送结束符/END 的场景：读够指定长度立即返回，无需等到超时。
        /// </summary>
        /// <param name="bytesToRead">期望读取的字节数</param>
        /// <returns>设备返回的字符串结果</returns>
        /// <exception cref="InvalidOperationException">设备未连接</exception>
        /// <exception cref="ArgumentOutOfRangeException">读取长度非法</exception>
        /// <exception cref="VisaException">读取失败</exception>
        public string ReadResponse(int bytesToRead)
        {
            if (bytesToRead <= 0)
                throw new ArgumentOutOfRangeException(nameof(bytesToRead), "读取字节数必须大于0");

            if (!_isConnected || _visaSession == null)
                throw new InvalidOperationException("VISA设备未连接，无法读取返回结果");

            try
            {
                byte[] buffer = _visaSession.RawIO.Read(bytesToRead);
                return Encoding.UTF8.GetString(buffer).TrimEnd('\n', '\r', '\0');
            }
            catch (IOTimeoutException ex)
            {
                throw new VisaException(
                    $"VISA读取超时（{_timeoutMs}ms）：期望 {bytesToRead} 字节但未读满。请确认返回长度是否正确。", ex);
            }
            catch (Exception ex)
            {
                throw new VisaException($"VISA设备返回结果读取失败：{ex.Message}", ex);
            }
        }

        /// <summary>
        /// 手动实现Query逻辑：发送查询命令 + 读取返回结果（替代原生Query方法）
        /// </summary>
        /// <param name="command">VISA查询命令（如：*IDN?）</param>
        /// <returns>设备返回的字符串结果</returns>
        /// <exception cref="InvalidOperationException">设备未连接</exception>
        /// <exception cref="ArgumentNullException">命令为空</exception>
        /// <exception cref="VisaException">查询失败</exception>
        public string QueryString(string command)
        {
            SendCommand(command);
            string result = ReadResponse();
            return result;
        }

        /// <summary>
        /// 发送查询命令并转换为数值结果
        /// </summary>
        /// <param name="command">VISA查询命令</param>
        /// <returns>设备返回的数值结果</returns>
        /// <exception cref="InvalidOperationException">设备未连接</exception>
        /// <exception cref="ArgumentNullException">命令为空</exception>
        /// <exception cref="FormatException">返回结果无法转换为数值</exception>
        /// <exception cref="VisaException">查询失败</exception>
        public double QueryNumeric(string command)
        {
            string result = QueryString(command);
            if (!double.TryParse(result, out double numericResult))
                throw new FormatException($"VISA返回结果无法转换为数值：{result}");
            return numericResult;
        }
        #endregion

        #region 资源释放（IDisposable实现）
        public void Dispose()
        {
            Disconnect();
            GC.SuppressFinalize(this);
        }

        ~VisaCommunicator()
        {
            Disconnect();
        }
        #endregion
    }

    /// <summary>
    /// 自定义VISA异常，便于业务层捕获和处理
    /// </summary>
    public class VisaException : Exception
    {
        public VisaException() : base() { }

        public VisaException(string message) : base(message) { }

        public VisaException(string message, Exception innerException) : base(message, innerException) { }
    }
}