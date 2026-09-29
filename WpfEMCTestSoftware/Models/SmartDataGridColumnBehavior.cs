using Microsoft.Xaml.Behaviors;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace WpfEMCTestSoftware.Models
{

    /// <summary>
    /// DataGrid智能列生成行为（自动绑定、动态列、类型识别）
    /// </summary>
    public class SmartDataGridColumnBehavior : Behavior<DataGrid>
    {
        #region 依赖属性（支持绑定视图模型配置）
        /// <summary>
        /// 数据模型类型（如 typeof(Student)）
        /// </summary>
        public Type ModelType
        {
            get => (Type)GetValue(ModelTypeProperty);
            set => SetValue(ModelTypeProperty, value);
        }

        public static readonly DependencyProperty ModelTypeProperty =
            DependencyProperty.Register(nameof(ModelType), typeof(Type), typeof(SmartDataGridColumnBehavior),
                new PropertyMetadata(null, OnModelTypeChanged));

        /// <summary>
        /// 列配置变更通知（用于动态刷新列）
        /// </summary>
        public bool RefreshColumnsTrigger
        {
            get => (bool)GetValue(RefreshColumnsTriggerProperty);
            set => SetValue(RefreshColumnsTriggerProperty, value);
        }

        public static readonly DependencyProperty RefreshColumnsTriggerProperty =
            DependencyProperty.Register(nameof(RefreshColumnsTrigger), typeof(bool), typeof(SmartDataGridColumnBehavior),
                new PropertyMetadata(false, OnRefreshColumnsTriggerChanged));
        #endregion

        #region 事件处理
        private static void OnModelTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SmartDataGridColumnBehavior behavior && behavior.AssociatedObject != null)
            {
                behavior.GenerateColumns();
            }
        }

        private static void OnRefreshColumnsTriggerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SmartDataGridColumnBehavior behavior && behavior.AssociatedObject != null && (bool)e.NewValue)
            {
                behavior.GenerateColumns();
                // 重置触发器
                behavior.RefreshColumnsTrigger = false;
            }
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            // 初始生成列
            if (ModelType != null)
            {
                GenerateColumns();
            }
        }
        #endregion

        /// <summary>
        /// 智能生成列（核心逻辑）
        /// </summary>
        private void GenerateColumns()
        {
            var dataGrid = AssociatedObject;
            if (dataGrid == null || ModelType == null) return;

            // 清空现有列
            dataGrid.Columns.Clear();

            // 获取模型所有公共属性（排除索引器）
            var properties = ModelType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => !p.GetIndexParameters().Any())
                .OrderBy(p => p.MetadataToken); // 保持属性定义顺序

            foreach (var prop in properties)
            {
                // 获取列配置特性
                var columnSetting = prop.GetCustomAttribute<DataGridColumnSettingAttribute>();
                if (columnSetting == null || !columnSetting.IsVisible)
                    continue; // 跳过无配置或隐藏的列

                // 根据属性类型创建对应列
                DataGridColumn dataGridColumn = CreateColumnByPropertyType(prop, columnSetting);
                if (dataGridColumn == null) continue;

                // 配置列基础属性
                ConfigureColumnBaseSettings(dataGridColumn, prop, columnSetting);

                // 添加到DataGrid
                dataGrid.Columns.Add(dataGridColumn);
            }
        }

        /// <summary>
        /// 根据属性类型创建对应列
        /// </summary>
        private DataGridColumn CreateColumnByPropertyType(PropertyInfo prop, DataGridColumnSettingAttribute setting)
        {
            return prop.PropertyType switch
            {
                //布尔列（如"状态"勾选）：DataGridCheckBoxColumn 在 IsReadOnly 表格里点不动，
                //改用模板列内嵌可交互CheckBox，勾选后经TwoWay绑定回写模型，再由"保存"按钮统一落库
                Type t when t == typeof(bool) || t == typeof(bool?) => CreateInteractiveCheckBoxColumn(prop),
                Type t when t.IsNumericType() => new DataGridTextColumn(), // 数字类型用文本列（支持排序）
                _ => new DataGridTextColumn() // 默认文本列（字符串、枚举等）
            };
        }

        /// <summary>
        /// 创建可勾选的复选框模板列（CellTemplate内容不受DataGrid只读模式限制）
        /// </summary>
        private DataGridColumn CreateInteractiveCheckBoxColumn(PropertyInfo prop)
        {
            DataGridTemplateColumn column = new();
            FrameworkElementFactory factory = new(typeof(CheckBox));
            factory.SetValue(System.Windows.Controls.CheckBox.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            factory.SetValue(System.Windows.Controls.CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
                new System.Windows.Data.Binding(prop.Name)
                {
                    Mode = System.Windows.Data.BindingMode.TwoWay,
                    UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
                });
            column.CellTemplate = new DataTemplate { VisualTree = factory };
            return column;
        }

        /// <summary>
        /// 配置列的基础设置（绑定、标题、宽度、对齐等）
        /// </summary>
        private void ConfigureColumnBaseSettings(DataGridColumn column, PropertyInfo prop, DataGridColumnSettingAttribute setting)
        {
            // 绑定属性
            if (column is DataGridBoundColumn boundColumn)
            {
                boundColumn.Binding = new System.Windows.Data.Binding(prop.Name)
                {
                    Mode = prop.CanWrite ? System.Windows.Data.BindingMode.TwoWay : System.Windows.Data.BindingMode.OneWay,
                    UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
                };
            }

            // 中文标题
            column.Header = setting.ChineseTitle;

            // 列宽度（支持固定值、Auto、*）
            if (double.TryParse(setting.Width, out var length))
            {
                column.Width = length;
            }
            else
            {
                column.Width = DataGridLength.Auto;
            }

            // 水平对齐方式
            if (column is DataGridTextColumn textColumn)
            {
                textColumn.ElementStyle = new Style(typeof(TextBlock))
                {
                    Setters = {
                        new Setter(TextBlock.HorizontalAlignmentProperty, setting.HorizontalAlignment),

                    }
                };
            }
            else if (column is DataGridCheckBoxColumn checkBoxColumn)
            {
                checkBoxColumn.ElementStyle = new Style(typeof(CheckBox))
                {
                    Setters = { new Setter(CheckBox.HorizontalAlignmentProperty, setting.HorizontalAlignment), new Setter(CheckBox.VerticalAlignmentProperty, setting.VerticalAlignment) }
                };
            }

            // 排序支持
            column.CanUserSort = setting.CanSort;
        }
    }

    // 扩展方法：判断是否为数字类型
    public static class TypeExtensions
    {
        public static bool IsNumericType(this Type type)
        {
            if (type.IsNullableType())
                type = Nullable.GetUnderlyingType(type)!;

            return type == typeof(int) || type == typeof(long) || type == typeof(short) ||
                   type == typeof(byte) || type == typeof(float) || type == typeof(double) ||
                   type == typeof(decimal) || type == typeof(uint) || type == typeof(ulong) ||
                   type == typeof(ushort) || type == typeof(sbyte);
        }

        public static bool IsNullableType(this Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }
    }
}
