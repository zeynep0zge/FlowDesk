using FlowDesk.Common;
using FlowDesk.Constants;
using FlowDesk.Models;
using FlowDesk.Services.Interfaces;
using FlowDesk.Services.Models;
using FlowDesk.ViewModels.Account;
using Microsoft.AspNetCore.Identity;

namespace FlowDesk.Services
{
    public sealed class AccountAuthenticationService
        : IAccountAuthenticationService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AccountAuthenticationService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public async Task<ServiceResult<AccountAuthenticationResult>>
            LoginAsync(
                LoginViewModel model,
                string? localReturnUrl)
        {
            ApplicationUser? user =
                await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                return AuthenticationFailure(
                    "E-posta veya şifre hatalı.");
            }

            if (!user.EmailConfirmed || !user.IsApproved)
            {
                bool passwordIsValid =
                    await _userManager.CheckPasswordAsync(
                        user,
                        model.Password);

                if (!passwordIsValid)
                {
                    return AuthenticationFailure(
                        "E-posta veya şifre hatalı.");
                }

                if (!user.EmailConfirmed)
                {
                    return AuthenticationFailure(
                        "Giriş yapabilmek için e-posta adresinizi doğrulamalısınız.");
                }

                return AuthenticationFailure(
                    "Hesabınız yönetici onayı bekliyor.");
            }

            SignInResult result =
                await _signInManager.PasswordSignInAsync(
                    user,
                    model.Password,
                    model.RememberMe,
                    lockoutOnFailure: true);

            if (result.Succeeded)
            {
                if (!string.IsNullOrWhiteSpace(localReturnUrl))
                {
                    return ServiceResult<AccountAuthenticationResult>
                        .Success(new AccountAuthenticationResult
                        {
                            ReturnUrl = localReturnUrl
                        });
                }

                return await ResolveRoleTargetAsync(user);
            }

            if (result.IsLockedOut)
            {
                return AuthenticationFailure(
                    "Çok fazla başarısız giriş yapıldı. Lütfen daha sonra tekrar deneyin.");
            }

            if (result.IsNotAllowed)
            {
                return AuthenticationFailure(
                    "Giriş yapabilmek için e-posta adresinizi doğrulamalısınız.");
            }

            return AuthenticationFailure(
                "E-posta veya şifre hatalı.");
        }

        public async Task<ServiceResult<AccountAuthenticationResult>>
            ResolveRoleTargetAsync(ApplicationUser user)
        {
            if (await _userManager.IsInRoleAsync(
                    user,
                    AppRoles.ProjectManager))
            {
                return RoleTarget("ProjectManager");
            }

            if (await _userManager.IsInRoleAsync(
                    user,
                    AppRoles.Analyst))
            {
                return RoleTarget("Analyst");
            }

            if (await _userManager.IsInRoleAsync(
                    user,
                    AppRoles.DepartmentManager))
            {
                return RoleTarget("DepartmentManager");
            }

            if (await _userManager.IsInRoleAsync(
                    user,
                    AppRoles.Employee))
            {
                return RoleTarget("Employee");
            }

            await _signInManager.SignOutAsync();

            return AuthenticationFailure(
                "Bu kullanıcıya sistem rolü atanmamış.",
                useFreshLoginModel: true,
                email: user.Email ?? string.Empty);
        }

        public async Task<ServiceResult> LogoutAsync()
        {
            await _signInManager.SignOutAsync();
            return ServiceResult.Success();
        }

        private static ServiceResult<AccountAuthenticationResult>
            RoleTarget(string controllerName)
        {
            return ServiceResult<AccountAuthenticationResult>.Success(
                new AccountAuthenticationResult
                {
                    ControllerName = controllerName,
                    ActionName = "Index"
                });
        }

        private static ServiceResult<AccountAuthenticationResult>
            AuthenticationFailure(
                string message,
                bool useFreshLoginModel = false,
                string email = "")
        {
            return new ServiceResult<AccountAuthenticationResult>
            {
                IsSuccess = false,
                Data = new AccountAuthenticationResult
                {
                    Email = email,
                    UseFreshLoginModel = useFreshLoginModel,
                    Errors =
                    [
                        new AccountAuthenticationError(
                            string.Empty,
                            message)
                    ]
                }
            };
        }
    }
}
