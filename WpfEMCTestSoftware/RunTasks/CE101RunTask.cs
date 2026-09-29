using WpfEMCTestSoftware.Models.DisplayModules;
using WpfEMCTestSoftware.Models.TestModels;

namespace WpfEMCTestSoftware.RunTasks
{
    /// <summary>
    /// CE101 电源线传导发射（传导安全裕度·发射类）：
    /// 电流探头 + 接收机逐频点测量电源线传导发射电流，与 CE101 限值比较得裕度。
    /// 接收机读数即 dBμA（探头传输阻抗已计入接收机换算；未配置修正表时修正量为 0）。
    /// 裕度 = 限值 − 实测（≥0 合格）。框架/配置/保存逻辑见 EMIEmissionRunTaskBase。
    /// </summary>
    public class CE101RunTask : EMIEmissionRunTaskBase
    {
        public CE101RunTask() : base("Chart.CESpectrum", "CE101 实测") { }

        protected override string TaskTitle => "CE101 电源线传导发射";
        protected override string UnitLabel => "dBμA";
        protected override string SaveDirName => "CE101";
        protected override string GetSpectrumChartKey() => "Chart.CESpectrum";

        /// <summary>
        /// 读数修正量：电流探头传输阻抗表（频段线损里 ToName=="电流探头" 那条）。
        /// 实测 = 接收机读数(dBμV) − 电缆损耗 + 探头传输阻抗(dB)；
        /// 未配置时按 0 处理（接收机直读 dBμA）。
        /// </summary>
        protected override double LevelCompensation(double freqMHz)
        {
            var probe = CurrentBandParms?.CableLosses.FirstOrDefault(c => c.ToName == "电流探头");
            var cable = CurrentBandParms?.CableLosses.FirstOrDefault(c => c.ToName == "接收机");
            return InterpCoeff(probe?.Points, freqMHz) - InterpCoeff(cable?.Points, freqMHz);
        }
    }
}
