using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using WpfEMCTestSoftware.ViewModels;
using WpfEMCTestSoftware.ViewModels.Dialogs;
using WpfEMCTestSoftware.ViewModels.PageViewModels;
using WpfEMCTestSoftware.ViewModels.PageViewModels.DeviceViewModels;
using WpfEMCTestSoftware.ViewModels.PageViewModels.HardwareViewModels;
using WpfEMCTestSoftware.ViewModels.PageViewModels.TestConfigViewModels;

namespace WpfEMCTestSoftware
{
    /// <summary>
    /// ViewModel定位器 - 管理所有ViewModel的创建和生命周期
    /// 作用：相当于一个"ViewModel工厂"，需要哪个ViewModel就从这个类获取
    /// </summary>
    internal class ViewModelLocator
    {
        // 静态只读的依赖注入容器
        // 作用：存储所有注册的ViewModel类型，需要时可以创建实例
        private static readonly IServiceProvider _serviceProvider;

        // 静态构造函数：在类第一次使用前自动执行一次
        static ViewModelLocator()
        {
            // 1. 创建一个服务集合（相当于"购物车"，先把要买的东西放进去）
            var services = new ServiceCollection();
            // 2. 注册消息传递服务（用于ViewModel之间通信）
            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
            // 3. ⭐⭐⭐ 注册所有的ViewModel（告诉容器有哪些类型需要管理）⭐⭐⭐
            // 格式：services.AddTransient<ViewModel类型>();
            // AddTransient 表示：每次请求都创建一个新实例
            services.AddTransient<MainViewModel>();
            services.AddTransient<AmpifierParaViewModel>();
            services.AddTransient<LimitConfigViewModel>();
            services.AddTransient<HistoricalDataViewModel>();
            services.AddTransient<HardwareConfigViewModel>();
            services.AddTransient<DeviceParaViewModel>();
            services.AddTransient<DeviceListViewModel>();
            services.AddTransient<MultiplierFreqParaViewModel>();
            services.AddTransient<ReceiverParaViewModel>();
            services.AddTransient<SwitchMatrixParaViewModel>();
            services.AddTransient<PowerMeterParaViewModel>();
            services.AddTransient<FieldStrengthMeterParaViewModel>();
            services.AddTransient<SignalSouceParaViewModel>();
            services.AddTransient<PhaseAttParaViewModel>();
            services.AddTransient<AmpifierTestViewModel>();
            services.AddTransient<StirrerParaViewModel>();
            services.AddTransient<SignalSourceTestViewModel>();
            services.AddTransient<RS103ViewModel>();
            services.AddTransient<QuickStartViewModel>();
            services.AddTransient<SupportToolViewModel>();
            services.AddTransient<PhaseTestViewModel>();
            services.AddTransient<RunConfigViewModel>();
            services.AddTransient<HelpViewModel>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<AlertDialogViewModel>();
            services.AddTransient<UserManagementViewModel>();
            services.AddTransient<EMSHardViewModel>();
            services.AddTransient<RVC_FieldUniformityConfigViewModel>();
            services.AddTransient<RVC_FieldUniformityTestViewModel>();
            services.AddTransient<RVC_EMSTestConfigViewModel>();
            services.AddTransient<RVC_HardwareViewModel>();
            services.AddTransient<EMS_CS114ConfigViewModel>();
            services.AddTransient<RVC_FieldUniformityAnalysisViewModel>();
            // 4. 构建服务提供者（相当于"结账"，把购物车里的东西打包好）
            _serviceProvider = services.BuildServiceProvider();
        }
        // ⭐⭐⭐ 以下是公开的属性，XAML通过绑定这些属性获取ViewModel实例 ⭐⭐⭐

        // 每个属性对应一个ViewModel的获取方法
        // 格式：public ViewModel类型 ViewModel名称 => _serviceProvider.GetRequiredService<ViewModel类型>();
        public MainViewModel MainViewModel =>
            _serviceProvider.GetRequiredService<MainViewModel>();
        public LoginViewModel LoginViewModel =>
            _serviceProvider.GetRequiredService<LoginViewModel>();

        public AmpifierParaViewModel AmpifierParaViewModel =>
            _serviceProvider.GetRequiredService<AmpifierParaViewModel>();

