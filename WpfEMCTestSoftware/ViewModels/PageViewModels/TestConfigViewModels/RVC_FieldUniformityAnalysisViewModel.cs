using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Company.UI.Controls.UseControls;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Text;
using System.Windows;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Enums;

// ================================================================
// ★ NEW: 单频点分析结果数据类 — 用于 DataGrid/图表数据源和 CSV 导出
// ================================================================
public class UniformityResultItem
{
    public double LimitE { get; set; }                       //场强限值
    public double Frequency { get; set; }          // Hz
    public double ACF { get; set; }                // 天线耦合系数 (线性)
    public double InsertionLoss_dB { get; set; }   // 插入损耗 (dB)
    public double QValue { get; set; }             // 品质因数
    public double Tau_us { get; set; }             // 时间常数 (μs)
    public double ExAvg { get; set; }              // Ex 归一化均值
    public double EyAvg { get; set; }
    public double EzAvg { get; set; }
    public double EcAvg { get; set; }              // Ec 归一化均值
    public double SigmaX_dB { get; set; }          // σx (dB)
    public double SigmaY_dB { get; set; }
    public double SigmaZ_dB { get; set; }
    public double SigmaGlobal_dB { get; set; }     // 全局 σ (dB)
    public double Grc { get; set; }                // 混响室增益
    public int PositionCount { get; set; }         // 有效位置点数 N
    public bool IsPass { get; set; }               // 是否合格 (σx/σy/σz < 3dB)


}




namespace WpfEMCTestSoftware.ViewModels.PageViewModels.TestConfigViewModels
{
    public partial class RVC_FieldUniformityAnalysisViewModel : BaseViewModel
    {
        public class FreqPointResult
        {
            public double MaxXField { get; set; }       //同频不同角度，场强X轴最大值
            public double MaxYField { get; set; }       //同频不同角度，场强Y轴最大值
            public double MaxZField { get; set; }       //同频不同角度，场强Z轴最大值
            public double MaxCField { get; set; }       //同频不同角度，合成最大值
            public double AvgForwardPower { get; set; } //同频不同角度，正向功率平均值
            public double AvgRecvPower { get; set; }   //接收功率平均数
        }
        public Dictionary<double, Dictionary<double, FreqPointResult>> posFreqDict { get; set; } = new();
        /// <summary>
        /// 数据绑定
        /// </summary>
        [ObservableProperty]
        private string _fieldUniformityAnalysisTestName;
        [ObservableProperty]
        private ObservableCollection<string> _fieldUniformityAnalysisValues;
        //位置点测试状态
        [ObservableProperty]
        private ObservableCollection<ChannelStatus> _channelStates = new()
        {
            ChannelStatus.Idle,
            ChannelStatus.Idle,
            ChannelStatus.Idle,
            ChannelStatus.Idle,
            ChannelStatus.Idle,
            ChannelStatus.Idle,
            ChannelStatus.Idle,
            ChannelStatus.Idle,
            ChannelStatus.Idle
        };
        // =============================================================
        // ★ 绑定左侧 "Q及时间常数计算" 面板）
        // =============================================================
        [ObservableProperty]
        private double _chamberVolume_V = 8.0;
        [ObservableProperty]
        private double _etaT2 = 0.75;            // 发射天线效率
        [ObservableProperty]
        private double _etaR6 = 0.75;            // 接收天线效率
                                                 // ========== 分析结果集 — 存储所有频点 ==========
        [ObservableProperty]
        private ObservableCollection<UniformityResultItem> _uniformityResults = new();

        // ========== 当前频点 — 绑定右侧面板所有字段 ==========
        [ObservableProperty]
        private UniformityResultItem _curItem = new();
        // =============================================================
        // ★ NEW: 滑块 & 当前频点索引
        // =============================================================
        [ObservableProperty]
        private int _sliderMaxIndex = 0;
        [ObservableProperty]
        private int _sliderIndex = 0;
        // SliderIndex 变化时 → 从 UniformityResults 取对应项赋给 CurItem
        // ========== 进度条 ==========
        [ObservableProperty]
        private double _analysisProgress = 0;

