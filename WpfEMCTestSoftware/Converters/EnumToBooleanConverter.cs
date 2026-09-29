using System.Globalization;
using System.Windows.Data;
using WpfEMCTestSoftware.Enums;

namespace WpfEMCTestSoftware.Converters
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            //后台枚举 -> 前台布尔值 (决定RadioButton是否打勾)
            if (value == null || parameter == null)
            {
                return false;
            }
            LimitType type = (LimitType)value;
            if (int.TryParse(parameter.ToString(), out int i))
            {
                if ((int)type == i)
                {
                    return true;
                }
            }
            return false;

        }
        // 前台布尔值 -> 后台枚举 (用户点击RadioButton后更新后台数据)
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 如果 value 不是 true（即用户取消了勾选），则什么都不做，防止把后台数据改乱
            if (value == null || !(value is bool boolValue) || !boolValue)
                return Binding.DoNothing;

            if (parameter == null || targetType == null)
                return Binding.DoNothing;

            try
            {
                // 将暗号（parameter）解析回对应的枚举类型返回给后台
                // Enum.Parse 能够自动处理字符串（如 "ConstData"）或数字（如 "0"）的转换
                if (int.TryParse(parameter.ToString(), out int i))
                {
                    return (LimitType)i;
                }
                else
                {
                    return Binding.DoNothing;
                }
            }
            catch
            {
                return Binding.DoNothing;
            }
        }
    }
}
