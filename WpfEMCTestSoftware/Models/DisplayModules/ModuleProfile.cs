using MaterialDesignThemes.Wpf;
using System.Collections.ObjectModel;
using WpfEMCTestSoftware.RunTasks;

namespace WpfEMCTestSoftware.Models.DisplayModules
{
    /// <summary>
    /// 模块类型：决定模块在运行页（RunConfigPage）的哪个容器里渲染
    /// </summary>
    public enum ModuleKind
    {
        /// <summary>曲线图 —— 中央图表区</summary>
        Chart,
        /// <summary>参数卡 —— 右侧参数卡区</summary>
        Card,
        /// <summary>表格 —— 左侧"结果"页（结果表 / 校验比对表）</summary>
        Table,
        /// <summary>附加操作按钮 —— 底部按钮条</summary>
        Control,
    }

    /// <summary>
    /// 测试大类：新建模板时决定"默认显示哪些模块"
    /// </summary>
    public enum TestCategory
    {
        /// <summary>EMI 传导发射：CE101 / CE102 / CE107</summary>
        EMI_Conducted,
        /// <summary>EMI 辐射发射：RE101 / RE102</summary>
        EMI_Radiated,
        /// <summary>EMS 传导敏感度：CS101 / CS102 / CS112 / CS114 / CS115 / CS116</summary>
        EMS_Conducted,
        /// <summary>EMS 辐射敏感度：RS101 / RS103</summary>
        EMS_Radiated,
        /// <summary>混响室</summary>
        ReverbChamber,
        Unknown,
    }

    /// <summary>
    /// 测试模式：说明书里每个 EMS 测试项都分「参考校准」和「EUT 测试」两段
    /// </summary>
    public enum RunMode
    {
        /// <summary>参考校准测试（Reference Calibration）</summary>
        Calibration,
        /// <summary>EUT 测试（EUT Qualification）</summary>
        Eut,
    }

    /// <summary>
    /// 模块目录条目（"零件表"的一条）：描述一个模块长什么样，不含数据
    /// </summary>
    public class ModuleDescriptor
    {
        public string Key { get; init; } = "";
        public ModuleKind Kind { get; init; } = ModuleKind.Chart;
        public string Title { get; init; } = "";

        // ===== Chart 专用 =====
        public string YAxisTitle { get; init; } = "";
        public bool IsLogX { get; init; }
        /// <summary>Y 轴下限（NaN=自动按数据自适应）。固定量程的图直接配数值；
        /// 要跟限制线联动的图（注入电平）保持 NaN，由 ChannelChartModule 按 IsYReferenceLine 参考线推算</summary>
        public double YMin { get; init; } = double.NaN;
        /// <summary>Y 轴上限（NaN=自动），语义同 YMin</summary>
        public double YMax { get; init; } = double.NaN;

        // ===== Control 专用 =====
        public PackIconKind Icon { get; init; } = PackIconKind.CircleOutline;
        public string Foreground { get; init; } = "#FF00B3FF";

        // ===== Table 专用 =====
        public string[] Columns { get; init; } = [];
    }