        // ========== 整体判定 ==========
        [ObservableProperty]
        private bool _overallPass = false;
        [ObservableProperty]
        private string _resultLabel = "合格";
        // ========== 基本信息 ==========
        [ObservableProperty]
        private double _minFre = 0;
        [ObservableProperty]
        private double _maxFre = 10;
        [ObservableProperty]
        private string _freRange;
        [ObservableProperty]
        private string _testPositionCounts;
        // 图标\
        [ObservableProperty]
        public PackIconKind _resultIcon = PackIconKind.PassportCheck;
        #region 图表数据数据绑定   
        //均匀性分析图形
        [ObservableProperty]
        private ObservableCollection<ChartLineModel> _sigmaChartLine = new ObservableCollection<ChartLineModel>();
        //ACF测试数据
        [ObservableProperty]
        private ObservableCollection<ChartLineModel> _acfChartLine = new ObservableCollection<ChartLineModel>();
        //Q测试数据
        [ObservableProperty]
        private ObservableCollection<ChartLineModel> _qChartLine = new ObservableCollection<ChartLineModel>();
        //时间常数
        [ObservableProperty]
        private ObservableCollection<ChartLineModel> _tChartLine = new ObservableCollection<ChartLineModel>();
        //归一化场强均值数据
        [ObservableProperty]
        private ObservableCollection<ChartLineModel> _eAvgChartLine = new ObservableCollection<ChartLineModel>();
        //插入损耗IL测试数据
        [ObservableProperty]
        private ObservableCollection<ChartLineModel> _iLChartLine = new ObservableCollection<ChartLineModel>();
        #endregion
        // ========== 分析标准名称 ==========
        [ObservableProperty]
        private string _analysisStandard = "GB/T 17626.21";
        private EMCStandard Curstandard;
        partial void OnSliderIndexChanged(int value)
        {
            if (UniformityResults == null || UniformityResults.Count == 0) return;
            if (value < 0) value = 0;
            if (value >= UniformityResults.Count) value = UniformityResults.Count - 1;
            CurItem = UniformityResults[value];
        }

