using FlowDesk.Common;
using FlowDesk.Data;
using FlowDesk.Models;
using FlowDesk.Services.Interfaces;
using FlowDesk.Services.Models;
using FlowDesk.ViewModels.Account;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace FlowDesk.Services
{
    public sealed class AccountEmailVerificationService
        : IAccountEmailVerificationService
    {
        private const string VerificationSuccessMessage =
            "E-posta adresiniz doğrulandı. Hesabınız yönetici onayına gönderildi.";

        private const string ResendInformationMessage =
            "Hesabınız doğrulama için uygunsa yeni kod gönderildi. Lütfen e-posta kutunuzu kontrol edin.";

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppDbContext _dbContext;
        private readonly IPasswordHasher<EmailVerificationRequest>
            _emailVerificationHasher;
        private readonly IEmailService _emailService;
        private readonly ILogger<AccountEmailVerificationService> _logger;

        public AccountEmailVerificationService(
            UserManager<ApplicationUser> userManager,
            AppDbContext dbContext,
            IPasswordHasher<EmailVerificationRequest>
                emailVerificationHasher,
            IEmailService emailService,
            ILogger<AccountEmailVerificationService> logger)
        {
            _userManager = userManager;
            _dbContext = dbContext;
            _emailVerificationHasher = emailVerificationHasher;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<ServiceResult<EmailVerificationResult>>
            VerifyAsync(VerifyEmailCodeViewModel model)
        {
            ApplicationUser? user =
                await _userManager.FindByEmailAsync(model.Email);

            if (user == null || user.EmailConfirmed)
            {
                return VerificationFailure(
                    "Kod geçersiz veya süresi dolmuş.");
            }

            EmailVerificationRequest? verificationRequest =
                await _dbContext.EmailVerificationRequests
                    .Where(x =>
                        x.UserId == user.Id &&
                        !x.IsInvalidated &&
                        x.VerifiedAtUtc == null)
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .FirstOrDefaultAsync();

            if (verificationRequest == null)
            {
                return VerificationFailure(
                    "Kod geçersiz veya süresi dolmuş.");
            }

            if (verificationRequest.ExpiresAtUtc <= DateTime.UtcNow)
            {
                verificationRequest.IsInvalidated = true;
                await _dbContext.SaveChangesAsync();

                return VerificationFailure(
                    "Doğrulama kodunun süresi dolmuş. Yeni kod isteyin.");
            }

            if (verificationRequest.FailedAttemptCount >= 5)
            {
                verificationRequest.IsInvalidated = true;
                await _dbContext.SaveChangesAsync();

                return VerificationFailure(
                    "Çok fazla yanlış deneme yapıldı. Yeni kod isteyin.");
            }

            PasswordVerificationResult verificationResult =
                _emailVerificationHasher.VerifyHashedPassword(
                    verificationRequest,
                    verificationRequest.CodeHash,
                    model.Code);

            if (verificationResult == PasswordVerificationResult.Failed)
            {
                verificationRequest.FailedAttemptCount++;

                if (verificationRequest.FailedAttemptCount >= 5)
                {
                    verificationRequest.IsInvalidated = true;
                }

                await _dbContext.SaveChangesAsync();

                return VerificationFailure(
                    "Doğrulama kodu hatalı.");
            }

            verificationRequest.VerifiedAtUtc = DateTime.UtcNow;
            verificationRequest.IsInvalidated = true;
            user.EmailConfirmed = true;

            IdentityResult updateResult =
                await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                return VerificationFailure(updateResult.Errors
                    .Select(error => new EmailVerificationError(
                        string.Empty,
                        error.Description))
                    .ToList());
            }

            await _dbContext.SaveChangesAsync();

            return new ServiceResult<EmailVerificationResult>
            {
                IsSuccess = true,
                SuccessMessage = VerificationSuccessMessage,
                Data = new EmailVerificationResult()
            };
        }

        public async Task<ServiceResult<EmailVerificationResendResult>>
            ResendAsync(string email)
        {
            string normalizedEmail = email?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(normalizedEmail))
            {
                ApplicationUser? user =
                    await _userManager.FindByEmailAsync(normalizedEmail);

                if (user != null &&
                    !user.EmailConfirmed &&
                    !string.IsNullOrWhiteSpace(user.Email))
                {
                    EmailVerificationRequest? latestRequest =
                        await _dbContext.EmailVerificationRequests
                            .Where(x => x.UserId == user.Id)
                            .OrderByDescending(x => x.CreatedAtUtc)
                            .FirstOrDefaultAsync();

                    bool canResend =
                        latestRequest == null ||
                        latestRequest.CreatedAtUtc <=
                            DateTime.UtcNow.AddSeconds(-60);

                    if (canResend)
                    {
                        await CreateAndSendEmailVerificationCodeAsync(user);
                    }
                }
            }

            return new ServiceResult<EmailVerificationResendResult>
            {
                IsSuccess = true,
                SuccessMessage = ResendInformationMessage,
                Data = new EmailVerificationResendResult
                {
                    Email = normalizedEmail
                }
            };
        }

        private async Task<bool> CreateAndSendEmailVerificationCodeAsync(
            ApplicationUser user)
        {
            List<EmailVerificationRequest> previousRequests =
                await _dbContext.EmailVerificationRequests
                    .Where(x =>
                        x.UserId == user.Id &&
                        !x.IsInvalidated &&
                        x.VerifiedAtUtc == null)
                    .ToListAsync();

            foreach (EmailVerificationRequest previousRequest
                     in previousRequests)
            {
                previousRequest.IsInvalidated = true;
            }

            string code = RandomNumberGenerator
                .GetInt32(0, 1000000)
                .ToString("D6");

            var verificationRequest = new EmailVerificationRequest
            {
                UserId = user.Id,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
            };

            verificationRequest.CodeHash =
                _emailVerificationHasher.HashPassword(
                    verificationRequest,
                    code);

            _dbContext.EmailVerificationRequests.Add(
                verificationRequest);

            await _dbContext.SaveChangesAsync();

            try
            {
                await _emailService.SendAsync(
                    user.Email!,
                    "FlowDesk E-posta Doğrulama Kodu",
                    $"""
                    <div style="font-family:Arial,sans-serif;color:#0f172a;line-height:1.6">
                        <h2 style="color:#1e3a8a">FlowDesk</h2>
                        <p>E-posta doğrulama kodunuz:</p>
                        <div style="font-size:32px;font-weight:bold;letter-spacing:8px;margin:24px 0;color:#1e3a8a">
                            {code}
                        </div>
                        <p>Bu kod 10 dakika geçerlidir.</p>
                        <p>Bu işlemi siz başlatmadıysanız bu e-postayı dikkate almayın.</p>
                    </div>
                    """);

                return true;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Kullanıcı {UserId} için doğrulama e-postası gönderilemedi.",
                    user.Id);

                try
                {
                    _dbContext.EmailVerificationRequests.Remove(
                        verificationRequest);

                    await _dbContext.SaveChangesAsync();
                }
                catch (Exception cleanupException)
                {
                    _logger.LogError(
                        cleanupException,
                        "Gönderilemeyen doğrulama kaydı temizlenemedi. UserId: {UserId}",
                        user.Id);
                }

                return false;
            }
        }

        private static ServiceResult<EmailVerificationResult>
            VerificationFailure(string message)
        {
            return VerificationFailure(
            [
                new EmailVerificationError(
                    string.Empty,
                    message)
            ]);
        }

        private static ServiceResult<EmailVerificationResult>
            VerificationFailure(
                IReadOnlyList<EmailVerificationError> errors)
        {
            return new ServiceResult<EmailVerificationResult>
            {
                IsSuccess = false,
                Data = new EmailVerificationResult
                {
                    Errors = errors
                }
            };
        }
    }
}
