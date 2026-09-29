using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WpfEMCTestSoftware.Enums;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace WpfEMCTestSoftware.Converters
{
    public class ChannelStatusToBrushConverter : IValueConverter
    {
        Brush brush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not ChannelStatus status)
                return brush;
            brush = status switch
            {
                ChannelStatus.Idle => new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                ChannelStatus.Selected => new SolidColorBrush(Color.FromRgb(255, 255, 0)), // 选中黄色
                ChannelStatus.TestFinshed => new SolidColorBrush(Color.FromRgb(0, 255, 0)), // 测试完成绿色
                _ => new SolidColorBrush(Color.FromRgb(255, 255, 255))
            };
            return brush;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

    }
}
