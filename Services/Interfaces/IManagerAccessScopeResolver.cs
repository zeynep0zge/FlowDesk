using FlowDesk.Common;

namespace FlowDesk.Services.Interfaces;

public interface IManagerAccessScopeResolver
{
    Task<ServiceResult<ManagerAccessScope>> ResolveAsync(
        int? managerUserId);
}
