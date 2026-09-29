using ClassLibrary.Generic;
using System.Windows;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Views;

namespace WpfEMCTestSoftware
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // 初始化主题管理器（读取持久化配置）
            ThemeManager.Initialize();

            if (bool.TryParse(ClsData.Instance.QueryPara("IsCN"), out bool isDebug))
            {
                GenericParameter.Language.IsEnglish = isDebug;
            }
            GenericHelper.CleanOldVersionFiles();
            LoginWindow login = new();
            if (login.ShowDialog() == true)
            {
                MainWindow mainWindow = new();
                mainWindow.Show();
            }
        }
    }
}
