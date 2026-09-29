using ClassLibrary.Generic;

namespace EMCDeviceLibrary
{
    /// <summary>
    /// 设备型号枚举（定义EMC测试系统中所有支持的设备类型）
    /// 每个枚举值对应一种真实硬件设备（信号源、接收机、功放、功率计等）
    /// </summary>
    public enum DeviceMode
    {
        Triple = -9,           // 三用表/其他综合设备
        RadioFrequency = -8,   // 射频相关设备
        SignalSouce,           // 信号源（通用）
        RFSwitch,              // 射频开关
        RFSwitchsp8t,          // 8通道射频开关
        Server,                // 服务器/上位机
        PCanBasic,             // CAN总线适配器

        //==================== 信号源 ====================
        KEYSIGHT_N517XB = 0,    // 是德科技信号源
        Rigol_DSG815 = 1,       // 普源信号源
        LS10004 = 2,            //LS信号源
        Ceyear_Sight_1435,      //思仪信号源

        //==================== 接收设备 ====================
        KEYSIGHT_N9048B = 10001,// 是德科技接收机/频谱仪
        Baluelec_SpectrumAnalyzer_MSA = 10002,
        KEYSIGHT_SpectrumAnalyzer_N9020A=10003,

        //==================== 场强仪 ====================
        AR_Flxxxx = 20001,      // AR场强探头
        WAVECONTROL_SMP3 = 20002,// 波控场强仪
        EP408 = 20003,          // EP408场强仪
        EP600 = 20004,          // EP600场强仪
        EMS04 = 20005,          // EMS04场强测试设备

        //==================== 功放 ====================
        Sinpel_Ampifier = 30001,// 南京华凯功放

        //==================== 功率计 ====================
        Agilet_N191xA = 40001,  // 安捷伦功率计
        KEYSIGHT_U8487A = 40002,// 是德功率传感器
        KEYSIGHT_E4419B = 40003,// 是德功率计
        Baluelec_POWER = 40004, //白鹭功率计

        //==================== 移相衰减器 ====================
        Sinpel_PhaseAtt = 50001,// 华凯移相衰减器

        //==================== 倍频器 ====================
        Sinpel_MultiplierFre = 60001,// 华凯倍频器

        //==================== 开关矩阵 ====================
        Sinpel_SwitchMatrix4_4 = 70001,// 4*4射频开关矩阵

        //==================== 示波器 ====================
        KEYSIGHT_DSO_X3052G = 80001,        // 是德示波器
        RIGIL_MS05204 = 80002,              // 普源示波器
        Tektronix_TBS1000C = 80003,            // 泰克示波器
        //==================== 频谱仪 ====================
        //KEYSIGHT_SpectrumAnalyzer_N9020A = 90001,// 仕德频谱仪
        Ceyear_SpectrumAnalyzer_4051H,// 思仪频谱仪
        //=============搅拌器100001-109999================
        Stirrer_A = 100001,
        //=============天线110001-119999===================
        Antenna_A = 110001,
        //======================天线塔======================
        AntennaTower_A = 120001,
        //====================矢量网络分析仪=====================
        Ceyear_NetworkAnalyzer_3672E = 130001,
    };

    /// <summary>
    /// 通讯方式枚举（定义设备支持的所有通讯类型）
    /// </summary>
    public enum CommunicationMode
    {
        LAN,      // 网口通讯（TCP/IP）
        Serial,   // 串口通讯（RS232/RS485）
        CAN,      // CAN总线通讯
        VISA,     // VISA仪器通讯（SCPI指令）
        None      // 无通讯
    }

    /// <summary>
    /// 设备工厂类（核心：根据设备型号自动创建对应的设备通讯实例）
    /// 作用：解耦设备创建逻辑，外部只需传入设备型号，自动返回可控制的设备对象
    /// </summary>
    public class ClsFactory
    {
        /// <summary>
        /// 根据设备型号，动态创建对应的设备通讯实例
        /// 原理：根据枚举名字 → 反射创建对应类的实例
        /// </summary>
        /// <param name="Device">设备型号枚举（如KEYSIGHT_N517XB）</param>
        /// <returns>返回对应的设备实例（继承自SockConnect）</returns>
        public static SockConnect? GainSock(DeviceMode Device)
        {
            // 获取设备枚举对应的字符串名称（例如 KEYSIGHT_N517XB）
            string DName = Device.ToString();

            // 通过【反射】动态创建实例：根据类名在 EMCDeviceLibrary 程序集中查找并创建对象
            object? obj = GenericMethods.CreateInstanceByClassName(DName, "EMCDeviceLibrary", out string msg);

            // 如果创建成功，并且是 SockConnect 类型，则返回该设备实例
            return obj is SockConnect sock ? sock : null;
        }
    }
}
