namespace WpfEMCTestSoftware.Models
{
    public class globalDefinition
    {
        public enum TestTemplateConfigFlagEnum
        {
            AmplifierTestConfig = 10,
            EMSRadiatedConfig,
            EMSConductedConfig,
            EMIConductedConfig,
            EMIRadiatedConfig,

        }
        public static class LimitHeaders
        {
            public const string loss = "频率(MHz),插损值(dB)";
            public const string ampMaxInput = "频率(MHz),最大输入(dBm)";
            public const string coupleFactor = "频率(MHz),耦合值(dBm)";
            public const string frePower = "频率(MHz),功率(W)";
            public const string probeFactor = "频率(MHz),因子";
            public const string signalLimit = "频率(MHz),限制(dBm)";
        }

        public static class LimitFolders
        {
            public const string loss = "插损";
            public const string ampMaxInput = "功放最大输入";
            public const string coupleFactor = "耦合系数";
            public const string frePower = "频率功率表";
            public const string probeFactor = "探头校准因子";
            public const string signalLimit = "信号源限制输出表";
        }
        public static Dictionary<string, string> LimitFolderToHeader = new Dictionary<string, string>
        {
            {LimitFolders.loss,LimitHeaders.loss },
            {LimitFolders.ampMaxInput,LimitHeaders.ampMaxInput },
            {LimitFolders.coupleFactor,LimitHeaders.coupleFactor },
            {LimitFolders.frePower,LimitHeaders.frePower  },
            {LimitFolders.probeFactor,LimitHeaders.probeFactor  },
            {LimitFolders.signalLimit,LimitHeaders.signalLimit  }   ,
        };


    }

}
