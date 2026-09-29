using ClassLibrary;
using MaterialDesignThemes.Wpf;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using WpfEMCTestSoftware.Enums;
using WpfEMCTestSoftware.Models;

namespace WpfEMCTestSoftware
{
    /// <summary>
    /// 通用工具类
    /// </summary>
    public static class GenericHelper
    {
        #region 公共变量/属性
        public static ClsUserInfo CurUser = new();
        #endregion

        /// <summary>
        /// 获取字符串资源
        /// </summary>
        public static string? GetStringResource(string resourceKey)
        {
            object obj = Application.Current.Resources[resourceKey] ?? $"[{resourceKey}]"; // 未找到时显示Key，便于调试
            return obj?.ToString();
        }
        #region 文件目录与Tree间操作

        /// <summary>
        /// 将路径下的目录结构加载为TreeItem集合
        /// </summary>
        /// <param name="rootPath"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static TreeItem? LoadTreeItemsFromDirectory(string rootPath)
        {
            // 验证路径是否存在
            if (!Directory.Exists(rootPath))
            {
                throw new DirectoryNotFoundException($"指定的路径不存在: {rootPath}");
            }

            // 创建根节点
            var rootDirectoryInfo = new DirectoryInfo(rootPath);
            var rootItem = new TreeItem
            {
                Name = rootDirectoryInfo.Name,
                FullName = rootPath,
                IconForeground = Brushes.Yellow, // 蓝色图标
                IconKind = PackIconKind.Folder, // 根文件夹默认图标
                Children = []
            };

            // 递归加载子节点
            LoadChildItems(rootItem);

            return rootItem;
        }
        public static ObservableCollection<DeviceInfo>? globalDevices = [];
        public static class DeviceCacheStore
        {
            // 按类型独立存储，存的是真实子类
            public static ObservableCollection<SignalSourcePara> SignalSources { get; set; } = new();
            public static ObservableCollection<AmpifierPara> Amplifiers { get; set; } = new();
            public static ObservableCollection<PowerMeterPara> PowerMeters { get; set; } = new();
            public static ObservableCollection<FieldStrengthMeterPara> FieldStrengthMeters { get; set; } = new();
            public static ObservableCollection<SwitchMatrixPara> SwitchMatrixs { get; set; } = new();
            public static ObservableCollection<DeviceInfo> NetworkAnalysis { get; set; } = new();
            public static ObservableCollection<DeviceInfo> Receivers { get; set; } = new();
        }
        public static Dictionary<string, string> DeviceTypeMapping = new()
        {
            {"Signal source","信号源"},
            {"Receiver","接收机"},
            {"Field strength Meter", "场强仪"},
            {"Amplifier" ,"功放"},
            {"Power meter","功率计" },
            {"Phase attenuator","移相衰减器"},
            {"Multiplier frequency","倍频器"},
            { "Switch matrix" ,"开关矩阵"},
            {"Oscilloscope", "频谱仪" },
            {"NetworkAnalysis", "矢量网络分析仪"},
        };

