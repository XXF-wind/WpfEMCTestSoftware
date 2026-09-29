using ClassLibrary;
using ClassLibrary.Generic;
using Company.UI.Controls.UseControls;
using EMCDeviceLibrary;
using ScottPlot;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using WpfEMCTestSoftware.Enums;
using WpfEMCTestSoftware.Models;
using WpfEMCTestSoftware.Models.DisplayModules;
using WpfEMCTestSoftware.Models.TestModels;

namespace WpfEMCTestSoftware.RunTasks
{
    /// <summary>
    /// EMI 发射类安全裕度测试基类（CE101 电源线传导发射 / RE102 电场辐射发射共用框架）。
    /// 测试流程（安全裕度判定）：
    ///   每频点：接收机设频 → 读数 → 修正（探头传输阻抗 / 天线系数+电缆损耗）→ 与限值比较
    ///   → 裕度 = 限值 − 实测（≥0 合格，&lt;0 超标）→ 打点曲线 + 结果表行。
    /// 框架/通用逻辑与 CS114RunTask 一致：频段循环（外层）+ 频点队列（基类 FrequencyQueue）、
    /// 设备分队列读数、log 域限值插值、模板复用 EMS_CS114ConfigModel（频段+限值+硬件）。
    /// 发射类测试不施加信号，没有内层逼近循环；暂停/停止同样由 TestContext 状态驱动。
    /// </summary>
    public abstract class EMIEmissionRunTaskBase : BaseRunTask
    {
        /// <summary>运行参数（复用 CS114 配置模型：频段参数/限值/线损结构完全通用）</summary>
        public CS114RuntimeParam RuntimeParam { get; } = new();

        /// <summary>设备通道（发射类只需要接收机；结构复用 CS114DeviceChannel）</summary>
        public CS114DeviceChannel DeviceChannel { get; } = new();

        /// <summary>接收机（发射类测试的读数设备，必选）</summary>
        public SockConnect? ReceiverSock { get; private set; }

        // 接收机独立接收队列（与 CS114 相同：按设备分队列，防互相顶掉读数）
        private readonly Queue<double> _readLevelQueue = [];

        // 曲线：实测值 + 限值参考线（同一图表通道，limit 用 IsYReferenceLine 标记）
        private readonly ObservableCollection<ChartLineModel> _spectrumCurves = [];

        /// <summary>实测曲线（结果表/保存数据也用它）</summary>
        protected ChartLineModel MeasuredLine { get; }

        /// <summary>限值参考线（BuildLimitCurve 在配置映射后整组写入）</summary>
        protected ChartLineModel LimitLine { get; }

        /// <summary>测试项标题（日志/保存用，如 "CE101 电源线传导发射"）</summary>
        protected abstract string TaskTitle { get; }

        /// <summary>实测值单位（dBμA / dBμV/m），用于卡片、日志与保存文件表头</summary>
        protected abstract string UnitLabel { get; }

        /// <summary>保存目录名（System\数据文件\{SaveDirName}）</summary>
        protected abstract string SaveDirName { get; }

        /// <summary>
        /// 读数修正量（dB）：实测值 = 接收机读数 + 本修正。
        /// CE101：电流探头传输阻抗（未配置按 0，接收机直读 dBμA）；
        /// RE102：天线系数 + 电缆损耗（接收机读 dBμV → dBμV/m）。
        /// </summary>
        protected abstract double LevelCompensation(double freqMHz);

        protected EMIEmissionRunTaskBase(string chartKey, string measuredLineTitle)
        {
            MeasuredLine = new ChartLineModel
            {
                Title = measuredLineTitle,
                LineColor = ScottPlot.Colors.DodgerBlue,
                LineWidth = 1.5f,
                XData = [],
                YData = [],
            };
            //限值参考线：IsYReferenceLine=true，ChannelChartModule 按它的 Y 最大值推算纵轴量程。
            //⚠ 查找一律用 IsYReferenceLine 标记，别按标题字符串找；只在 BuildLimitCurve 时整组写入
            LimitLine = new ChartLineModel
            {
                Title = "limit",
                IsYReferenceLine = true,
                LineColor = ScottPlot.Colors.Red,
                LineStyle = LinePattern.Dashed,
                LineWidth = 1.5f,
                XData = [],
                YData = [],
            };
            _spectrumCurves.Add(MeasuredLine);
            _spectrumCurves.Add(LimitLine);
            RegisterChannel(chartKey, _spectrumCurves);
        }

