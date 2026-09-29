using ClassLibrary.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Company.UI.Controls;
using System.Collections.ObjectModel;
using System.Data;
using System.Text.Json;
using System.Windows;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Models;
namespace WpfEMCTestSoftware.ViewModels
{
    public partial class LoginViewModel : BaseViewModel
    {
        public event Action<bool>? LoginResultEvent;

        [ObservableProperty]
        private string _userName = string.Empty;
        [ObservableProperty]
        private string _passWord = string.Empty;

        [ObservableProperty]
        private ClsUserInfo _userInfo = new();

        /// <summary>
        /// 用户列表
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _userNames = [];

        List<ClsUserInfo> _userTable = new();
        /// <summary>
        /// 操作类型，登录(false)或注册(true)
        /// </summary>
        [ObservableProperty]
        private bool _operType = false;

        /// <summary>
        /// 记住密码
        /// </summary>
        [ObservableProperty]
        private bool _isRememberPwd = false;
        public LoginViewModel()
        {
            Initialize();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        private void Initialize()
        {
            _userTable = ClsData.Instance.GainUserInfo();
            if (_userTable != null && _userTable.Count > 0)
                UserNames = [.. _userTable.AsEnumerable().Select(s => s.UserName).ToList()];
            UserName = ClsData.Instance.QueryPara("User");
            PassWord = ClsData.Instance.QueryPara("Pwd");

            IsRememberPwd = bool.Parse(ClsData.Instance.QueryPara("IsRememberPwd", "false"));
        }

        /// <summary>
        /// 用户操作：登录或注册
        /// </summary>
        [RelayCommand]
        private void OnUserOper(object obj)
        {
            if (obj != null)
            {
                if (obj.ToString() == "Login")
                {
                    // 用户验证
                    if (CanLogin(UserName, PassWord))
                    {
                        if (IsRememberPwd)
                        {
                            ClsData.Instance.AddOrUpdatePara("User", UserName);
                            ClsData.Instance.AddOrUpdatePara("Pwd", PassWord);
                            ClsData.Instance.AddOrUpdatePara("IsRememberPwd", IsRememberPwd.ToString());
                        }
                        LoginResultEvent?.Invoke(true);
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(UserName))
                    {
                        FlyoutNotication.ShowNotication(GenericParameter.Language.IsNullAuthorizedPersonMsg,
                                GenericParameter.Language.PromptInformationMsg);
                        return;
                    }
                    // 注册用户时，验证用户名是否已存在
                    if (CanLogin(UserInfo.UserName, UserInfo.PassWord))
                    {
                        FlyoutNotication.ShowNotication(GenericParameter.Language.UserExistMsg, GenericParameter.Language.PromptInformationMsg);
                        return;
                    }
                    // 注册新用户
                    try
                    {
                        if (ClsData.Instance.SaveUserInfo(UserInfo))
                        {
                            ClsData.Instance.AddOperLog("用户注册", $"授权人:{UserName},注册信息:{JsonSerializer.Serialize(UserInfo)}");
                            FlyoutNotication.ShowNotication(GenericParameter.Language.RegisterSuccessMsg,
                                GenericParameter.Language.PromptInformationMsg);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(GenericParameter.Language.RegisterFailedMsg + ex.Message);
                    }
                }
            }
        }

        /// <summary>
        /// 确定当前用户名和密码组合是否可用于登录
        /// </summary>
        /// <returns>如果用户名和密码有效且与现有用户对应，则为true；否则为false</returns>
        private bool CanLogin(string UName, string Pwd)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(UName) || string.IsNullOrWhiteSpace(Pwd))
                {
                    FlyoutNotication.ShowNotication(GenericParameter.Language.IsNullUserOrPwdMsg,
                                GenericParameter.Language.PromptInformationMsg);
                    return false;
                }
                List<ClsUserInfo> users = ClsData.Instance.GainUserInfo(UName);
                if (users == null || users.Count == 0)
                {
                    FlyoutNotication.ShowNotication(GenericParameter.Language.UserNotExistMsg,
                                GenericParameter.Language.PromptInformationMsg);
                    return false;
                }
                else
                {
                    if (OperType) return true; // 注册时只需验证用户名是否存在

                    string pwd = users[0].PassWord;
                    if (pwd != Pwd)
                    {
                        FlyoutNotication.ShowNotication(GenericParameter.Language.WrongPasswordMsg,
                                GenericParameter.Language.PromptInformationMsg);
                        return false;
                    }
                    if (!users[0].IsActive)
                    {
                        FlyoutNotication.ShowNotication(GenericParameter.Language.UserExpiredMsg,
                                GenericParameter.Language.PromptInformationMsg);
                        return false;
                    }
                    GenericHelper.CurUser = new ClsUserInfo()
                    {
                        ValidityPeriod = users[0].ValidityPeriod,
                        UserId = users[0].UserId,
                        PassWord = users[0].PassWord,
                        IsActive = users[0].IsActive,

                    };
                }
                return true;
            }
            catch (Exception ex)
            {
                // 记录详细的异常信息
                //Console.WriteLine($"异常类型: {ex.GetType().Name}");
                //Console.WriteLine($"异常消息: {ex.Message}");
                //Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                MessageBox.Show(GenericParameter.Language.LoginError + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 切换操作类型
        /// </summary>
        [RelayCommand]
        private void OnSwitchOperType()
        {
            OperType = !OperType;
        }
    }
}
