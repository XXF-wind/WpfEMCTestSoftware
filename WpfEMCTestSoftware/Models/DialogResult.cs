namespace WpfEMCTestSoftware.Models
{
    public class DialogResult
    {
        /// <summary>
        /// 结果是否确认
        /// </summary>
        public bool? IsConfirmed { get; set; }
        /// <summary>
        /// 输出信息
        /// </summary>
        public object? Data { get; set; }
        public DialogResult(bool? result, object? data = null)
        {
            IsConfirmed = result;
            Data = data;
        }
        public DialogResult()
        {
            IsConfirmed = null;
            Data = null;
        }
    }

    /// <summary>
    /// 弹窗页信息
    /// </summary>
    public class DialogInfo
    {
        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; set; } = string.Empty;
        /// <summary>
        /// 第一行标签（空代表隐藏第一行）
        /// </summary>
        public string LabelFirst { get; set; } = string.Empty;
        /// <summary>
        /// 第二行标签
        /// </summary>
        public string LabelSecond { get; set; } = string.Empty;

        public string InputData { get; set; } = string.Empty;
        /// <summary>
        /// 第一行选择框选项
        /// </summary>
        public List<string> Items { get; set; } = [];


        public DialogInfo(string title, List<string> items, string labelFirst = "", string labelSecond = "", string inputstring = "")
        {
            Title = title;
            Items = items;
            LabelFirst = labelFirst;
            LabelSecond = labelSecond;
            InputData = inputstring;
        }
        public DialogInfo()
        {

        }
    }
}
