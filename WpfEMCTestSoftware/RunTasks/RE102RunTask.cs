using WpfEMCTestSoftware.Models.DisplayModules;
using WpfEMCTestSoftware.Models.TestModels;

namespace WpfEMCTestSoftware.RunTasks
{
    /// <summary>
    /// RE102 电场辐射发射（辐射安全裕度·发射类）：
    /// 测试天线 + 接收机逐频点测量 EUT 电场辐射发射，与 RE102 限值比较得裕度。
    /// 实测(dBμV/m) = 接收机读数(dBμV) + 天线系数(dB/m) + 电缆损耗(dB)。
    /// 裕度 = 限值 − 实测（≥0 合格）。框架/配置/保存逻辑见 EMIEmissionRunTaskBase。
    /// </summary>
    public class RE102RunTask : EMIEmissionRunTaskBase
    {
        public RE102RunTask() : base("Chart.RESpectrum", "RE102 实测") { }

        protected override string TaskTitle => "RE102 电场辐射发射";
        protected override string UnitLabel => "dBμV/m";
        protected override string SaveDirName => "RE102";
        protected override string GetSpectrumChartKey() => "Chart.RESpectrum";

        /// <summary>
        /// 读数修正量：天线系数（频段线损里 ToName=="天线" 那条，按频率插值）
        /// + 天线到接收机电缆损耗（ToName=="接收机" 那条）。
        /// 天线系数未配置时告警并按 0 处理（数据偏保守，不影响流程）。
        /// </summary>
        protected override double LevelCompensation(double freqMHz)
        {
            var antenna = CurrentBandParms?.CableLosses.FirstOrDefault(c => c.ToName == "天线");
            var cable = CurrentBandParms?.CableLosses.FirstOrDefault(c => c.ToName == "接收机");
            double af = InterpCoeff(antenna?.Points, freqMHz);
            if (antenna != null && antenna.Points.Count == 0)
                AddLog("WARN", $"[{freqMHz:F6} MHz] 未配置天线系数，按 0 dB/m 处理，实测值偏小");
            return af + InterpCoeff(cable?.Points, freqMHz);
        }
    }
}