    /// <summary>
    /// 模块目录：系统里所有可用模块的"零件表"
    /// 新增一条监测曲线 / 一张参数卡 / 一列结果表 = 这里加一行 + 在注册表里引用
    /// </summary>
    public static class ModuleCatalog
    {
        public static readonly Dictionary<string, ModuleDescriptor> Descriptors = new()
        {
            // ============================================================
            // 一、图表（Chart）：未注册曲线通道时图显示为空，不影响其他模块
            // ============================================================
            // ---- 通用（曲线通道由各 RunTask 子类在构造函数里自行 RegisterChannel）----
            // Chart.Signal/Field/Power：DO160(混响室) 注册；Chart.Signal/Power：CS114 也注册（同 Key 自建集合）
            // YMin/YMax：固定量程直接配数值（2026-09-21 定稿：信号源 -20~20 dBm、功率 0~100 W 不许出负数）；
            // 注入电平 YMin/YMax 保持 NaN —— 由 ChannelChartModule 按 limit 参考线动态推算
            ["Chart.Signal"] = new() { Key = "Chart.Signal", Kind = ModuleKind.Chart, Title = "信号源", YAxisTitle = "dBm", IsLogX = true, YMin = -20, YMax = 20 },
            ["Chart.Field"] = new() { Key = "Chart.Field", Kind = ModuleKind.Chart, Title = "场强", IsLogX = true, YAxisTitle = "v/m" },
            ["Chart.Power"] = new() { Key = "Chart.Power", Kind = ModuleKind.Chart, Title = "功率", YAxisTitle = "W", IsLogX = true, YMin = 0, YMax = 100 },
            // ---- EMS 传导 / 辐射：RunTask 里 RegisterChannel 后即可启用 ----
            // IsLogX=true：EMS 扫频跨 4~5 个数量级（10kHz~数百MHz），线性轴会把数据挤成一团"糊"在左侧，
            // 对数轴按十倍频程出刻度（10kHz/100kHz/1MHz/10MHz...），与老软件图表一致（2026-09-21 UI 优化）

            ["Chart.InjectLevel"] = new() { Key = "Chart.InjectLevel", Kind = ModuleKind.Chart, Title = "注入电平", YAxisTitle = "dBμA", IsLogX = true, YMin = 0, YMax = 100 },
            // ---- EMI 发射：频谱 + 限值线 ----
            ["Chart.Spectrum"] = new() { Key = "Chart.Spectrum", Kind = ModuleKind.Chart, Title = "频谱", YAxisTitle = "dBμV", IsLogX = true },
            ["Chart.LimitLine"] = new() { Key = "Chart.LimitLine", Kind = ModuleKind.Chart, Title = "限值线", YAxisTitle = "dBμV", IsLogX = true },
            // ---- EMI 发射：传导/辐射安全裕度专用频谱图（实测曲线 + limit 参考线同一通道）----
            // CE101 纵轴 dBμA（电流探头传导发射）；RE102 纵轴 dBμV/m（天线辐射发射）；
            // 两条线由 RunTask 在同一通道内 RegisterChannel（limit 打 IsYReferenceLine 标记）
            ["Chart.CESpectrum"] = new() { Key = "Chart.CESpectrum", Kind = ModuleKind.Chart, Title = "CE101 传导发射频谱", YAxisTitle = "dBμA", IsLogX = true },
            ["Chart.RESpectrum"] = new() { Key = "Chart.RESpectrum", Kind = ModuleKind.Chart, Title = "RE102 辐射发射频谱", YAxisTitle = "dBμV/m", IsLogX = true },

            // ============================================================
            // 二、参数卡（Card）：卡片只画键值对，内容由 RunTask 的 IPanelDataProvider 提供
            // ============================================================
            ["Card.TestInfo"] = new() { Key = "Card.TestInfo", Kind = ModuleKind.Card, Title = "测试信息" },
            ["Card.Band"] = new() { Key = "Card.Band", Kind = ModuleKind.Card, Title = "当前频段" },
            ["Card.Level"] = new() { Key = "Card.Level", Kind = ModuleKind.Card, Title = "电平检测" },
            ["Card.Modulation"] = new() { Key = "Card.Modulation", Kind = ModuleKind.Card, Title = "调制方式" },
            ["Card.Feedback"] = new() { Key = "Card.Feedback", Kind = ModuleKind.Card, Title = "反馈控制" },
            ["Card.RefCal"] = new() { Key = "Card.RefCal", Kind = ModuleKind.Card, Title = "参考校准" },
            ["Card.Position"] = new() { Key = "Card.Position", Kind = ModuleKind.Card, Title = "天线位置" },
            ["Card.PowerCtrl"] = new() { Key = "Card.PowerCtrl", Kind = ModuleKind.Card, Title = "电源控制" },
            ["Card.Receiver"] = new() { Key = "Card.Receiver", Kind = ModuleKind.Card, Title = "接收机设置" },
            ["Card.Detector"] = new() { Key = "Card.Detector", Kind = ModuleKind.Card, Title = "检波器" },
            // 说明书 EMS 项：监视 EUT 是否敏感，发现敏感确定门限电平
            ["Card.EutState"] = new() { Key = "Card.EutState", Kind = ModuleKind.Card, Title = "EUT 状态监测" },

            // ============================================================
            // 三、表格（Table）：列头由目录决定，行由 RunTask 提供
            // ============================================================
            // 说明书：测试结果自动生成"结果判断、余量大小、频率对应检波器" → 含检波器列
            ["Table.Result"] = new()
            {
                Key = "Table.Result",
                Kind = ModuleKind.Table,
                Title = "测试结果",
                Columns = ["频率", "限值", "实测值", "检波器", "余量", "判定"],
            },
            ["Table.CheckVerify"] = new()
            {
                Key = "Table.CheckVerify",
                Kind = ModuleKind.Table,
                Title = "系统校验比对",
                Columns = ["校验频率", "限值", "限值-6dB", "示波器读数", "软件读数", "偏差"],
            },

            // ============================================================
            // 四、附加操作按钮（Control）：主按钮（开始/停止/暂停/保存）通用，这些按测试类型配
            // ============================================================
            ["Ctl.Result"] = new() { Key = "Ctl.Result", Kind = ModuleKind.Control, Title = "生成结果", Icon = PackIconKind.ChartBox, Foreground = "#FF00B3FF" },
            ["Ctl.Report"] = new() { Key = "Ctl.Report", Kind = ModuleKind.Control, Title = "导出报告", Icon = PackIconKind.FileExport, Foreground = "#FFC300FF" },
            ["Ctl.ExportCharts"] = new() { Key = "Ctl.ExportCharts", Kind = ModuleKind.Control, Title = "导出曲线图", Icon = PackIconKind.Image, Foreground = "#FF00B3FF" },
            ["Ctl.Screenshot"] = new() { Key = "Ctl.Screenshot", Kind = ModuleKind.Control, Title = "接收机截图", Icon = PackIconKind.Camera, Foreground = "#FF00B3FF" },
            ["Ctl.CurveCenter"] = new() { Key = "Ctl.CurveCenter", Kind = ModuleKind.Control, Title = "曲线居中", Icon = PackIconKind.ImageFilterCenterFocus, Foreground = "#FF00B3FF" },
            ["Ctl.Polarization"] = new() { Key = "Ctl.Polarization", Kind = ModuleKind.Control, Title = "改变极化", Icon = PackIconKind.SwapHorizontal, Foreground = "#FFFF8C00" },
            ["Ctl.Close"] = new() { Key = "Ctl.Close", Kind = ModuleKind.Control, Title = "关闭测试", Icon = PackIconKind.CloseBox, Foreground = "#FFFF0C00" },
        };

