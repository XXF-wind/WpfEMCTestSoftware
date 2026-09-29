using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using WpfEMCTestSoftware.Core;

namespace WpfEMCTestSoftware.ViewModels
{
    public partial class HelpViewModel : BaseViewModel
    {


        //===================================绑定属性======================================//

        [ObservableProperty]
        private string? _checkUpdateText = "检查新版本";
        [ObservableProperty]
        private string? _statusText = string.Empty;
        //最新版本
        [ObservableProperty]
        private string _latestVersion = string.Empty;
        [ObservableProperty]
        private bool _isChecking = false;
        //是否存在最新更新
        [ObservableProperty]
        private bool _hasNewVersion = false;

        // 缓存需要下载的文件列表
        private List<NeedUpdateItem>? _pendingUpdateFiles;
        /// <summary>
        /// 上传给服务端的本地文件信息
        /// </summary>
        public class ClientFileInfo
        {
            public string FileName { get; set; } = string.Empty;
            public string Version { get; set; } = string.Empty;
        }
        //请求体
        public class BatchCheckRequest
        {
            public string ProductType { get; set; } = string.Empty;
            public string MacAddress { get; set; } = string.Empty;
            public List<ClientFileInfo> LocalFiles { get; set; } = new();
        }
        /// <summary>服务器返回：待更新文件</summary>
        public class NeedUpdateItem
        {
            public string fileName { get; set; } = string.Empty;
            public string serverVersion { get; set; } = string.Empty;
            public string localVersion { get; set; } = string.Empty;
            public long fileSize { get; set; }
        }
        private const string ServerBaseUrl = "http://2du68512nr32.vicp.fun:21422";
        private const string CheckBatchApiUrl = ServerBaseUrl + "/api/Updates/check-batch";
        private const string DownloadTemplate = ServerBaseUrl + "/api/Updates/download";


        public HelpViewModel()
        {

        }
        [RelayCommand]
        private async Task OnUpdateButton()
        {
            if (HasNewVersion)
            {
                await OnDownLoadVersion();
            }
            else
            {
                await OnCheckUpdate();
            }
        }
        /// <summary>
        /// 执行下载更新
        /// </summary>
        /// <returns></returns>
        private async Task OnDownLoadVersion()
        {
            if (_pendingUpdateFiles == null || !_pendingUpdateFiles.Any())
                return;

            try
            {
                IsChecking = true;
                CheckUpdateText = "开始下载更新...";
                string appPath = AppContext.BaseDirectory;


                foreach (var item in _pendingUpdateFiles)
                {
                    var targetPath = Path.Combine(appPath, item.fileName);
                    if (File.Exists(targetPath))
                    {
                        var oldPath = targetPath + ".old";
                        // 如果已有旧的 .old 先删掉
                        if (File.Exists(oldPath))
                            File.Delete(oldPath);
                        File.Move(targetPath, oldPath);
                    }
                    //拼接路由地址
                    string url = Path.Combine(DownloadTemplate, Uri.EscapeDataString("EMC"), Uri.EscapeDataString(item.fileName));
                    string newFilePath = Path.Combine(appPath, $"{item.fileName}");
                    StatusText = $"正在下载：{item.fileName}";
                    await DownloadFile(url, newFilePath);
                }

                StatusText = "更新包下载完成！关闭软件重新打开即可生效";
                // 更新状态重置
                HasNewVersion = false;
                _pendingUpdateFiles = null;
            }
            catch (Exception ex)
            {
                CheckUpdateText = $"下载失败：{ex.Message}";
            }
            finally
            {
                IsChecking = false;
                await Task.Delay(4000);
                CheckUpdateText = "检查新版本";
            }
        }


        private async Task DownloadFile(string url, string saveFilePath)
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            using var stream = await httpClient.GetStreamAsync(url);
            using var fs = new FileStream(saveFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await stream.CopyToAsync(fs);
        }

        private async Task OnCheckUpdate()
        {
            if (IsChecking)
                return;
            try
            {
                IsChecking = true;
                StatusText = "正在检测更新...";
                HasNewVersion = false;
                _pendingUpdateFiles = null;
                //1. 获取本机MAC地址
                string mac = GetLocalMac();
                if (string.IsNullOrEmpty(mac))
                {
                    StatusText = "无法读取网卡MAC地址";
                    return;
                }

                //2. 扫描本地exe并读取版本
                var localFileList = new List<ClientFileInfo>();
                string appPath = AppContext.BaseDirectory;
                string[] allowExt = { ".exe" };
                foreach (var file in Directory.GetFiles(appPath, "*.*", SearchOption.TopDirectoryOnly))
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (!allowExt.Contains(ext)) continue;

                    var fileVerInfo = FileVersionInfo.GetVersionInfo(file);
                    string ver = fileVerInfo.ProductVersion ?? fileVerInfo.FileVersion ?? "0.0.0.0";

                    localFileList.Add(new ClientFileInfo
                    {
                        FileName = Path.GetFileName(file),
                        Version = ver
                    });
                }

                // 扫描不到本地exe直接退出
                if (!localFileList.Any())
                {
                    StatusText = "未扫描到本地程序文件";
                    return;
                }

                //3. 构造请求
                var req = new BatchCheckRequest
                {
                    ProductType = "EMC",
                    MacAddress = mac,
                    LocalFiles = localFileList
                };

                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
                string jsonBody = JsonSerializer.Serialize(req);
                var content = new StringContent(jsonBody, Encoding.Default, "application/json");
                var response = await httpClient.PostAsync(CheckBatchApiUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    StatusText = $"检测失败：{err}";
                    return;
                }

                string respJson = await response.Content.ReadAsStringAsync();
                var jsonOpt = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var updateItems = JsonSerializer.Deserialize<List<NeedUpdateItem>>(respJson, jsonOpt);

                if (updateItems != null && updateItems.Any())
                {
                    // 关键：缓存文件列表 + 开启下载模式
                    _pendingUpdateFiles = updateItems;
                    HasNewVersion = true;
                    LatestVersion = updateItems.First().serverVersion;
                    StatusText = $"检测到最新的版本：Ver{LatestVersion}";
                    CheckUpdateText = "下载更新";
                }
                else
                {
                    HasNewVersion = false;
                    _pendingUpdateFiles = null;
                    StatusText = "当前已是最新版本";
                }
            }
            catch (HttpRequestException)
            {
                StatusText = "无法连接更新服务器，请检查网络";
            }
            catch (Exception ex)
            {
                StatusText = $"异常：{ex.Message}";
            }
            finally
            {
                IsChecking = false;

            }

        }
        /// <summary>
        /// 获取本机第一个可用网卡
        /// </summary>
        /// <returns></returns>
        private string GetLocalMac()
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                // 跳过回环、隧道虚拟网卡
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                    continue;
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;

                var macBytes = ni.GetPhysicalAddress().GetAddressBytes();
                return BitConverter.ToString(macBytes).Replace("-", ":");
            }
            return string.Empty;
        }
    }
}
