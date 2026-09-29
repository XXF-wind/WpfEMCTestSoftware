using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Models;
using WpfEMCTestSoftware.Models.EventMessages;
using static WpfEMCTestSoftware.Models.globalDefinition;


namespace WpfEMCTestSoftware.ViewModels
{
    public partial class LimitConfigViewModel : BaseViewModel
    {
        // ===================== 图表绑定核心 =====================       
        [ObservableProperty]
        private string _xTitie = "频率(MHz)";
        [ObservableProperty]
        private string _yTitle = "电平(dBm)";
        [ObservableProperty]
        private string _valueLabel = "校准数据";


        /// <summary>
        /// 树形结构数据
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<TreeItem>? _treeItems = [];
        // 当前选中的节点
        [ObservableProperty]
        private TreeItem _curTreeItem = new();
        [ObservableProperty]
        private DataTable _csvDataTable = new DataTable();
        /// <summary>
        /// 当前选中的文件名
        /// </summary>
        [ObservableProperty]
        private string _fileName = string.Empty;
        private readonly Dispatcher _dispatcher;
        public LimitConfigViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            LoadTreeItems();

        }

        /// <summary>
        /// 加载硬件模板文件树节点
        /// </summary>
        private void LoadTreeItems()
        {
            TreeItems?.Clear();
            string TemplatePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "System", "参数管理");
            TreeItem? item = GenericHelper.LoadTreeItemsFromDirectory(TemplatePath);
            if (item != null)
                TreeItems?.Add(item);
        }
        private DataTable ReadCsvToDataTable(string filePath)
        {
            DataTable dt = new DataTable();
            string[] lines = File.ReadAllLines(filePath);
            if (lines.Length == 0)
                return dt;
            // 拆分第一行
            string[] firstRow = lines[0].Split(',');
            // ==========================================
            // 判断：第一行是不是纯数字
            // 有数字 → 无表头 → 自动生成 A,B,C...
            // 无数字 → 有表头 → 用第一行做表头
            // ==========================================
            bool hasHeader = !HasNumericValue(firstRow);
            int dataStartIndex = hasHeader ? 1 : 0;
            // ======================
            // 创建列
            // ======================
            if (hasHeader)
            {
                foreach (string header in firstRow)
                    dt.Columns.Add(header.Trim());
            }
            else
            {
                // 自动生成列名 A,B,C,D...
                for (int i = 0; i < firstRow.Length; i++)
                    dt.Columns.Add($"Column {(char)('A' + i)}");
            }
            // ======================
            // 加载所有数据行
            // ======================
            for (int i = dataStartIndex; i < lines.Length; i++)
            {
                string[] fields = lines[i].Split(',');
                // 补齐列（防止列数不够，导致显示不全）
                while (fields.Length < dt.Columns.Count)
                {
                    Array.Resize(ref fields, fields.Length + 1);
                }
                dt.Rows.Add(fields);
            }
            return dt;
        }
        private bool HasNumericValue(string[] row)
        {
            foreach (string cell in row)
            {
                if (double.TryParse(cell.Trim(), out _))
                    return true;
            }
            return false;
        }
        private void SaveDataTableToCsv(DataTable dt, string filePath)
        {
            var lines = new List<string>();
            // 表头
            lines.Add(string.Join(",", dt.Columns.Cast<DataColumn>()));
            // 行内容
            foreach (DataRow row in dt.Rows)
            {
                lines.Add(string.Join(",", row.ItemArray));
            }

            File.WriteAllLines(filePath, lines);
        }

        /// <summary>
        /// 设备树节点选中,子节点:查看，修改，删除 根节点：新建
        /// </summary>
        /// <param name="item"></param>
        public void SelectedTreeItem(TreeItem item)
        {
            CurTreeItem = item;
            if (item.Children?.Count == 0)
            {
                FileName = CurTreeItem.Name;
                if (File.Exists(CurTreeItem.FullName))
                {
                    // 读取 CSV 并显示为表格
                    CsvDataTable = ReadCsvToDataTable(CurTreeItem.FullName);
                    UpdateChartFromCsv(CsvDataTable);
                }
            }
        }
        /// <summary>
        /// 从CSV表格自动生成图表数据
        /// 第1列 = X轴（频率）
        /// 第2列 = Y轴（数值）
        /// Y轴标题 = 第二列的表头
        /// </summary>
        private void UpdateChartFromCsv(DataTable dt)
        {
            try
            {
                if (dt == null || dt.Rows.Count == 0)
                    return;
                if (dt.Columns.Count >= 2)
                {
                    YTitle = dt.Columns[1].ColumnName;      // Y轴=第二列表头
                    ValueLabel = dt.Columns[1].ColumnName;  // 图例也用第二列名称
                }
                // ======================
                // 逐行读取数据 X Y
                // ======================
                foreach (DataRow row in dt.Rows)
                {
                    // 第一列 = X 频率
                    if (!double.TryParse(row[0].ToString(), out double x))
                        continue;
                    // 第二列 = Y 数值
                    if (!double.TryParse(row[1].ToString(), out double y))
                        continue;

                    // 添加到图表
                    //ListValue.Add(new ObservablePoint(x, y));
                }
            }
            catch
            {

            }
        }

        /// <summary>
        /// 添加，根节点(文件夹下)下可以添加空白模板
        /// </summary>
        [RelayCommand]
        private void OnAddTable()
        {

            if (CurTreeItem == null) return;

            var allowedFolders = new List<string>
            {
                LimitFolders.loss,
                LimitFolders.ampMaxInput,
                LimitFolders.coupleFactor,
                LimitFolders.frePower,
                LimitFolders.probeFactor,
                LimitFolders.signalLimit,
            };

            if (!allowedFolders.Contains(CurTreeItem.Name))
            {
                MessageBox.Show("请在指定的配置文件夹下添加文件！");
                return;
            }

            try
            {
                string folderPath = CurTreeItem.FullName;

                // ==============================
                // 弹出输入框，让用户自己输入文件名
                // ==============================
                string defaultFileName = $"新配置_{CurTreeItem.Name}";
                string? inputFileName = Microsoft.VisualBasic.Interaction.InputBox(
                    "请输入文件名",
                    "新建CSV文件",
                    defaultFileName, -1, -1);

                // 用户取消输入
                if (string.IsNullOrWhiteSpace(inputFileName))
                {
                    return;
                }

                // 自动补充 .csv 后缀
                if (!inputFileName.EndsWith(".csv"))
                {
                    inputFileName += ".csv";
                }

                string newFilePath = Path.Combine(folderPath, inputFileName);

                // 重名判断
                if (File.Exists(newFilePath))
                {
                    MessageBox.Show("文件名已存在！");
                    return;
                }
                string header = string.Empty;
                if (LimitFolderToHeader.ContainsKey(CurTreeItem.Name))
                    header = LimitFolderToHeader[CurTreeItem.Name];
                // 创建文件
                File.WriteAllText(newFilePath, header);
                // 刷新树
                LoadTreeItems();

                MessageBox.Show($"创建成功：\n{inputFileName}", "成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建失败：{ex.Message}");
            }


        }
        /// <summary>
        /// 删除已有的csv
        /// </summary>
        [RelayCommand]
        private void OnDeleteTable()
        {
            // 1. 不能删除文件夹，只能删除【文件】
            if (CurTreeItem == null || CurTreeItem.Children?.Count > 0)
            {
                MessageBox.Show("请选择要删除的 CSV 文件！", "提示");
                return;
            }

            // 2. 二次确认
            if (MessageBox.Show($"确定要删除文件：\n{CurTreeItem.Name}\n\n删除后无法恢复！", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                // 3. 获取真实文件路径
                string filePath = CurTreeItem.FullName;

                if (File.Exists(filePath))
                {
                    // 4. 删除文件
                    File.Delete(filePath);

                    // 5. 清空表格
                    CsvDataTable?.Clear();

                    // 6. 刷新树
                    LoadTreeItems();

                    MessageBox.Show("文件删除成功！", "成功");
                }
                else
                {
                    MessageBox.Show("文件不存在！", "错误");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除失败：{ex.Message}", "失败");
            }


        }


        /// <summary>
        /// 确认/保存点击命令
        /// </summary>
        [RelayCommand]
        private void OnConfirm()
        {
            if (CurTreeItem == null || CsvDataTable == null)
                return;

            try
            {
                // 把表格写回文件
                SaveDataTableToCsv(CsvDataTable, CurTreeItem.FullName);
                MessageBox.Show("保存成功！", "提示");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败：{ex.Message}", "错误");
            }

        }
        /// <summary>
        /// 另存点击命令
        /// </summary>
        [RelayCommand]
        private void OnSaveAS()
        {
            // 1. 数据校验
            if (CsvDataTable == null || CsvDataTable.Rows.Count == 0)
            {
                MessageBox.Show("没有数据可保存！", "提示");
                return;
            }

            if (CurTreeItem == null)
            {
                MessageBox.Show("请先选择文件！", "提示");
                return;
            }

            try
            {
                // 2. 获取【当前选中的文件夹】
                string folderPath;

                // 如果是文件夹
                if (CurTreeItem.Children != null && CurTreeItem.Children.Count > 0)
                {
                    folderPath = CurTreeItem.FullName;
                }
                else
                {
                    // 如果是文件，取文件所在的目录
                    folderPath = Path.GetDirectoryName(CurTreeItem.FullName)!;
                }

                // 3. 用户输入文件名
                string defaultFileName = Path.GetFileNameWithoutExtension(CurTreeItem.Name);
                string? inputFileName = Microsoft.VisualBasic.Interaction.InputBox(
                    "请输入文件名",
                    "另存为CSV文件",
                    defaultFileName, -1, -1);
                if (string.IsNullOrWhiteSpace(inputFileName))
                    return;

                if (!inputFileName.EndsWith(".csv"))
                    inputFileName += ".csv";
                string newFilePath = Path.Combine(folderPath, inputFileName);
                // 4. 重名判断
                if (File.Exists(newFilePath))
                {
                    MessageBox.Show("文件名已存在！", "警告");
                    return;
                }
                // 5. 保存
                SaveDataTableToCsv(CsvDataTable, newFilePath);
                LoadTreeItems();
                MessageBox.Show("另存成功！", "成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"另存失败：{ex.Message}", "错误");
            }
        }

        /// <summary>
        /// 取消点击命令
        /// </summary>
        [RelayCommand]
        private void OnCencel()
        {
            Messenger.Send(new ExitClickEventMessage(-1), (int)PageExitMessageToken.SubPageToMainWindow);
        }
    }
}
