using CommunityToolkit.Mvvm.ComponentModel;
using Company.UI.Controls.UseControls;
using EMCDeviceLibrary;
using System.Collections.ObjectModel;
using System.Data;
using WpfEMCTestSoftware.Enums;
using WpfEMCTestSoftware.Models.TestModels;

namespace WpfEMCTestSoftware.RunTasks
{

    /// <summary>
    /// 场均校准运行参数
    /// 共用一套采集任务，采集逻辑流程比较统一
    /// 分标准独立分析数据模块
    /// </summary>
    public class RCFCRuntimeParam
    {
        private EMCStandard _standard;
        public EMCStandard Standard
        {
            get => _standard;
            set => _standard = value;
        }
        private AdjustPriority _curReverbTestMode;
        public AdjustPriority CurReverbTestMode
        {
            get => _curReverbTestMode;
            set => _curReverbTestMode = value;
        }
        private int _minPositionPoints;             //最少位置点数
        public int MinPositionPoints
        {
            get => _minPositionPoints;
            set => _minPositionPoints = value;
        }
        private int _currentTestPoint;              //当前测试点
        public int CurrentTestPoint
        {
            get => _currentTestPoint;
            set => _currentTestPoint = value;
        }
        public List<RVC_FUParam> RVC_FUListParams { get; set; } = [];
    }
    /// <summary>
    /// 场均测试时所需的设备
    /// </summary>
    public class RCFCDeviceChannel
    {
        // 必选设备
        public SockConnect? SignalSock { get; set; }         // 信号源
        public SockConnect? FieldStrengthSock { get; set; }  // 场强仪
        public SockConnect? ForwardPowerSock { get; set; }   // 正向功率计

        public SockConnect? StirrerVSock { get; set; }          //垂直搅拌器
        public SockConnect? StirrerHSock { get; set; }         //水平搅拌器

        // 可选设备
        public SockConnect? ReservePowerSock { get; set; }  // 反向功率计
        public SockConnect? AmplifierSock { get; set; }     // 功放
        public SockConnect? SwitchSock { get; set; }         // 开关
    }
    /// <summary>
    /// 混响室场地均匀性校准任务
    /// </summary>
    public partial class RCFURunTask : BaseRunTask
    {
        #region 图表数据数据绑定   
        //场强峰值曲线数据X,Y,Z,C
        [ObservableProperty]
        private ObservableCollection<ChartLineModel> _peakFieldCurveCollection = new ObservableCollection<ChartLineModel>();
        //发射天线注入平均功率监测
        [ObservableProperty]
        private ObservableCollection<ChartLineModel> _txAntennaCurveCollection = new ObservableCollection<ChartLineModel>();
        //监测天线接收峰值功率监测
        [ObservableProperty]
        private ObservableCollection<ChartLineModel> _monAntennaCurveCollection = new ObservableCollection<ChartLineModel>();
        #endregion
        #region  存放过程数据DataTable，转为csv导出

        DataTable pointFieldDataTable;
        #endregion
        #region 场均校准运行任务参数
        public RCFCRuntimeParam m_RCFCRuntimeParam { get; }
        public RCFCDeviceChannel m_RCFCDeviceChannel { get; }
        /// <summary>
        /// 历史点位缓存,最新的频率点和最新的角度点
        /// </summary>
        List<int> positionList = new List<int>();

        #endregion