        // ==================== 当前频段参数（与 CS114 同构） ====================
        /// <summary>当前频段的分频段参数；ImmunityLimit 与 CommonFreAndHardParms 按下标一一对应</summary>
        protected CS114BandParms? CurrentBandParms
        {
            get
            {
                int i = TestContext.CurrentBandIndex;
                return RuntimeParam.ImmunityLimit.Count > 0 && i >= 0 && i < RuntimeParam.ImmunityLimit.Count
                    ? RuntimeParam.ImmunityLimit[i]
                    : null;
            }
        }

        protected bool HasBandParms => CurrentBandParms != null;

        // ==================== 基类流程钩子 ====================
        /// <summary>外层循环的频点队列：按当前频段配置算出全部频点</summary>
        protected override void RefreshInOrOutLoopQueue()
        {
            FrequencyQueue.Clear();
            FrequencyQueue = CalculateAllFreqPoints(TestContext.CurrentBandIndex);
        }

        /// <summary>发射类无限值跟随线之外的动态限值线，基类空实现即可</summary>
        protected override void RefreshLimitLinesByFreqRange() { }

        /// <summary>频段子任务初始化：校验接收机 + 配置接收参数（发射类无信号源/功放）</summary>
        protected override bool AsiggnBandSubTask()
        {
            try
            {
                ReceiverSock = GetSockByType(DeviceType.接收机);
                if (ReceiverSock == null)
                {
                    sendMessagelog("接收机通信sock为null");
                    return false;
                }
                ReceiverSock.RecInfoPush += ReceiverSock_RecInfoPush;
                SetupReceiveParams();
                sendMessagelog("模块初始化成功！");
                return true;
            }
            catch (Exception ex)
            {
                sendMessagelog($"模块初始化异常：{ex.Message}");
                return false;
            }
        }

