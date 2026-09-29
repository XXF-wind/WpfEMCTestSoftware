using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using WpfEMCTestSoftware.Models.TestModels;

namespace WpfEMCTestSoftware.Views.PageViews.TestConfigViews
{
    /// <summary>
    /// CS114 限值数据导入弹窗：
    /// 导入传导安全裕度测试数据（频率,等级），加上偏置（默认 +6dB，可改）生成本频段限值曲线，
    /// 也支持手动添加/微调限值点；保存后写回对应频段（限值类型自动切为"文件"）
    /// </summary>
    public partial class CS114LimitImportDialog : Window
    {
        public CS114LimitImportVM VM { get; } = new();

        /// <summary>保存后的限值点集合（IsSaved==true 时有效）</summary>
        public List<CS114LimitPoint>? ResultPoints => VM.BuildResult();

        /// <summary>偏置[dB]</summary>
        public double Offset => VM.OffsetDB;

        /// <summary>裕度数据文件路径（未导入则为空）</summary>
        public string SourcePath => VM.SourcePath ?? "";

        public CS114LimitImportDialog(EMS_CS114FreBand band)
        {
            InitializeComponent();
            VM.Initialize(band);
            DataContext = VM;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        /// <summary>
        /// 输入法兼容：中文标点状态下按小数点键打出全角"。"，绑定 double 的表格单元格会拒绝该值，
        /// 表现为"小数点输不进去"。隧道阶段把全角转半角后再交给控件（详见 ImeInputHelper）。
        /// </summary>
        private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Generic.ImeInputHelper.HandlePreviewTextInput(e);
        }

        /// <summary>
        /// 小数点按键级兜底：主键盘句点/小键盘点键直接拦截并手动插入 "."，
        /// 不依赖输入法状态与控件自身校验（详见 ImeInputHelper）。
        /// </summary>
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Generic.ImeInputHelper.HandlePreviewKeyDown(e);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            VM.IsSaved = true;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }

    /// <summary>
    /// 限值导入弹窗 ViewModel（弹窗内部使用）
    /// </summary>
    public partial class CS114LimitImportVM : ObservableObject
    {
        [ObservableProperty]
        private string _bandInfo = "";

        [ObservableProperty]
        private string? _sourcePath = "";

        [ObservableProperty]
        private double _offsetDB = 6;

        [ObservableProperty]
        private ObservableCollection<CS114LimitPoint> _limitPoints = new();

        /// <summary>最近一次导入的原始数据（重新应用偏置时使用）</summary>
        private List<CS114LimitPoint> _rawPoints = new();

        public bool IsSaved { get; set; }

        public void Initialize(EMS_CS114FreBand band)
        {
            var bp = band.BandParms;
            BandInfo = $"频段 {band.StartFre}{band.StartFreUnit} ~ {band.EndFreq}{band.EndFreUnit}";
            OffsetDB = bp.LimitOffsetDB;
            SourcePath = bp.ImmunityTableAddr;
            LimitPoints = new ObservableCollection<CS114LimitPoint>(
                bp.LimitTable.Select(p => new CS114LimitPoint { FreqMHz = p.FreqMHz, LevelDB = p.LevelDB }));
            _rawPoints = LimitPoints.Select(p => new CS114LimitPoint { FreqMHz = p.FreqMHz, LevelDB = p.LevelDB }).ToList();
        }

        /// <summary>
        /// 保存时构建结果（未点保存返回 null）
        /// </summary>
        public List<CS114LimitPoint>? BuildResult()
        {
            if (!IsSaved) return null;
            return LimitPoints.Select(p => new CS114LimitPoint { FreqMHz = p.FreqMHz, LevelDB = p.LevelDB }).ToList();
        }

        /// <summary>
        /// 导入传导安全裕度测试数据（每行：频率,等级），导入后立即应用当前偏置
        /// </summary>
        [RelayCommand]
        private void OnImport()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "选择传导安全裕度测试数据文件",
                Filter = "数据文件|*.csv;*.txt;*.json|所有文件|*.*"
            };
            if (openFileDialog.ShowDialog() != true) return;
            try
            {
                string ext = Path.GetExtension(openFileDialog.FileName).ToLower();
                var points = new List<CS114LimitPoint>();
                if (ext == ".json")
                {
                    var loaded = JsonSerializer.Deserialize<List<CS114LimitPoint>>(File.ReadAllText(openFileDialog.FileName),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (loaded != null) points.AddRange(loaded);
                }
                else
                {
                    foreach (var line in File.ReadLines(openFileDialog.FileName))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.Length == 0) continue;
                        if (trimmed.StartsWith("频率") || trimmed.StartsWith("Freq") || trimmed.StartsWith("#")) continue;
                        var parts = trimmed.Split(new[] { ',', '\t', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 2) continue;
                        points.Add(new CS114LimitPoint { FreqMHz = parts[0], LevelDB = parts[1] });
                    }
                }

                if (points.Count == 0)
                {
                    MessageBox.Show("未从文件中解析到有效数据点（格式：频率,等级）", "提示");
                    return;
                }

                _rawPoints = points;
                SourcePath = openFileDialog.FileName;
                ApplyOffset();
                MessageBox.Show($"导入成功，共 {points.Count} 个数据点，已应用偏置 +{OffsetDB}dB！", "提示");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败：{ex.Message}", "提示");
            }
        }

        /// <summary>
        /// 把当前偏置应用到最近导入的原始数据上（限值等级 = 原始数据 + 偏置）
        /// </summary>
        [RelayCommand]
        private void OnReapplyOffset()
        {
            if (_rawPoints.Count == 0)
            {
                MessageBox.Show("请先导入裕度测试数据，再应用偏置", "提示");
                return;
            }
            ApplyOffset();
        }

        private void ApplyOffset()
        {
            LimitPoints = new ObservableCollection<CS114LimitPoint>(
                _rawPoints.Select(p => new CS114LimitPoint { FreqMHz = p.FreqMHz, LevelDB = p.LevelDB + OffsetDB }));
        }

        [RelayCommand]
        private void OnAddRow()
        {
            LimitPoints.Add(new CS114LimitPoint());
        }

        [RelayCommand]
        private void OnDeleteRow()
        {
            if (LimitPoints.Count > 0)
                LimitPoints.RemoveAt(LimitPoints.Count - 1);
        }

        [RelayCommand]
        private void OnClear()
        {
            if (LimitPoints.Count == 0) return;
            if (MessageBox.Show("确定清除当前所有限值点吗？", "提示", MessageBoxButton.OKCancel,
                MessageBoxImage.Question) != MessageBoxResult.OK) return;
            LimitPoints.Clear();
            _rawPoints.Clear();
        }
    }
}
