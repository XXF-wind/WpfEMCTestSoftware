using ClassLibrary.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Company.UI.Controls;
using Company.UI.Controls.Models;
using EMCDeviceLibrary;
using MaterialDesignThemes.Wpf;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Enums;
using WpfEMCTestSoftware.Models.EventMessages;
using WpfEMCTestSoftware.ViewModels.PageViewModels.TestConfigViewModels;
using WpfEMCTestSoftware.Views;
using WpfEMCTestSoftware.Views.PageViews.TestConfigViews;
using Page = System.Windows.Controls.Page;

namespace WpfEMCTestSoftware.ViewModels
{
    public partial class MainViewModel : BaseViewModel
    {
        #region 变量/属性定义
        /// <summary>
        /// 使用语言
        /// </summary>
        public bool IsCN
        {
            get => !GenericParameter.Language.IsEnglish;
            set
            {
                GenericParameter.Language.IsEnglish = !value;
            }
        }

        [ObservableProperty]
        private string _companyName = "南京华凯电子科技有限公司";

        /// <summary>
        /// 侧边栏项信息
        /// </summary>
        [ObservableProperty]
        public ObservableCollection<NavigationItems> _items = [];

        #region 测试运行锁定（导航栏 / 与测试无关的全局操作）
        /// <summary>
        /// 是否有测试正在进行中（准备中 / 运行中 / 暂停 / 保持频点 / 自检 等）。
        /// 由 TestRunMonitor 全局状态同步，导航栏与主题/语言切换据此禁用。
        /// </summary>
        [ObservableProperty]
        private bool _isTestRunning;

        /// <summary>导航与主题/语言切换是否可用（测试中不可用）</summary>
        public bool NavigationEnabled => !IsTestRunning;

        partial void OnIsTestRunningChanged(bool value)
        {
            OnPropertyChanged(nameof(NavigationEnabled));
        }

        private void OnTestStateChanged(object? sender, TestState state)
        {
            IsTestRunning = TestRunMonitor.IsTestActive;
        }
        #endregion

        /// <summary>
        /// 页面标题
        /// </summary>
        [ObservableProperty]
        private string _pageTitle = string.Empty;

        /// <summary>
        /// 当前页面
        /// </summary>
        [ObservableProperty]
        private Page? _currentPage;
        /// <summary>
        /// 前一页面
        /// </summary>
        private Page? _previousPage;
        /// <summary>
        /// 前一次试验环境名称
        /// </summary>
        private string? _previousEnvName;

        #region 主题切换
        /// <summary>
        /// 当前主题显示名称（显示在切换按钮上）
        /// </summary>
        [ObservableProperty]
        private string _themeDisplayName = "浅色";

        /// <summary>
        /// 循环切换主题命令
        /// </summary>
        [RelayCommand]
        private void CycleTheme()
        {
            string next = ThemeManager.GetNextTheme();
            ThemeManager.SwitchTheme(next);

            // 更新按钮显示文字
            var theme = ThemeManager.AvailableThemes.FirstOrDefault(t => t.Key == next);
            ThemeDisplayName = theme.DisplayName;
        }
        #endregion

