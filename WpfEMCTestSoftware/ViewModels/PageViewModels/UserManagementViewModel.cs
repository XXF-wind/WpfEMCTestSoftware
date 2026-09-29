using ClassLibrary.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Company.UI.Controls;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Models;

namespace WpfEMCTestSoftware.ViewModels.PageViewModels
{
    public partial class UserManagementViewModel : BaseViewModel
    {
        /// <summary>
        /// 用户信息列表
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ClsUserInfo> _userList = [];

        /// <summary>
        /// 当前选中用户
        /// </summary>
        [ObservableProperty]
        private ClsUserInfo _selectUser = new();

        /// <summary>
        /// 到期日
        /// </summary>
        [ObservableProperty]
        private string _expirationDate = string.Empty;

        /// <summary>
        /// 是否修改密码
        /// </summary>
        [ObservableProperty]
        private bool _isChangePwd = false;

        /// <summary>
        /// 新密码
        /// </summary>
        [ObservableProperty]
        private string _newPassword = string.Empty;

        /// <summary>
        /// 二次确认密码
        /// </summary>
        [ObservableProperty]
        private string _twoStepVerificationPwd = string.Empty;

        public UserManagementViewModel()
        {
            LoadUserList();
        }

        /// <summary>
        /// 加载用户列表
        /// </summary>
        private void LoadUserList()
        {
            List<ClsUserInfo> users = ClsData.Instance.GainUserInfo();
            UserList = [.. users];
        }

        /// <summary>
        /// 选中事件
        /// </summary>
        /// <param name="e"></param>
        [RelayCommand]
        private void OnSelectionChanged(SelectionChangedEventArgs e)
        {

        }

        /// <summary>
        /// 密码修改
        /// </summary>
        [RelayCommand]
        private void OnChangePassword()
        {
            if (SelectUser != null && SelectUser.UserName == GenericHelper.CurUser.UserName)
            {
                IsChangePwd = !IsChangePwd;
            }
            else
            {
                FlyoutNotication.ShowNotication(GenericParameter.Language.OnlyChangeSelfPwdMsg,
                    GenericParameter.Language.PromptCaption);
            }
        }

        /// <summary>
        /// 保存用户信息
        /// </summary>
        [RelayCommand]
        private void OnSaveUser()
        {
            if (SelectUser == null || string.IsNullOrEmpty(SelectUser.UserName))
            {
                FlyoutNotication.ShowNotication(GenericParameter.Language.UnselectedUserMsg,
                    GenericParameter.Language.PromptCaption);
                return;
            }
            if (IsChangePwd)
            {
                if (string.IsNullOrEmpty(NewPassword) || string.IsNullOrEmpty(TwoStepVerificationPwd)
                    || NewPassword != TwoStepVerificationPwd)
                {
                    FlyoutNotication.ShowNotication(GenericParameter.Language.TwoStepVerificationPwdErrMsg,
                        GenericParameter.Language.PromptCaption);
                    return;
                }
                SelectUser.PassWord = NewPassword;
            }
            if (ClsData.Instance.SaveUserInfo(SelectUser))
            {
                FlyoutNotication.ShowNotication(GenericParameter.Language.SaveUserSuccessMsg,
                        GenericParameter.Language.PromptCaption);
                LoadUserList();
            }
            else
            {
                FlyoutNotication.ShowNotication(GenericParameter.Language.SaveUserFailedMsg,
                        GenericParameter.Language.PromptCaption);
            }
        }

        /// <summary>
        /// 删除用户
        /// </summary>
        [RelayCommand]
        private void OnDeleteUser()
        {
            if (MessageBox.Show(GenericParameter.Language.IsDelUserMsg) == MessageBoxResult.OK)
            {
                if (SelectUser == null || string.IsNullOrEmpty(SelectUser.UserName))
                {
                    FlyoutNotication.ShowNotication(GenericParameter.Language.UnselectedUserMsg,
                        GenericParameter.Language.PromptCaption);
                    return;
                }
                ClsData.Instance.DeleteUser(SelectUser.UserId);
                LoadUserList();
            }
        }
    }
}
