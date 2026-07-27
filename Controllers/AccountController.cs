using FlowDesk.Constants;
using FlowDesk.Common;
using FlowDesk.Data;
using FlowDesk.Models;
using FlowDesk.Services.Interfaces;
using FlowDesk.Services.Models;
using FlowDesk.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;


namespace FlowDesk.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppDbContext _dbContext;
        private readonly IEmailService _emailService;
        private readonly IAccountRegistrationService
            _accountRegistrationService;
        private readonly IAccountEmailVerificationService
            _accountEmailVerificationService;

        private readonly IPasswordHasher<PasswordResetRequest>
            _passwordResetHasher;


        public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        AppDbContext dbContext,
        IEmailService emailService,
        IPasswordHasher<PasswordResetRequest> passwordResetHasher,
        IAccountRegistrationService accountRegistrationService,
        IAccountEmailVerificationService
            accountEmailVerificationService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _dbContext = dbContext;
            _emailService = emailService;
            _passwordResetHasher = passwordResetHasher;
            _accountRegistrationService = accountRegistrationService;
            _accountEmailVerificationService =
                accountEmailVerificationService;
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

            var user = await _userManager.FindByEmailAsync(model.Email);

            // Hesabın sistemde olup olmadığını dışarıya açıklamıyoruz.
            if (user != null && !string.IsNullOrWhiteSpace(user.Email))
            {
                var previousRequests =
                    await _dbContext.PasswordResetRequests
                        .Where(x =>
                            x.UserId == user.Id &&
                            !x.IsInvalidated &&
                            x.CompletedAtUtc == null)
                        .ToListAsync();

                foreach (var previousRequest in previousRequests)
                {
                    previousRequest.IsInvalidated = true;
                }

                var code = RandomNumberGenerator
                    .GetInt32(100000, 1000000)
                    .ToString();

                var resetRequest = new PasswordResetRequest
                {
                    UserId = user.Id,
                    CreatedAtUtc = DateTime.UtcNow,
                    ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
                };

                resetRequest.CodeHash =
                    _passwordResetHasher.HashPassword(
                        resetRequest,
                        code);

                _dbContext.PasswordResetRequests.Add(resetRequest);
                await _dbContext.SaveChangesAsync();

                await _emailService.SendAsync(
                    user.Email,
                    "FlowDesk Şifre Sıfırlama Kodu",
                    $"""
            <div style="font-family:Arial,sans-serif">
                <h2>FlowDesk</h2>
                <p>Şifre sıfırlama kodunuz:</p>

                <div style="
                    font-size:32px;
                    font-weight:bold;
                    letter-spacing:8px;
                    margin:24px 0;">
                    {code}
                </div>

                <p>Bu kod 10 dakika geçerlidir.</p>
                <p>Bu işlemi siz başlatmadıysanız e-postayı dikkate almayın.</p>
            </div>
            """);
            }

            TempData["Info"] =
                "Hesabınız bulunuyorsa doğrulama kodu e-posta adresinize gönderildi.";

            return RedirectToAction(
                nameof(VerifyResetCode),
                new { email = model.Email });
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

            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Kod geçersiz veya süresi dolmuş.");

                return View(model);
            }

            var resetRequest = await _dbContext.PasswordResetRequests
                .Where(x =>
                    x.UserId == user.Id &&
                    !x.IsInvalidated &&
                    x.CompletedAtUtc == null &&
                    x.CodeVerifiedAtUtc == null)
                .OrderByDescending(x => x.CreatedAtUtc)
                .FirstOrDefaultAsync();

            if (resetRequest == null ||
                resetRequest.ExpiresAtUtc <= DateTime.UtcNow)
            {
                if (resetRequest != null)
                {
                    resetRequest.IsInvalidated = true;
                    await _dbContext.SaveChangesAsync();
                }

                ModelState.AddModelError(
                    string.Empty,
                    "Kod geçersiz veya süresi dolmuş.");

                return View(model);
            }

            if (resetRequest.FailedAttemptCount >= 5)
            {
                resetRequest.IsInvalidated = true;
                await _dbContext.SaveChangesAsync();

                ModelState.AddModelError(
                    string.Empty,
                    "Çok fazla yanlış deneme yapıldı. Yeni kod isteyin.");

                return View(model);
            }

            var verificationResult =
                _passwordResetHasher.VerifyHashedPassword(
                    resetRequest,
                    resetRequest.CodeHash,
                    model.Code);

            if (verificationResult == PasswordVerificationResult.Failed)
            {
                resetRequest.FailedAttemptCount++;

                if (resetRequest.FailedAttemptCount >= 5)
                {
                    resetRequest.IsInvalidated = true;
                }

                await _dbContext.SaveChangesAsync();

                ModelState.AddModelError(
                    string.Empty,
                    "Doğrulama kodu hatalı.");

                return View(model);
            }

            var resetSessionToken =
                Convert.ToHexString(
                    RandomNumberGenerator.GetBytes(32));

            resetRequest.CodeVerifiedAtUtc = DateTime.UtcNow;
            resetRequest.ResetSessionHash =
                HashResetSessionToken(resetSessionToken);
            resetRequest.ResetSessionExpiresAtUtc =
                DateTime.UtcNow.AddMinutes(10);

            await _dbContext.SaveChangesAsync();

            return RedirectToAction(
                nameof(ResetPassword),
                new
                {
                    email = model.Email,
                    token = resetSessionToken
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

            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Şifre sıfırlama isteği geçersiz.");

                return View(model);
            }

            var sessionHash =
                HashResetSessionToken(model.ResetSessionToken);

            var resetRequest =
                await _dbContext.PasswordResetRequests
                    .Where(x =>
                        x.UserId == user.Id &&
                        x.ResetSessionHash == sessionHash &&
                        x.CodeVerifiedAtUtc != null &&
                        x.ResetSessionExpiresAtUtc > DateTime.UtcNow &&
                        x.CompletedAtUtc == null &&
                        !x.IsInvalidated)
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .FirstOrDefaultAsync();

            if (resetRequest == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Şifre sıfırlama bağlantısı geçersiz veya süresi dolmuş.");

                return View(model);
            }

            var identityToken =
                await _userManager.GeneratePasswordResetTokenAsync(user);

            var result =
                await _userManager.ResetPasswordAsync(
                    user,
                    identityToken,
                    model.NewPassword);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }

                return View(model);
            }

            resetRequest.CompletedAtUtc = DateTime.UtcNow;
            resetRequest.IsInvalidated = true;

            await _dbContext.SaveChangesAsync();

            TempData["Success"] =
                "Şifreniz başarıyla güncellendi. Yeni şifrenizle giriş yapabilirsiniz.";

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

        private static string HashResetSessionToken(string token)
        {
            var bytes = Encoding.UTF8.GetBytes(token);
            var hash = SHA256.HashData(bytes);

            return Convert.ToHexString(hash);
        }

    }
}