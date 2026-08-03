using System.Security.Claims;
using FlowDesk.Common;
using FlowDesk.Services.Models;

namespace FlowDesk.Services.Interfaces;

public interface IAuthenticatedActorContextResolver
{
    Task<ServiceResult<ActorContext>> ResolveAsync(
        ClaimsPrincipal principal);
}