        /// <summary>
        /// 递归加载子节点
        /// </summary>
        private static void LoadChildItems(TreeItem parentItem)
        {
            try
            {
                // 加载子文件夹
                var directories = Directory.GetDirectories(parentItem.FullName);
                foreach (var dirPath in directories)
                {
                    var dirInfo = new DirectoryInfo(dirPath);

                    var dirItem = new TreeItem
                    {
                        Name = dirInfo.Name,
                        FullName = dirPath,
                        IconKind = PackIconKind.Folder, // 子文件夹图标    
                        Children = new ObservableCollection<TreeItem>()
                    };
                    parentItem.Children?.Add(dirItem);
                    // 递归加载子文件夹的内容
                    LoadChildItems(dirItem);
                }

                var files = Directory.GetFiles(parentItem.FullName);
                foreach (var filePath in files)
                {
                    var fileInfo = new FileInfo(filePath);
                    var fileItem = new TreeItem
                    {
                        Name = fileInfo.Name,
                        FullName = fileInfo.FullName,
                        IconKind = GetFileIconKind(fileInfo.Extension), // 根据文件扩展名设置图标
                        Children = []
                    };
                    parentItem.Children?.Add(fileItem);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                // 处理权限不足的情况
                var accessDeniedItem = new TreeItem
                {
                    Name = "【权限不足】",
                    IconKind = PackIconKind.Error,
                    Children = []
                };
                parentItem.Children?.Add(accessDeniedItem);
                LogManager.WriteLog($"权限不足: {ex.Message}");
            }
            catch (Exception ex)
            {
                // 处理其他异常
                var errorItem = new TreeItem
                {
                    Name = "【加载错误】",
                    IconKind = PackIconKind.Warning,
                    Children = new ObservableCollection<TreeItem>()
                };
                parentItem.Children?.Add(errorItem);
                LogManager.WriteLog($"加载失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据文件扩展名获取对应的图标类型
        /// </summary>
        private static PackIconKind GetFileIconKind(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                // 常见文件类型对应的图标
                ".txt" => PackIconKind.FileDocument,
                ".doc" or ".docx" => PackIconKind.FileWordBox,
                ".xls" or ".xlsx" => PackIconKind.FileExcelBox,
                ".ppt" or ".pptx" => PackIconKind.FilePowerpointBox,
                ".pdf" => PackIconKind.FilePdfBox,
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => PackIconKind.FileImage,
                ".mp3" or ".wav" or ".flac" => PackIconKind.FileMusic,
                ".mp4" or ".avi" or ".mkv" => PackIconKind.FileVideo,
                ".exe" => PackIconKind.FileCog,
                ".dll" => PackIconKind.FileCode,
                ".json" or ".xml" or ".yaml" => PackIconKind.FileCode,
                ".cs" or ".cpp" or ".java" or ".py" => PackIconKind.FileCode,
                ".html" or ".css" or ".js" => PackIconKind.FileCode,
                ".sql" => PackIconKind.Database,
                // 默认文件图标
                _ => PackIconKind.FileMusic
            };
        }

        // 递归查找节点的父节点
        public static TreeItem? FindParentNode(ObservableCollection<TreeItem> parentList, TreeItem targetNode)
        {
            foreach (var parent in parentList)
            {
                if (parent.Children == null)
                {
                    continue;
                }
                // 子节点中找到目标，返回当前父节点
                if (parent.Children.Contains(targetNode))
                {
                    return parent;
                }

                // 递归查找子节点的子节点
                var found = FindParentNode(parent.Children, targetNode);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        #endregion

        /// <summary>
        /// 剥离带#开头的行，返回纯JSON内容
        /// </summary>
        public static string StripFreqRangePrefix(string content)
        {
            string[] lines = content.Split('\n');
            var jsonLines = new List<string>();
            bool isPrefixLine = true;
            foreach (var line in lines)
            {
                string trimmedLine = line.TrimStart();
                // 跳过以FREQ_RANGES开头的行
                if (isPrefixLine && trimmedLine.StartsWith('#'))
                {
                    isPrefixLine = false;
                    continue;
                }
                jsonLines.Add(line);
            }
            // 拼接JSON行并去除首尾空白
            return string.Join(Environment.NewLine, jsonLines).Trim();
        }

        public static void SortDevicesByType()
        {
            // 清空
            DeviceCacheStore.SignalSources.Clear();
            DeviceCacheStore.Amplifiers.Clear();
            DeviceCacheStore.PowerMeters.Clear();
            DeviceCacheStore.FieldStrengthMeters.Clear();
            DeviceCacheStore.SwitchMatrixs.Clear();
            DeviceCacheStore.NetworkAnalysis.Clear();
            DeviceCacheStore.Receivers.Clear();

            // 按真实类型分类存储
            foreach (var device in globalDevices)
            {
                switch (device)
                {
                    case SignalSourcePara signalSource:
                        DeviceCacheStore.SignalSources.Add(signalSource);
                        break;
                    case AmpifierPara amp:
                        DeviceCacheStore.Amplifiers.Add(amp);
                        break;
                    case PowerMeterPara powerMeter:
                        DeviceCacheStore.PowerMeters.Add(powerMeter);
                        break;
                    case FieldStrengthMeterPara field:
                        DeviceCacheStore.FieldStrengthMeters.Add(field);
                        break;
                    case SwitchMatrixPara sw:
                        DeviceCacheStore.SwitchMatrixs.Add(sw);
                        break;
                    case ReceiverPara recv:
                        DeviceCacheStore.Receivers.Add(recv);
                        break;
                    case DeviceInfo Network:
                        DeviceCacheStore.NetworkAnalysis.Add(Network);
                        break;

                }
            }
        }
        /// <summary>
        /// 将不同单位的频率数值统一转换为以 MHz（兆赫）为单位的数值。
        /// </summary>
        /// <param name="data">需要进行转换的原始频率数值。</param>
        /// <param name="freUnit">原始数值对应的频率单位（Hz, kHz, MHz, GHz）。</param>
        /// <returns>返回转换后以 MHz 为基准的双精度浮点数（double）。</returns>
        public static double ConvertToMhzData(double data, FrequencyUnit freUnit)
        {
            double MHzData = 0.0;
            switch ((int)freUnit)
            {
                case (int)FrequencyUnit.Hz:
                    MHzData = data / 1000000.0;
                    break;
                case (int)FrequencyUnit.KHz:
                    MHzData = data / 1000.0;
                    break;
                case (int)FrequencyUnit.MHz:
                    MHzData = data;
                    break;
                case (int)FrequencyUnit.GHz:
                    MHzData = data * 1000;
                    break;
                default:
                    break;
            }
            return MHzData;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mhzData"></param>
        /// <returns></returns>
        public static (double Value, FrequencyUnit Unit) ConvertMhzToAppropriateUnit(double mhzData)
        {
            // 1. 如果数值大于等于 1000，说明用 GHz 表示更合适 (例如 1500 MHz -> 1.5 GHz)
            if (mhzData >= 1000.0)
            {
                return (mhzData / 1000.0, FrequencyUnit.GHz);
            }
            // 2. 如果数值在 1 到 1000 之间，保持 MHz 不变 (例如 100 MHz -> 100 MHz)
            else if (mhzData >= 1.0)
            {
                return (mhzData, FrequencyUnit.MHz);
            }
            // 3. 如果数值小于 1，但乘以 1000 后大于等于 1，说明用 kHz 表示更合适 (例如 0.1 MHz -> 100 kHz)
            else if (mhzData * 1000.0 >= 1.0)
            {
                return (mhzData * 1000.0, FrequencyUnit.KHz);
            }
            // 4. 如果数值非常小，连 kHz 都不到，则降级为 Hz (例如 0.0005 MHz -> 500 Hz)
            else
            {
                return (mhzData * 1000000.0, FrequencyUnit.Hz);
            }
        }

        public static T DeepClone<T>(this T source)
        {
            if (source == null) return default;

            // 系统原生JSON序列化 → 反序列化 = 深拷贝
            var json = JsonSerializer.Serialize(source);
            return JsonSerializer.Deserialize<T>(json);
        }
        public static string GetDescription(this Enum enumVal)
        {
            var field = enumVal.GetType().GetField(enumVal.ToString());
            var attr = field?.GetCustomAttribute<DescriptionAttribute>();
            return attr?.Description ?? enumVal.ToString();
        }

        public static Dictionary<double, double> ReadcsvToDictory(string filepath)
        {
            Dictionary<double, double> FCouplingData = new Dictionary<double, double>();
            if (!File.Exists(filepath)) return FCouplingData;
            try
            {
                var lines = File.ReadAllLines(filepath);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string[] parts = line.Split(',');
                    if (parts.Length < 2)
                        continue;
                    if (double.TryParse(parts[0], out double freq) && double.TryParse(parts[1], out double value))
                    {
                        FCouplingData[freq] = value;
                    }
                }
            }
            catch
            {
                return FCouplingData;
            }
            return FCouplingData;
        }

        /// <summary>
        /// 程序启动时自动清理 .old 文件 → 移动到 backup 备份目录
        /// </summary>
        public static void CleanOldVersionFiles()
        {
            try
            {
                var oldFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.old");
                if (oldFiles.Length == 0) return;

                // 按时间创建备份文件夹
                var backupDir = Path.Combine(AppContext.BaseDirectory, "backup", DateTime.Now.ToString("yyyy-MM-dd_HHmmss"));
                Directory.CreateDirectory(backupDir);

                foreach (var oldFile in oldFiles)
                {
                    var fileName = Path.GetFileName(oldFile);
                    var dest = Path.Combine(backupDir, fileName);
                    File.Move(oldFile, dest);
                }
            }
            catch
            {
                // 清理失败不影响启动
            }
        }
    }
}
