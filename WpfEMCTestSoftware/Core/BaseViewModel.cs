using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace WpfEMCTestSoftware.Core
{
    /// <summary>
    /// 所有 ViewModel 的父类
    /// ViewModel基类：实现释放逻辑
    /// </summary>
    public class BaseViewModel : ObservableRecipient, IReleaseable
    {
        public BaseViewModel(IMessenger messenger) : base(messenger)
        {
            // 默认激活消息接收功能
            IsActive = true;
        }

        /// <summary>
        /// 全局语言管理器（所有页面可直接访问）
        /// </summary>
        public LanguageManager LangManager => LanguageManager.Instance;

        public BaseViewModel()
        {
            // 默认激活消息接收功能
            IsActive = true;
        }

        public virtual void Release()
        {
            ClsData.Instance.Close();
            // 1. 注销所有消息监听（核心：ObservableRecipient的关键）
            IsActive = false;

            // 2. 清理VM中的资源（子类重写扩展）
            ReleaseVmResources();
        }

        /// <summary>
        /// 子类重写：释放VM自身资源（定时器、事件、异步任务等）
        /// </summary>
        protected virtual void ReleaseVmResources()
        {
            // 示例：停止定时器、取消异步任务、清空集合等
        }
    }
}
