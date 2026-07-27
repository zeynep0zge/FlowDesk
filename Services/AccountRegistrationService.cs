using FlowDesk.Common;
using FlowDesk.Constants;
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
    public sealed class AccountRegistrationService
        : IAccountRegistrationService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppDbContext _dbContext;
        private readonly IEmailService _emailService;
        private readonly IPasswordHasher<EmailVerificationRequest>
            _emailVerificationHasher;
        private readonly ILogger<AccountRegistrationService> _logger;

        public AccountRegistrationService(
            UserManager<ApplicationUser> userManager,
            AppDbContext dbContext,
            IEmailService emailService,
            IPasswordHasher<EmailVerificationRequest>
                emailVerificationHasher,
            ILogger<AccountRegistrationService> logger)
        {
            _userManager = userManager;
            _dbContext = dbContext;
            _emailService = emailService;
            _emailVerificationHasher = emailVerificationHasher;
            _logger = logger;
        }

        public async Task<ServiceResult<AccountRegistrationResult>>
            RegisterAsync(RegisterViewModel model)
        {
            List<AccountRegistrationError> validationErrors = [];

            if (!AppRoles.IsSelfRegistrable(model.RequestedRole))
            {
                validationErrors.Add(new AccountRegistrationError(
                    nameof(model.RequestedRole),
                    "Geçerli bir rol seçiniz."));
            }

            if (!DepartmentOptions.Contains(model.Department))
            {
                validationErrors.Add(new AccountRegistrationError(
                    nameof(model.Department),
                    "Geçerli bir departman seçiniz."));
            }

            if (validationErrors.Count > 0)
            {
                return Failure(validationErrors);
            }

            ApplicationUser? existingUser =
                await _userManager.FindByEmailAsync(model.Email);

            if (existingUser != null)
            {
                return Failure(
                [
                    new AccountRegistrationError(
                        nameof(model.Email),
                        "Bu e-posta adresiyle daha önce hesap oluşturulmuş.")
                ]);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                Department = model.Department,
                RequestedRole = model.RequestedRole,
                EmailConfirmed = false,
                IsApproved = false,
                CreatedAtUtc = DateTime.UtcNow
            };

            IdentityResult createResult =
                await _userManager.CreateAsync(user, model.Password);

            if (!createResult.Succeeded)
            {
                return Failure(createResult.Errors
                    .Select(error => new AccountRegistrationError(
                        string.Empty,
                        error.Description))
                    .ToList());
            }

            bool emailSent =
                await CreateAndSendEmailVerificationCodeAsync(user);

            return ServiceResult<AccountRegistrationResult>.Success(
                new AccountRegistrationResult
                {
                    Email = user.Email!,
                    EmailSent = emailSent
                });
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

        private static ServiceResult<AccountRegistrationResult> Failure(
            IReadOnlyList<AccountRegistrationError> errors)
        {
            return new ServiceResult<AccountRegistrationResult>
            {
                IsSuccess = false,
                Data = new AccountRegistrationResult
                {
                    Errors = errors
                }
            };
        }
    }
}
