namespace WpfEMCTestSoftware.Core
{
    /// <summary>
    /// 可释放的VM接口（所有VM实现此接口）
    /// 资源释放接口
    /// </summary>
    public interface IReleaseable
    {
        void Release();
    }
}
