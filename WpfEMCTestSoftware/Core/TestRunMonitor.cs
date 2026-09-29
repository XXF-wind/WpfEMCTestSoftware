using System;
using System.Windows;
using WpfEMCTestSoftware.Enums;

namespace WpfEMCTestSoftware.Core
{
    /// <summary>
    /// 全局测试运行状态监视器。
    /// 由各 RunTask 在状态机迁移时上报（统一从 BaseRunTask.RefreshRunState 调用），
    /// 主窗体订阅后据此锁定导航栏、主题/语言切换等与测试无关的全局操作。
    /// </summary>
    public static class TestRunMonitor
    {
        private static TestState _current = TestState.IDLE;

        /// <summary>
        /// 当前全局测试状态（同一时刻只允许一个测试在跑，取最后一次上报的值）。
        /// </summary>
        public static TestState CurrentState
        {
            get => _current;
            private set => _current = value;
        }

        /// <summary>
        /// 是否有测试正在进行：准备中 / 运行中 / 暂停 / 保持频点 / 自检 等都属于"进行中"，
        /// 仅 IDLE / STOPPED / COMPLETED 视为可自由操作。
        /// </summary>
        public static bool IsTestActive
            => _current is not (TestState.IDLE or TestState.STOPPED or TestState.COMPLETED);

        /// <summary>
        /// 测试状态变化事件（始终在 UI 线程抛出，订阅方可直接更新绑定属性）。
        /// </summary>
        public static event EventHandler<TestState>? StateChanged;

        /// <summary>
        /// 上报一次状态变化。状态未变则忽略；内部确保事件在 UI 线程抛出。
        /// </summary>
        public static void SetState(TestState state)
        {
            if (_current == state) return;
            _current = state;

            var handler = StateChanged;
            if (handler == null) return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                handler(null, state);
            else
                dispatcher.Invoke(() => handler(null, state));
        }
    }
}
