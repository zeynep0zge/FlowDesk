using FlowDesk.Common;
using FlowDesk.Services.Models;
using FlowDesk.ViewModels.Account;

namespace FlowDesk.Services.Interfaces
{
    public interface IAccountPasswordResetService
    {
        Task<ServiceResult<PasswordResetRequestResult>>
            RequestResetAsync(ForgotPasswordViewModel model);

        Task<ServiceResult<PasswordResetCodeResult>>
            VerifyCodeAsync(VerifyResetCodeViewModel model);

        Task<ServiceResult<PasswordResetCompletionResult>>
            ResetPasswordAsync(ResetPasswordViewModel model);
    }
}
