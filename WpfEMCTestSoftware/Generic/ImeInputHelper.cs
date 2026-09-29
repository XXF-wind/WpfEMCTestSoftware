using System.Windows.Controls;
using System.Windows.Input;

namespace WpfEMCTestSoftware.Generic
{
    /// <summary>
    /// 输入法兼容助手：中文输入法在"中文标点"状态下，按主键盘小数点键打出的是全角"。"，
    /// 绑定 double 的输入框会拒绝/还原该值，表现为"小数点输不进去"。
    /// 在 PreviewTextInput（隧道阶段，先于控件自身校验）把全角字符统一转成半角，
    /// 只拦截含全角字符的输入，正常半角输入原样放行。
    /// 用法：在 Page / Window 根元素挂 PreviewTextInput，事件处理里调用 <see cref="HandlePreviewTextInput"/>。
    /// </summary>
    public static class ImeInputHelper
    {
        /// <summary>
        /// 在 PreviewKeyDown 事件处理中调用：把主键盘句点键（OemPeriod）和小键盘点键（Decimal）
        /// 在按键层直接拦截并手动插入半角 "."。
        /// 这是兜底方案：不再依赖输入法把小数点转成什么字符、也不依赖控件自身校验——
        /// 只要按的是小数点键，就由这里直接往焦点输入框里插 "."。
        /// 返回 true 表示本次按键已被接管。
        /// </summary>
        public static bool HandlePreviewKeyDown(KeyEventArgs e)
        {
            // IME 正在处理的按键交给文本输入事件（PreviewTextInput）转换
            if (e.Key == Key.ImeProcessed) return false;

            if (e.Key != Key.OemPeriod && e.Key != Key.Decimal) return false;

            e.Handled = true;   // 抢在输入法与控件校验之前，字符不再走常规输入路径
            if (Keyboard.FocusedElement is TextBox box)
            {
                int caret = box.CaretIndex;
                box.SelectedText = ".";
                box.CaretIndex = caret + 1;
            }
            return true;
        }

        /// <summary>
        /// 在 PreviewTextInput 事件处理中调用；返回 true 表示本次输入已被接管（全角已转半角并插入）。
        /// </summary>
        public static bool HandlePreviewTextInput(TextCompositionEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text)) return false;

            string half = ToHalfWidth(e.Text);
            if (half == e.Text) return false;   // 本来就是半角 → 交给控件自己处理

            e.Handled = true;                   // 拦下这次输入，手动插入半角字符
            if (Keyboard.FocusedElement is TextBox box)
            {
                int caret = box.CaretIndex;
                box.SelectedText = half;        // 有选中内容时按正常输入替换
                box.CaretIndex = caret + half.Length;
            }
            return true;
        }

        /// <summary>
        /// 全角转半角：句号/全角句点 → "."，全角数字 → 0-9，全角正负号 → +/-。
        /// </summary>
        public static string ToHalfWidth(string text)
        {
            char[] chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (c == '\u3002' || c == '\uFF0E' || c == '\uFF61')          // 。 ． ｡ → 小数点
                    chars[i] = '.';
                else if (c >= '\uFF10' && c <= '\uFF19')                      // ０~９ → 0~9
                    chars[i] = (char)(c - '\uFF10' + '0');
                else if (c == '\uFF0B')                                       // ＋ → +
                    chars[i] = '+';
                else if (c == '\uFF0D' || c == '\u2212')                      // － − → -
                    chars[i] = '-';
            }
            return new string(chars);
        }
    }
}
