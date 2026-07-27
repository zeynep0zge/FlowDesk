using FlowDesk.Common;
using FlowDesk.Services.Models;
using FlowDesk.ViewModels.Account;

namespace FlowDesk.Services.Interfaces
{
    public interface IAccountEmailVerificationService
    {
        Task<ServiceResult<EmailVerificationResult>> VerifyAsync(
            VerifyEmailCodeViewModel model);

        Task<ServiceResult<EmailVerificationResendResult>> ResendAsync(
            string email);
    }
}
