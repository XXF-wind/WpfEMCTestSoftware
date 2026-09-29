using CommunityToolkit.Mvvm.ComponentModel;
using MaterialDesignThemes.Wpf;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Windows.Media.Imaging;

namespace WpfEMCTestSoftware.Models
{
    // 使用部分类声明，允许将类的定义分散在多个文件中
    public partial class TreeItem : ObservableObject
    {
        // [ObservableProperty] 特性：自动生成具有属性更改通知的完整属性
        // 实际生成的属性名为 "Name"，对应私有字段 _name
        [ObservableProperty]
        private string _name = string.Empty; // 节点显示名称
        [ObservableProperty]
        private Brush _iconForeground = Brushes.Goldenrod;

        [ObservableProperty]
        private string _fullName = string.Empty;// 节点完整名称（可能包含路径或其他标识）

        /// <summary>
        /// MaterialDesignThemes.Wpf.PackIcon
        /// </summary>
        [ObservableProperty]
        private PackIconKind _iconKind; //来自 MaterialDesignThemes 库的矢量图标，资源占用小，可缩放

        /// <summary>
        /// 图片图标
        /// </summary>
        [ObservableProperty]
        private BitmapImage? _icon;

        private string _path = "";
        public string Path
        {
            get => _path;
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    BitmapImage bitmap = new();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(value);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    if (bitmap != null)
                        Icon = bitmap;
                }
                _path = value;
            }
        }

        [ObservableProperty]
        private ObservableCollection<TreeItem>? _children = [];
    }
}
