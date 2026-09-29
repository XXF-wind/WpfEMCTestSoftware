using System.Collections.Generic;

namespace WpfEMCTestSoftware.ViewModels
{
    /// <summary>
    /// 一张已渲染曲线图的快照：标题 + PNG 字节（来自 UserChart.ExportToPng）。
    /// 运行页 View 抓取后通过 RunConfigViewModel.CaptureChartImages 提供给 ViewModel，
    /// 用于"合并导出 PNG"与"报告嵌入图片"两种用途。
    /// </summary>
    public class ChartCapture
    {
        /// <summary>图表标题（来自 UserChart.ChartTitle，如"信号源输出"）</summary>
        public string Title { get; set; } = "";

        /// <summary>图表 PNG 位图字节（已渲染，含坐标轴/网格/曲线）</summary>
        public byte[] Png { get; set; } = System.Array.Empty<byte>();
    }

    /// <summary>抓取当前运行页所有曲线图快照的回调签名</summary>
    public interface IChartCaptureProvider
    {
        IList<ChartCapture> CaptureCharts();
    }
}