        /// <summary>
        /// 加载ReverbUniformityRecord数据表中所有测试项目
        /// </summary>
        [RelayCommand]
        private void OnFirstLoadPage()
        {
            FieldUniformityAnalysisValues = ClsData.Instance.GetUniformityTestNames();
            //初始化图表曲线
            InitChart();
        }
        private void GetBasicInfo(DataTable dt)
        {
            DataRow firstRow = dt.Rows[dt.Rows.Count - 1];
            Curstandard = Convert.IsDBNull(firstRow["TestStandard"])
                ? EMCStandard.None
                : (EMCStandard)Convert.ToInt16(firstRow["TestStandard"]);

            if (!Convert.IsDBNull(firstRow["TestStandard"]))
                AnalysisStandard = Curstandard.ToString().Replace("_", " ");
            //计算频率最大最小值
            var freRows = dt.AsEnumerable().Where(r => !r.IsNull("Frequency"));
            //直接存hz
            if (freRows.Any())
            {
                MinFre = freRows.Min(r => Convert.ToDouble(r["Frequency"]));
                MaxFre = freRows.Max(r => Convert.ToDouble(r["Frequency"]));
            }
            var minFreqMhz = GenericHelper.ConvertToMhzData(MinFre, FrequencyUnit.Hz);
            var maxFreqMhz = GenericHelper.ConvertToMhzData(MaxFre, FrequencyUnit.Hz);
            FreRange = $"{minFreqMhz:F2} - {maxFreqMhz:F2} MHz";
            var positionRows = dt.AsEnumerable().Where(r => !r.IsNull("Position"));
            int DistinctPosCount = 0;
            if (positionRows.Any())
            {
                DistinctPosCount = positionRows.Select(r => Convert.ToInt32(r["Position"])).Distinct().Count();
            }
            TestPositionCounts = $"{DistinctPosCount}个";

        }
        /// <summary>
        /// 分析数据
        /// </summary>
        [RelayCommand]
        private async void OnAnalysisData()
        {
            AnalysisProgress = 0;
            UniformityResults.Clear();
            posFreqDict.Clear();
            CurItem = new UniformityResultItem();

            DataTable dt = ClsData.Instance.GetDataTableFromReverbUniformityRecord(FieldUniformityAnalysisTestName);
            if (dt == null || dt.Rows.Count == 0) return;
            //提取频率 位置点基本信息
            GetBasicInfo(dt);
            //计算数据
            CalcUniformity(dt);
            // 排序
            var sorted = UniformityResults.OrderBy(r => r.Frequency).ToList();
            UniformityResults = new ObservableCollection<UniformityResultItem>(sorted);

            // 滑块
            SliderMaxIndex = Math.Max(0, UniformityResults.Count - 1);
            SliderIndex = 0;      // → 触发 OnSliderIndexChanged → CurItem = UniformityResults[0]

            switch (Curstandard)
            {
                case EMCStandard.GB_17626_21:
                    OverallPass = CheckUniformity_GB_17626_21(UniformityResults);
                    break;
                case EMCStandard.DO_160G:
                    OverallPass = CheckUniformity_DO160G(UniformityResults);
                    break;
                case EMCStandard.IEC61000_4_21:
                    OverallPass = CheckUniformity_IEC61000_4_21(UniformityResults);
                    break;
                case EMCStandard.DO_160F:
                    OverallPass = CheckUniformity_DO160F(UniformityResults);
                    break;
                case EMCStandard.GJB151B:
                    OverallPass = CheckUniformity_GJB151B(UniformityResults);
                    break;
                case EMCStandard.GBT_33014_11:
                    OverallPass = CheckUniformity_GBT33014_11(UniformityResults);
                    break;
            }
            //判定条件不一样

            ResultLabel = OverallPass ? "合格" : "不合格";
            ResultIcon = OverallPass ? PackIconKind.PassportCheck : PackIconKind.PassportAlert;
            AnalysisProgress = 100;
        }
        #region 各标准对应的算法

        private Dictionary<double, List<FreqPointResult>> AggregateReverbRaw(DataTable dtRecord)
        {
            // key:(Position位置点, Frequency频率)
            var tempGroup = new Dictionary<(int Position, double Frequency), List<(double X, double Y, double Z, double C, double FPower, double RecvPower)>>();
            foreach (DataRow row in dtRecord.Rows)
            {
                int pos = Convert.ToInt32(row["Position"]);
                double freq = Convert.ToDouble(row["Frequency"]);
                double x = Convert.ToDouble(row["XFiled"]);
                double y = Convert.ToDouble(row["YFiled"]);
                double z = Convert.ToDouble(row["ZFiled"]);
                double c = Convert.ToDouble(row["CField"]);
                double fPower = Convert.ToDouble(row["ForwardPower"]);
                double recvPower = Convert.ToDouble(row["RecvPower"]);

                var key = (pos, freq);
                if (!tempGroup.ContainsKey(key))
                {
                    tempGroup[key] = new List<(double X, double Y, double Z, double C, double FPower, double RecvPower)>();
                }
                tempGroup[key].Add((x, y, z, c, fPower, recvPower));
            }
            foreach (var kv in tempGroup)
            {
                int pos = kv.Key.Position;
                double freq = kv.Key.Frequency;
                var list = kv.Value;

                FreqPointResult res = new FreqPointResult();
                // X/Y/Z/C 各自取角度下最大值
                res.MaxXField = list.Max(t => t.X);
                res.MaxYField = list.Max(t => t.Y);
                res.MaxZField = list.Max(t => t.Z);
                res.MaxCField = list.Max(t => t.C);
                // 正向功率取角度下平均值
                res.AvgForwardPower = list.Average(t => t.FPower);
                //接收功率取平均值
                res.AvgRecvPower = list.Average(t => t.RecvPower);

                if (!posFreqDict.ContainsKey(pos))
                {
                    posFreqDict[pos] = new Dictionary<double, FreqPointResult>();
                }
                posFreqDict[pos][freq] = res;
            }
            //----------------------到此完成聚合:每个位置点---每个频点，多个角度归算完毕----------------------------------------//
            // posFreqDict[位置][频率] → FreqPointResult（MaxX/MaxY/MaxZ/MaxC/AvgPower）
            //----------------------下一步:按【频率】分组，收集全部位置点的MaxX,MaxY,MaxZ，-------------------------------------//
            Dictionary<double, List<FreqPointResult>> freqAllPosDict = new Dictionary<double, List<FreqPointResult>>();
            foreach (var kv in posFreqDict)
            {
                var freDic = kv.Value;
                foreach (var frekv in freDic)
                {
                    double f = frekv.Key;
                    FreqPointResult item = frekv.Value;
                    if (!freqAllPosDict.ContainsKey(f))
                    {
                        freqAllPosDict[f] = new List<FreqPointResult>();
                    }
                    freqAllPosDict[f].Add(item);
                }
            }
            return freqAllPosDict;
        }