        public static ModuleDescriptor Get(string key)
            => Descriptors.TryGetValue(key, out var d) ? d : new ModuleDescriptor { Key = key, Title = key };
    }

    /// <summary>
    /// 一个测试项要显示的全套模块（图表 + 参数卡 + 表格 + 附加按钮）
    /// </summary>
    public class TestDisplayProfile
    {
        public string[] Charts { get; init; } = [];
        public string[] Cards { get; init; } = [];
        public string[] Tables { get; init; } = [];
        public string[] Controls { get; init; } = [];
    }

    /// <summary>
    /// 测试类别默认预设（第 2 层配置）：新建模板且模板里没存显示配置时用它
    /// </summary>
    public static class TestCategoryPreset
    {
        /// <summary>根据测试项名称判断测试大类</summary>
        public static TestCategory CategoryOf(string? testName) => testName switch
        {
            "CE101" or "CE102" or "CE107" => TestCategory.EMI_Conducted,
            "RE101" or "RE102" => TestCategory.EMI_Radiated,
            "CS101" or "CS102" or "CS112" or "CS114" or "CS115" or "CS116" => TestCategory.EMS_Conducted,
            "RS101" or "RS103" => TestCategory.EMS_Radiated,
            "混响室" or "ReverberationChamber" => TestCategory.ReverbChamber,
            _ => TestCategory.Unknown,
        };

