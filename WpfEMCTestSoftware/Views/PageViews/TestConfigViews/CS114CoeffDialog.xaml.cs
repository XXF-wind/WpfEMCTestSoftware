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
    /// CS114 系数设置弹窗：编辑某条链路的频率-系数表，支持手动添加/导入/导出/清除，
    /// 点保存后由主页面持久化（下次使用自动加载）
    /// </summary>
    public partial class CS114CoeffDialog : Window
    {
        /// <summary>
        /// 弹窗内部绑定上下文
        /// </summary>
        public CS114CoeffDialogVM VM { get; } = new();

        /// <summary>
        /// 保存结果（IsSaved==true 时有效）
        /// </summary>
        public CS114CableLoss? Result => VM.BuildResult();

        public CS114CoeffDialog(CS114CableLoss link)
        {
            InitializeComponent();
            VM.Initialize(link);
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
    /// 系数弹窗 ViewModel（弹窗内部使用）
    /// </summary>
    public partial class CS114CoeffDialogVM : ObservableObject
    {
        [ObservableProperty]
        private string _linkDisplayName = "";

        [ObservableProperty]
        private string _statusText = "未添加";

        [ObservableProperty]
        private string _pointCountInfo = "共 0 个系数点";

        [ObservableProperty]
        private bool _hasPoints;

        [ObservableProperty]
        private ObservableCollection<CS114CoeffPoint> _coeffPoints = new();

        private CS114CableLoss? _sourceLink;
        private string _source = "手动";

        public bool IsSaved { get; set; }

        public void Initialize(CS114CableLoss link)
        {
            _sourceLink = link;
            LinkDisplayName = link.DisplayName;
            CoeffPoints = new ObservableCollection<CS114CoeffPoint>(
                link.Points.Select(p => new CS114CoeffPoint { FreqMHz = p.FreqMHz, CoefDB = p.CoefDB }));
            _source = string.IsNullOrEmpty(link.Source) ? "手动" : link.Source;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            HasPoints = CoeffPoints.Count > 0;
            StatusText = HasPoints ? "已添加" : "未添加";
            PointCountInfo = $"共 {CoeffPoints.Count} 个系数点";
            OnPropertyChanged(nameof(HasPoints));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(PointCountInfo));
        }

        /// <summary>
        /// 保存时构建结果（未点保存返回 null）
        /// </summary>
        public CS114CableLoss? BuildResult()
        {
            if (!IsSaved || _sourceLink == null) return null;
            return new CS114CableLoss
            {
                FromName = _sourceLink.FromName,
                ToName = _sourceLink.ToName,
                DisplayName = _sourceLink.DisplayName,
                Points = CoeffPoints.ToList(),
                Source = _source,
            };
        }

        [RelayCommand]
        private void OnAddRow()
        {
            CoeffPoints.Add(new CS114CoeffPoint());
            RefreshStatus();
        }

        [RelayCommand]
        private void OnDeleteRow()
        {
            // 删除最后聚焦/最后一行：此处删除最后一行，简单直观
            if (CoeffPoints.Count > 0)
            {
                CoeffPoints.RemoveAt(CoeffPoints.Count - 1);
                RefreshStatus();
            }
        }

        [RelayCommand]
        private void OnImport()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "导入系数文件",
                Filter = "系数文件|*.csv;*.txt;*.json|所有文件|*.*"
            };
            if (openFileDialog.ShowDialog() != true) return;
            try
            {
                string ext = Path.GetExtension(openFileDialog.FileName).ToLower();
                var points = new List<CS114CoeffPoint>();
                if (ext == ".json")
                {
                    var loaded = JsonSerializer.Deserialize<List<CS114CoeffPoint>>(File.ReadAllText(openFileDialog.FileName));
                    if (loaded != null) points.AddRange(loaded);
                }
                else
                {
                    // 文本格式：每行 "频率,系数"（支持逗号/制表符/空格分隔，自动跳过表头）
                    foreach (var line in File.ReadLines(openFileDialog.FileName))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.Length == 0) continue;
                        if (trimmed.StartsWith("频率") || trimmed.StartsWith("Freq") || trimmed.StartsWith("#")) continue;
                        var parts = trimmed.Split(new[] { ',', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 2) continue;
                        points.Add(new CS114CoeffPoint { FreqMHz = parts[0], CoefDB = parts[1] });

                    }
                }

                if (points.Count == 0)
                {
                    MessageBox.Show("未从文件中解析到有效系数点（格式：频率,系数）", "提示");
                    return;
                }
                CoeffPoints = new ObservableCollection<CS114CoeffPoint>(points);
                _source = "导入";
                RefreshStatus();
                MessageBox.Show($"导入成功，共 {points.Count} 个系数点！", "提示");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败：{ex.Message}", "提示");
            }
        }

        [RelayCommand]
        private void OnExport()
        {
            if (CoeffPoints.Count == 0)
            {
                MessageBox.Show("当前没有系数点可导出", "提示");
                return;
            }
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Title = "导出系数文件",
                FileName = $"{LinkDisplayName}_系数",
                DefaultExt = ".csv",
                Filter = "CSV 文件|*.csv|文本文件|*.txt|所有文件|*.*"
            };
            if (saveFileDialog.ShowDialog() != true) return;
            try
            {
                var lines = new List<string> { "频率(MHz),系数(dB)" };
                lines.AddRange(CoeffPoints.Select(p => $"{p.FreqMHz},{p.CoefDB}"));
                File.WriteAllLines(saveFileDialog.FileName, lines);
                MessageBox.Show("导出成功！", "提示");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败：{ex.Message}", "提示");
            }
        }

        [RelayCommand]
        private void OnClear()
        {
            if (CoeffPoints.Count == 0) return;
            if (MessageBox.Show("确定清除当前所有系数点吗？", "提示", MessageBoxButton.OKCancel,
                MessageBoxImage.Question) != MessageBoxResult.OK) return;
            CoeffPoints.Clear();
            _source = "手动";
            RefreshStatus();
        }
    }
}