        public RCFURunTask()
        {
            m_RCFCRuntimeParam = new RCFCRuntimeParam();
            pointFieldDataTable = new DataTable();
            m_RCFCDeviceChannel = new RCFCDeviceChannel();
            //生成测试名称
            GenerateatTestName();
        }
        /// <summary>
        /// 测试名称，直接以当前开始测试时间命名
        /// 是否加入频段，标准等字段？
        /// </summary>
        protected override void GenerateatTestName()
        {
            //临时表测试名称
            TestContext.TestName = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        }
        /// <summary>
        /// 剔除历史频点
        /// </summary>
        protected override void TrimQueueByHistoryBreakPoint()
        {
            // 当前频段还没有生成点位，直接返回
            if (FrequencyQueue.Count == 0 && StirrerHQueue.Count == 0 && StirrerVQueue.Count == 0)
                return;

            switch (m_RCFCRuntimeParam.CurReverbTestMode)
            {
                case AdjustPriority.FrequencyFirst:
                    // 频率优先模式：持续弹出小于等于历史最后频点的数据
                    while (FrequencyQueue.Count > 0)
                    {
                        double peekFreq = FrequencyQueue.Peek();
                        if (peekFreq <= TestContext.LastFrequency)
                        {
                            // 已测点，直接丢弃
                            FrequencyQueue.TryDequeue(out _);
                        }
                        else
                        {
                            // 遇到未测点，停止裁剪
                            break;
                        }
                    }
                    break;

                case AdjustPriority.StirrerFirst:
                    // 搅拌器优先模式，剔除 ≤ 历史最后角度
                    while (StirrerHQueue.Count > 0)
                    {
                        double peekAng = StirrerHQueue.Peek();
                        if (peekAng <= TestContext.LastAngele)
                            StirrerHQueue.TryDequeue(out _);
                        else
                            break;
                    }
                    while (StirrerVQueue.Count > 0)
                    {
                        double peekAng = StirrerVQueue.Peek();
                        if (peekAng <= TestContext.LastAngele)
                            StirrerVQueue.TryDequeue(out _);
                        else
                            break;
                    }
                    break;
            }
        }
        /// <summary>
        /// 更新内外循环队列
        /// </summary>
        protected override void RefreshInOrOutLoopQueue()
        {
            FrequencyQueue.Clear();
            StirrerVQueue.Clear();
            StirrerHQueue.Clear();
            FrequencyQueue = CalculateAllFreqPoints(TestContext.CurrentBandIndex);
            CalculateAllAnglePoints(StirrerVQueue, StirrerHQueue);
        }

        /// <summary>
        /// 从数据库中加载历史数据
        /// </summary>
        /// <param name="testName"></param>
        public override void LoadHistoryFromDataBase(string testName)
        {
            if (string.IsNullOrEmpty(testName))
                return;
            DataTable dt = ClsData.Instance.GetDataTableFromReverbUniformityTemp(testName);
            if (dt == null || dt.Rows.Count == 0)
                return;
            positionList = (from a in dt.AsEnumerable()
                            select a.Field<int>("Position")).Distinct().ToList();
            DataRow lastRow = dt.Rows[dt.Rows.Count - 1];
            TestContext.LastFrequency = Convert.IsDBNull(lastRow["Frequency"]) ? 0 : Convert.ToDouble(lastRow["Frequency"]);
            TestContext.LastAngele = Convert.IsDBNull(lastRow["Angle"]) ? 0 : Convert.ToDouble(lastRow["Angle"]);
        }

        /// <summary>
        /// 更新点位通道状态
        /// </summary>
        public override void UpdateDataToPos()
        {
            // 获取点位总数
            int pointCount = positionList.Count;
            // 无点位直接退出
            if (pointCount <= 0) return;

            // 遍历除最后一个点位外的所有通道
            for (int i = 0; i < pointCount - 1; i++)
            {
                int pos = positionList[i];
                // 设置通道状态：测试完成
                ChannelStates[pos] = ChannelStatus.TestFinshed;
            }
            // 最后一个点位通道设为【选中状态】
            TestContext.CurPosition = positionList[pointCount - 1];
            ChannelStates[TestContext.CurPosition] = ChannelStatus.Selected;
            //判断频率属于哪个频段范围
            for (int i = 0; i < positionList.Count; i++)
            {
                double startFre = GenericHelper.ConvertToMhzData(CommonFreAndHardParms[i].FreqBase.StartFre, CommonFreAndHardParms[i].FreqBase.StartFreUnit);
                double stopFre = GenericHelper.ConvertToMhzData(CommonFreAndHardParms[i].FreqBase.EndFre, CommonFreAndHardParms[i].FreqBase.EndFreUnit);
                if ((TestContext.LastFrequency >= startFre) && (TestContext.LastFrequency <= stopFre))
                {
                    TestContext.CurrentBandIndex = i;
                    break;
                }
            }
            TestContext.IsRestoreFromHistory = true;

        }