        /// <summary>
        /// 8个位置点
        ///  E_total:不涉及
        /// σ 计算路径:先分后合：每个轴向单独算 s，再 RSS 合成
        /// 修正系数：1.06
        /// </summary>
        /// <param name="dtRecord"></param>
        private void CalcUniformity(DataTable dtRecord)
        {
            //----------------------到此完成聚合:每个位置点---每个频点，多个角度归算完毕----------------------------------------//
            // posFreqDict[位置][频率] → FreqPointResult（MaxX/MaxY/MaxZ/MaxC/AvgPower）
            //----------------------下一步:按【频率】分组，收集全部位置点的MaxX,MaxY,MaxZ，-------------------------------------//
            Dictionary<double, List<FreqPointResult>> freqAllPosDict = AggregateReverbRaw(dtRecord);
            int total = freqAllPosDict.Count;
            int done = 0;
            //----------------------下一步:遍历整个频率，执行整套标准差计算-------------------------------------//
            foreach (var freqKv in freqAllPosDict)
            {
                // N就是位置点数量，对应公式i=1~N
                double currentFreq = freqKv.Key;
                var posResultList = freqKv.Value;
                int N = posResultList.Count;
                if (N < 2)
                {
                    done++; continue;
                }

                //==============================步骤1：每个步骤位置，先计算归一化场强===================================//
                var normList = new List<(double exi, double eyi, double ezi, double eci, double accfi)>();
                foreach (var p in posResultList)
                {
                    double sqrtP = Math.Sqrt(p.AvgForwardPower);
                    // 功率为0保护，防止除0
                    if (Math.Abs(sqrtP) < 1e-12)
                        continue;
                    double exi = p.MaxXField / sqrtP;
                    double eyi = p.MaxYField / sqrtP;
                    double ezi = p.MaxZField / sqrtP;
                    double eci = p.MaxCField / sqrtP;
                    double accf_i = 0.0; //腔体特征系数
                    if (Math.Abs(p.AvgForwardPower) > 1e-12)
                    {
                        accf_i = p.AvgRecvPower / p.AvgForwardPower;

                    }
                    normList.Add((exi, eyi, ezi, eci, accf_i));
                }
                int validN = normList.Count;
                if (validN < 2)
                {
                    done++; continue;
                }
                // ==========================步骤2：对归一化电场求平均值 e_x,avg  e_y,avg  e_z,avg ===========================//


                // 1.求X,Y,Z分量平均值 e_x,avg e_y,avg e_z,avg
                double exAvg = normList.Average(p => p.exi);
                double eyAvg = normList.Average(p => p.eyi);
                double ezAvg = normList.Average(p => p.ezi);
                double ecAvg = normList.Average((p) => p.eci);
                //2.对【归一电场】做偏差平方求和
                double sumx = normList.Sum(t => Math.Pow(t.exi - exAvg, 2));
                double sumy = normList.Sum(t => Math.Pow(t.eyi - eyAvg, 2));
                double sumz = normList.Sum(t => Math.Pow(t.ezi - ezAvg, 2));

                //3.样本标准差，分母validCount-1 
                double sigmax_sample = Math.Sqrt(sumx / (validN - 1));
                double sigmay_sample = Math.Sqrt(sumy / (validN - 1));
                double sigmaz_sample = Math.Sqrt(sumz / (validN - 1));
                // 4.IEC标准强制乘以1.06瑞利分布修正因子（红框1.06）分轴标准差
                double sigmaX = 1.06 * sigmax_sample;
                double sigmaY = 1.06 * sigmay_sample;
                double sigmaZ = 1.06 * sigmaz_sample;
                // 5.总σ，合轴标准差

                //.先收集全部3*N个归一电场样本
                List<double> allE = new List<double>();
                foreach (var t in normList)
                {
                    allE.Add(t.exi);
                    allE.Add(t.eyi);
                    allE.Add(t.ezi);
                }

                //全局总平均值 e_avg（所有X/Y/Z样本一起平均）
                double e_global_avg = allE.Average();

                //按标准公式求和：每一个exi/eyi/ezi 都减去【同一个全局e_global_avg】
                double sumGlobal = normList.Sum(t =>
                {
                    var dx = Math.Pow(t.exi - e_global_avg, 2);
                    var dy = Math.Pow(t.eyi - e_global_avg, 2);
                    var dz = Math.Pow(t.ezi - e_global_avg, 2);
                    return dx + dy + dz;
                });
                //分母 3N‑1；
                double sigma_global = Math.Sqrt(sumGlobal / (3 * validN - 1));

                //===================步骤3：dB标准差====================================//
                double sigmaX_dB = 20 * Math.Log10((sigmaX + exAvg) / exAvg);
                double sigmaY_dB = 20 * Math.Log10((sigmaY + eyAvg) / eyAvg);
                double sigmaZ_dB = 20 * Math.Log10((sigmaZ + ezAvg) / ezAvg);

                //==================全局综合dB标准差（仅报告输出，不判合格）======================//
                double sigmaGlobal_dB = 20 * Math.Log10((sigma_global + e_global_avg) / e_global_avg);
                //===================步骤4: ACF=============================//
                double acf = normList.Average(p => p.accfi);
                //===================步骤5: 混响室增益=======================//
                double Grc = normList.Sum(p => (p.exi + p.eyi + p.ezi)) / 3 * validN;
                //===========================Q值===============================//
                // ==========================================================
                // ★★★ NEW: Q值计算 ★★★
                // Q = (16π³ · V · CCF) / (η_T2 · η_R6 · λ³)
                // λ = c / f  (c = 3×10⁸ m/s)
                // ==========================================================
                double c_speed = 3e8;
                double lambda = c_speed / currentFreq;                       // 波长 (m)

                double qValue = 0;

                if (acf > 0 && EtaT2 > 0 && EtaR6 > 0 && lambda > 0)
                {
                    qValue = (16.0 * Math.Pow(Math.PI, 3) * ChamberVolume_V * acf)
                           / (EtaT2 * EtaR6 * Math.Pow(lambda, 3));
                }

                // ★★★ NEW: 时间常数 τ = Q / (2πf) ★★★
                double tau = (currentFreq > 0 && qValue > 0)
                    ? qValue / (2.0 * Math.PI * currentFreq)
                    : 0.0;
                double tau_us = tau * 1e6;  // 转换为 μs 便于显示

                // ★★★ NEW: 插入损耗 IL = −10·log₁₀(ACF) ★★★
                double insertionLoss_dB = (acf > 0)
                    ? -10.0 * Math.Log10(acf)
                    : 0.0;

                // -------- 均匀性判定 (σx/σy/σz 各轴 < 3dB) --------
                bool isPass = sigmaX_dB < 3.0 && sigmaY_dB < 3.0 && sigmaZ_dB < 3.0;

                // ★★★ NEW: 存入结果集 ★★★
                UniformityResults.Add(new UniformityResultItem
                {
                    Frequency = currentFreq,
                    ACF = acf,
                    InsertionLoss_dB = insertionLoss_dB,
                    QValue = qValue,
                    Tau_us = tau_us,
                    ExAvg = exAvg,
                    EyAvg = eyAvg,
                    EzAvg = ezAvg,
                    EcAvg = ecAvg,
                    SigmaX_dB = sigmaX_dB,
                    SigmaY_dB = sigmaY_dB,
                    SigmaZ_dB = sigmaZ_dB,
                    SigmaGlobal_dB = sigmaGlobal_dB,
                    Grc = Grc,
                    PositionCount = validN,
                    IsPass = isPass,

                });

                //异步更新数据
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    SigmaChartLine[0].XData.Add(currentFreq);
                    SigmaChartLine[0].YData.Add(sigmaX_dB);
                    SigmaChartLine[1].XData.Add(currentFreq);
                    SigmaChartLine[1].YData.Add(sigmaY_dB);
                    SigmaChartLine[2].XData.Add(currentFreq);
                    SigmaChartLine[2].YData.Add(sigmaY_dB);
                    SigmaChartLine[3].XData.Add(currentFreq);
                    SigmaChartLine[3].YData.Add(sigmaGlobal_dB);
                    AcfChartLine[0].XData.Add(currentFreq);
                    AcfChartLine[0].YData.Add(acf);
                    QChartLine[0].XData.Add((double)currentFreq);
                    QChartLine[0].YData.Add((double)qValue);
                    TChartLine[0].XData.Add(currentFreq);
                    TChartLine[0].YData.Add(tau_us);
                    EAvgChartLine[0].XData.Add(currentFreq);
                    EAvgChartLine[0].YData.Add(ecAvg);
                    ILChartLine[0].XData.Add(currentFreq);
                    ILChartLine[0].YData.Add(insertionLoss_dB);
                }));
                done++;
                AnalysisProgress = (double)done / total * 100;
            }


        }
        private bool CheckUniformity_DO160G(ObservableCollection<UniformityResultItem> results)
        {
            return true;
        }
        private bool CheckUniformity_DO160F(ObservableCollection<UniformityResultItem> results)
        {
            return true;
        }
        private bool CheckUniformity_GJB151B(ObservableCollection<UniformityResultItem> results)
        {
            return true;
        }
        private bool CheckUniformity_IEC61000_4_21(ObservableCollection<UniformityResultItem> results)
        {
            return true;
        }
        private bool CheckUniformity_GBT33014_11(ObservableCollection<UniformityResultItem> results)
        {
            return true;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="results"></param>
        /// <returns></returns>
        private bool CheckUniformity_GB_17626_21(ObservableCollection<UniformityResultItem> results)
        {
            int passCount = UniformityResults.Count(r => (r.IsPass == true));
            int allCount = UniformityResults.Count();

            return true;
        }


        #endregion

        /// <summary>
        /// 导出数据
        /// </summary>
        [RelayCommand]
        private void OnExportData()
        {
            if (UniformityResults == null || UniformityResults.Count == 0)
            {
                MessageBox.Show("暂无均匀度测试数据，无法导出", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveFileDialog saveDlg = new SaveFileDialog
            {
                // 文件筛选器，限定csv
                Filter = "CSV表格文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                // 输入框默认文字，用户可以随意删除、修改
                FileName = $"场均匀度测试_{DateTime.Now:yyyyMMdd}.csv",
                // 默认后缀，用户只写名字也会自动补上.csv
                DefaultExt = ".csv",
                // 弹窗初始打开目录：程序根目录
                InitialDirectory = AppDomain.CurrentDomain.BaseDirectory,
                Title = "保存场均匀度数据"
            };

            // 用户点击【保存】返回true；取消直接退出
            if (saveDlg.ShowDialog() != true)
                return;

            // 用户最终填写+选定的完整路径
            string savePath = saveDlg.FileName;

            try
            {
                StringBuilder sb = new StringBuilder();
                // 表头和你的Excel截图完全对齐
                sb.AppendLine("频率(MHz),ACF值(线性),插入损耗(dB),Q值,时间常数(us),Ec归一化均值(V/m)/√W,Ex标准偏差,Ey标准偏差,Ez标准偏差,Ec标准偏差");

                foreach (var item in UniformityResults)
                {
                    double freqMHz = item.Frequency / 1e6;
                    sb.AppendLine(string.Join(",",
                        freqMHz.ToString("F6"),
                        item.ACF.ToString("F10"),
                        item.InsertionLoss_dB.ToString("F6"),
                        item.QValue.ToString("F3"),
                        item.Tau_us.ToString("F2"),
                        item.EcAvg.ToString("F6"),
                        item.SigmaX_dB.ToString("F6"),
                        item.SigmaY_dB.ToString("F6"),
                        item.SigmaZ_dB.ToString("F6"),
                        item.SigmaGlobal_dB.ToString("F6")
                    ));
                }

                // UTF8带BOM，Excel打开中文不乱码
                File.WriteAllText(savePath, sb.ToString(), new UTF8Encoding(true));
                MessageBox.Show($"导出成功\n保存路径：{savePath}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        [RelayCommand]
        private void OnExit()
        {

        }

        public RVC_FieldUniformityAnalysisViewModel()
        {

        }
        //[ObservableProperty]
        //private ObservableCollection<ChartLineModel> _sigmaChartLine = new ObservableCollection<ChartLineModel>();
        ////ACF测试数据
        //[ObservableProperty]
        //private ObservableCollection<ChartLineModel> _acfChartLine = new ObservableCollection<ChartLineModel>();
        ////Q测试数据
        //[ObservableProperty]
        //private ObservableCollection<ChartLineModel> _qChartLine = new ObservableCollection<ChartLineModel>();
        ////时间常数
        //[ObservableProperty]
        //private ObservableCollection<ChartLineModel> _tChartLine = new ObservableCollection<ChartLineModel>();
        ////归一化场强均值数据
        //[ObservableProperty]
        //private ObservableCollection<ChartLineModel> _eAvgChartLine = new ObservableCollection<ChartLineModel>();
        ////插入损耗IL测试数据
        //[ObservableProperty]
        //private ObservableCollection<ChartLineModel> _iLChartLine = new ObservableCollection<ChartLineModel>();

        private void InitChart()
        {
            SigmaChartLine.Add(new ChartLineModel()
            {
                Title = "X",
                LineColor = ScottPlot.Colors.Blue,
                YData = [],
                XData = []
            });
            SigmaChartLine.Add(new ChartLineModel()
            {
                Title = "Y",
                LineColor = ScottPlot.Colors.Yellow,
                YData = [],
                XData = []
            });
            SigmaChartLine.Add(new ChartLineModel()
            {
                Title = "Z",
                LineColor = ScottPlot.Colors.Green,
                YData = [],
                XData = []
            });
            SigmaChartLine.Add(new ChartLineModel()
            {
                Title = "XYZ",
                LineColor = ScottPlot.Colors.Red,
                YData = [],
                XData = []
            });
            AcfChartLine.Add(new ChartLineModel()
            {
                Title = "ACF",
                LineColor = ScottPlot.Colors.Green,
                YData = [],
                XData = []
            });
            QChartLine.Add(new ChartLineModel()
            {
                Title = "Q",
                LineColor = ScottPlot.Colors.Yellow,
                YData = [],
                XData = []
            });
            TChartLine.Add(new ChartLineModel()
            {
                Title = "t",
                LineColor = ScottPlot.Colors.Purple,
                YData = [],
                XData = []
            });
            EAvgChartLine.Add(new ChartLineModel()
            {
                Title = "Eavg",
                LineColor = ScottPlot.Colors.Purple,
                YData = [],
                XData = []
            });
            ILChartLine.Add(new ChartLineModel()
            {
                Title = "IL",
                LineColor = ScottPlot.Colors.Blue,
                YData = [],
                XData = []
            });
        }



    }
}