        private void SetupReceiveParams()
        {
            ReceiverSock?.SendData((int)Communication.初始化, "200");
            ClsDelay.Delay.MicrosecondDelay(200000);
            //参考电平：限值 + 10 dB（无频段参数时给个常规值）
            double level = HasBandParms ? (CurrentBandParms!.StartLevel + 10) : 100;
            ReceiverSock?.SendData((int)Communication.参考电平, level.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>释放：取消订阅、关不了射频（发射类无输出设备），清空队列</summary>
        protected override void ReleaseDevice()
        {
            try
            {
                if (ReceiverSock != null)
                    ReceiverSock.RecInfoPush -= ReceiverSock_RecInfoPush;
                ReceiveDataQueue.Clear();
            }
            catch (Exception ex)
            {
                sendMessagelog($"释放设备异常：{ex.Message}");
            }
        }

        protected override void PauseOutput() { }   //发射类无射频输出，暂停无需关源
        protected override void ResumeOutput() { }

        private void ReceiverSock_RecInfoPush(string obj)
        {
            if (!double.TryParse(obj, out double value)) return;
            _readLevelQueue.Enqueue(value);
            while (_readLevelQueue.Count > 1) _readLevelQueue.Dequeue();   //只保留最新一条
        }

        // ==================== 外层循环：逐频点扫频测量 ====================
        /// <summary>
        /// 执行外层循环：测完当前频段所有频点。每频点：更新界面 → 接收机设频 → 读数 →
        /// 修正 → 与限值比较得裕度 → 打点 + 结果表。队列耗尽通知基类切下一频段。
        /// </summary>
        protected override void ExcuteOutsideLoopEvent()
        {
            if (TestContext.currentTestState == TestState.PAUSED)
                return;

            while (FrequencyQueue.Count > 0)
            {
                if (!IsLoopActive)
                    return;

                //1. 取频点 + 更新界面
                TestContext.CurrentFre = FrequencyQueue.Dequeue();
                TestContext.LastFrequency = TestContext.CurrentFre;
                RunOnUI(() =>
                {
                    RefreshCurrentFrequency();
                    UpdateTestProgress();
                });

                //2. 本频点限值（log 域插值，与 CS114 同一套逻辑）
                var bp = CurrentBandParms;
                double limit = bp == null ? double.NaN : GetReceiverLimit(TestContext.CurrentFre, bp);
                TestContext.LevelLimit = limit;

                //3. 接收机设频（中心频率 + 标记点 + 跨度）
                SetReadLevelFrequency();

                //4. 频点驻留等待（配置的 PauseTime，默认 10ms；给接收机扫过此处留时间）
                int pauseMs = bp?.PauseTime is double p && p > 0 ? (int)p : 10;
                DwellMs(pauseMs);

                //5. 读数 + 修正
                double raw = WaitSockValue(ReceiverSock, _readLevelQueue, 3000, (int)Communication.READ_DBUA);
                if (double.IsNaN(raw))
                {
                    if (!IsLoopActive) return;
                    AddLog("WARN", $"[{TestContext.CurrentFre:F6} MHz] 接收机读数超时，本频点记无效值");
                }
                double measured = double.IsNaN(raw) ? double.NaN : raw + LevelCompensation(TestContext.CurrentFre);
                TestContext.LastInjectLevel_dBuA = measured;   //复用字段：当前实测电平
                RunOnUI(() => RefreshLevelCard());

                //6. 裕度判定（发射类：裕度 = 限值 − 实测，≥0 合格）
                double margin = double.IsNaN(measured) || double.IsNaN(limit) ? double.NaN : limit - measured;
                string verdict = double.IsNaN(margin) ? "-" : (margin >= 0 ? "合格" : "超标");
                if (!double.IsNaN(margin) && margin < 0)
                    AddLog("WARN", $"[{TestContext.CurrentFre:F6} MHz] 超标：实测 {measured:F3} {UnitLabel}，" +
                        $"限值 {limit:F3} {UnitLabel}，裕度 {margin:F2} dB");
                else if (!double.IsNaN(margin))
                    AddLog("INFO", $"[{TestContext.CurrentFre:F6} MHz] 实测 {measured:F3} {UnitLabel}，" +
                        $"裕度 {margin:F2} dB（合格）");

                //7. 打点（x 必须 Hz）+ 结果表行
                double xHz = TestContext.CurrentFre * 1_000_000;
                if (!double.IsNaN(measured))
                    AddChannelData(GetSpectrumChartKey(), xHz, measured);
                AddResultRow(TestContext.CurrentFre, limit, measured, margin, verdict);
            }
            NotifyOutsideLoopComplete();
        }

        /// <summary>实测/限值所在的图表通道 Key（子类构造时注册的同一 Key）</summary>
        protected abstract string GetSpectrumChartKey();

        /// <summary>休眠 ms（分段睡眠，随时响应暂停/停止）</summary>
        private void DwellMs(int totalMs)
        {
            int slept = 0;
            while (slept < totalMs && IsLoopActive)
            {
                Thread.Sleep(10);
                slept += 10;
            }
        }

        private void SetReadLevelFrequency()
        {
            ReceiverSock?.SendData((int)Communication.FREQUENCY, TestContext.CurrentFre.ToString("F6"));
            ClsDelay.Delay.MicrosecondDelay(200000);
            ReceiverSock?.SendData((int)Communication.MARKFREQ, TestContext.CurrentFre.ToString("F6"));
            ClsDelay.Delay.MicrosecondDelay(200000);
            double span = TestContext.CurrentFre switch
            {
                > 200 => 200,
                _ => TestContext.CurrentFre
            };
            ReceiverSock?.SendData((int)Communication.SPAN, span.ToString("F6"));
        }

        /// <summary>等待接收机回数（队列模式同步等，超时返回 NaN；等待中可被暂停/停止打断）</summary>
        protected double WaitSockValue(SockConnect? sock, Queue<double> queue, int timeoutMs, int readtype)
        {
            if (sock == null) return double.NaN;
            sock.SendData(readtype, "");
            DateTime start = DateTime.Now;
            while (queue.Count == 0)
            {
                if (!IsLoopActive) return double.NaN;   //等待中用户停止/暂停/本轮已作废
                if ((DateTime.Now - start).TotalMilliseconds > timeoutMs) return double.NaN;
                Thread.Sleep(10);
            }
            return queue.Dequeue();
        }

        // ==================== 参数卡（当前频段 + 电平检测） ====================
        public override IEnumerable<ParamGroup> GetParamGroups()
        {
            foreach (var g in base.GetParamGroups())
                yield return g;
            WriteLevelCard();
            yield return LevelGroup;
        }

        private ParamGroup LevelGroup => Group("Card.Level", "电平检测");

        private string LevelLimitModeText => CurrentBandParms == null
            ? "-" : CurrentBandParms.LimitMode == LimitType.Const ? "定值" : "文件";

        private string LimitText => HasBandParms ? $"{TestContext.LevelLimit:F3} {UnitLabel}" : "-";

        private string MeasuredText => HasBandParms
            ? (double.IsNaN(TestContext.LastInjectLevel_dBuA) ? "-" : $"{TestContext.LastInjectLevel_dBuA:0.###} {UnitLabel}")
            : "-";

        private string MarginText
        {
            get
            {
                if (!HasBandParms || double.IsNaN(TestContext.LevelLimit) || double.IsNaN(TestContext.LastInjectLevel_dBuA))
                    return "-";
                return $"{TestContext.LevelLimit - TestContext.LastInjectLevel_dBuA:F2} dB";
            }
        }

        private void WriteLevelCard() => LevelGroup.Set(
            ("限值类型", LevelLimitModeText),
            ("限值等级", LimitText),
            ("当前实测值", MeasuredText),
            ("裕度(限值-实测)", MarginText));

        /// <summary>刷新实测值/裕度两行（UI 线程调用）</summary>
        public void RefreshLevelCard()
        {
            LevelGroup.Set(("当前实测值", MeasuredText), ("裕度(限值-实测)", MarginText));
        }

        // ==================== 限值参考线 ====================
        /// <summary>
        /// 配置映射完成后整组写入限值折线（与 CS114.BuildLimitCurve 同一画法）：
        /// 每频段一段——定值模式两点连线（起始=终止即水平线），文件模式按限值表逐点；
        /// 各频段首尾相接铺满整个测试频率范围。x 单位 Hz。
        /// </summary>
        public void BuildLimitCurve()
        {
            LimitLine.XData.Clear();
            LimitLine.YData.Clear();
            for (int b = 0; b < CommonFreAndHardParms.Count && b < RuntimeParam.ImmunityLimit.Count; b++)
            {
                var fre = CommonFreAndHardParms[b].FreqBase;
                double fMinMHz = GenericHelper.ConvertToMhzData(fre.StartFre, fre.StartFreUnit);
                double fMaxMHz = GenericHelper.ConvertToMhzData(fre.EndFre, fre.EndFreUnit);
                if (fMaxMHz <= fMinMHz) continue;
                var bp = RuntimeParam.ImmunityLimit[b];

                if (bp.LimitMode == LimitType.File && bp.LimitTable.Count > 0)
                {
                    var pts = bp.LimitTable
                        .Select(p => (double.TryParse(p.FreqMHz, NumberStyles.Float, CultureInfo.InvariantCulture, out double db) ? db : 0.0,
                                      double.TryParse(p.LevelDB, NumberStyles.Float, CultureInfo.InvariantCulture, out double ldb) ? ldb : 0.0))
                        .Where(p => p.Item1 >= fMinMHz && p.Item1 <= fMaxMHz)
                        .OrderBy(p => p.Item1).ToList();
                    foreach (var (fMHz, lv) in pts)
                    {
                        LimitLine.XData.Add(fMHz * 1_000_000);
                        LimitLine.YData.Add(lv);
                    }
                }
                else
                {
                    LimitLine.XData.Add(fMinMHz * 1_000_000);
                    LimitLine.YData.Add(bp.StartLevel);
                    LimitLine.XData.Add(fMaxMHz * 1_000_000);
                    LimitLine.YData.Add(bp.EndLevel);
                }
            }
        }

        // ==================== 结果表 ====================
        /// <summary>往 Table.Result 追加一行（行集合与界面共享引用，加行界面自动刷新）</summary>
        private void AddResultRow(double freqMHz, double limit, double measured, double margin, string verdict)
        {
            var t = Table("Table.Result", ModuleCatalog.Get("Table.Result").Title, ModuleCatalog.Get("Table.Result").Columns);
            string fmt(double v, string unit) => double.IsNaN(v) ? "-" : $"{v:0.###}";
            var row = new ResultRow(
                $"{freqMHz:F6}",
                double.IsNaN(limit) ? "-" : $"{limit:0.###} {UnitLabel}",
                double.IsNaN(measured) ? "-" : $"{measured:0.###} {UnitLabel}",
                "PK",                                     //发射类测量固定峰值检波（TODO：按配置切换 QP/AV）
                double.IsNaN(margin) ? "-" : $"{margin:F2} dB",
                verdict);
            row.No = t.Rows.Count + 1;
            row.IsOverLimit = verdict == "超标";
            row.IsCritical = !double.IsNaN(margin) && margin >= 0 && margin < 3;   //余量<3dB 临界
            t.Rows.Add(row);
        }

        // ==================== 限值插值（与 CS114RunTask 同一套公式） ====================
        /// <summary>本频点限值：文件模式按 LimitTable 在 log10(f) 域插值；定值起止不同按频段起止频率 log 域连线</summary>
        protected double GetReceiverLimit(double freqMHz, CS114BandParms bp, int bandIndex = -1)
        {
            if (bp.LimitMode == LimitType.File && bp.LimitTable.Count > 0)
            {
                var pts = bp.LimitTable
                    .Select(p => (double.TryParse(p.FreqMHz, NumberStyles.Float, CultureInfo.InvariantCulture, out double db) ? db : 0.0,
                                  double.TryParse(p.LevelDB, NumberStyles.Float, CultureInfo.InvariantCulture, out double ldb) ? ldb : 0.0))
                    .ToList();
                return InterpLog(freqMHz, pts);
            }
            if (bp.StartLevel == bp.EndLevel)
                return bp.StartLevel;
            var fre = CommonFreAndHardParms[bandIndex >= 0 ? bandIndex : TestContext.CurrentBandIndex].FreqBase;
            double startFreq = GenericHelper.ConvertToMhzData(fre.StartFre, fre.StartFreUnit);
            double endFreq = GenericHelper.ConvertToMhzData(fre.EndFre, fre.EndFreUnit);
            if (startFreq <= 0 || endFreq <= startFreq)
                return bp.StartLevel;
            return InterpLog(freqMHz, [(startFreq, bp.StartLevel), (endFreq, bp.EndLevel)]);
        }

        /// <summary>对数域插值（锚点在 log10(f) 轴上连线，范围外钳到端点值）</summary>
        protected static double InterpLog(double x, List<(double X, double Y)> pts)
        {
            var valid = pts.Where(p => p.X > 0).ToList();
            if (valid.Count == 0) return 0;
            if (valid.Count == 1 || x <= valid[0].X) return valid[0].Y;
            if (x >= valid[^1].X) return valid[^1].Y;
            double logX = Math.Log10(x);
            double prevLog = Math.Log10(valid[0].X);
            for (int i = 1; i < valid.Count; i++)
            {
                double curLog = Math.Log10(valid[i].X);
                if (logX <= curLog)
                {
                    double t = (logX - prevLog) / (curLog - prevLog);
                    return valid[i - 1].Y + t * (valid[i].Y - valid[i - 1].Y);
                }
                prevLog = curLog;
            }
            return valid[^1].Y;
        }

        /// <summary>线损/系数表线性插值（表为空返回 0，不参与补偿）</summary>
        protected static double InterpCoeff(List<CS114CoeffPoint>? pts, double freqMHz)
        {
            if (pts == null || pts.Count == 0) return 0.0;
            var list = pts.OrderBy(p => double.TryParse(p.FreqMHz, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : 0.0)
                          .Select(p => (double.TryParse(p.FreqMHz, NumberStyles.Float, CultureInfo.InvariantCulture, out double d2) ? d2 : 0.0,
                                        double.TryParse(p.CoefDB, NumberStyles.Float, CultureInfo.InvariantCulture, out double c) ? c : 0.0))
                          .ToList();
            if (list.Count == 0) return 0;
            double x = freqMHz;
            if (list.Count == 1 || x <= list[0].Item1) return list[0].Item2;
            if (x >= list[^1].Item1) return list[^1].Item2;
            for (int i = 1; i < list.Count; i++)
            {
                if (x <= list[i].Item1)
                {
                    double t = (x - list[i - 1].Item1) / (list[i].Item1 - list[i - 1].Item1);
                    return list[i - 1].Item2 + t * (list[i].Item2 - list[i - 1].Item2);
                }
            }
            return list[^1].Item2;
        }

        // ==================== 保存数据（安全裕度报告 CSV） ====================
        /// <summary>
        /// 保存测试数据：Frequency(MHz),Limit,Measured,Margin(dB),Verdict。
        /// 目录：System\数据文件\{SaveDirName}；文件名：模板名_时间戳.csv。
        /// 裕度 = 限值 − 实测（≥0 合格）。发射类测试不区分校准/正式阶段（校准阶段按正式测量执行）。
        /// </summary>
        public override void SaveData()
        {
            int n = Math.Min(MeasuredLine.XData.Count, MeasuredLine.YData.Count);
            if (n == 0)
            {
                System.Windows.MessageBox.Show($"{TaskTitle}暂无数据，测试结束后再保存", "提示");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Frequency(MHz),Limit({UnitLabel}),Measured({UnitLabel}),Margin(dB),Verdict");
            for (int i = 0; i < n; i++)
            {
                double freqMHz = MeasuredLine.XData[i] / 1e6;
                double measured = MeasuredLine.YData[i];
                var bp = BandParmsOf(freqMHz);
                double limit = bp == null ? double.NaN : GetReceiverLimit(freqMHz, bp, BandIndexOf(freqMHz));
                string limitText = double.IsNaN(limit) ? "-" : limit.ToString("0.###", CultureInfo.InvariantCulture);
                string marginText = double.IsNaN(limit) ? "-" : (limit - measured).ToString("0.###", CultureInfo.InvariantCulture);
                string verdict = double.IsNaN(limit) ? "-" : (limit - measured >= 0 ? "合格" : "超标");
                sb.AppendLine($"{freqMHz.ToString("F6", CultureInfo.InvariantCulture)},{limitText}," +
                    $"{measured.ToString("0.###", CultureInfo.InvariantCulture)},{marginText},{verdict}");
            }

            string dir = Path.Combine(GenericParameter.SysPath, "System", "数据文件", SaveDirName);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir,
                $"{SanitizeFileName(TestTemplateName)}_{DateTime.Now:yyyyMMddHHmmss}.csv");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

            LastSavedReportPath = path;   // 供界面把曲线图保存到报告同目录
            AddLog("INFO", $"【保存数据】{TaskTitle} 测试数据已保存：{path}（共 {n} 个频点）");
            System.Windows.MessageBox.Show($"测试数据保存到{path}完成", "提示");
        }

        /// <summary>按频率(MHz)找所属频段下标（容差 1e-9 吸收浮点误差）；找不到返回 -1</summary>
        protected int BandIndexOf(double freqMHz)
        {
            for (int b = 0; b < CommonFreAndHardParms.Count; b++)
            {
                var fre = CommonFreAndHardParms[b].FreqBase;
                double start = GenericHelper.ConvertToMhzData(fre.StartFre, fre.StartFreUnit);
                double end = GenericHelper.ConvertToMhzData(fre.EndFre, fre.EndFreUnit);
                if (freqMHz >= start - 1e-9 && freqMHz <= end + 1e-9)
                    return b;
            }
            return -1;
        }

        private CS114BandParms? BandParmsOf(double freqMHz)
        {
            int b = BandIndexOf(freqMHz);
            return b >= 0 && b < RuntimeParam.ImmunityLimit.Count ? RuntimeParam.ImmunityLimit[b] : null;
        }

        /// <summary>文件名清洗：模板名可能带路径非法字符，统一换成下划线</summary>
        protected static string SanitizeFileName(string name)
            => string.Join("_", (name ?? "未命名").Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
    }
}