        /// <summary>
        /// 执行外层循环事件
        /// 外层消耗队列
        /// </summary>
        protected override void ExcuteOutsideLoopEvent()
        {
            // =========【关键】检测暂停，直接截断递归链条 =========
            if (TestContext.currentTestState == TestState.PAUSED)
                return;
            bool isOutsideloopValid = false;
            switch (m_RCFCRuntimeParam.CurReverbTestMode)
            {
                case AdjustPriority.FrequencyFirst:
                    if (FrequencyQueue.TryDequeue(out TestContext.CurrentFre))
                    {
                        SetFrequency();
                        isOutsideloopValid = true;
                    }
                    break;
                case AdjustPriority.StirrerFirst:
                    if (StirrerHQueue.TryDequeue(out TestContext.CurrentStirrerH) || StirrerVQueue.TryDequeue(out TestContext.CurrentStirrerV))
                    {
                        MoveTwoStirrer(TestContext.CurrentStirrerV, TestContext.CurrentStirrerH);
                        WaitMechanismStable();
                        isOutsideloopValid = true;
                    }
                    break;
                default:
                    break;
            }
            //通知上层，外层队列耗尽
            if (!isOutsideloopValid)
            {
                NotifyOutsideLoopComplete();
            }
            else
            {
                // 外层准备就绪 → 启动内层循环采集
                ExcuteInsideLoopEvent();
            }
        }
        /// <summary>
        /// 执行内层循环事件
        /// </summary>
        protected override void ExcuteInsideLoopEvent()
        {
            // =========【关键】检测暂停，直接截断递归链条 =========
            if (TestContext.currentTestState == TestState.PAUSED)
                return;
            //执行单次内层采集
            switch (m_RCFCRuntimeParam.CurReverbTestMode)
            {
                case AdjustPriority.FrequencyFirst:
                    RunStirrerCircleAndCollectData();
                    break;
                case AdjustPriority.StirrerFirst:
                    RunFreqSweepAndCollectData();
                    break;
                default: break;
            }
            //递归回外层
            ExcuteOutsideLoopEvent();
        }
        protected void MoveTwoStirrer(double angleV, double angleH)
        {
            m_RCFCDeviceChannel?.StirrerVSock?.SendData((int)Communication.SET_STIRRER_ANGLE, angleV.ToString());
            m_RCFCDeviceChannel?.StirrerHSock?.SendData((int)Communication.SET_STIRRER_ANGLE, angleH.ToString());
        }
        /// <summary>
        /// 等待电机稳定
        /// </summary>
        protected void WaitMechanismStable()
        {

        }
        /// <summary>
        /// 采集测量数据
        /// </summary>
        protected void CollecMeasurementData()
        {

        }
        /// <summary>
        /// 写入数据库
        /// </summary>
        public void WriteToDatabase()
        {
            ClsData.Instance.SaveTestContext(TestContext);
        }
        /// <summary>
        /// 搅拌器按照角度进行旋转和采集数据
        /// </summary>
        protected void RunStirrerCircleAndCollectData()
        {
            var stirrerVlist = StirrerVQueue.ToList();
            var stirrerHlist = StirrerHQueue.ToList();
            //垂直搅拌器角度
            foreach (double angleV in stirrerVlist)
            {
                //水平搅拌器角度
                foreach (double angleH in stirrerHlist)
                {
                    // =========【关键】检测暂停，直接截断递归链条 =========
                    if (TestContext.currentTestState == TestState.PAUSED)
                        return;
                    //1.驱动两台搅拌同时走到当前一组角度
                    MoveTwoStirrer(angleH, angleV);
                    //2，等待电机到位 
                    WaitMechanismStable();
                    //3.当前角度静止，执行采集扫描读取数据
                    CollecMeasurementData();
                    //4.实时保存数据库
                    WriteToDatabase();
                }
            }
        }
        /// <summary>
        /// 执行频点步进扫描并采集测量数据
        /// </summary>
        protected void RunFreqSweepAndCollectData()
        {
            var frequencylist = FrequencyQueue.ToList();
            foreach (double fre in frequencylist)
            {
                // =========【关键】检测暂停，直接截断递归链条 =========
                if (TestContext.currentTestState == TestState.PAUSED)
                    return;
                TestContext.CurrentFre = fre;
                SetFrequency();
                CollecMeasurementData();
            }
        }
        /// <summary>
        /// 设置频率
        /// </summary>
        protected void SetFrequency()
        {

        }




    }

}
