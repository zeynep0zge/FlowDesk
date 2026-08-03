using FlowDesk.Common;
using FlowDesk.Data;
using FlowDesk.Models;
using FlowDesk.Services.Interfaces;
using FlowDesk.Services.Models;
using FlowDesk.ViewModels.Account;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace FlowDesk.Services
{
    public sealed class AccountPasswordResetService
        : IAccountPasswordResetService
    {
        private const string RequestInformationMessage =
            "Hesabınız bulunuyorsa doğrulama kodu e-posta adresinize gönderildi.";

        private const string ResetSuccessMessage =
            "Şifreniz başarıyla güncellendi. Yeni şifrenizle giriş yapabilirsiniz.";

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppDbContext _dbContext;
        private readonly IPasswordHasher<PasswordResetRequest>
            _passwordResetHasher;
        private readonly IEmailService _emailService;

        public AccountPasswordResetService(
            UserManager<ApplicationUser> userManager,
            AppDbContext dbContext,
            IPasswordHasher<PasswordResetRequest> passwordResetHasher,
            IEmailService emailService)
        {
            _userManager = userManager;
            _dbContext = dbContext;
            _passwordResetHasher = passwordResetHasher;
            _emailService = emailService;
        }

        public async Task<ServiceResult<PasswordResetRequestResult>>
            RequestResetAsync(ForgotPasswordViewModel model)
        {
            ApplicationUser? user =
                await _userManager.FindByEmailAsync(model.Email);

            if (user != null && !string.IsNullOrWhiteSpace(user.Email))
            {
                List<PasswordResetRequest> previousRequests =
                    await _dbContext.PasswordResetRequests
                        .Where(x =>
                            x.UserId == user.Id &&
                            !x.IsInvalidated &&
                            x.CompletedAtUtc == null)
                        .ToListAsync();

                foreach (PasswordResetRequest previousRequest
                         in previousRequests)
                {
                    previousRequest.IsInvalidated = true;
                }

                string code = RandomNumberGenerator
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

            return new ServiceResult<PasswordResetRequestResult>
            {
                IsSuccess = true,
                SuccessMessage = RequestInformationMessage,
                Data = new PasswordResetRequestResult
                {
                    Email = model.Email
                }
            };
        }

        public async Task<ServiceResult<PasswordResetCodeResult>>
            VerifyCodeAsync(VerifyResetCodeViewModel model)
        {
            ApplicationUser? user =
                await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                return CodeFailure(
                    "Kod geçersiz veya süresi dolmuş.");
            }

            PasswordResetRequest? resetRequest =
                await _dbContext.PasswordResetRequests
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

                return CodeFailure(
                    "Kod geçersiz veya süresi dolmuş.");
            }

            if (resetRequest.FailedAttemptCount >= 5)
            {
                resetRequest.IsInvalidated = true;
                await _dbContext.SaveChangesAsync();

                return CodeFailure(
                    "Çok fazla yanlış deneme yapıldı. Yeni kod isteyin.");
            }

            PasswordVerificationResult verificationResult =
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

                return CodeFailure(
                    "Doğrulama kodu hatalı.");
            }

            string resetSessionToken =
                Convert.ToHexString(
                    RandomNumberGenerator.GetBytes(32));

            resetRequest.CodeVerifiedAtUtc = DateTime.UtcNow;
            resetRequest.ResetSessionHash =
                HashResetSessionToken(resetSessionToken);
            resetRequest.ResetSessionExpiresAtUtc =
                DateTime.UtcNow.AddMinutes(10);

            await _dbContext.SaveChangesAsync();

            return ServiceResult<PasswordResetCodeResult>.Success(
                new PasswordResetCodeResult
                {
                    Email = model.Email,
                    ResetSessionToken = resetSessionToken
                });
        }

        public async Task<ServiceResult<PasswordResetCompletionResult>>
            ResetPasswordAsync(ResetPasswordViewModel model)
        {
            ApplicationUser? user =
                await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                return CompletionFailure(
                    "Şifre sıfırlama isteği geçersiz.");
            }

            string sessionHash =
                HashResetSessionToken(model.ResetSessionToken);

            PasswordResetRequest? resetRequest =
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
                return CompletionFailure(
                    "Şifre sıfırlama bağlantısı geçersiz veya süresi dolmuş.");
            }

            try
            {
                return await _dbContext.Database
                    .CreateExecutionStrategy()
                    .ExecuteAsync(async () =>
                    {
                        await using var transaction =
                            await _dbContext.Database
                                .BeginTransactionAsync();

                        string identityToken = await _userManager
                            .GeneratePasswordResetTokenAsync(user);

                        IdentityResult result =
                            await _userManager.ResetPasswordAsync(
                                user,
                                identityToken,
                                model.NewPassword);

                        if (!result.Succeeded)
                        {
                            await transaction.RollbackAsync();
                            return CompletionFailure(result.Errors
                                .Select(error => new PasswordResetError(
                                    string.Empty,
                                    error.Description))
                                .ToList());
                        }

                        resetRequest.CompletedAtUtc = DateTime.UtcNow;
                        resetRequest.IsInvalidated = true;
                        await _dbContext.SaveChangesAsync();

                        await transaction.CommitAsync();
                        return new ServiceResult<
                            PasswordResetCompletionResult>
                        {
                            IsSuccess = true,
                            SuccessMessage = ResetSuccessMessage,
                            Data = new PasswordResetCompletionResult()
                        };
                    });
            }
            catch (Exception exception)
                when (exception is DbUpdateException or
                      InvalidOperationException)
            {
                return CompletionFailure(
                    "Şifre sıfırlama işlemi tamamlanamadı.");
            }
        }

        private static string HashResetSessionToken(string token)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(token);
            byte[] hash = SHA256.HashData(bytes);

            return Convert.ToHexString(hash);
        }

        private static ServiceResult<PasswordResetCodeResult>
            CodeFailure(string message)
        {
            return new ServiceResult<PasswordResetCodeResult>
            {
                IsSuccess = false,
                Data = new PasswordResetCodeResult
                {
                    Errors =
                    [
                        new PasswordResetError(
                            string.Empty,
                            message)
                    ]
                }
            };
        }

        private static ServiceResult<PasswordResetCompletionResult>
            CompletionFailure(string message)
        {
            return CompletionFailure(
            [
                new PasswordResetError(
                    string.Empty,
                    message)
            ]);
        }

        private static ServiceResult<PasswordResetCompletionResult>
            CompletionFailure(
                IReadOnlyList<PasswordResetError> errors)
        {
            return new ServiceResult<PasswordResetCompletionResult>
            {
                IsSuccess = false,
                Data = new PasswordResetCompletionResult
                {
                    Errors = errors
                }
            };
        }
    }
}
