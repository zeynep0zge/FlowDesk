using FlowDesk.Common;
using FlowDesk.Services.Models;
using FlowDesk.ViewModels.Account;

namespace FlowDesk.Services.Interfaces
{
    public interface IAccountRegistrationService
    {
        Task<ServiceResult<AccountRegistrationResult>> RegisterAsync(
            RegisterViewModel model);
    }
}