        /// <summary>各类测试的默认模块集合</summary>
        public static TestDisplayProfile Of(TestCategory c) => c switch
        {
            // EMI 传导发射：频谱 + 限值线，接收机参数卡 + 校验比对表
            TestCategory.EMI_Conducted => new TestDisplayProfile
            {
                Charts = ["Chart.Spectrum", "Chart.LimitLine"],
                Cards = ["Card.Band", "Card.Receiver", "Card.Detector"],
                Tables = ["Table.CheckVerify", "Table.Result"],
                Controls = ["Ctl.Screenshot", "Ctl.CurveCenter", "Ctl.Result", "Ctl.ExportCharts", "Ctl.Report"],
            },
            // EMI 辐射发射：同上（天线/场强卡由 RunTask 决定是否给数据）
            TestCategory.EMI_Radiated => new TestDisplayProfile
            {
                Charts = ["Chart.Spectrum", "Chart.LimitLine"],
                Cards = ["Card.Band", "Card.Receiver", "Card.Detector"],
                Tables = ["Table.Result"],
                Controls = ["Ctl.Screenshot", "Ctl.CurveCenter", "Ctl.Result", "Ctl.ExportCharts", "Ctl.Report"],
            },
            // EMS 传导敏感度：信号源 + 前向/反向功率 + 注入电平
            TestCategory.EMS_Conducted => new TestDisplayProfile
            {
                Charts = ["Chart.Signal", "Chart.Power", "Chart.InjectLevel"],
                Cards = ["Card.Band", "Card.Level", "Card.Modulation", "Card.Feedback", "Card.RefCal", "Card.EutState"],
                Tables = ["Table.Result"],
                Controls = ["Ctl.Close", "Ctl.Result", "Ctl.ExportCharts", "Ctl.Report"],
            },
            // EMS 辐射敏感度：信号源 + 场强 + 前向功率（Chart.Power 内前向/反向两条曲线）
            TestCategory.EMS_Radiated => new TestDisplayProfile
            {
                Charts = ["Chart.Signal", "Chart.Field", "Chart.Power"],
                Cards = ["Card.Band", "Card.Level", "Card.RefCal", "Card.Position", "Card.EutState"],
                Tables = ["Table.Result"],
                Controls = ["Ctl.Polarization", "Ctl.Close", "Ctl.Result", "Ctl.ExportCharts", "Ctl.Report"],
            },
            // 混响室
            TestCategory.ReverbChamber => new TestDisplayProfile
            {
                Charts = ["Chart.Signal", "Chart.Field", "Chart.Power"],
                Cards = ["Card.Band", "Card.EutState"],
                Tables = ["Table.Result"],
                Controls = ["Ctl.ExportCharts", "Ctl.Report"],
            },
            // 未知测试项：三大通用图表 + 频段卡 + 结果表（= 改造前行为）
            _ => new TestDisplayProfile
            {
                Charts = ["Chart.Signal", "Chart.Field", "Chart.Power"],
                Cards = ["Card.Band", "Card.EutState"],
                Tables = ["Table.Result"],
                Controls = ["Ctl.ExportCharts", "Ctl.Report"],
            },
        };
    }

    /// <summary>
    /// 第 3 层配置：测试项 → 模块集合（每个测试项一行）
    /// ★ 新增测试项时只加这里一行；模板 JSON 里的 displayModules 可再按模板覆盖
    /// </summary>
    public static class ModuleProfileRegistry
    {
        /// <summary>未注册测试项的回退配置（= 改造前的运行页行为）</summary>
        public static readonly TestDisplayProfile Default = TestCategoryPreset.Of(TestCategory.Unknown);

