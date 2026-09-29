// Core/EventToCommandAction.cs
// .NET 8.0 引用此命名空间（替代旧版 System.Windows.Interactivity）
using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Input;

namespace WpfEMCTestSoftware.Core
{
    /// <summary>
    /// 事件转命令的行为类
    /// .NET 8.0 适配版：事件转Command工具（无第三方依赖）
    /// </summary>
    public class EventToCommandAction : TriggerAction<DependencyObject>
    {
        #region 依赖属性：Command（绑定ViewModel的ICommand）
        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register(
                nameof(Command),
                typeof(ICommand),
                typeof(EventToCommandAction),
                new PropertyMetadata(null));

        public ICommand Command
        {
            get => (ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }
        #endregion

        #region 依赖属性：PassEventArgsToCommand（是否传递事件参数）
        public static readonly DependencyProperty PassEventArgsToCommandProperty =
            DependencyProperty.Register(
                nameof(PassEventArgsToCommand),
                typeof(bool),
                typeof(EventToCommandAction),
                new PropertyMetadata(false));

        public bool PassEventArgsToCommand
        {
            get => (bool)GetValue(PassEventArgsToCommandProperty);
            set => SetValue(PassEventArgsToCommandProperty, value);
        }
        #endregion

        /// <summary>
        /// 事件触发时执行（核心逻辑）
        /// </summary>
        /// <param name="parameter">事件参数（此处为CustomEventArgs）</param>
        protected override void Invoke(object parameter)
        {
            if (Command == null) return;

            // 传递事件参数给Command（CustomEventArgs）
            object commandParameter = PassEventArgsToCommand ? parameter : null;

            // 检查命令可执行状态，执行命令
            if (Command.CanExecute(commandParameter))
            {
                Command.Execute(commandParameter);
            }
        }
    }
}