        public LimitConfigViewModel LimitConfigViewModel =>
            _serviceProvider.GetRequiredService<LimitConfigViewModel>();
        public HistoricalDataViewModel HistoricalDataViewModel =>
            _serviceProvider.GetRequiredService<HistoricalDataViewModel>();
        public HardwareConfigViewModel HardwareConfigViewModel =>
            _serviceProvider.GetRequiredService<HardwareConfigViewModel>();
        public DeviceParaViewModel DeviceParaViewModel =>
            _serviceProvider.GetRequiredService<DeviceParaViewModel>();
        public DeviceListViewModel DeviceListViewModel =>
            _serviceProvider.GetRequiredService<DeviceListViewModel>();
        public MultiplierFreqParaViewModel MultiplierFreqParaViewModel =>
            _serviceProvider.GetRequiredService<MultiplierFreqParaViewModel>();
        public ReceiverParaViewModel ReceiverParaViewModel =>
            _serviceProvider.GetRequiredService<ReceiverParaViewModel>();
        public SwitchMatrixParaViewModel SwitchMatrixParaViewModel =>
            _serviceProvider.GetRequiredService<SwitchMatrixParaViewModel>();
        public PowerMeterParaViewModel PowerMeterParaViewModel =>
            _serviceProvider.GetRequiredService<PowerMeterParaViewModel>();
        public FieldStrengthMeterParaViewModel FieldStrengthMeterParaViewModel =>
            _serviceProvider.GetRequiredService<FieldStrengthMeterParaViewModel>();
        public StirrerParaViewModel StirrerParaViewModel =>
            _serviceProvider.GetRequiredService<StirrerParaViewModel>();

        public SignalSouceParaViewModel SignalSouceParaViewModel =>
            _serviceProvider.GetRequiredService<SignalSouceParaViewModel>();
        public PhaseAttParaViewModel PhaseAttParaViewModel =>
            _serviceProvider.GetRequiredService<PhaseAttParaViewModel>();
        public AmpifierTestViewModel AmpifierTestViewModel =>
            _serviceProvider.GetRequiredService<AmpifierTestViewModel>();
        public SignalSourceTestViewModel SignalSourceTestViewModel =>
            _serviceProvider.GetRequiredService<SignalSourceTestViewModel>();
        public RS103ViewModel RS103ViewModel =>
            _serviceProvider.GetRequiredService<RS103ViewModel>();

        public QuickStartViewModel QuickStartViewModel =>
            _serviceProvider.GetRequiredService<QuickStartViewModel>();

        public SupportToolViewModel SupportToolViewModel =>
            _serviceProvider.GetRequiredService<SupportToolViewModel>();

        public PhaseTestViewModel PhaseTestViewModel =>
            _serviceProvider.GetRequiredService<PhaseTestViewModel>();
        public RunConfigViewModel RunConfigViewModel =>
            _serviceProvider.GetRequiredService<RunConfigViewModel>();
        public HelpViewModel HelpViewModel =>
            _serviceProvider.GetRequiredService<HelpViewModel>();

        public AlertDialogViewModel AlertDialogViewModel =>
             _serviceProvider.GetRequiredService<AlertDialogViewModel>();

        public UserManagementViewModel UserManagementViewModel =>
           _serviceProvider.GetRequiredService<UserManagementViewModel>();
        public EMSHardViewModel EMSHardViewModel =>
         _serviceProvider.GetRequiredService<EMSHardViewModel>();
        public RVC_FieldUniformityConfigViewModel RVC_FieldUniformityConfigViewModel =>
            _serviceProvider.GetRequiredService<RVC_FieldUniformityConfigViewModel>();
        public RVC_FieldUniformityTestViewModel RVC_FieldUniformityTestViewModel =>
            _serviceProvider.GetRequiredService<RVC_FieldUniformityTestViewModel>();

        public RVC_EMSTestConfigViewModel RVC_EMSTestConfigViewModel =>
            _serviceProvider.GetRequiredService<RVC_EMSTestConfigViewModel>();

        public EMS_CS114ConfigViewModel EMS_CS114ConfigViewModel =>
            _serviceProvider.GetRequiredService<EMS_CS114ConfigViewModel>();
        public RVC_HardwareViewModel RVC_HardwareViewModel =>
            _serviceProvider.GetRequiredService<RVC_HardwareViewModel>();
        public RVC_FieldUniformityAnalysisViewModel RVC_FieldUniformityAnalysisViewModel =>
            _serviceProvider.GetRequiredService<RVC_FieldUniformityAnalysisViewModel>();


    }
}