        private static readonly Dictionary<string, TestDisplayProfile> Profiles = new()
        {
            // ===== 已接入运行流 =====
            ["混响室"] = TestCategoryPreset.Of(TestCategory.ReverbChamber),
            ["ReverberationChamber"] = TestCategoryPreset.Of(TestCategory.ReverbChamber),
            ["CS114"] = TestCategoryPreset.Of(TestCategory.EMS_Conducted),

            // ===== 安全裕度测试项：CE101 传导发射 / RE102 辐射发射 / RS103 辐射敏感度 =====
            // 曲线 key 必须与各 RunTask 构造函数里 RegisterChannel 的 key 完全一致，否则图是空的
            // CE101（传导安全裕度·发射类）：实测 dBμA + 限值参考线同图；接收机参数卡 + 结果表
            ["CE101"] = new TestDisplayProfile
            {
                Charts = ["Chart.CESpectrum"],
                Cards = ["Card.Band", "Card.Level"],
                Tables = ["Table.Result"],
                Controls = ["Ctl.Result", "Ctl.ExportCharts", "Ctl.Report"],
            },
            // RE102（辐射安全裕度·发射类）：实测 dBμV/m + 限值参考线同图
            ["RE102"] = new TestDisplayProfile
            {
                Charts = ["Chart.RESpectrum"],
                Cards = ["Card.Band", "Card.Level"],
                Tables = ["Table.Result"],
                Controls = ["Ctl.Result", "Ctl.ExportCharts", "Ctl.Report"],
            },
            // RS103（辐射安全裕度·敏感度类）：信号源输出 + 场强（含限值参考线）+ 前向/反向功率
            ["RS103"] = new TestDisplayProfile
            {
                Charts = ["Chart.Signal", "Chart.Field", "Chart.Power"],
                Cards = ["Card.Band", "Card.Level"],
                Tables = ["Table.Result"],
                Controls = ["Ctl.Polarization", "Ctl.Result", "Ctl.ExportCharts", "Ctl.Report"],
            },

            // ===== 以下测试项等 RunTask 建好后即可直接生效（曲线通道在 RunTask 里 RegisterChannel）=====
            ["CS101"] = new TestDisplayProfile
            {
                Charts = ["Chart.Signal", "Chart.Power", "Chart.InjectLevel"],
                Cards = ["Card.Band", "Card.Level", "Card.RefCal"],
                Tables = ["Table.Result"],
                Controls = ["Ctl.Close", "Ctl.ExportCharts", "Ctl.Report"],
            },
            ["CE102"] = TestCategoryPreset.Of(TestCategory.EMI_Conducted),
        };

        /// <summary>取测试项的模块配置，未注册则回退默认</summary>
        public static TestDisplayProfile Get(string? testName)
            => testName != null && Profiles.TryGetValue(testName, out var p) ? p : Default;
    }

    /// <summary>
    /// 模块构建器：按 key 列表实例化模块并绑定任务数据源
    /// </summary>
    public static class ModuleHost
    {
        /// <summary>构建图表模块（曲线走 BaseRunTask.CurveChannels）</summary>
        public static ObservableCollection<ITestDisplayModule> BuildCharts(IEnumerable<string> keys, BaseRunTask? task)
        {
            var list = new ObservableCollection<ITestDisplayModule>();
            foreach (var key in keys)
            {
                var d = ModuleCatalog.Get(key);
                //YMin/YMax 经模块属性 → DataTemplate 绑定 → UserChart.YMinLimit/YMaxLimit 落地（NaN=自动）
                var module = new ChannelChartModule(d.Key, d.Title, d.YAxisTitle, d.IsLogX, d.YMin, d.YMax);
                if (task != null) module.Bind(task);
                list.Add(module);
            }
            return list;
        }

        /// <summary>构建参数卡模块（数据走 IPanelDataProvider.GetParamGroups）</summary>
        public static ObservableCollection<ParamCardModule> BuildCards(IEnumerable<string> keys, BaseRunTask? task)
        {
            var list = new ObservableCollection<ParamCardModule>();
            foreach (var key in keys)
            {
                var d = ModuleCatalog.Get(key);
                var module = new ParamCardModule(d.Key, d.Title);
                if (task != null) module.Bind(task);
                list.Add(module);
            }
            return list;
        }

        /// <summary>构建结果/校验表格模块（数据走 IPanelDataProvider.GetResultTables）</summary>
        public static ObservableCollection<ResultTableModule> BuildTables(IEnumerable<string> keys, BaseRunTask? task)
        {
            var list = new ObservableCollection<ResultTableModule>();
            foreach (var key in keys)
            {
                var d = ModuleCatalog.Get(key);
                var module = new ResultTableModule(d.Key, d.Title, d.Columns);
                if (task != null) module.Bind(task);
                list.Add(module);
            }
            return list;
        }

        /// <summary>构建附加操作按钮（纯描述，无数据绑定）</summary>
        public static ObservableCollection<ControlModule> BuildControls(IEnumerable<string> keys)
        {
            var list = new ObservableCollection<ControlModule>();
            foreach (var key in keys)
            {
                var d = ModuleCatalog.Get(key);
                list.Add(new ControlModule(d.Key, d.Title, d.Icon, d.Foreground));
            }
            return list;
        }
    }
}
