using FlowDesk.Common;
using FlowDesk.Models;
using FlowDesk.Services.Models;
using FlowDesk.ViewModels.Account;

namespace FlowDesk.Services.Interfaces
{
    public interface IAccountAuthenticationService
    {
        Task<ServiceResult<AccountAuthenticationResult>> LoginAsync(
            LoginViewModel model,
            string? localReturnUrl);

        Task<ServiceResult<AccountAuthenticationResult>>
            ResolveRoleTargetAsync(ApplicationUser user);

        Task<ServiceResult> LogoutAsync();
    }
}