        #region 状态栏
        private string _version = "ver 1.0.0";
        /// <summary>
        /// 版本
        /// </summary>
        public string Version
        {
            get => _version;
            set
            {
                if (value != _version)
                {
                    _version = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _url = "www.sinpel.cn";
        /// <summary>
        /// URL网址
        /// </summary>
        public string URL
        {
            get => _url;
            set
            {
                if (value != _url)
                {
                    _url = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _phone = "4008560060";
        /// <summary>
        /// 联系电话
        /// </summary>
        public string Phone
        {
            get => _phone;
            set
            {
                if (value != _phone)
                {
                    _phone = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _email = "sales@sinpel.cn";
        /// <summary>
        /// 邮箱
        /// </summary>
        public string Email
        {
            get => _email;
            set
            {
                if (value != _email)
                {
                    _email = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _curtime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        /// <summary>
        /// 当前时间
        /// </summary>
        public string CurTime
        {
            get => _curtime;
            set
            {
                _curtime = value;
                OnPropertyChanged();
            }
        }
        private DispatcherTimer timer = new DispatcherTimer();
        #endregion

        //设备连接对象
        SockConnect? sock;

        public List<PackIconKind> lstIcon = [PackIconKind.TelevisionPlay, PackIconKind.Devices, PackIconKind.CharityOutline, PackIconKind.Tools, PackIconKind.HelpCircleOutline, PackIconKind.Database, PackIconKind.FilterCogOutline, PackIconKind.ArchiveClockOutline, PackIconKind.AccountCogOutline];

        private readonly Dispatcher _dispatcher;
        #endregion

        public MainViewModel() : base()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();

            // 初始化主题显示名
            var currentTheme = ThemeManager.AvailableThemes.FirstOrDefault(t => t.Key == ThemeManager.CurrentTheme);
            ThemeDisplayName = currentTheme.DisplayName;

            InitPageList();
            GenericHelper.globalDevices = [.. DeviceSerAndDeser.GetDeviceInfos(ClsData.Instance.GainDeviceInfo())];
            GenericHelper.SortDevicesByType();
            //SetDeviceEventMessage
            //设置设备参数事件触发（设备列表->主窗体）
            Messenger.Register<SetDeviceEventMessage, int>(this, (int)DeviceParaMessageToken.DeviceListToMainWindow, (r, m) => ViewDeviceDetails(m));
            //退出页面事件触发（子页面->主窗体）：令牌1=发给主窗体的切页请求，消息值=目标页面索引
            Messenger.Register<ExitClickEventMessage, int>(this, (int)PageExitMessageToken.SubPageToMainWindow, (r, m) => SwitchPage(m.Value));
            //运行配置页面
            Messenger.Register<RunConfigEventMessage, int>(this, (int)IntMessageToken.SwithToRunConfigPage, (r, m) => OnRunConfigPage(m));
            //切换页面
            Messenger.Register<UniversalIntMessage, int>(this, (int)IntMessageToken.SwitchPage, (r, m) => SwitchPage(m.Value));
            //UnifComposite
            Messenger.Register<RVCCompositeConfigMessage, int>(this, (int)IntMessageToken.UnifComposite, (r, m) => ReceiveRVCComposite(m.Value));
            //接收混响室测试页面配置信息
            Messenger.Register<CompositeRVC_EMSConfigMessage, int>(this, (int)IntMessageToken.RVCTestomposite, (r, m) => HandleRvcEmsFullConfig(m.Value));
            Messenger.Register<UniversalIntMessage, int>(this, (int)IntMessageToken.RVCTestomposite, (r, m) => HandleRvcEmsPageOnlyFlag(m.Value));
            //接收CS114测试页面配置信息
            Messenger.Register<CompositeCS114ConfigMessage, int>(this, (int)IntMessageToken.CS114Composite, (r, m) => HandleCS114FullConfig(m.Value));
            Messenger.Register<UniversalIntMessage, int>(this, (int)IntMessageToken.CS114Composite, (r, m) => HandleCS114PageOnlyFlag(m.Value));
            SwitchPage(0);

            // 订阅全局测试运行状态：测试中锁定导航栏与主题/语言切换
            IsTestRunning = TestRunMonitor.IsTestActive;
            TestRunMonitor.StateChanged += OnTestStateChanged;
        }

        /// <summary>
        /// 运行配置页面
        /// </summary>
        /// <param name="message"></param>
        private void OnRunConfigPage(RunConfigEventMessage message)
        {
            ReleasePreviousPage();
            RunConfigPage runConfig = new();
            CurrentPage = runConfig;
            _previousPage = CurrentPage as Page;
            if (message.Value != null)
            {
                Messenger.Send(message, (int)IntMessageToken.PassEnvName);
                _previousEnvName = message.Value;
            }
            else
            {
                var envMsg = new RunConfigEventMessage(_previousEnvName);
                Messenger.Send(envMsg, (int)IntMessageToken.PassEnvName);
            }
        }
        /// <summary>
        /// 接收RCV场均匀性复合配置数据，切换至测试页面并赋值配置信息
        /// </summary>
        /// <param name="config"></param>
        private void ReceiveRVCComposite(RVCCompositeConfig config)
        {
            SwitchPage(1005);
            if (CurrentPage == null)
                return;
            var testPage = (RVC_FieldUniformityTestPage)CurrentPage;
            RVC_FieldUniformityTestViewModel testViewModel = (RVC_FieldUniformityTestViewModel)testPage.DataContext;
            testViewModel.M_RVCCompositeConfig = config;
        }
        /// <summary>
        /// 接收混响室测试复合数据
        /// </summary>
        /// <param name="config"></param>
        private void HandleRvcEmsFullConfig(CompositeRVC_EMSConfig config)
        {
            //打开配置页面
            SwitchPage(1007);
            if (CurrentPage == null) return;
            var testPage = (RVC_EMSTestConfigPage)CurrentPage;
            RVC_EMSTestConfigViewModel testViewModel = (RVC_EMSTestConfigViewModel)testPage.DataContext;
            testViewModel.RVCEMSTestModel = config.Model;
            testViewModel.AlreadyTestEnvPath = config.FilePath;
            testViewModel.CurTestEnvName = _previousEnvName;
        }
        private void HandleRvcEmsPageOnlyFlag(int i)
        {
            SwitchPage(1007);
            if (CurrentPage == null) return;
            var testPage = (RVC_EMSTestConfigPage)CurrentPage;
            RVC_EMSTestConfigViewModel testViewModel = (RVC_EMSTestConfigViewModel)testPage.DataContext;
            testViewModel.CurTestEnvName = _previousEnvName;
        }
        /// <summary>
        /// 接收CS114测试复合数据：切换至CS114配置页面并回填历史参数
        /// </summary>
        /// <param name="config"></param>
        private void HandleCS114FullConfig(CompositeCS114Config config)
        {
            //打开配置页面
            SwitchPage(1008);
            if (CurrentPage == null) return;
            var testPage = (EMS_CS114ConfigPage)CurrentPage;
            EMS_CS114ConfigViewModel testViewModel = (EMS_CS114ConfigViewModel)testPage.DataContext;
            if (config.Model != null)
                testViewModel.CS114TestModel = config.Model;
            testViewModel.AlreadyTestEnvPath = config.FilePath;
            testViewModel.CurTestEnvName = _previousEnvName ?? "CS114";
            //左侧硬件模板随首个频段显示（页面 FirstLoadPage 只在频段列表为空时才覆盖）
            var firstBand = testViewModel.CS114TestModel.ListFreBands.FirstOrDefault();
            if (firstBand != null)
                testViewModel.CurFreBand = firstBand;
        }
        /// <summary>
        /// 仅打开CS114配置页面（新建模板，不回填参数）
        /// </summary>
        /// <param name="i"></param>
        private void HandleCS114PageOnlyFlag(int i)
        {
            SwitchPage(1008);
            if (CurrentPage == null) return;
            var testPage = (EMS_CS114ConfigPage)CurrentPage;
            EMS_CS114ConfigViewModel testViewModel = (EMS_CS114ConfigViewModel)testPage.DataContext;
            testViewModel.CurTestEnvName = _previousEnvName ?? "CS114";
            testViewModel.AlreadyTestEnvPath = "";
        }
        /// <summary>
        /// 查看设备详情
        /// </summary>
        /// <param name="message"></param>
        private void ViewDeviceDetails(SetDeviceEventMessage message)
        {
            //1. 释放之前的页面资源
            ReleasePreviousPage();
            //2. 创建新的设备参数页面
            CurrentPage = new DeviceParaPage();
            //3. 记录用于下次释放
            _previousPage = CurrentPage as Page;
            //4. 将设备信息转发给新页面，使用不同的令牌：主窗体 -> 设备参数页
            Messenger.Send(message, (int)DeviceParaMessageToken.MainWindowToDevicePara);
        }

        private int clearIce = 0;
        private void Timer_Tick(object? sender, EventArgs e)
        {
            CurTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            if (clearIce++ > 100)
            {
                clearIce = 0;
                ClearMemory();
            }
        }

        /// <summary>
        /// 释放内存
        /// </summary>
        public static void ClearMemory()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        /// <summary>
        /// 初始化页列表
        /// </summary>
        private void InitPageList()
        {
            Items.Clear();
            Type type = !IsCN ? typeof(PageTitleEN) : typeof(PageTitle);
            foreach (var value in Enum.GetValues(type))
            {
                string? data = value.ToString();
                if (!string.IsNullOrEmpty(data))
                {
                    int i = (int)Enum.Parse(type, data);
                    Items.Add(new NavigationItems() { Icon = lstIcon[i], Title = value.ToString() });
                }
            }
        }

        #region 控件触发方法
        /// <summary>
        /// 项点击命令
        /// </summary>
        /// <param name="args"></param>
        [RelayCommand]
        private void ClickItem(CustomEventArgs args)
        {
            if (args == null) return;
            // 测试中锁定导航栏：忽略导航项的切页请求（防御性拦截，XAML 已禁用导航栏）
            if (IsTestRunning) return;
            Type type = !IsCN ? typeof(PageTitleEN) : typeof(PageTitle);
            PageTitle = args.Message;
            int i = (int)Enum.Parse(type, PageTitle);
            SwitchPage(i);
        }

        /// <summary>
        /// 页面切换
        /// </summary>
        /// <param name="i"></param>
        private void SwitchPage(int i)
        {
            ReleasePreviousPage();
            CurrentPage = i switch
            {
                0 => new QuickStartPage(),
                1 => new DeviceListPage(),
                2 => new LimitConfigPage(),
                3 => new SupportToolPage(),
                4 => new HelpPage(),
                1004 => new RVC_FieldUniformityConfigPage(),
                1005 => new RVC_FieldUniformityTestPage(),
                1006 => new RVC_FieldUniformityConfigAnalysis(),
                1007 => new RVC_EMSTestConfigPage(),
                1008 => new EMS_CS114ConfigPage(),
                2001 => new PhaseTestPage(),
                _ => null,
            };

            if (CurrentPage != null)
            {
                _previousPage = CurrentPage as Page;
            }
        }

        /// <summary>
        /// 释放前一页资源
        /// </summary>
        private void ReleasePreviousPage()
        {
            if (_previousPage == null) return;

            _dispatcher.Invoke(() =>
            {
                if (_previousPage.Parent is Frame frame)
                {
                    frame.Content = null;
                }
                _previousPage.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                if (_previousPage.DataContext is IReleaseable releaseableVm)
                {
                    releaseableVm.Release();
                }
                _previousPage.DataContext = null;
                _previousPage = null;
            });

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        /// <summary>
        /// 语言变更
        /// </summary>
        [RelayCommand]
        private void ChangeLanguage()
        {
            ClsData.Instance.AddOrUpdatePara("IsCN", IsCN ? "1" : "0");
            if (IsCN)
            {
                IsCN = false;
                LangManager.SwitchToEnglish();
            }
            else
            {
                IsCN = true;
                LangManager.SwitchToChinese();
            }
            InitPageList();
        }

        [RelayCommand]
        private void UnLoaded()
        {
            ClsData.Instance.Close();
            Release();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Application.Current.Shutdown();
        }

        /// <summary>
        /// 释放主 VM：注销全局测试状态订阅，避免跨页面泄漏
        /// </summary>
        public override void Release()
        {
            TestRunMonitor.StateChanged -= OnTestStateChanged;
            base.Release();
        }
        #endregion
    }
}
