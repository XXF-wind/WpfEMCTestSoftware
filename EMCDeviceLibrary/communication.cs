namespace EMCDeviceLibrary
{
    public enum Communication
    {
        NONE = 0,
        /// <summary>
        /// 通信速率
        /// </summary>
        SPEED,
        /// <summary>
        /// 工作频率
        /// </summary>
        FREQUENCY,
        /// <summary>
        /// 工作频率2
        /// </summary>
        FREQUENCY2,
        /// <summary>
        /// 输出功率
        /// </summary>
        OUTPUTPOWER,
        /// <summary>
        /// 输入功率
        /// </summary>
        INPUTPOWER,
        /// <summary>
        /// 衰减
        /// </summary>
        ATTENUATION,
        /// <summary>
        /// 周期值
        /// </summary>
        PERIODVALUE,
        /// <summary>
        /// 脉宽
        /// </summary>
        PULSEWIDTH,
        /// <summary>
        /// 工作模式
        /// </summary>
        WORKINGMODE,
        /// <summary>
        /// 开关时间
        /// </summary>
        SWITCHINGTIME,
        RF_STAT,
        /// <summary>
        /// 起始频率
        /// </summary>
        STARTFREQ,
        /// <summary>
        /// 终止频率
        /// </summary>
        STOPFREQ,
        /// <summary>
        /// 扫频频率驻留时间
        /// </summary>
        DWELLTIME,
        /// <summary>
        /// 校准
        /// </summary>
        CALIBRATION,
        /// <summary>
        /// 单路功放开关
        /// </summary>
        SINGLESWITCH,
        /// <summary>
        /// 切换单位
        /// </summary>
        SWITCHUNIT,
        /// <summary>
        /// 输出状态
        /// </summary>
        OUTPUTSTATE,
        /// <summary>
        /// 调制输出状态
        /// </summary>
        MODOUTSTATE,
        /// <summary>
        /// 调制使能
        /// </summary>
        MODENABLE,
        /// <summary>
        /// 幅度
        /// </summary>
        LEVEL,
        /// <summary>
        /// AM:DEPT
        /// </summary>
        AM_DEPT,
        /// <summary>
        /// AM:STATe
        /// </summary>
        AM_STATe,
        /// <summary>
        /// AM:TYPE
        /// </summary>
        AM_TYPE,
        /// <summary>
        /// 功放射频输出
        /// </summary>
        ENABlE_RFOUT,
        /// <summary>
        /// 功放射频关闭
        /// </summary>
        DISABlE_RFOUT,
        /// <summary>
        /// 搅拌器设置角度
        /// </summary>
        SET_STIRRER_ANGLE,
        /// <summary>
        /// 搅拌器设置转速
        /// </summary>
        SET_STIRRER_SPEED,
        /// <summary>
        /// 切换开关通道
        /// </summary>
        SWITCH_CHANNEL,
        MARKFREQ,
        视频带宽,
        参考电平,
        初始化,
        读取数据,
        分辨带宽,
        耦合方式,
        SPAN,
        峰值,
        /// <summary>
        /// 外参考使能（LS10004 信号源 SG:REF：1=开启 0=关闭）
        /// </summary>
        SGREF,
        /// <summary>
        /// 读取数据的单位
        /// </summary>
        READ_DBM,
        READ_DBUA,
        READ_DBUV,
    }

}
