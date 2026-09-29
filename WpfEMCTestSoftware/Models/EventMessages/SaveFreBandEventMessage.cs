using CommunityToolkit.Mvvm.Messaging.Messages;
using System.Data;
using WpfEMCTestSoftware.Enums;
using WpfEMCTestSoftware.Models.HardwareModels;
using WpfEMCTestSoftware.Models.TestModels;

namespace WpfEMCTestSoftware.Models.EventMessages
{
    /// <summary>
    /// 
    /// </summary>



    public class SaveHardConfigMessage : ValueChangedMessage<RVC_EMSHardModel?>
    {
        public SaveHardConfigMessage(RVC_EMSHardModel? info) : base(info) { }
    }
    /// <summary>
    /// 通用字符消息
    /// </summary>
    public class UniversalStrMessage : ValueChangedMessage<string>
    {
        public UniversalStrMessage(string info) : base(info) { }
    }
    /// <summary>
    /// 通用整型消息
    /// </summary>
    public class UniversalIntMessage : ValueChangedMessage<int>
    {
        public UniversalIntMessage(int info) : base(info) { }
    }
    /// <summary>
    /// 图表实时数据点（横纵坐标+曲线类型）
    /// </summary>

    public class ChartPointMessage
    {
        public ChartDataType ChartDataType { get; set; }
        public double x { get; set; }
        public double y { get; set; }
    }

    public class ChartUpdateMessage : ValueChangedMessage<ChartPointMessage>
    {
        public ChartUpdateMessage(ChartPointMessage info) : base(info)
        {

        }
    }
    //复合消息类
    public class RVCCompositeConfig
    {
        public RVC_FieldUniformityConfigModel Model { get; set; }
        public DataTable Table { get; set; }
        public RVCCompositeConfig(RVC_FieldUniformityConfigModel model, DataTable table)
        {
            Model = model;
            Table = table;
        }
    }
    //复合消息:混响室测试页面的配置信息和文件路径
    public class CompositeRVC_EMSConfig
    {
        public RVC_EMSTestConfigModel Model { get; set; }
        public string FilePath { get; set; }
        public CompositeRVC_EMSConfig(RVC_EMSTestConfigModel model, string path)
        {
            Model = model;
            FilePath = path;
        }
    }

    public class RVCCompositeConfigMessage : ValueChangedMessage<RVCCompositeConfig>
    {
        public RVCCompositeConfigMessage(RVCCompositeConfig info) : base(info) { }
    }

    public class CompositeRVC_EMSConfigMessage : ValueChangedMessage<CompositeRVC_EMSConfig>
    {
        public CompositeRVC_EMSConfigMessage(CompositeRVC_EMSConfig info) : base(info) { }
    }

    /// <summary>
    /// 复合消息:CS114测试页面的配置信息和文件路径
    /// （Model 为 null 表示新建模板，只切页面不回填）
    /// </summary>
    public class CompositeCS114Config
    {
        public EMS_CS114ConfigModel? Model { get; set; }
        public string FilePath { get; set; }
        public CompositeCS114Config(EMS_CS114ConfigModel? model, string path)
        {
            Model = model;
            FilePath = path;
        }
    }

    public class CompositeCS114ConfigMessage : ValueChangedMessage<CompositeCS114Config>
    {
        public CompositeCS114ConfigMessage(CompositeCS114Config info) : base(info) { }
    }


}
