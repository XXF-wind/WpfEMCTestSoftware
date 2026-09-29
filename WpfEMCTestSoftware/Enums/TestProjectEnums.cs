using System.ComponentModel;

namespace WpfEMCTestSoftware.Enums
{
    public enum TestProject
    {
        混响室,
        CE101,
        CS114,
        RE102,
        RS103
    }
    public enum TestProjectEN
    {
        ReverberationChamber,
        CE101,
        CS114,
        RE102,
        RS103
    }
    /// <summary>
    /// 频率单位枚举
    /// </summary>
    public enum FrequencyUnit
    {
        Hz,
        KHz,
        MHz,
        GHz
    };
    public enum STEPMODE
    {
        LIN,//线性增长，每次增长的频率差是相同的，频率的增长是均匀的
        LOG,//百分比

    };
    /// <summary>
    /// 调制类型
    /// </summary>
    public enum ModulationType
    {
        PWM,
        AM,
    };
    public enum LimitType
    {
        [Description("常量")]
        Const,
        [Description("文件")]
        File,
    };
    /// <summary>
    /// 单频点功率逼近的退出原因（对应 Qt handleTerminationConditions）。
    /// 正常退出只有一种：读数进入容差窗口（达到电平限值）→ 驻留调制；
    /// 其余均为异常退出（打到最大步数 / 超出设备能力上限），只记录告警并结束本频点。
    /// 扩展方式：新增检测 → 本枚举加一项 + CheckApproachExit 加一条判定 + HandleApproachExit 加一条日志分支。
    /// </summary>
    public enum ApproachExitReason
    {
        /// <summary>未满足任何退出条件 → 继续调整信号源功率逼近</summary>
        Continue,
        /// <summary>【正常】读数进入容差窗口，已达电平限值 → 驻留调制</summary>
        ReachedLevel,
        /// <summary>【正常】读数进入容差窗口，已达功率限制 → 驻留调制</summary>
        ReachedPower,
        /// <summary>【异常】调整次数超过最大值（防死循环兜底）</summary>
        AdjustCountExceeded,
        /// <summary>【异常】信号源输出超过上限（功放最大输出 + 信号线线损）</summary>
        SignalOverLimit,
        /// <summary>【异常·预留】功放输出超过额定功率</summary>
        AmplifierOverRated,
        /// <summary>【异常·预留】驻波比超限（VSWR）</summary>
        VswrOverLimit,
    }
    /// <summary>
    /// EMC测试执行标准
    /// </summary>
    public enum EMCStandard
    {
        [Description("IEC61000-4-21")]
        IEC61000_4_21,
        [Description("DO-160F")]
        DO_160F,
        [Description("DO-160G")]
        DO_160G,
        [Description("GJB151B")]
        GJB151B,
        [Description("GJB 33014.11")]
        GBT_33014_11,
        [Description("GB 17626.21")]
        GB_17626_21,
        None,
    }
    /// <summary>
    /// 切换频段动作
    /// </summary>
    public enum BandSwitchConfirmMode
    {
        [Description("需弹窗提示")]
        NeedConfirm,
        [Description("直接执行")]
        NoConfirm,
    }
    /// <summary>
    ///  混响室EMS辐射发射测试 工作模式
    /// </summary>
    public enum ReverbTestMode
    {
        [Description("mode-tuned 步进模式")]
        ModeTuned,
        [Description("mode-stirrer 搅拌模式")]
        ModeStirrer,
        [Description("静止测试模式")]
        StaticTest
    }
    /// <summary>
    /// 测试阶段枚举
    /// </summary>
    public enum TestStageType
    {
        [Description("校准测试")]
        CalibrationTest,
        [Description("正式测试")]
        FormalTest,
    }

    /// <summary>
    /// 测试模式
    /// </summary>
    public enum TestMode
    {
        [Description("mode-tuned频率步进")]
        ModeTuned_FreqStep,
        [Description("mode-tuned步进频率")]
        ModeTuned_StepFreq,
        [Description("mode-stirrer搅拌模式")]
        ModeStirrer,
        [Description("静止测试模式")]
        StaticMode,
        [Description("预校准")]
        PreCalibration,
        [Description("正式测试")]
        FormalTest
    }
    /// <summary>
    /// 图表数据类型，用来区分发送给哪个图表
    /// </summary>
    public enum ChartDataType
    {
        SignalSource,   //信号源
        FieldStrength, //场强探头      
        PowerMeter,   //功率计

    }
    public enum LineType
    {
        SignalSource,   //信号源
        FieldStrength, //场强探头      
        PowerMeter,   //功率计

    }
    public enum TestState
    {
        IDLE,
        PRERUN,         //准备开始 按下开始按钮到程序
        RUNNING,
        KEEPFRERUNNING, //保持单频点输出
        ONCheckDevice,  //自检设备
        PENDING_KEEP_FRE,
        PAUSED,
        STOPPED,
        EXIT,
        COMPLETED
    };

    public enum DeviceType
    {
        未知设备 = -1,
        信号源,
        接收机,
        场强仪,
        功放,
        功率计,
        衰减器,
        倍频器,
        开关矩阵,
        示波器,
        频谱仪,
        搅拌器,
        天线,
        天线塔,
        矢量网络分析仪,
    }
    /// <summary>
    /// 整型消息令牌（按业务场景分类，统一管理）
    /// </summary>
    public enum IntMessageToken
    {
        /// <summary>页面切换</summary>
        SwitchPage = 0,
        /// <summary>传递图表数据</summary>
        ChartData = 1,
        /// <summary>场均匀性复合数据</summary>
        UnifComposite = 2,
        /// <summary>混响室测试页面复合数据</summary>
        RVCTestomposite = 3,
        /// <summary>跳转到运行配置页面</summary>
        SwithToRunConfigPage,
        /// <summary>传递当前环境名称</summary>
        PassEnvName,
        /// <summary>CS114测试页面复合数据（配置模型 + 文件路径 / 仅切页面标志）</summary>
        CS114Composite,

    }
    public enum AdjustPriority
    {
        //频点在外层循环
        [Description("频点-搅拌器")]
        FrequencyFirst,
        //搅拌器在内层循环
        [Description("搅拌器-频点")]
        StirrerFirst,
    }
    public enum ChannelStatus
    {
        Idle,
        Selected,
        TestFinshed,
    }

    public static class DeviceIndexConst
    {
        public const int SignalSource = 0;
        public const int SwitchMatirx = 1;
        public const int Amplifier = 2;
        public const int ForwardPower = 3;
        public const int ReversePower = 4;
        public const int StrengthProbe = 5;
        public const int StirrerH = 6;          //水平搅拌器
        public const int StirrerV = 7;          //垂直搅拌器
        public const int AntennaTower = 8;          //天线塔
        public const int ReceiverMain1 = 9;         //1号主接收机
        public const int ReceiverMain2 = 10;         //2号主接收机
        public const int TransmitAntenna = 11;      //发射天线
        public const int ReceiverAntenna = 12;      //接收天线
        public const int Oscilloscope = 13;         //示波器
        public const int SpectrumAnalyzer = 14;     //频谱仪
    }

}
