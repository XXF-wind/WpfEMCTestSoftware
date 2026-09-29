using WpfEMCTestSoftware.RunTasks;

namespace WpfEMCTestSoftware.Models.DisplayModules
{
    /// <summary>
    /// 测试显示模块接口：RunConfigPage 外壳页上的可插拔显示单元
    /// 每个测试项通过 ModuleProfileRegistry 声明启用哪些模块，外壳页动态加载
    /// </summary>
    public interface ITestDisplayModule
    {
        /// <summary>模块唯一标识，如 "Chart.Signal"、"Card.Band"</summary>
        string ModuleKey { get; }

        /// <summary>模块显示标题</summary>
        string Title { get; }

        /// <summary>绑定测试任务（订阅数据源），切换测试模板时调用</summary>
        void Bind(BaseRunTask task);

        /// <summary>解绑并释放，页面卸载或切换模板前调用</summary>
        void Unbind();
    }
}
