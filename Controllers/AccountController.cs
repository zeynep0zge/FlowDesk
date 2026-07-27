using FlowDesk.Constants;
using FlowDesk.Common;
using FlowDesk.Models;
using FlowDesk.Services.Interfaces;
using FlowDesk.Services.Models;
using FlowDesk.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace FlowDesk.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAccountRegistrationService
            _accountRegistrationService;
        private readonly IAccountEmailVerificationService
            _accountEmailVerificationService;
        private readonly IAccountPasswordResetService
            _accountPasswordResetService;


        public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAccountRegistrationService accountRegistrationService,
        IAccountEmailVerificationService accountEmailVerificationService,
        IAccountPasswordResetService accountPasswordResetService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _accountRegistrationService = accountRegistrationService;
            _accountEmailVerificationService =
                accountEmailVerificationService;
            _accountPasswordResetService = accountPasswordResetService;
        }
        [AllowAnonymous]
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(
            ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            ServiceResult<PasswordResetRequestResult> result =
                await _accountPasswordResetService
                    .RequestResetAsync(model);

            TempData["Info"] = result.SuccessMessage;

            return RedirectToAction(
                nameof(VerifyResetCode),
                new { email = result.Data!.Email });
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult VerifyResetCode(string email)
        {
            return View(new VerifyResetCodeViewModel
            {
                Email = email
            });
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyResetCode(
            VerifyResetCodeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            ServiceResult<PasswordResetCodeResult> result =
                await _accountPasswordResetService
                    .VerifyCodeAsync(model);

            if (!result.IsSuccess)
            {
                foreach (PasswordResetError error
                         in result.Data?.Errors ?? [])
                {
                    ModelState.AddModelError(error.Key, error.Message);
                }

                return View(model);
            }

            return RedirectToAction(
                nameof(ResetPassword),
                new
                {
                    email = result.Data!.Email,
                    token = result.Data.ResetSessionToken
                });
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult ResetPassword(
    string email,
    string token)
        {
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(token))
            {
                return BadRequest();
            }

            return View(new ResetPasswordViewModel
            {
                Email = email,
                ResetSessionToken = token
            });
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(
            ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            ServiceResult<PasswordResetCompletionResult> result =
                await _accountPasswordResetService
                    .ResetPasswordAsync(model);

            if (!result.IsSuccess)
            {
                foreach (PasswordResetError error
                         in result.Data?.Errors ?? [])
                {
                    ModelState.AddModelError(error.Key, error.Message);
                }

                return View(model);
            }

            TempData["Success"] = result.SuccessMessage;

            return RedirectToAction(nameof(Login));
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var currentUser = await _userManager.GetUserAsync(User);

                if (currentUser != null)
                {
                    return await RedirectByRoleAsync(currentUser);
                }
            }

            var model = new RegisterViewModel();
            PrepareRegisterOptions(model);

            return View(model);
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            model.FullName = model.FullName?.Trim() ?? string.Empty;
            model.Email = model.Email?.Trim() ?? string.Empty;
            model.Department = model.Department?.Trim() ?? string.Empty;
            model.RequestedRole = model.RequestedRole?.Trim() ?? string.Empty;

            if (!ModelState.IsValid)
            {
                PrepareRegisterOptions(model);
                return View(model);
            }

            ServiceResult<AccountRegistrationResult> result =
                await _accountRegistrationService.RegisterAsync(model);

            if (!result.IsSuccess)
            {
                foreach (AccountRegistrationError error
                         in result.Data?.Errors ?? [])
                {
                    ModelState.AddModelError(error.Key, error.Message);
                }

                PrepareRegisterOptions(model);
                return View(model);
            }

            AccountRegistrationResult registration = result.Data!;

            if (registration.EmailSent)
            {
                TempData["Info"] =
                    "Doğrulama kodu e-posta adresinize gönderildi.";
            }
            else
            {
                TempData["ErrorMessage"] =
                    "Hesabınız oluşturuldu ancak doğrulama e-postası gönderilemedi. Lütfen kodu yeniden göndermeyi deneyin.";
            }

            return RedirectToAction(
                nameof(VerifyEmailCode),
                new { email = registration.Email });
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult VerifyEmailCode(string? email)
        {
            return View(new VerifyEmailCodeViewModel
            {
                Email = email?.Trim() ?? string.Empty
            });
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmailCode(
            VerifyEmailCodeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            ServiceResult<EmailVerificationResult> result =
                await _accountEmailVerificationService.VerifyAsync(model);

            if (!result.IsSuccess)
            {
                foreach (EmailVerificationError error
                         in result.Data?.Errors ?? [])
                {
                    ModelState.AddModelError(error.Key, error.Message);
                }

                return View(model);
            }

            TempData["Success"] = result.SuccessMessage;

            return RedirectToAction(nameof(PendingApproval));
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendEmailVerificationCode(
            string email)
        {
            ServiceResult<EmailVerificationResendResult> result =
                await _accountEmailVerificationService.ResendAsync(email);

            TempData["Info"] = result.SuccessMessage;

            return RedirectToAction(
                nameof(VerifyEmailCode),
                new { email = result.Data!.Email });
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult PendingApproval()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var currentUser = await _userManager.GetUserAsync(User);

                if (currentUser != null)
                {
                    return await RedirectByRoleAsync(currentUser);
                }
            }

            var model = new LoginViewModel
            {
                ReturnUrl = returnUrl
            };

            return View(model);
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "E-posta veya şifre hatalı.");

                return View(model);
            }

            if (!user.EmailConfirmed || !user.IsApproved)
            {
                bool passwordIsValid =
                    await _userManager.CheckPasswordAsync(
                        user,
                        model.Password);

                if (!passwordIsValid)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "E-posta veya şifre hatalı.");
                }
                else if (!user.EmailConfirmed)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Giriş yapabilmek için e-posta adresinizi doğrulamalısınız.");
                }
                else
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Hesabınız yönetici onayı bekliyor.");
                }

                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                user,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                if (!string.IsNullOrWhiteSpace(model.ReturnUrl)
                    && Url.IsLocalUrl(model.ReturnUrl))
                {
                    return LocalRedirect(model.ReturnUrl);
                }

                return await RedirectByRoleAsync(user);
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Çok fazla başarısız giriş yapıldı. Lütfen daha sonra tekrar deneyin.");
            }
            else if (result.IsNotAllowed)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Giriş yapabilmek için e-posta adresinizi doğrulamalısınız.");
            }
            else
            {
                ModelState.AddModelError(
                    string.Empty,
                    "E-posta veya şifre hatalı.");
            }

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();

            return RedirectToAction(nameof(Login));
        }

        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private async Task<IActionResult> RedirectByRoleAsync(
            ApplicationUser user)
        {
            if (await _userManager.IsInRoleAsync(
                    user,
                    AppRoles.ProjectManager))
            {
                return RedirectToAction(
                    "Index",
                    "ProjectManager");
            }

            if (await _userManager.IsInRoleAsync(
                    user,
                    AppRoles.Analyst))
            {
                return RedirectToAction(
                    "Index",
                    "Analyst");
            }

            if (await _userManager.IsInRoleAsync(
                    user,
                    AppRoles.DepartmentManager))
            {
                return RedirectToAction(
                    "Index",
                    "DepartmentManager");
            }

            if (await _userManager.IsInRoleAsync(
                    user,
                    AppRoles.Employee))
            {
                return RedirectToAction(
                    "Index",
                    "Employee");
            }

            await _signInManager.SignOutAsync();

            ModelState.AddModelError(
                string.Empty,
                "Bu kullanıcıya sistem rolü atanmamış.");

            return View("Login", new LoginViewModel
            {
                Email = user.Email ?? string.Empty
            });
        }

        private static void PrepareRegisterOptions(
            RegisterViewModel model)
        {
            model.RoleOptions = AppRoles.SelfRegistrable
                .Select(role => new SelectListItem(
                    AppRoles.GetDisplayName(role),
                    role))
                .ToList();

            model.DepartmentOptions = DepartmentOptions.All
                .Select(department => new SelectListItem(
                    department,
                    department))
                .ToList();
        }

    }
}
